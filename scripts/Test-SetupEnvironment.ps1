[CmdletBinding()]
param([string]$LogPath, [switch]$SkipAdministratorCheck)
$ErrorActionPreference = 'Stop'
function Verify-NoReparse([string]$Path) {
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if ((Test-Path -LiteralPath $cursor) -and (((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) { throw "Reparse dizin kabul edilmez: $cursor" }
        $parent = Split-Path -Parent $cursor
        if ($parent -eq $cursor) { break }; $cursor = $parent
    }
}
try {
    if (-not $SkipAdministratorCheck) {
        $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
        if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Kurulum için yönetici yetkisi gerekir.' }
    }
    $os = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    $x64 = @(Get-CimInstance Win32_Processor | Where-Object { $_.Architecture -eq 9 }).Count -gt 0
    if ([int]$os.CurrentMajorVersionNumber -lt 10 -or -not $x64) {
        throw 'Bu x64 paket Windows 10 veya Windows 11 içindir. Windows edisyonu, güncelleme sürümü ve destek tarihi kurulumu engellemez.'
    }
    $target = [IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'PortSentinel'))
    $data = [IO.Path]::GetFullPath((Join-Path $env:ProgramData 'PortSentinel'))
    Verify-NoReparse $target; Verify-NoReparse $data
    if (Get-Service -Name PortSentinel -ErrorAction SilentlyContinue) { throw 'PortSentinel servisi zaten var. Önce eski kurulumu Windows Uygulamalar listesinden kaldırın; veriler korunur.' }
    if (Test-Path -LiteralPath $target) { throw 'PortSentinel kurulum dizini zaten var. Mevcut veya sahipliği belirsiz dosyalar üzerine kurulum yapılmaz.' }
    if (Test-Path -LiteralPath $data) {
        $receiptFile = Join-Path $data 'install-receipt.json'
        if (-not (Test-Path -LiteralPath $receiptFile)) { throw 'Mevcut veri dizininin PortSentinel sahipliği doğrulanamadı.' }
        $receipt = Get-Content -LiteralPath $receiptFile -Raw | ConvertFrom-Json
        if ($receipt.Product -ne 'PortSentinel' -or [IO.Path]::GetFullPath($receipt.InstallPath) -ne $target -or @($receipt.WindowsPolicyChanges).Count -ne 0) { throw 'Veri dizini sahipliği veya politika uyumluluğu doğrulanamadı.' }
    }
    $message = 'Kurulum ön kontrolü başarılı. Bu kontrol Windows ayarlarını, servisleri ve USB politikalarını değiştirmedi.'
    if ($LogPath) { [IO.File]::WriteAllText($LogPath, $message, [Text.UTF8Encoding]::new($true)) }
    Write-Host $message
    exit 0
} catch {
    $message = $_.Exception.Message
    if ($LogPath) { [IO.File]::WriteAllText($LogPath, $message, [Text.UTF8Encoding]::new($true)) }
    Write-Error $message -ErrorAction Continue
    exit 1
}
