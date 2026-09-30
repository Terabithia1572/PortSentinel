using PortSentinel.Domain;
using Xunit;

namespace PortSentinel.UnitTests;

public sealed class DomainTests
{
    internal static DeviceObservation Device(string serial = "SERIAL001", string? instance = null, bool unique = true) => new(new()
    { PhysicalInstanceId = instance ?? $"USB\\VID_1234&PID_ABCD\\{serial}", SerialNumber = serial, VendorId = "1234", ProductId = "ABCD",
        HasUniqueId = unique, IsUsbStorage = true }, "Aynı model", "Üretici", 1000, "USBSTOR", ["USBSTOR\\" + serial], [new("partition1", "E:", 1000)]);

    [Fact] public void Ten_devices_have_independent_decisions()
    {
        var devices = Enumerable.Range(0, 10).Select(i => Device("SERIAL" + i)).ToArray();
        var allowed = devices.Where((_, i) => i % 2 == 0).Select(d => d.Identity.Key).ToHashSet();
        var result = AccessEvaluator.Evaluate(devices, allowed, true);
        Assert.Equal(5, result.Count(r => r.Effective == AccessState.Allowed));
        Assert.Equal(5, result.Count(r => r.Effective == AccessState.Blocked));
    }
    [Fact] public void Same_model_different_serials_do_not_share_permission()
    {
        var a = Device("SERIAL_A"); var b = Device("SERIAL_B");
        var result = AccessEvaluator.Evaluate([a, b], new HashSet<string> { a.Identity.Key }, true);
        Assert.Equal(AccessState.Allowed, result[0].Effective); Assert.Equal(AccessState.Blocked, result[1].Effective);
    }
    [Theory] [InlineData("")] [InlineData("0000")] [InlineData("*")] [InlineData("SERIAL?1")] [InlineData("A")]
    public void Missing_or_unsafe_serial_is_ambiguous(string serial) => Assert.Equal(IdentityQuality.Ambiguous, Device(serial).Identity.Quality);
    [Fact] public void Missing_unique_capability_is_port_dependent() => Assert.Equal(IdentityQuality.Ambiguous, Device(unique: false).Identity.Quality);
    [Fact] public void Duplicate_serial_on_distinct_physical_nodes_is_denied()
    {
        var a = Device(instance: "USB\\port1"); var b = Device(instance: "USB\\port2");
        var results = AccessEvaluator.Evaluate([a, b], new HashSet<string> { a.Identity.Key }, true);
        Assert.All(results, r => { Assert.Equal(AccessState.AmbiguousIdentity, r.Desired); Assert.Equal(AccessState.Blocked, r.Effective); });
    }
    [Fact] public void Stable_serial_follows_port_and_hub_in_model()
    {
        var a = Device(instance: "USB\\port1"); var b = Device(instance: "USB\\hub\\port2");
        Assert.Equal(a.Identity.Key, b.Identity.Key);
    }
    [Theory] [InlineData(false, false)] [InlineData(true, true)]
    public void Non_storage_and_system_disks_are_out_of_scope(bool usb, bool system)
    {
        var a = Device(); a = a with { Identity = a.Identity with { IsUsbStorage = usb, IsSystemDisk = system } };
        Assert.Equal(AccessState.OutOfScope, AccessEvaluator.Evaluate([a], new HashSet<string>(), true)[0].Effective);
    }
    [Fact] public void Multiple_partitions_share_physical_rule()
    {
        var a = Device() with { Transport = "UASP", Volumes = [new("p1", "E:", 500), new("p2", "F:", 500)] };
        Assert.Single(AccessEvaluator.Evaluate([a], new HashSet<string> { a.Identity.Key }, true));
    }
    [Fact] public void Desired_deny_does_not_claim_physical_block_without_verified_engine()
    {
        var result = AccessEvaluator.Evaluate([Device()], new HashSet<string>(), false)[0];
        Assert.Equal(AccessState.Blocked, result.Desired); Assert.Equal(AccessState.Unverified, result.Effective);
    }
    [Fact] public void Case_and_whitespace_normalization_is_stable()
    {
        var a = Device().Identity;
        Assert.Equal(a.Key, (a with { SerialNumber = " serial001 ", ProductId = "abcd" }).Key);
    }
}
