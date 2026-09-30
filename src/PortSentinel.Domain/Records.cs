namespace PortSentinel.Domain;

public sealed class RegisteredDevice
{
    public Guid Id { get; set; }
    public string IdentityKey { get; set; } = "";
    public DeviceIdentity Identity { get; set; } = new();
    public string DisplayName { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}
public sealed class AccessRule
{
    public Guid Id { get; set; }
    public Guid RegisteredDeviceId { get; set; }
    public bool Allowed { get; set; }
    public DateTime ModifiedUtc { get; set; }
    public string ActorSid { get; set; } = "";
}
public sealed class DeviceConnectionEvent
{
    public long Id { get; set; }
    public DateTime OccurredUtc { get; set; }
    public string PhysicalInstanceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string Kind { get; set; } = "";
    public string IdentityJson { get; set; } = "";
}
public sealed class AuditEvent
{
    public long Id { get; set; }
    public DateTime OccurredUtc { get; set; }
    public string ActorSid { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string Details { get; set; } = "";
    public string CorrelationId { get; set; } = "";
}
public sealed class PolicyRevision
{
    public long Id { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? VerifiedUtc { get; set; }
    public RevisionState State { get; set; }
    public string DesiredPolicyJson { get; set; } = "";
    public string Details { get; set; } = "";
}
public sealed class ApplicationSettings
{
    public int Id { get; set; } = 1;
    public int ScanIntervalSeconds { get; set; } = 10;
    public int RetentionDays { get; set; } = 30;
}
