[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('Install','Uninstall')][string]$Action, [Parameter(Mandatory)][string]$LogPath, [string]$PackagePath)
$ErrorActionPreference = 'Stop'
try {
    if ($Action -eq 'Install') {
        if (-not $PackagePath) { throw 'Setup paket yolu eksik.' }
        $output = & (Join-Path $PSScriptRoot 'Install.ps1') -PackagePath $PackagePath -InnoSetup -SelfContained *>&1 | Out-String
    } else {
        $output = & (Join-Path $PSScriptRoot 'Uninstall.ps1') -RemoveInstalledPayload *>&1 | Out-String
    }
    [IO.File]::WriteAllText($LogPath, $output, [Text.UTF8Encoding]::new($true))
    Write-Host $output
    exit 0
} catch {
    $message = ($output + [Environment]::NewLine + ($_ | Out-String))
    [IO.File]::WriteAllText($LogPath, $message, [Text.UTF8Encoding]::new($true))
    Write-Error $_.Exception.Message -ErrorAction Continue
    exit 1
}
