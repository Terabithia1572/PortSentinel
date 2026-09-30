using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortSentinel.Application;
using PortSentinel.Domain;
using PortSentinel.Infrastructure.Persistence;

namespace PortSentinel.IntegrationTests;

internal sealed class DatabaseFixture : IAsyncDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "PortSentinelTest-" + Guid.NewGuid().ToString("N"));
    public string FilePath { get; }
    public SqlitePolicyStore Store { get; }
    public TestFactory Factory { get; }
    public DatabaseFixture()
    {
        Directory.CreateDirectory(path); FilePath = Path.Combine(path, "test.db");
        Factory = new(new DbContextOptionsBuilder<SentinelDbContext>().UseSqlite($"Data Source={FilePath};Pooling=False").Options);
        Store = new(Factory);
    }
    public async Task InitializeAsync() => await Store.InitializeAsync(default);
    public ValueTask DisposeAsync() { Directory.Delete(path, true); return ValueTask.CompletedTask; }
}
internal sealed class TestFactory(DbContextOptions<SentinelDbContext> options) : IDbContextFactory<SentinelDbContext>
{ public SentinelDbContext CreateDbContext() => new(options); }
internal sealed class TestInventory : IDeviceInventory
{
    public IReadOnlyList<DeviceObservation> Devices { get; set; } = [];
    public bool Fail { get; set; }
    public Task<IReadOnlyList<DeviceObservation>> ScanAsync(CancellationToken ct) => Fail ? throw new IOException("test inventory failure") : Task.FromResult(Devices);
    public static DeviceObservation Device(string serial = "SERIAL001") => new(new() { PhysicalInstanceId = "USB\\VID_1234&PID_ABCD\\" + serial,
        SerialNumber = serial, VendorId = "1234", ProductId = "ABCD", HasUniqueId = true, IsUsbStorage = true },
        "Test USB", "Üretici", 1000, "USBSTOR", ["USBSTOR\\" + serial], [new("p1", "E:", 1000)]);
}
internal sealed class AssessmentTestBackend : IPolicyBackend
{
    public int ApplyCalls { get; private set; }
    public bool Throw { get; set; }
    public Task<BackendAssessment> AssessAsync(CancellationToken ct) => Task.FromResult(new BackendAssessment("Test assessment", false, "Unverified", []));
    public Task<PolicyApplyResult> ApplyAsync(PolicyRevision revision, CancellationToken ct)
    { ApplyCalls++; if (Throw) throw new IOException("test failure"); return Task.FromResult(new PolicyApplyResult(RevisionState.Unverified, "Test only: unverified")); }
}
internal static class TestCoordinator
{
    public static SentinelCoordinator Create(IPolicyStore store, TestInventory inventory, AssessmentTestBackend? backend = null)
        => new(store, inventory, backend ?? new(), NullLogger<SentinelCoordinator>.Instance);
    public static Caller Admin { get; } = new("S-1-5-21-test-admin", true);
    public static Caller Reader { get; } = new("S-1-5-21-test-reader", false);
}
