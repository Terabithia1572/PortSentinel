using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PortSentinel.Application;
using PortSentinel.Contracts;
using PortSentinel.Infrastructure.Ipc;
using PortSentinel.Infrastructure.Persistence;
using PortSentinel.Infrastructure.Windows;
using Serilog;
using Serilog.Formatting.Compact;

var development = args.Contains("--development", StringComparer.Ordinal);
var baseDirectory = development ? Path.GetFullPath(Path.Combine(FindDevelopmentRoot(), "artifacts", "development"))
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PortSentinel");
if (development) Directory.CreateDirectory(baseDirectory);
else ProtectedStorage.Verify(baseDirectory);
using var databaseOwner = new FileStream(Path.Combine(baseDirectory, "service.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
var pipeName = development ? "PortSentinel.Development.v1" : PipeProtocol.ProductionPipe;
Log.Logger = new LoggerConfiguration().MinimumLevel.Information().MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning).Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(new CompactJsonFormatter(), Path.Combine(baseDirectory, "logs", "service-.jsonl"),
        rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 5 * 1024 * 1024, rollOnFileSizeLimit: true,
        retainedFileCountLimit: 30, retainedFileTimeLimit: TimeSpan.FromDays(30)).CreateLogger();
try
{
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [], ContentRootPath = AppContext.BaseDirectory });
    builder.Services.AddWindowsService(o => o.ServiceName = "PortSentinel");
    builder.Services.AddSerilog();
    builder.Services.AddDbContextFactory<SentinelDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(baseDirectory, "portsentinel.db")};Default Timeout=5;Pooling=False"));
    builder.Services.AddSingleton<IPolicyStore, SqlitePolicyStore>();
    builder.Services.AddSingleton<FixedPowerShell>();
    builder.Services.AddSingleton<IDeviceInventory, WindowsDeviceInventory>();
    builder.Services.AddSingleton<IPolicyBackend, DefenderAssessmentBackend>();
    builder.Services.AddSingleton<SentinelCoordinator>();
    builder.Services.AddSingleton(sp => new PipeServer(pipeName, sp.GetRequiredService<SentinelCoordinator>(), sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PipeServer>>()));
    builder.Services.AddHostedService<SentinelWorker>();
    using var host = builder.Build();
    Log.Information("Starting PortSentinel. Development={Development}; enforcement is unverified; no Windows policy writes.", development);
    await host.RunAsync();
}
catch (Exception ex) { Log.Fatal(ex, "PortSentinel terminated"); Environment.ExitCode = 1; }
finally { await Log.CloseAndFlushAsync(); }

static string FindDevelopmentRoot()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "PortSentinel.sln"))) return directory.FullName;
    throw new InvalidOperationException("Geliştirme modu PortSentinel kaynak çözümü altında çalıştırılmalıdır.");
}
