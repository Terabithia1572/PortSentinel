using System.IO.Pipes;
using System.Security.Principal;
using PortSentinel.Contracts;

namespace PortSentinel.Infrastructure.Ipc;

public sealed class PipeSentinelClient(string pipeName, bool development = false) : ISentinelClient
{
    public async Task<Response> SendAsync(Request request, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous,
            TokenImpersonationLevel.Impersonation);
        if (!development) PipePeerVerifier.RequireRunningService();
        try { await pipe.ConnectAsync(5000, deadline.Token); }
        catch (TimeoutException ex)
        {
            throw new IOException("PortSentinel servisi bağlantı açmadı. Biraz sonra Yenile'ye basın; devam ederse güncel setup'ı yeniden çalıştırarak kurulumu onarın.", ex);
        }
        if (!development) PipePeerVerifier.VerifyService(pipe);
        await PipeProtocol.WriteAsync(pipe, request, PipeProtocol.MaxRequestBytes, deadline.Token);
        return await PipeProtocol.ReadAsync<Response>(pipe, PipeProtocol.MaxResponseBytes, deadline.Token);
    }
}
