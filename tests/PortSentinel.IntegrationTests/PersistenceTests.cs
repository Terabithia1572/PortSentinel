using Microsoft.EntityFrameworkCore;
using PortSentinel.Application;
using PortSentinel.Contracts;
using PortSentinel.Domain;
using Xunit;

namespace PortSentinel.IntegrationTests;

public sealed class PersistenceTests
{
    [Fact] public async Task Migrations_rule_and_audit_are_durable_across_restart()
    {
        await using var fixture = new DatabaseFixture(); await fixture.InitializeAsync();
        var revision = await fixture.Store.ChangeRuleAsync(TestInventory.Device(), null, true, "Cihaz", 0, TestCoordinator.Admin, "c1", default);
        await fixture.Store.CompleteRevisionAsync(revision.Id, new(RevisionState.Unverified, "No enforcement"), default);
        var restarted = new PortSentinel.Infrastructure.Persistence.SqlitePolicyStore(fixture.Factory); await restarted.InitializeAsync(default);
        var snapshot = await restarted.ReadAsync(default);
        Assert.Single(snapshot.Devices); Assert.True(Assert.Single(snapshot.Rules).Allowed);
        Assert.Equal(RevisionState.Unverified, Assert.Single(snapshot.Revisions).State);
        Assert.Null(snapshot.Revisions[0].VerifiedUtc); Assert.Single(snapshot.Audit);
    }
    [Fact] public async Task Duplicate_registration_and_revision_conflict_are_atomic()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        await f.Store.ChangeRuleAsync(TestInventory.Device(), null, true, "a", 0, TestCoordinator.Admin, "c", default);
        var duplicate = await Assert.ThrowsAsync<BusinessException>(() => f.Store.ChangeRuleAsync(TestInventory.Device(), null, true, "b", 1, TestCoordinator.Admin, "c", default));
        Assert.Equal("DuplicateDevice", duplicate.Code);
        var conflict = await Assert.ThrowsAsync<BusinessException>(() => f.Store.ChangeRuleAsync(TestInventory.Device("SERIAL002"), null, true, "b", 0, TestCoordinator.Admin, "c", default));
        Assert.Equal("PolicyConflict", conflict.Code);
        var data = await f.Store.ReadAsync(default); Assert.Single(data.Devices); Assert.Single(data.Revisions);
    }
    [Fact] public async Task Interrupted_revision_is_reconciled_without_claiming_enforcement()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        await f.Store.ChangeRuleAsync(TestInventory.Device(), null, true, "a", 0, TestCoordinator.Admin, "c", default);
        await f.Store.InitializeAsync(default);
        Assert.Equal(RevisionState.Interrupted, (await f.Store.ReadAsync(default)).Revisions[0].State);
    }
    [Fact] public async Task Permission_revoke_updates_desired_only_and_keeps_identity()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        await f.Store.ChangeRuleAsync(TestInventory.Device(), null, true, "a", 0, TestCoordinator.Admin, "c", default);
        var id = (await f.Store.ReadAsync(default)).Devices[0].Id;
        await f.Store.ChangeRuleAsync(null, id, false, null, 1, TestCoordinator.Admin, "c2", default);
        var data = await f.Store.ReadAsync(default); Assert.False(data.Rules[0].Allowed); Assert.Single(data.Devices); Assert.Equal(2, data.DesiredRevision);
    }
    [Fact] public async Task Store_checks_authorization_even_without_pipeline()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        await Assert.ThrowsAsync<BusinessException>(() => f.Store.ChangeRuleAsync(TestInventory.Device(), null, true, "a", 0, TestCoordinator.Reader, "c", default));
        Assert.Empty((await f.Store.ReadAsync(default)).Devices);
    }
    [Fact] public async Task Sqlite_unique_index_blocks_duplicate_identity()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        await f.Store.ChangeRuleAsync(TestInventory.Device(), null, true, "a", 0, TestCoordinator.Admin, "c", default);
        await using var db = f.Factory.CreateDbContext();
        db.Devices.Add(new() { Id = Guid.NewGuid(), IdentityKey = TestInventory.Device().Identity.Key, Identity = TestInventory.Device().Identity });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact] public async Task Retention_removes_old_events_and_audits()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        await f.Store.RecordConnectionsAsync([new() { OccurredUtc = DateTime.UtcNow.AddDays(-40) }, new() { OccurredUtc = DateTime.UtcNow }], default);
        await f.Store.AuditAsync(new() { OccurredUtc = DateTime.UtcNow.AddDays(-40) }, default);
        await f.Store.PruneAsync(default);
        Assert.Single((await f.Store.ReadAsync(default)).Events); Assert.Empty((await f.Store.ReadAsync(default)).Audit);
    }
}
