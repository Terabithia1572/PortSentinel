using System.Security.Cryptography;
using System.Text;

namespace PortSentinel.Domain;

public enum IdentityQuality { StableSerial, Ambiguous }
public enum AccessState { Allowed, Blocked, AmbiguousIdentity, Applying, Unverified, OutOfScope }
public enum RevisionState { Pending, Unverified, Interrupted, Failed, Verified }

public sealed record DeviceIdentity
{
    public string PhysicalInstanceId { get; init; } = "";
    public string SerialNumber { get; init; } = "";
    public string VendorId { get; init; } = "";
    public string ProductId { get; init; } = "";
    public bool HasUniqueId { get; init; }
    public bool IsUsbStorage { get; init; }
    public bool IsSystemDisk { get; init; }
    public string Key => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        string.Join('|', VendorId.Trim().ToUpperInvariant(), ProductId.Trim().ToUpperInvariant(), SerialNumber.Trim().ToUpperInvariant()))));
    public IdentityQuality Quality => HasUniqueId && SerialNumber.Length is >= 3 and <= 128
        && SerialNumber.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
        && SerialNumber.Any(c => c != '0') && VendorId.Length == 4 && ProductId.Length == 4
        && VendorId.All(Uri.IsHexDigit) && ProductId.All(Uri.IsHexDigit)
        ? IdentityQuality.StableSerial : IdentityQuality.Ambiguous;
}

public sealed record StorageVolume(string PartitionId, string? DriveLetter, long SizeBytes);
public sealed record DeviceObservation(DeviceIdentity Identity, string Name, string Manufacturer,
    long SizeBytes, string Transport, IReadOnlyList<string> DiskInstanceIds, IReadOnlyList<StorageVolume> Volumes);
public sealed record AccessDecision(AccessState Desired, AccessState Effective, string Reason);

public static class AccessEvaluator
{
    public static IReadOnlyList<AccessDecision> Evaluate(IReadOnlyList<DeviceObservation> devices,
        IReadOnlySet<string> allowedKeys, bool verified)
    {
        var collisions = devices.Where(d => d.Identity.Quality == IdentityQuality.StableSerial)
            .GroupBy(d => d.Identity.Key).Where(g => g.Select(d => d.Identity.PhysicalInstanceId)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1).Select(g => g.Key).ToHashSet();
        return devices.Select(d =>
        {
            var id = d.Identity;
            if (!id.IsUsbStorage || id.IsSystemDisk)
                return new AccessDecision(AccessState.OutOfScope, AccessState.OutOfScope, "USB depolama kapsamı dışında veya sistem diski.");
            if (id.Quality == IdentityQuality.Ambiguous || collisions.Contains(id.Key))
                return new AccessDecision(AccessState.AmbiguousIdentity, verified ? AccessState.Blocked : AccessState.Unverified,
                    "Seri/UniqueID eksik veya kimlik çakışıyor; fiziksel cihaza izin verilemez.");
            var desired = allowedKeys.Contains(id.Key) ? AccessState.Allowed : AccessState.Blocked;
            return new AccessDecision(desired, verified ? desired : AccessState.Unverified,
                verified ? "Doğrulanmış etkin politika." : "İstenen karar: " + (desired == AccessState.Allowed ? "izin" : "ret") + "; Windows erişim engeli doğrulanmadı.");
        }).ToArray();
    }
}
