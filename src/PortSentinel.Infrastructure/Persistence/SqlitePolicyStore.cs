using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortSentinel.Application;
using PortSentinel.Domain;

namespace PortSentinel.Infrastructure.Persistence;

public sealed class SqlitePolicyStore(IDbContextFactory<SentinelDbContext> factory) : IPolicyStore
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
        await db.Revisions.Where(r => r.State == RevisionState.Pending).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.State, RevisionState.Interrupted)
            .SetProperty(r => r.Details, "Servis önceki politika işlemi tamamlanmadan durdu. Windows etkin politikası doğrulanamadı; otomatik serbest bırakma yapılmadı."), ct);
    }
    public async Task<StoreSnapshot> ReadAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var data = new StoreSnapshot(await db.Devices.AsNoTracking().OrderBy(d => d.DisplayName).ToArrayAsync(ct),
            await db.Rules.AsNoTracking().ToArrayAsync(ct),
            await db.Connections.AsNoTracking().OrderByDescending(e => e.Id).Take(100).ToArrayAsync(ct),
            await db.Audit.AsNoTracking().OrderByDescending(e => e.Id).Take(100).ToArrayAsync(ct),
            await db.Revisions.AsNoTracking().OrderByDescending(r => r.Id).Take(50).ToArrayAsync(ct),
            await db.Settings.AsNoTracking().SingleAsync(ct));
        await tx.CommitAsync(ct);
        return data;
    }
    public async Task<PolicyRevision> ChangeRuleAsync(DeviceObservation? device, Guid? registeredId, bool allow,
        string? name, long expectedRevision, Caller caller, string correlationId, CancellationToken ct)
    {
        RequireAdmin(caller);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await db.Revisions.MaxAsync(r => (long?)r.Id, ct) ?? 0;
        if (current != expectedRevision) throw new BusinessException("PolicyConflict", "Politika başka bir işlemle değişti. Listeyi yenileyin.");
        RegisteredDevice registered;
        if (allow)
        {
            if (device == null || device.Identity.Quality != IdentityQuality.StableSerial || !device.Identity.IsUsbStorage || device.Identity.IsSystemDisk)
                throw new BusinessException("UnsafeIdentity", "Belirsiz veya kapsam dışı kimliğe izin verilemez.");
            if (await db.Devices.CountAsync(ct) >= 200 && !await db.Devices.AnyAsync(d => d.IdentityKey == device.Identity.Key, ct))
                throw new BusinessException("Capacity", "200 cihaz kayıt sınırına ulaşıldı.");
            registered = await db.Devices.SingleOrDefaultAsync(d => d.IdentityKey == device.Identity.Key, ct) ?? new RegisteredDevice
            { Id = Guid.NewGuid(), IdentityKey = device.Identity.Key, Identity = device.Identity,
                DisplayName = name!, Manufacturer = device.Manufacturer, CreatedUtc = DateTime.UtcNow };
            if (db.Entry(registered).State == EntityState.Detached) db.Devices.Add(registered);
            var existing = await db.Rules.SingleOrDefaultAsync(r => r.RegisteredDeviceId == registered.Id, ct);
            if (existing?.Allowed == true) throw new BusinessException("DuplicateDevice", "Bu fiziksel cihaz için zaten açık izin var.");
        }
        else registered = await db.Devices.SingleOrDefaultAsync(d => d.Id == registeredId, ct)
            ?? throw new BusinessException("NotFound", "Cihaz kaydı bulunamadı.");
        var rule = await db.Rules.SingleOrDefaultAsync(r => r.RegisteredDeviceId == registered.Id, ct);
        if (!allow && rule?.Allowed != true) throw new BusinessException("AlreadyRevoked", "Bu cihazın açık izni yok.");
        if (rule == null) { rule = new() { Id = Guid.NewGuid(), RegisteredDeviceId = registered.Id }; db.Rules.Add(rule); }
        rule.Allowed = allow; rule.ModifiedUtc = DateTime.UtcNow; rule.ActorSid = caller.Sid;
        await db.SaveChangesAsync(ct);
        var keys = await db.Rules.Where(r => r.Allowed).Join(db.Devices, r => r.RegisteredDeviceId, d => d.Id, (_, d) => d.IdentityKey).OrderBy(k => k).ToArrayAsync(ct);
        var revision = new PolicyRevision { CreatedUtc = DateTime.UtcNow, State = RevisionState.Pending,
            DesiredPolicyJson = JsonSerializer.Serialize(new { defaultUsbStorage = "deny", allowedIdentityKeys = keys }),
            Details = "İstenen politika kaydedildi; Windows uygulaması bekleniyor." };
        db.Revisions.Add(revision);
        db.Audit.Add(NewAudit(caller, allow ? "Grant" : "Revoke", "DesiredSaved", registered.IdentityKey, correlationId));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return revision;
    }
    public async Task RenameAsync(Guid id, string name, Caller caller, string correlationId, CancellationToken ct)
    {
        RequireAdmin(caller);
        await using var db = await factory.CreateDbContextAsync(ct);
        var device = await db.Devices.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new BusinessException("NotFound", "Cihaz bulunamadı.");
        device.DisplayName = name;
        db.Audit.Add(NewAudit(caller, "Rename", "Başarılı", id.ToString(), correlationId));
        await db.SaveChangesAsync(ct);
    }
    public async Task UpdateSettingsAsync(int scanSeconds, int retentionDays, Caller caller, string correlationId, CancellationToken ct)
    {
        RequireAdmin(caller);
        await using var db = await factory.CreateDbContextAsync(ct);
        var s = await db.Settings.SingleAsync(ct); s.ScanIntervalSeconds = scanSeconds; s.RetentionDays = retentionDays;
        db.Audit.Add(NewAudit(caller, "Settings", "Başarılı", $"Tarama={scanSeconds}s; saklama={retentionDays}gün", correlationId));
        await db.SaveChangesAsync(ct);
    }
    public async Task CompleteRevisionAsync(long id, PolicyApplyResult result, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var revision = await db.Revisions.SingleAsync(r => r.Id == id, ct);
        revision.State = result.State; revision.Details = result.Details;
        revision.VerifiedUtc = result.State == RevisionState.Verified ? DateTime.UtcNow : null;
        await db.SaveChangesAsync(ct);
    }
    public async Task RecordConnectionsAsync(IReadOnlyList<DeviceConnectionEvent> events, CancellationToken ct)
    { await using var db = await factory.CreateDbContextAsync(ct); db.Connections.AddRange(events); await db.SaveChangesAsync(ct); }
    public async Task AuditAsync(AuditEvent audit, CancellationToken ct)
    { await using var db = await factory.CreateDbContextAsync(ct); db.Audit.Add(audit); await db.SaveChangesAsync(ct); }
    public async Task PruneAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var days = (await db.Settings.SingleAsync(ct)).RetentionDays;
        var cutoff = DateTime.UtcNow.AddDays(-days);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Audit.Where(e => e.OccurredUtc < cutoff).ExecuteDeleteAsync(ct);
        await db.Connections.Where(e => e.OccurredUtc < cutoff).ExecuteDeleteAsync(ct);
        // Bound row growth even during connection storms; policy revisions are retained for reconciliation.
        var auditBoundary = await db.Audit.OrderByDescending(e => e.Id).Skip(9999).Select(e => (long?)e.Id).FirstOrDefaultAsync(ct);
        if (auditBoundary.HasValue) await db.Audit.Where(e => e.Id < auditBoundary.Value).ExecuteDeleteAsync(ct);
        var eventBoundary = await db.Connections.OrderByDescending(e => e.Id).Skip(9999).Select(e => (long?)e.Id).FirstOrDefaultAsync(ct);
        if (eventBoundary.HasValue) await db.Connections.Where(e => e.Id < eventBoundary.Value).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
    }
    private static void RequireAdmin(Caller caller) { if (!caller.IsAdministrator) throw new BusinessException("Unauthorized", "Windows yönetici yetkisi gerekiyor."); }
    private static AuditEvent NewAudit(Caller caller, string operation, string outcome, string details, string correlationId) => new()
    { OccurredUtc = DateTime.UtcNow, ActorSid = caller.Sid, Operation = operation, Outcome = outcome, Details = details, CorrelationId = correlationId };
}
