# Yalnız boş test makinesinde çalıştırın. Bu test gerçek SCM/Program Files/ProgramData kullanır.
[CmdletBinding()]
param([string]$InstallerPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Setup.Common.ps1')
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
[xml]$properties = Get-Content (Join-Path $root 'Directory.Build.props') -Raw
$version = [string]$properties.Project.PropertyGroup.Version
if (-not $InstallerPath) { $InstallerPath = Join-Path $root "artifacts/installer/PortSentinel-Setup-$version.exe" }
$paths = Get-PortSentinelPaths; $target = $paths.Target; $data = $paths.Data
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Lifecycle test requires an administrator.' }
if ((Get-Service PortSentinel -ErrorAction SilentlyContinue) -or (Test-Path -LiteralPath $target) -or (Test-Path -LiteralPath $data)) { throw 'Lifecycle test requires a machine without any PortSentinel service/program/data directory.' }
$output = Join-Path $root ('artifacts/lifecycle/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$results = [Collections.Generic.List[string]]::new()
function Assert-Lifecycle([bool]$Condition, [string]$Name) { if (-not $Condition) { throw "FAILED: $Name" }; $results.Add($Name); Write-Host "PASS: $Name" }
function Run-Installer([string]$Executable, [string]$Name) {
    $log = Join-Path $output "$Name.log"
    $process = Start-Process -FilePath $Executable -ArgumentList @('/SP-','/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + $log + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $log -Tail 30; throw "$Name failed: $($process.ExitCode)" }
}
function Assert-Running {
    $service = Get-CimInstance Win32_Service -Filter "Name='PortSentinel'"
    Assert-Lifecycle ($service.State -eq 'Running' -and $service.StartName -ieq 'NT AUTHORITY\LocalService' -and $service.PathName -eq ('"' + (Join-Path $target 'service/PortSentinel.Service.exe') + '"')) 'SCM Running, LocalService, quoted binary path'
    & (Join-Path $PSScriptRoot 'Test-ServiceConnection.ps1')
}
function Move-TestData([string]$Name) {
    $state = Get-OwnedInstallation $target $data (Get-CimInstance Win32_Service -Filter "Name='PortSentinel'")
    if ($state.Receipt.InstalledBy -ne 'InnoSetup') { throw 'Test data ownership mismatch.' }
    Stop-OwnedService
    Assert-SafeTree $data
    $destination = [IO.Path]::GetFullPath((Join-Path $output $Name))
    if ($data -ne (Get-PortSentinelPaths).Data -or -not $destination.StartsWith($root + '\artifacts\lifecycle\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Test data move escaped scope.' }
    Move-Item -LiteralPath $data -Destination $destination
}
try {
    Run-Installer $InstallerPath 'fresh-install'; Assert-Running
    $marker = Join-Path $data 'lifecycle-preserve.txt'; [IO.File]::WriteAllText($marker,'PortSentinel lifecycle test')
    Run-Installer $InstallerPath 'repair-running'; Assert-Running
    Assert-Lifecycle (Test-Path -LiteralPath $marker) 'Running-service repair preserves data'
    Stop-OwnedService
    $database = Join-Path $data 'portsentinel.db'; $hash = Get-PayloadHash $database
    Run-Installer (Join-Path $target 'unins000.exe') 'normal-uninstall'
    Assert-Lifecycle (-not (Get-Service PortSentinel -ErrorAction SilentlyContinue) -and -not (Test-Path -LiteralPath $target)) 'Normal uninstall removes service and payload'
    Assert-Lifecycle ((Get-PayloadHash $database) -eq $hash -and (Test-Path -LiteralPath $marker)) 'Uninstall preserves database byte-for-byte'
    Run-Installer $InstallerPath 'reinstall-with-data'; Assert-Running
    Assert-Lifecycle (Test-Path -LiteralPath $marker) 'Reinstall preserves old data'
    Move-TestData 'preserved-data'
    Run-Installer (Join-Path $target 'unins000.exe') 'missing-data-uninstall'
    Assert-Lifecycle (-not (Get-Service PortSentinel -ErrorAction SilentlyContinue) -and -not (Test-Path -LiteralPath $target) -and -not (Test-Path -LiteralPath $data)) 'Uninstall succeeds without ProgramData/receipt'
    @{Version=$version; Passed=$results.Count; Tests=@($results); WindowsPolicyWrites=0; RebootTested=$false} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'verification.json') -Encoding UTF8
    Write-Host "Installer lifecycle: $($results.Count) checks passed."
} finally {
    # Only our initially-empty fixture may be cleaned. Unknown or changed files make ownership validation fail.
    if (Test-Path -LiteralPath (Join-Path $target 'unins000.exe')) {
        $null = Get-OwnedInstallation $target $data (Get-CimInstance Win32_Service -Filter "Name='PortSentinel'")
        Run-Installer (Join-Path $target 'unins000.exe') 'cleanup-uninstall'
    }
    if (Test-Path -LiteralPath $data) { Move-TestData 'cleanup-data' }
}
