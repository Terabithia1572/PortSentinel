[CmdletBinding()]
param([switch]$Desktop)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if ($Desktop) { & dotnet run --project src/PortSentinel.Desktop -c Release --no-build --no-restore -- --development }
    else { & dotnet run --project src/PortSentinel.Service -c Release --no-build --no-restore -- --development }
    if ($LASTEXITCODE -ne 0) { throw 'Geliştirme süreci başarısız.' }
} finally { Pop-Location }
