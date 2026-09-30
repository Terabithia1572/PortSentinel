using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using PortSentinel.Contracts;
using PortSentinel.Infrastructure.Ipc;
using Xunit;
using Xunit.Abstractions;

namespace PortSentinel.IntegrationTests;

[Trait("Category", "WindowsIpc")]
public sealed class WindowsIpcTests(ITestOutputHelper output)
{
    [Fact] public async Task Pipe_server_authenticates_real_Windows_identity()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        var name = "PortSentinel.Test." + Guid.NewGuid().ToString("N");
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var server = new PipeServer(name, TestCoordinator.Create(f.Store, new()), new TestLogger(output));
        var running = server.RunAsync(ct.Token);
        try
        {
            var response = await new PipeSentinelClient(name, development: true).SendAsync(new(1, Operation.Snapshot), ct.Token);
            using var identity = WindowsIdentity.GetCurrent();
            Assert.Equal(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator), response.Snapshot!.IsAdministrator);
            Assert.False(response.Snapshot.Backend.ProtectionVerified);
        }
        finally { await StopServerAsync(ct, running); }
    }
    [Fact] public async Task Restricted_standard_token_cannot_mutate_through_real_pipe()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        var name = "PortSentinel.Test." + Guid.NewGuid().ToString("N");
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var server = new PipeServer(name, TestCoordinator.Create(f.Store, new()), new TestLogger(output));
        var running = server.RunAsync(ct.Token);
        using var identity = WindowsIdentity.GetCurrent();
        var sid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var sidBytes = new byte[sid.BinaryLength]; sid.GetBinaryForm(sidBytes, 0); var pinned = GCHandle.Alloc(sidBytes, GCHandleType.Pinned);
        SafeAccessTokenHandle? token = null;
        try
        {
            var disable = new SidAndAttributes { Sid = pinned.AddrOfPinnedObject() };
            if (!CreateRestrictedToken(identity.AccessToken, 1, 1, [disable], 0, IntPtr.Zero, 0, IntPtr.Zero, out token))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var response = await WindowsIdentity.RunImpersonatedAsync(token, () => new PipeSentinelClient(name, true)
                .SendAsync(new(1, Operation.Settings, ScanIntervalSeconds: 7), ct.Token));
            Assert.False(response.Success); Assert.Equal("Unauthorized", response.Error!.Code);
            Assert.Equal(10, (await f.Store.ReadAsync(default)).Settings.ScanIntervalSeconds);
        }
        finally { token?.Dispose(); pinned.Free(); await StopServerAsync(ct, running); }
    }
    [Fact] public async Task Spoofed_admin_field_is_rejected_and_server_accepts_next_connection()
    {
        await using var f = new DatabaseFixture(); await f.InitializeAsync();
        var name = "PortSentinel.Test." + Guid.NewGuid().ToString("N");
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var running = new PipeServer(name, TestCoordinator.Create(f.Store, new()), NullLogger<PipeServer>.Instance).RunAsync(ct.Token);
        try
        {
            await using (var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation))
            {
                await pipe.ConnectAsync(5000, ct.Token);
                await PipeProtocol.WriteAsync(pipe, new { version = 1, operation = "Snapshot", admin = true }, 65536, ct.Token);
                var rejection = await PipeProtocol.ReadAsync<Response>(pipe, PipeProtocol.MaxResponseBytes, ct.Token);
                Assert.Equal("InvalidMessage", rejection.Error!.Code);
            }
            Assert.True((await new PipeSentinelClient(name, true).SendAsync(new(1, Operation.Snapshot), ct.Token)).Success);
        }
        finally { await StopServerAsync(ct, running); }
    }
    private static async Task StopServerAsync(CancellationTokenSource cancellation, Task server)
    {
        await cancellation.CancelAsync();
        // Cancellation may end the loop between requests or interrupt WaitForConnectionAsync.
        // Both are valid shutdowns; authentication assertions above must not depend on that race.
        try { await server; }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
    }
    [StructLayout(LayoutKind.Sequential)] private struct SidAndAttributes { public IntPtr Sid; public uint Attributes; }
    private sealed class TestLogger(ITestOutputHelper output) : ILogger<PipeServer>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> format)
            => output.WriteLine(format(state, exception) + "\n" + exception);
    }
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateRestrictedToken(SafeAccessTokenHandle existing, uint flags, uint disableCount,
        [In] SidAndAttributes[] disable, uint deleteCount, IntPtr privileges, uint restrictCount, IntPtr restrictions, out SafeAccessTokenHandle token);
}
