[CmdletBinding()]
param([switch]$KeepFiles)
$ErrorActionPreference = 'Stop'
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Yükseltilmiş PowerShell gerekir.' }
$target = [IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'PortSentinel'))
$data = [IO.Path]::GetFullPath((Join-Path $env:ProgramData 'PortSentinel'))
if (((Get-Item -LiteralPath $target -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Kurulum dizini reparse point; kaldırma reddedildi.' }
if (((Get-Item -LiteralPath $data -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Veri dizini reparse point; kaldırma reddedildi.' }
$receipt = Get-Content -LiteralPath (Join-Path $data 'install-receipt.json') -Raw | ConvertFrom-Json
if ($receipt.Product -ne 'PortSentinel' -or [IO.Path]::GetFullPath($receipt.InstallPath) -ne $target) { throw 'Kurulum sahipliği doğrulanamadı.' }
if ($receipt.InstalledBy -eq 'InnoSetup' -and -not $KeepFiles) { throw 'Bu kurulum Inno Setup tarafından yönetilir. Windows Uygulamalar listesinden PortSentinel kaldırıcıyı çalıştırın.' }
if (@($receipt.WindowsPolicyChanges).Count -ne 0) { throw 'Bu kaldırıcı politika değişikliği içeren yeni backend sürümünü geri alamaz.' }
$tree = @(Get-ChildItem -LiteralPath $target -Force -Recurse)
if (@($tree | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -ne 0) { throw 'Kurulum ağacında reparse point var; kaldırma reddedildi.' }
$service = Get-CimInstance Win32_Service -Filter "Name='PortSentinel'"
if ($service -and $service.PathName.Trim('"') -ne $receipt.ServiceBinary) { throw 'Servis başka araçla değişmiş; kayıt silinmez.' }
$paths = @()
foreach ($file in $receipt.Files) {
    $resolved = [IO.Path]::GetFullPath($file.Path)
    if (-not $resolved.StartsWith($target + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Dosya kapsamı kurulum dizini dışında.' }
    $cursor = $resolved
    while ($cursor -and $cursor.StartsWith($target, [StringComparison]::OrdinalIgnoreCase)) {
        if ((Test-Path -LiteralPath $cursor) -and (((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) { throw 'Reparse point içeren dosya silinmez.' }
        $cursor = Split-Path -Parent $cursor
    }
    if (Test-Path -LiteralPath $resolved) {
        if ((Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash -ne $file.SHA256) { throw "Sonradan değiştirilmiş dosya korunur: $resolved" }
        $paths += $resolved
    }
}
if ($service) {
    Stop-Service PortSentinel
    & sc.exe delete PortSentinel
    if ($LASTEXITCODE -ne 0) { throw 'Servis silinemedi; dosyalar korundu.' }
}
if ($KeepFiles) {
    Write-Host 'Sahiplik/hash kontrolü tamamlandı ve servis kaydı kaldırıldı. Dosyaları Inno Setup kaldıracak; ProgramData ve Windows politikaları korunur.'
    return
}
# Only receipt-owned, unchanged files are removed. ProgramData and unknown files remain.
foreach ($path in $paths) { Remove-Item -LiteralPath $path }
$dirs = @(Get-ChildItem -LiteralPath $target -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending)
foreach ($dir in $dirs) {
    $absolute = [IO.Path]::GetFullPath($dir.FullName)
    if (-not $absolute.StartsWith($target + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Dizin kapsamı geçersiz.' }
    if (@(Get-ChildItem -LiteralPath $absolute -Force).Count -eq 0) { Remove-Item -LiteralPath $absolute }
}
if (@(Get-ChildItem -LiteralPath $target -Force).Count -eq 0) { Remove-Item -LiteralPath $target }
Write-Host "Servis ve sahip olunan dosyalar kaldırıldı. Veri/yedekler korundu: $data. Mevcut Windows politikaları değiştirilmedi."
