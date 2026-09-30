using PortSentinel.Domain;

namespace PortSentinel.Application;

public sealed record Caller(string Sid, bool IsAdministrator);
public sealed record BackendAssessment(string Name, bool ProtectionVerified, string Status, IReadOnlyList<string> Diagnostics);
public sealed record PolicyApplyResult(RevisionState State, string Details);
public sealed record StoreSnapshot(IReadOnlyList<RegisteredDevice> Devices, IReadOnlyList<AccessRule> Rules,
    IReadOnlyList<DeviceConnectionEvent> Events, IReadOnlyList<AuditEvent> Audit, IReadOnlyList<PolicyRevision> Revisions,
    ApplicationSettings Settings)
{
    public long DesiredRevision => Revisions.Count == 0 ? 0 : Revisions.Max(r => r.Id);
}
public interface IDeviceInventory { Task<IReadOnlyList<DeviceObservation>> ScanAsync(CancellationToken ct); }
public interface IPolicyBackend
{
    Task<BackendAssessment> AssessAsync(CancellationToken ct);
    Task<PolicyApplyResult> ApplyAsync(PolicyRevision revision, CancellationToken ct);
}
public interface IPolicyStore
{
    Task InitializeAsync(CancellationToken ct);
    Task<StoreSnapshot> ReadAsync(CancellationToken ct);
    Task<PolicyRevision> ChangeRuleAsync(DeviceObservation? device, Guid? registeredId, bool allow,
        string? name, long expectedRevision, Caller caller, string correlationId, CancellationToken ct);
    Task RenameAsync(Guid id, string name, Caller caller, string correlationId, CancellationToken ct);
    Task UpdateSettingsAsync(int scanSeconds, int retentionDays, Caller caller, string correlationId, CancellationToken ct);
    Task CompleteRevisionAsync(long id, PolicyApplyResult result, CancellationToken ct);
    Task RecordConnectionsAsync(IReadOnlyList<DeviceConnectionEvent> events, CancellationToken ct);
    Task AuditAsync(AuditEvent audit, CancellationToken ct);
    Task PruneAsync(CancellationToken ct);
}
public sealed class BusinessException(string code, string message) : Exception(message)
{ public string Code { get; } = code; }
