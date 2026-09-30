[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if (-not ('PortSentinel.SetupPeer' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace PortSentinel {
    public static class SetupPeer {
        [DllImport("kernel32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);
    }
}
'@
}
function Read-PipeBytes($Pipe, [int]$Count, $Token) {
    $buffer = [byte[]]::new($Count); $offset = 0
    while ($offset -lt $Count) {
        $read = $Pipe.ReadAsync($buffer, $offset, $Count - $offset, $Token).GetAwaiter().GetResult()
        if ($read -eq 0) { throw 'Servis yanıt vermeden bağlantıyı kapattı.' }
        $offset += $read
    }
    return ,$buffer
}
$deadline = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(90))
$pipe = [IO.Pipes.NamedPipeClientStream]::new('.', 'PortSentinel.v1', [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous, [Security.Principal.TokenImpersonationLevel]::Impersonation)
try {
    $null = $pipe.ConnectAsync(30000, $deadline.Token).GetAwaiter().GetResult()
    [uint32]$peer = 0
    $service = Get-CimInstance Win32_Service -Filter "Name='PortSentinel'"
    if (-not [PortSentinel.SetupPeer]::GetNamedPipeServerProcessId($pipe.SafePipeHandle, [ref]$peer) -or $service.State -ne 'Running' -or $peer -ne $service.ProcessId) { throw 'IPC sunucusu çalışan PortSentinel Windows servisiyle eşleşmiyor.' }
    $request = [Text.Encoding]::UTF8.GetBytes('{"version":1,"operation":"Snapshot"}')
    $header = [BitConverter]::GetBytes([int]$request.Length)
    $null = $pipe.WriteAsync($header, 0, $header.Length, $deadline.Token).GetAwaiter().GetResult()
    $null = $pipe.WriteAsync($request, 0, $request.Length, $deadline.Token).GetAwaiter().GetResult()
    $size = [BitConverter]::ToInt32((Read-PipeBytes $pipe 4 $deadline.Token), 0)
    if ($size -lt 1 -or $size -gt 1048576) { throw 'Servis yanıt boyutu geçersiz.' }
    $response = [Text.Encoding]::UTF8.GetString((Read-PipeBytes $pipe $size $deadline.Token)) | ConvertFrom-Json
    if (-not $response.success -or -not $response.snapshot) { throw ('Servis hazır değil: ' + $response.error.message) }
    Write-Host "Servis hazır. LocalService/SCM PID ve Named Pipe yanıtı doğrulandı (PID $peer)."
} catch {
    throw ('PortSentinel servis bağlantısı doğrulanamadı. Kurulum tamamlanmadı. Servis günlükleri: C:\ProgramData\PortSentinel\logs. Ayrıntı: ' + $_.Exception.Message)
} finally { $pipe.Dispose(); $deadline.Dispose() }
