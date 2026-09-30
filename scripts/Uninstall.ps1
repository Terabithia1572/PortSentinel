[CmdletBinding()]
param([switch]$KeepFiles, [switch]$RemoveInstalledPayload)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Setup.Common.ps1')
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Yükseltilmiş PowerShell gerekir.' }
$paths = Get-PortSentinelPaths
$target = $paths.Target; $data = $paths.Data
$service = Get-CimInstance Win32_Service -Filter "Name='PortSentinel'"
$state = Get-OwnedInstallation $target $data $service
if (-not $state.Receipt -and $state.Manifest.Count -eq 0) { throw 'Kurulum sahipliği doğrulanamadı.' }
if ($state.Receipt.InstalledBy -eq 'InnoSetup' -and -not $KeepFiles -and -not $RemoveInstalledPayload) { throw 'Bu kurulum Inno Setup tarafından yönetilir. Windows Uygulamalar listesinden PortSentinel kaldırıcıyı çalıştırın.' }
$paths = @()
foreach ($file in $state.Manifest) {
    $resolved = Resolve-PayloadPath $target $file.Path
    if (Test-Path -LiteralPath $resolved -PathType Leaf) { $paths += $resolved }
}
if ($service) {
    Stop-OwnedService
    & sc.exe delete PortSentinel
    if ($LASTEXITCODE -ne 0) { throw 'Servis silinemedi; dosyalar korundu.' }
}
if ($KeepFiles) {
    Write-Host 'Sahiplik/hash kontrolü tamamlandı ve servis kaydı kaldırıldı. Dosyaları Inno Setup kaldıracak; ProgramData ve Windows politikaları korunur.'
    return
}
# Yalnız önceden doğrulanmış paket dosyalarını sil. ProgramData/bilinmeyen dosyalar kalır.
foreach ($path in $paths) { Remove-Item -LiteralPath $path }
if (-not (Test-Path -LiteralPath $target)) { return }
$manifestPath = Join-Path $target 'checksums.json'
if (Test-Path -LiteralPath $manifestPath) { Remove-Item -LiteralPath $manifestPath }
$dirs = @(Get-ChildItem -LiteralPath $target -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending)
foreach ($dir in $dirs) {
    $absolute = [IO.Path]::GetFullPath($dir.FullName)
    if (-not $absolute.StartsWith($target + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Dizin kapsamı geçersiz.' }
    if (@(Get-ChildItem -LiteralPath $absolute -Force).Count -eq 0) { Remove-Item -LiteralPath $absolute }
}
if (@(Get-ChildItem -LiteralPath $target -Force).Count -eq 0) { Remove-Item -LiteralPath $target }
Write-Host "Servis ve sahip olunan dosyalar kaldırıldı. Veri/yedekler korundu: $data. Mevcut Windows politikaları değiştirilmedi."
