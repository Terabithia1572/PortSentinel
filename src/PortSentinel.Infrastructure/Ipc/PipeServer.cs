using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PortSentinel.Application;
using PortSentinel.Contracts;

namespace PortSentinel.Infrastructure.Ipc;

public sealed class PipeServer(string pipeName, SentinelCoordinator coordinator, ILogger<PipeServer> logger)
{
    public static PipeSecurity CreateSecurity()
    {
        var acl = new PipeSecurity();
        acl.SetAccessRuleProtection(true, false);
        var owner = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Servis Windows kimliği yok.");
        acl.SetOwner(owner);
        acl.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.ReadWrite, AccessControlType.Deny));
        acl.AddAccessRule(new(owner, PipeAccessRights.FullControl, AccessControlType.Allow));
        acl.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        acl.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        acl.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        return acl;
    }
    public async Task RunAsync(CancellationToken ct)
    {
        // A single reused pipe instance bounds resource use and removes gaps between accepted clients.
        await using var pipe = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous, 4096, 4096, CreateSecurity());
        while (!ct.IsCancellationRequested)
        {
            await pipe.WaitForConnectionAsync(ct);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(60));
            try
            {
                var request = await PipeProtocol.ReadAsync<Request>(pipe, PipeProtocol.MaxRequestBytes, deadline.Token);
                Caller? caller = null;
                pipe.RunAsClient(() =>
                {
                    using var identity = WindowsIdentity.GetCurrent(true) ?? throw new UnauthorizedAccessException("İstemci token'ı alınamadı.");
                    caller = new(identity.User?.Value ?? throw new UnauthorizedAccessException("İstemci SID yok."),
                        new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator));
                });
                var response = await coordinator.ExecuteAsync(request, caller!, deadline.Token);
                await ReplyAsync(pipe, response, deadline.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or OperationCanceledException)
            {
                logger.LogWarning(ex, "Rejected IPC connection");
                if (pipe.IsConnected && !deadline.IsCancellationRequested)
                {
                    var correlation = Guid.NewGuid().ToString("N");
                    try { await ReplyAsync(pipe, new Response(false, correlation,
                        Error: new("InvalidMessage", "Geçersiz veya yetkisiz IPC mesajı.", correlation)), deadline.Token); }
                    catch (Exception writeError) when (writeError is IOException or OperationCanceledException)
                    { logger.LogDebug(writeError, "IPC client disconnected before rejection response"); }
                }
            }
            finally { pipe.Disconnect(); }
        }
    }
    private async Task ReplyAsync(NamedPipeServerStream pipe, Response response, CancellationToken ct)
    {
        await PipeProtocol.WriteAsync(pipe, response, PipeProtocol.MaxResponseBytes, ct);
        // DisconnectNamedPipe discards unread bytes. Let the client read its response and close first.
        using var linger = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linger.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var extra = await pipe.ReadAsync(new byte[1], linger.Token);
            if (extra > 0) logger.LogWarning("Rejected multiple requests on single IPC connection");
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        { logger.LogDebug(ex, "IPC peer close/linger timeout"); }
    }
}
