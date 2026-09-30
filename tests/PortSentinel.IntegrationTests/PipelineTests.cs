using PortSentinel.Contracts;
using Xunit;

namespace PortSentinel.IntegrationTests;

public sealed class PipelineTests
{
    [Fact] public async Task Standard_caller_cannot_change_policy_and_is_audited()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        var inventory = new TestInventory { Devices = [TestInventory.Device()] }; var backend = new AssessmentTestBackend();
        var app = TestCoordinator.Create(f.Store, inventory, backend);
        var response = await app.ExecuteAsync(new(1, Operation.Grant, inventory.Devices[0].Identity.PhysicalInstanceId, "a"), TestCoordinator.Reader, default);
        Assert.False(response.Success); Assert.Equal("Unauthorized", response.Error!.Code); Assert.Equal(0, backend.ApplyCalls);
        Assert.Empty((await f.Store.ReadAsync(default)).Devices); Assert.Single((await f.Store.ReadAsync(default)).Audit);
    }
    [Fact] public async Task Ten_simultaneous_grants_cannot_silently_overwrite_revision()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        var inventory = new TestInventory { Devices = Enumerable.Range(0, 10).Select(i => TestInventory.Device("SERIAL00" + i)).ToArray() };
        var app = TestCoordinator.Create(f.Store, inventory);
        var results = await Task.WhenAll(inventory.Devices.Select(d => app.ExecuteAsync(new(1, Operation.Grant, d.Identity.PhysicalInstanceId, "a"), TestCoordinator.Admin, default)));
        Assert.Single(results, r => r.Success); Assert.Equal(9, results.Count(r => r.Error?.Code == "PolicyConflict"));
    }
    [Fact] public async Task Disconnect_during_grant_does_not_register_missing_device()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        var inventory = new TestInventory(); var app = TestCoordinator.Create(f.Store, inventory);
        var result = await app.ExecuteAsync(new(1, Operation.Grant, TestInventory.Device().Identity.PhysicalInstanceId, "a"), TestCoordinator.Admin, default);
        Assert.Equal("DeviceDisconnected", result.Error!.Code); Assert.Empty((await f.Store.ReadAsync(default)).Devices);
    }
    [Fact] public async Task Scan_failure_prevents_registration_and_marks_stale_inventory()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        var inventory = new TestInventory { Devices = [TestInventory.Device()] }; var app = TestCoordinator.Create(f.Store, inventory);
        await app.ReconcileAsync(default); inventory.Fail = true;
        var grant = await app.ExecuteAsync(new(1, Operation.Grant, inventory.Devices[0].Identity.PhysicalInstanceId, "a"), TestCoordinator.Admin, default);
        Assert.Equal("InventoryUnavailable", grant.Error!.Code);
        var read = await app.ExecuteAsync(new(1, Operation.Snapshot), TestCoordinator.Reader, default); Assert.NotNull(read.Snapshot!.InventoryError);
    }
    [Fact] public async Task Backend_failure_preserves_desired_revision_with_failed_state()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        var inventory = new TestInventory { Devices = [TestInventory.Device()] };
        var app = TestCoordinator.Create(f.Store, inventory, new() { Throw = true });
        var result = await app.ExecuteAsync(new(1, Operation.Grant, inventory.Devices[0].Identity.PhysicalInstanceId, "a"), TestCoordinator.Admin, default);
        Assert.True(result.Command!.DesiredSaved); Assert.False(result.Command.EnforcementVerified);
        var snapshot = await app.ExecuteAsync(new(1, Operation.Snapshot), TestCoordinator.Reader, default);
        Assert.Equal("Failed", snapshot.Snapshot!.Revisions[0].State); Assert.Null(snapshot.Snapshot.EffectiveRevision); Assert.False(snapshot.Snapshot.Backend.ProtectionVerified);
    }
    [Fact] public async Task Database_failure_becomes_error_with_correlation()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        File.Delete(f.FilePath);
        var result = await TestCoordinator.Create(f.Store, new()).ExecuteAsync(new(1, Operation.Snapshot), TestCoordinator.Reader, default);
        Assert.False(result.Success); Assert.Equal("InternalError", result.Error!.Code); Assert.NotEmpty(result.CorrelationId);
    }
}
