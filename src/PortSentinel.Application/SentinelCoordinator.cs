using Microsoft.Extensions.Logging;
using PortSentinel.Contracts;
using PortSentinel.Domain;
using System.Text.Json;

namespace PortSentinel.Application;

public sealed class SentinelCoordinator(IPolicyStore store, IDeviceInventory inventory,
    IPolicyBackend backend, ILogger<SentinelCoordinator> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly RequestValidator validator = new();
    private IReadOnlyList<DeviceObservation> observed = [];
    private DateTime? inventoryUtc;
    private string? inventoryError;

    public async Task ReconcileAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { await ScanCoreAsync(ct); await store.PruneAsync(ct); }
        finally { gate.Release(); }
    }
    private async Task ScanCoreAsync(CancellationToken ct)
    {
        try
        {
            var scanned = await inventory.ScanAsync(ct);
            var now = DateTime.UtcNow;
            var changes = new List<DeviceConnectionEvent>();
            foreach (var item in scanned.Where(s => !observed.Any(o => Same(o, s))))
                changes.Add(Connection(item, "Bağlandı / kimlik güncellendi", now));
            foreach (var item in observed.Where(o => !scanned.Any(s => Same(o, s))))
                changes.Add(Connection(item, "Ayrıldı / kimlik güncellendi", now));
            if (changes.Count > 0) await store.RecordConnectionsAsync(changes, ct);
            observed = scanned; inventoryUtc = now; inventoryError = null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            inventoryError = "Cihaz taraması başarısız; önceki envanter güncel kabul edilmez.";
            logger.LogError(ex, "USB inventory failed");
        }
    }
    private static bool Same(DeviceObservation a, DeviceObservation b) =>
        a.Identity == b.Identity && a.DiskInstanceIds.SequenceEqual(b.DiskInstanceIds)
        && a.Volumes.SequenceEqual(b.Volumes);
    private static DeviceConnectionEvent Connection(DeviceObservation d, string kind, DateTime now) => new()
    { OccurredUtc = now, PhysicalInstanceId = d.Identity.PhysicalInstanceId, DeviceName = d.Name,
        Kind = kind, IdentityJson = JsonSerializer.Serialize(d.Identity) };

    public async Task<Response> ExecuteAsync(Request request, Caller caller, CancellationToken ct)
    {
        var correlation = Guid.NewGuid().ToString("N");
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Fail("Validation", string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)), correlation);
        await gate.WaitAsync(ct);
        try
        {
            logger.LogInformation("Request {Operation} by {Sid} correlation {Correlation}", request.Operation, caller.Sid, correlation);
            if (request.Operation != Operation.Snapshot && !caller.IsAdministrator)
                throw new BusinessException("Unauthorized", "Yönetim işlemi için yükseltilmiş Windows yönetici token'ı gerekir.");
            if (request.Operation == Operation.Snapshot)
                return new(true, correlation, Snapshot: await SnapshotCoreAsync(caller, ct));
            if (request.Operation == Operation.Reconcile)
            {
                await ScanCoreAsync(ct);
                if (inventoryError != null) throw new BusinessException("InventoryUnavailable", inventoryError);
                await WriteAuditAsync(caller, request, "Başarılı", "Envanter uzlaştırıldı; erişim politikası değiştirilmedi.", correlation, ct);
                return new(true, correlation, Snapshot: await SnapshotCoreAsync(caller, ct));
            }
            if (request.Operation == Operation.PreviewPolicy)
            {
                var data = await store.ReadAsync(ct);
                var preview = PolicyPreviewCompiler.Compile(data.Devices, data.Rules);
                await WriteAuditAsync(caller, request, "Başarılı", "Politika XML önizlemesi; Windows'a uygulanmadı.", correlation, ct);
                return new(true, correlation, Preview: preview);
            }
            if (request.Operation == Operation.Settings)
                await store.UpdateSettingsAsync(request.ScanIntervalSeconds, request.RetentionDays, caller, correlation, ct);
            else if (request.Operation == Operation.Rename)
                await store.RenameAsync(ParseGuid(request.DeviceId), request.Name!.Trim(), caller, correlation, ct);
            else
            {
                DeviceObservation? device = null;
                if (request.Operation == Operation.Grant)
                {
                    await ScanCoreAsync(ct);
                    if (inventoryError != null) throw new BusinessException("InventoryUnavailable", inventoryError);
                    device = observed.SingleOrDefault(d => d.Identity.PhysicalInstanceId.Equals(request.DeviceId, StringComparison.OrdinalIgnoreCase))
                        ?? throw new BusinessException("DeviceDisconnected", "Cihaz artık bağlı değil. Listeyi yenileyin.");
                    var decision = AccessEvaluator.Evaluate(observed, new HashSet<string>(), false)[observed.ToList().IndexOf(device)];
                    if (decision.Desired is AccessState.OutOfScope or AccessState.AmbiguousIdentity)
                        throw new BusinessException("AmbiguousOrOutOfScope", decision.Reason);
                }
                var revision = await store.ChangeRuleAsync(device, device == null ? ParseGuid(request.DeviceId) : null,
                    device != null, request.Name?.Trim(), request.ExpectedRevision, caller, correlation, ct);
                PolicyApplyResult result;
                try { result = await backend.ApplyAsync(revision, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Policy application failed at revision {Revision}", revision.Id);
                    result = new(RevisionState.Failed, "İstenen izin kaydedildi; Windows uygulaması başarısız. Uzlaştırma gerekiyor.");
                }
                await store.CompleteRevisionAsync(revision.Id, result, ct);
                await WriteAuditAsync(caller, request, result.State.ToString(), result.Details, correlation, ct);
                return new(true, correlation, Command: new(true, result.State == RevisionState.Verified, revision.Id,
                    result.Details + (device == null ? " Açık dosyalar ve devam eden yazmalar iptal edilmedi; üretim motorunda güvenli çıkarma/yeniden takma gerekebilir." : "")));
            }
            return new(true, correlation, Snapshot: await SnapshotCoreAsync(caller, ct));
        }
        catch (BusinessException ex)
        {
            try { await WriteAuditAsync(caller, request, ex.Code, ex.Message, correlation, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception auditError)
            {
                logger.LogError(auditError, "Denied request audit failed correlation {Correlation}", correlation);
                return Fail("AuditUnavailable", "İşlem reddedildi; denetim kaydı veritabanına yazılamadı. Servis logunu inceleyin.", correlation);
            }
            return Fail(ex.Code, ex.Message, correlation);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Request failed correlation {Correlation}", correlation);
            return Fail("InternalError", "İşlem tamamlanamadı. Veritabanı/servis logunu correlation ID ile inceleyin; değişiklik kaydedilmiş olabilir, listeyi yenileyin.", correlation);
        }
        finally { gate.Release(); }
    }
    private static Guid ParseGuid(string? text) => Guid.TryParse(text, out var id) ? id : throw new BusinessException("Validation", "Cihaz kayıt kimliği geçersiz.");
    private static Response Fail(string code, string message, string correlation) => new(false, correlation, Error: new(code, message, correlation));
    private Task WriteAuditAsync(Caller c, Request r, string outcome, string details, string correlation, CancellationToken ct) => store.AuditAsync(new()
    { OccurredUtc = DateTime.UtcNow, ActorSid = c.Sid, Operation = r.Operation.ToString(), Outcome = outcome, Details = details, CorrelationId = correlation }, ct);
    private async Task<SnapshotDto> SnapshotCoreAsync(Caller caller, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct);
        var assessment = await backend.AssessAsync(ct);
        // The shipped assessment backend never claims enforcement. A future backend must prove exact revision and hardware coverage.
        var keys = data.Rules.Where(r => r.Allowed).Join(data.Devices, r => r.RegisteredDeviceId, d => d.Id, (_, d) => d.IdentityKey).ToHashSet();
        var decisions = AccessEvaluator.Evaluate(observed, keys, false);
        var devices = observed.Select((d, i) => new DeviceDto(d.Identity.PhysicalInstanceId, d.Name, d.Manufacturer, d.SizeBytes,
            d.Transport, Map(d.Identity), d.DiskInstanceIds, d.Volumes.Select(v => new VolumeDto(v.PartitionId, v.DriveLetter, v.SizeBytes)).ToArray(),
            decisions[i].Desired.ToString(), decisions[i].Effective.ToString(), decisions[i].Reason,
            caller.IsAdministrator && inventoryError == null && decisions[i].Desired is not (AccessState.AmbiguousIdentity or AccessState.OutOfScope))).ToArray();
        return new(DateTime.UtcNow, inventoryUtc, inventoryError, caller.IsAdministrator, data.DesiredRevision, null,
            new(assessment.Name, false, assessment.Status, assessment.Diagnostics), devices,
            data.Devices.Select(d => new RegisteredDto(d.Id.ToString(), d.DisplayName, Map(d.Identity),
                data.Rules.Any(r => r.RegisteredDeviceId == d.Id && r.Allowed), inventoryError == null && observed.Any(o => o.Identity.Key == d.IdentityKey))).ToArray(),
            data.Events.Select(e => new EventDto(e.OccurredUtc, e.Kind, e.DeviceName, e.PhysicalInstanceId)).ToArray(),
            data.Audit.Select(a => new AuditDto(a.OccurredUtc, a.ActorSid, a.Operation, a.Outcome, a.Details, a.CorrelationId)).ToArray(),
            data.Revisions.Select(r => new RevisionDto(r.Id, r.State.ToString(), r.CreatedUtc, r.VerifiedUtc, r.Details)).ToArray(),
            new(data.Settings.ScanIntervalSeconds, data.Settings.RetentionDays));
    }
    private static IdentityDto Map(DeviceIdentity d) => new(d.PhysicalInstanceId, d.SerialNumber, d.VendorId, d.ProductId,
        d.Quality.ToString(), d.Quality != IdentityQuality.StableSerial);
}
