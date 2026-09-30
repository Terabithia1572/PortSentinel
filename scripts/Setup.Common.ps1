# Windows PowerShell 5.1 ve PowerShell 7 ile ortak kurulum/sahiplik kontrolleri.
# Setup PowerShell 7'den başlatıldıysa PSModulePath 5.1'e uygun olmayabilir.
# Güvenilir sistem modüllerini çalışan PowerShell'in kendi dizininden yükle.
foreach ($module in @('Microsoft.PowerShell.Security','Microsoft.PowerShell.Utility')) {
    Import-Module (Join-Path $PSHOME "Modules/$module/$module.psd1") -ErrorAction Stop
}
$script:PortSentinelOwner = 'PortSentinel-InnoSetup-69D4AC9F-F550-46D6-AF21-39B2394C2547'
function Get-PayloadHash([string]$Path) {
    $stream = [IO.File]::OpenRead($Path); $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','') }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}
function Get-PortSentinelPaths {
    $programRoot = if ($env:ProgramW6432) { $env:ProgramW6432 } else { $env:ProgramFiles }
    @{ Target = [IO.Path]::GetFullPath((Join-Path $programRoot 'PortSentinel')); Data = [IO.Path]::GetFullPath((Join-Path $env:ProgramData 'PortSentinel')) }
}
function Assert-NoReparse([string]$Path) {
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if ((Test-Path -LiteralPath $cursor) -and (((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) { throw "Reparse dizin kabul edilmez: $cursor" }
        $parent = Split-Path -Parent $cursor
        if ($parent -eq $cursor) { break }; $cursor = $parent
    }
}
function Assert-SafeTree([string]$Path) {
    Assert-NoReparse $Path
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $pending = [Collections.Generic.Queue[string]]::new(); $pending.Enqueue($Path)
    while ($pending.Count) {
        foreach ($item in Get-ChildItem -LiteralPath $pending.Dequeue() -Force) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse dosya/dizin kabul edilmez: $($item.FullName)" }
            if ($item.PSIsContainer) { $pending.Enqueue($item.FullName) }
        }
    }
}
function Resolve-PayloadPath([string]$Root, [string]$RelativePath) {
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    if (-not $RelativePath -or [IO.Path]::IsPathRooted($RelativePath)) { throw 'Paket yolu göreli olmalıdır.' }
    $resolved = [IO.Path]::GetFullPath((Join-Path $rootPath $RelativePath))
    if (-not $resolved.StartsWith($rootPath + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Paket yolu kurulum dizini dışında.' }
    Assert-NoReparse $resolved
    return $resolved
}
function Read-PayloadManifest([string]$Root, [switch]$AllowMissingFiles) {
    Assert-SafeTree $Root
    # PS 5.1 ConvertFrom-Json diziyi tek pipeline nesnesi olarak döndürür.
    # @(... | ConvertFrom-Json) iç içe dizi yapar; doğrudan atama iki sürümde de çalışır.
    $manifest = Get-Content -LiteralPath (Join-Path $Root 'checksums.json') -Raw | ConvertFrom-Json
    if (-not $manifest -or @($manifest).Count -lt 2) { throw 'Paket dosya listesi eksik.' }
    $seen = @{}
    foreach ($file in $manifest) {
        if ($file.Path -isnot [string] -or $file.SHA256 -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Paket dosya kaydı geçersiz.' }
        $resolved = Resolve-PayloadPath $Root $file.Path
        if ($seen.ContainsKey($resolved)) { throw 'Paket dosya listesinde tekrar var.' }; $seen[$resolved] = $true
        if (Test-Path -LiteralPath $resolved -PathType Leaf) {
            if ((Get-PayloadHash $resolved) -ne $file.SHA256) { throw "Değiştirilmiş dosya korunur; paket hash uyuşmuyor: $($file.Path)" }
        } elseif (-not $AllowMissingFiles) { throw "Paket dosyası eksik: $($file.Path)" }
    }
    foreach ($required in @('service/PortSentinel.Service.exe','desktop/PortSentinel.Desktop.exe')) {
        if (-not $seen.ContainsKey((Resolve-PayloadPath $Root $required))) { throw "Paket dosya listesi eksik: $required" }
    }
    return $manifest
}
function Get-OwnedInstallation([string]$Target, [string]$Data, $Service) {
    $targetPath = [IO.Path]::GetFullPath($Target).TrimEnd('\')
    Assert-SafeTree $targetPath; Assert-SafeTree $Data
    $receiptPath = Join-Path $Data 'install-receipt.json'
    $receipt = $null; $manifest = @(); $emptyData = $false
    if (Test-Path -LiteralPath $Data) {
        if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
            if (@(Get-ChildItem -LiteralPath $Data -Force).Count -gt 0) { throw 'Mevcut veri dizininin PortSentinel sahipliği doğrulanamadı; veriler korundu.' }
            $emptyData = $true
        } else {
        $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        if ($receipt.Product -ne 'PortSentinel' -or [IO.Path]::GetFullPath($receipt.InstallPath).TrimEnd('\') -ne $targetPath -or $receipt.ServiceBinary -ne (Join-Path $targetPath 'service/PortSentinel.Service.exe') -or @($receipt.WindowsPolicyChanges).Count -ne 0) { throw 'Veri dizini sahipliği veya politika uyumluluğu doğrulanamadı.' }
        }
    }
    if (Test-Path -LiteralPath $targetPath) {
        $marker = Join-Path $targetPath 'installer-owner.txt'
        if (@(Get-ChildItem -LiteralPath $targetPath -Force).Count -eq 0) {
            # Önceki kaldırıcıdan kalmış tamamen boş sabit dizin yeniden kullanılabilir.
        } elseif ((Test-Path -LiteralPath $marker -PathType Leaf) -and (Get-Content -LiteralPath $marker -Raw).Trim() -eq $script:PortSentinelOwner) {
            $manifest = @(Read-PayloadManifest $targetPath -AllowMissingFiles)
        } elseif ($receipt -and $receipt.InstalledBy -eq 'PowerShell') {
            $manifest = @($receipt.Files | ForEach-Object {
                $absolute = [IO.Path]::GetFullPath($_.Path)
                if (-not $absolute.StartsWith($targetPath + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Kurulum dosyası kapsam dışında.' }
                if ((Test-Path -LiteralPath $absolute) -and (Get-PayloadHash $absolute) -ne $_.SHA256) { throw "Değiştirilmiş dosya korunur: $absolute" }
                [pscustomobject]@{ Path = $absolute.Substring($targetPath.Length + 1); SHA256 = $_.SHA256 }
            })
        } else { throw 'Mevcut kurulum dizininin PortSentinel sahipliği doğrulanamadı; dosyalar korundu.' }
    }
    if ($emptyData -and $manifest.Count -eq 0) { throw 'Boş veri dizininin PortSentinel sahipliği kurulum paketiyle doğrulanamadı.' }
    if ($Service) {
        if ($Service.PathName.Trim('"') -ne (Join-Path $targetPath 'service/PortSentinel.Service.exe') -or (-not $receipt -and $manifest.Count -eq 0)) { throw 'PortSentinel adlı servis bu kuruluma ait değil; servis değiştirilmedi.' }
    }
    [pscustomobject]@{ Receipt = $receipt; Manifest = $manifest; Repair = ((Test-Path -LiteralPath $targetPath) -or [bool]$Service); Service = $Service }
}
function Stop-OwnedService {
    $service = Get-Service -Name PortSentinel -ErrorAction SilentlyContinue
    if ($service -and $service.Status -ne 'Stopped') {
        Stop-Service -Name PortSentinel -ErrorAction Stop
        $service.WaitForStatus([ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(30))
    }
}
function Set-OwnedServiceBinaryPath([string]$BinaryPath) {
    if (-not ('PortSentinel.SetupServiceConfig' -as [type])) {
        Add-Type @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace PortSentinel {
    public static class SetupServiceConfig {
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        private static extern IntPtr OpenSCManager(string machine, string database, uint access);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        private static extern IntPtr OpenService(IntPtr manager, string name, uint access);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfig(IntPtr service, uint type, uint start, uint error,
            string path, string group, IntPtr tag, string dependencies, string account, string password, string display);
        [DllImport("advapi32.dll")] private static extern bool CloseServiceHandle(IntPtr handle);
        public static void SetBinaryPath(string path) {
            var manager = OpenSCManager(null, null, 1);
            if (manager == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            try {
                var service = OpenService(manager, "PortSentinel", 2);
                if (service == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                try {
                    if (!ChangeServiceConfig(service, uint.MaxValue, uint.MaxValue, uint.MaxValue,
                        "\"" + path + "\"", null, IntPtr.Zero, null, null, null, null))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                } finally { CloseServiceHandle(service); }
            } finally { CloseServiceHandle(manager); }
        }
    }
}
'@
    }
    # PowerShell 5.1 native argv işlemesi gömülü tırnakları silebilir; SCM API'si kullan.
    [PortSentinel.SetupServiceConfig]::SetBinaryPath($BinaryPath)
}
