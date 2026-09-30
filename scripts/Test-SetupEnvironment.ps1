[CmdletBinding()]
param([string]$LogPath, [switch]$SkipAdministratorCheck)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Setup.Common.ps1')
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
    $paths = Get-PortSentinelPaths
    $state = Get-OwnedInstallation $paths.Target $paths.Data (Get-CimInstance Win32_Service -Filter "Name='PortSentinel'")
    if (@(Get-Process -Name PortSentinel.Desktop -ErrorAction SilentlyContinue).Count -gt 0) { throw 'PortSentinel pencerelerini kapatın ve kurulumu yeniden başlatın.' }
    $message = if ($state.Repair) { 'Mevcut PortSentinel kurulumu doğrulandı. Eksik/yarım kurulum onarılacak; izin kayıtları ve veriler korunacak.' } else { 'Kurulum ön kontrolü başarılı.' }
    if ($LogPath) { [IO.File]::WriteAllText($LogPath, $message, [Text.UTF8Encoding]::new($true)) }
    Write-Host $message
    exit 0
} catch {
    $message = $_.Exception.Message
    if ($LogPath) { [IO.File]::WriteAllText($LogPath, $message, [Text.UTF8Encoding]::new($true)) }
    Write-Error $message -ErrorAction Continue
    exit 1
}
