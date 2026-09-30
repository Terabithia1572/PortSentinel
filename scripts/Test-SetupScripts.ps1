# Servis/Windows ayarı değiştirmeyen, özellikle Windows PowerShell 5.1 regresyon testi.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Setup.Common.ps1')
$workspace = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$root = Join-Path $workspace ('artifacts/setup-script-tests/' + [Guid]::NewGuid().ToString('N'))
$target = Join-Path $root 'program'; $data = Join-Path $root 'data'
New-Item -ItemType Directory -Path (Join-Path $target 'service'),(Join-Path $target 'desktop') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $target 'service/PortSentinel.Service.exe'),'service fixture')
[IO.File]::WriteAllText((Join-Path $target 'desktop/PortSentinel.Desktop.exe'),'desktop fixture')
[IO.File]::WriteAllText((Join-Path $target 'installer-owner.txt'),$script:PortSentinelOwner)
$manifest = @(Get-ChildItem -LiteralPath $target -File -Recurse | ForEach-Object { [pscustomobject]@{ Path=$_.FullName.Substring($target.Length + 1); SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $target 'checksums.json') -Encoding UTF8
$passed = 0
function Assert-True([bool]$Condition, [string]$Name) { if (-not $Condition) { throw "FAILED: $Name" }; $script:passed++; Write-Host "PASS: $Name" }
function Assert-Rejected([scriptblock]$Action, [string]$Name) {
    $rejected = $false; try { & $Action | Out-Null } catch { $rejected = $true }
    Assert-True $rejected $Name
}
try {
    Assert-True (@(Read-PayloadManifest $target).Count -eq 3) 'PS 5.1 JSON array has scalar Path entries'
    $emptyProgram = Join-Path $root 'empty-program'; New-Item -ItemType Directory -Path $emptyProgram | Out-Null
    Assert-True (Get-OwnedInstallation $emptyProgram $data $null).Repair 'Empty directory left by older uninstaller can be reused'
    Assert-True (Get-OwnedInstallation $target $data $null).Repair 'Partial install without ProgramData can be repaired'
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    Assert-True (Get-OwnedInstallation $target $data $null).Repair 'Owned partial install with empty data can be repaired'
    Remove-Item -LiteralPath $data
    Assert-Rejected { Resolve-PayloadPath $target '../outside.txt' } 'Path traversal rejected'
    Assert-Rejected { Get-OwnedInstallation $target $data ([pscustomobject]@{PathName='C:\foreign\service.exe'}) } 'Foreign SCM service rejected'
    [IO.File]::WriteAllText((Join-Path $target 'desktop/PortSentinel.Desktop.exe'),'modified')
    Assert-Rejected { Get-OwnedInstallation $target $data $null } 'Modified payload is preserved'
    Remove-Item -LiteralPath (Join-Path $target 'desktop/PortSentinel.Desktop.exe')
    Assert-True (Get-OwnedInstallation $target $data $null).Repair 'Missing payload files can be repaired'
    Remove-Item -LiteralPath (Join-Path $target 'installer-owner.txt')
    Assert-Rejected { Get-OwnedInstallation $target $data $null } 'Unowned program directory rejected'
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    Assert-Rejected { Get-OwnedInstallation (Join-Path $root 'absent') $data $null } 'Unowned data directory rejected'
    Write-Host "Setup script tests: $passed passed; PowerShell $($PSVersionTable.PSVersion)."
} finally {
    $absolute = [IO.Path]::GetFullPath($root)
    if (-not $absolute.StartsWith($workspace + '\artifacts\setup-script-tests\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture cleanup escaped workspace.' }
    Assert-SafeTree $absolute
    Remove-Item -LiteralPath $absolute -Recurse -Force
}
