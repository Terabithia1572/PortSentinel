using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PortSentinel.Application;
using PortSentinel.Infrastructure.Ipc;

internal sealed class SentinelWorker(IPolicyStore store, SentinelCoordinator coordinator, PipeServer pipe,
    ILogger<SentinelWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        try
        {
            await store.InitializeAsync(stoppingToken);
            // Listen before the first CIM scan, which can take 25 seconds on some machines.
            var tasks = new[] { pipe.RunAsync(lifetime.Token), ReconciliationLoopAsync(lifetime.Token) };
            await Task.WhenAny(tasks);
            await lifetime.CancelAsync();
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            logger.LogCritical(ex, "Service worker failed; exiting with failure for SCM recovery");
            throw;
        }
    }
    private async Task ReconciliationLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await coordinator.ReconcileAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogError(ex, "Reconciliation failed; enforcement remains unverified"); }
            var settings = (await store.ReadAsync(ct)).Settings;
            await Task.Delay(TimeSpan.FromSeconds(settings.ScanIntervalSeconds), ct);
        }
    }
}
