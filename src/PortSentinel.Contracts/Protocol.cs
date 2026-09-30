using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortSentinel.Contracts;

public enum Operation { Snapshot, Grant, Revoke, Rename, Settings, Reconcile, PreviewPolicy }
public sealed record Request([property: JsonRequired] int Version, [property: JsonRequired] Operation Operation, string? DeviceId = null, string? Name = null,
    long ExpectedRevision = 0, int ScanIntervalSeconds = 10, int RetentionDays = 30);
public sealed record Error(string Code, string Message, string CorrelationId);
public sealed record Response(bool Success, string CorrelationId, SnapshotDto? Snapshot = null,
    CommandDto? Command = null, PolicyPreviewDto? Preview = null, Error? Error = null);
public sealed record CommandDto(bool DesiredSaved, bool EnforcementVerified, long Revision, string Message);
public sealed record PolicyPreviewDto(string GroupsXml, string RulesXml, string Warning);
public sealed record IdentityDto(string InstanceId, string Serial, string Vid, string Pid, string Quality, bool PortDependent);
public sealed record VolumeDto(string Partition, string? DriveLetter, long SizeBytes);
public sealed record DeviceDto(string Id, string Name, string Manufacturer, long SizeBytes, string Transport,
    IdentityDto Identity, IReadOnlyList<string> Disks, IReadOnlyList<VolumeDto> Volumes,
    string DesiredState, string EffectiveState, string Reason, bool CanGrant);
public sealed record RegisteredDto(string Id, string Name, IdentityDto Identity, bool Allowed, bool Connected);
public sealed record EventDto(DateTime OccurredUtc, string Kind, string Device, string Details);
public sealed record AuditDto(DateTime OccurredUtc, string ActorSid, string Operation, string Outcome, string Details, string CorrelationId);
public sealed record RevisionDto(long Id, string State, DateTime CreatedUtc, DateTime? VerifiedUtc, string Details);
public sealed record SettingsDto(int ScanIntervalSeconds, int RetentionDays);
public sealed record BackendDto(string Name, bool ProtectionVerified, string Status, IReadOnlyList<string> Diagnostics);
public sealed record SnapshotDto(DateTime CapturedUtc, DateTime? InventoryUtc, string? InventoryError, bool IsAdministrator,
    long DesiredRevision, long? EffectiveRevision, BackendDto Backend, IReadOnlyList<DeviceDto> Devices,
    IReadOnlyList<RegisteredDto> Registered, IReadOnlyList<EventDto> Events, IReadOnlyList<AuditDto> Audit,
    IReadOnlyList<RevisionDto> Revisions, SettingsDto Settings);

public static class PipeProtocol
{
    public const string ProductionPipe = "PortSentinel.v1";
    public const int Version = 1;
    public const int MaxRequestBytes = 65536;
    public const int MaxResponseBytes = 1048576;
    public static JsonSerializerOptions Json { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32 };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
    public static async Task<T> ReadAsync<T>(Stream stream, int limit, CancellationToken ct)
    {
        var prefix = new byte[4];
        await stream.ReadExactlyAsync(prefix, ct);
        var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length <= 0 || length > limit) throw new InvalidDataException("IPC mesaj boyutu geçersiz.");
        var data = new byte[length];
        await stream.ReadExactlyAsync(data, ct);
        return JsonSerializer.Deserialize<T>(data, Json) ?? throw new JsonException("Boş IPC mesajı.");
    }
    public static async Task WriteAsync<T>(Stream stream, T value, int limit, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (bytes.Length > limit) throw new InvalidDataException("IPC yanıt boyutu sınırı aşıldı.");
        var prefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, bytes.Length);
        await stream.WriteAsync(prefix, ct);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }
}
