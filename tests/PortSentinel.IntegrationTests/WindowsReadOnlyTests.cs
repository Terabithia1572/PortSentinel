using Microsoft.Extensions.Logging.Abstractions;
using PortSentinel.Infrastructure.Windows;
using Xunit;

namespace PortSentinel.IntegrationTests;

[Trait("Category", "WindowsReadOnly")]
public sealed class WindowsReadOnlyTests
{
    [Fact] public async Task Real_Windows_inventory_returns_only_USB_ancestry_disks()
    {
        var devices = await new WindowsDeviceInventory(new()).ScanAsync(default);
        Assert.All(devices, d => { Assert.True(d.Identity.IsUsbStorage); Assert.StartsWith("USB\\VID_", d.Identity.PhysicalInstanceId); Assert.NotEmpty(d.DiskInstanceIds); });
    }
    [Fact] public async Task Real_Defender_assessment_never_claims_enforcement()
    {
        var backend = new DefenderAssessmentBackend(new(), NullLogger<DefenderAssessmentBackend>.Instance);
        var assessment = await backend.AssessAsync(default);
        Assert.False(assessment.ProtectionVerified); Assert.NotEmpty(assessment.Diagnostics);
        Assert.Contains(assessment.Diagnostics, d => d.StartsWith("Windows:"));
    }
}
