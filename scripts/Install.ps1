[CmdletBinding()]
param([string]$PackagePath, [switch]$UseInstalledFiles, [switch]$SelfContained)
$ErrorActionPreference = 'Stop'
function Require-Admin {
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Yükseltilmiş PowerShell gerekir.' }
}
function Invoke-ServiceCommand([string[]]$Arguments) {
    & sc.exe @Arguments
    if ($LASTEXITCODE -ne 0) { throw ('sc.exe başarısız: ' + ($Arguments -join ' ')) }
}
function Verify-NoReparse([string]$Path) {
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if (((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse dizin kabul edilmez: $cursor" }
        }
        $parent = Split-Path -Parent $cursor
        if ($parent -eq $cursor) { break }; $cursor = $parent
    }
}
Require-Admin
if (-not $PackagePath) {
    $parent = Split-Path -Parent $PSScriptRoot
    $PackagePath = if (Test-Path -LiteralPath (Join-Path $parent 'service')) { $parent } else { Join-Path $parent 'artifacts/publish' }
}
$os = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$x64 = @(Get-CimInstance Win32_Processor | Where-Object { $_.Architecture -eq 9 }).Count -gt 0
if ([int]$os.CurrentMajorVersionNumber -lt 10 -or -not $x64) { throw 'Bu x64 paket Windows 10 veya Windows 11 içindir. Windows edisyonu, güncelleme sürümü ve destek tarihi kurulumu engellemez.' }
if (-not $SelfContained) {
    $runtimes = & dotnet --list-runtimes
    if ($LASTEXITCODE -ne 0 -or -not ($runtimes -match '^Microsoft.WindowsDesktop.App 10\.0\.')) { throw '.NET 10 x64 Desktop Runtime kurulu olmalıdır.' }
}
if (Get-Service -Name PortSentinel -ErrorAction SilentlyContinue) { throw 'Servis zaten var. Mevcut kurulumu önce belgelenmiş akışla kaldırın; veri korunur.' }
$source = [IO.Path]::GetFullPath($PackagePath)
$target = Join-Path $env:ProgramFiles 'PortSentinel'
$data = Join-Path $env:ProgramData 'PortSentinel'
Verify-NoReparse $source; Verify-NoReparse $target; Verify-NoReparse $data
foreach ($file in @('service/PortSentinel.Service.exe', 'desktop/PortSentinel.Desktop.exe', 'checksums.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $file))) { throw "Paket eksik: $file" }
}
if ($SelfContained) {
    foreach ($component in @('service','desktop')) {
        foreach ($file in @('coreclr.dll','hostfxr.dll','System.Private.CoreLib.dll')) {
            if (-not (Test-Path -LiteralPath (Join-Path $source "$component/$file"))) { throw "Self-contained çalışma zamanı eksik: $component/$file" }
        }
        $config = Get-Content -LiteralPath (Join-Path $source "$component/PortSentinel.$((Get-Culture).TextInfo.ToTitleCase($component)).runtimeconfig.json") -Raw | ConvertFrom-Json
        if (-not $config.runtimeOptions.includedFrameworks -or $config.runtimeOptions.framework -or $config.runtimeOptions.frameworks) { throw "Self-contained runtime yapılandırması geçersiz: $component" }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $source 'desktop/PresentationFramework.dll'))) { throw 'WPF çalışma zamanı eksik.' }
}
$checksums = @(Get-Content -LiteralPath (Join-Path $source 'checksums.json') -Raw | ConvertFrom-Json)
foreach ($item in $checksums) {
    $file = [IO.Path]::GetFullPath((Join-Path $source $item.Path))
    if (-not $file.StartsWith($source + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Paket path kapsamı geçersiz.' }
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $item.SHA256) { throw "Paket hash uyuşmuyor: $($item.Path)" }
}
if ($UseInstalledFiles) {
    if ($source.TrimEnd('\') -ne [IO.Path]::GetFullPath($target).TrimEnd('\')) { throw 'Servis kaydı yalnız sabit, kurulu PortSentinel dizininden yapılabilir.' }
    if (-not (Test-Path -LiteralPath (Join-Path $source 'installer-owner.txt')) -or (Get-Content -LiteralPath (Join-Path $source 'installer-owner.txt') -Raw).Trim() -ne 'PortSentinel-InnoSetup-69D4AC9F-F550-46D6-AF21-39B2394C2547') { throw 'Inno Setup paket sahipliği doğrulanamadı.' }
} elseif (Test-Path -LiteralPath $target) { throw 'Kurulum dizini zaten var; sahipliği belirsiz dosyalar ezilmez.' }
if ((Test-Path -LiteralPath $data) -and -not (Test-Path -LiteralPath (Join-Path $data 'install-receipt.json'))) { throw 'Mevcut veri dizininin PortSentinel sahipliği doğrulanamadı.' }
New-Item -ItemType Directory -Path $target -Force | Out-Null
New-Item -ItemType Directory -Path $data -Force | Out-Null
$acl = [Security.AccessControl.DirectorySecurity]::new()
$acl.SetAccessRuleProtection($true, $false)
$acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
foreach ($sid in @('S-1-5-18', 'S-1-5-19', 'S-1-5-32-544')) {
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid), 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
}
Set-Acl -LiteralPath $data -AclObject $acl
$programAcl = [Security.AccessControl.DirectorySecurity]::new()
$programAcl.SetAccessRuleProtection($true, $false)
$programAcl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
foreach ($sid in @('S-1-5-18','S-1-5-32-544')) {
    $programAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid), 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
}
foreach ($sid in @('S-1-5-19','S-1-5-32-545')) {
    $programAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid), 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
}
Set-Acl -LiteralPath $target -AclObject $programAcl
if (-not $UseInstalledFiles) { Copy-Item -LiteralPath (Join-Path $source 'service'), (Join-Path $source 'desktop') -Destination $target -Recurse }
$baseline = @()
foreach ($key in @('HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions','HKLM:\SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices','HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Device Control')) {
    $values = if (Test-Path $key) { Get-ItemProperty $key | Select-Object * -ExcludeProperty PSPath,PSParentPath,PSChildName,PSDrive,PSProvider } else { $null }
    $baseline += @{ Key = $key; Exists = (Test-Path $key); Values = $values }
}
if (-not (Test-Path -LiteralPath (Join-Path $data 'policy-baseline.json'))) {
    @{ RecordedUtc = [DateTime]::UtcNow.ToString('o'); Snapshot = $baseline; WindowsPolicyChanges = @() } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $data 'policy-baseline.json') -Encoding UTF8
}
$exe = Join-Path $target 'service/PortSentinel.Service.exe'
if ($UseInstalledFiles) {
    # Inno owns its changing uninstall log; include only hash-manifest payload files in our receipt.
    $ownedFiles = @($checksums | ForEach-Object { @{ Path = [IO.Path]::GetFullPath((Join-Path $target $_.Path)); SHA256 = $_.SHA256 } })
} else {
    $ownedFiles = @(Get-ChildItem -LiteralPath $target -Recurse -File | ForEach-Object { @{ Path = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
}
@{ Product = 'PortSentinel'; InstalledBy = $(if ($UseInstalledFiles) { 'InnoSetup' } else { 'PowerShell' }); InstalledUtc = [DateTime]::UtcNow.ToString('o'); InstallPath = $target; ServiceBinary = $exe; Files = $ownedFiles; WindowsPolicyChanges = @() } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $data 'install-receipt.json') -Encoding UTF8
$created = $false
try {
    Invoke-ServiceCommand @('create','PortSentinel','binPath=',('"' + $exe + '"'),'start=','delayed-auto','obj=','NT AUTHORITY\LocalService','DisplayName=','PortSentinel')
    $created = $true
    Invoke-ServiceCommand @('description','PortSentinel','USB cihaz izin yönetimi. Fiziksel erişim engeli bu sürümde doğrulanmamıştır.')
    Invoke-ServiceCommand @('sdset','PortSentinel','D:(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;AU)')
    Invoke-ServiceCommand @('failure','PortSentinel','reset=','86400','actions=','restart/5000/restart/15000/restart/60000')
    Start-Service PortSentinel
    Write-Host "Kurulum tamamlandı. Arayüz: $target\desktop\PortSentinel.Desktop.exe"
    Write-Host 'Koruma doğrulanmadı. Windows/GPO/MDM erişim politikaları değiştirilmedi.'
} catch {
    if ($created) { Stop-Service PortSentinel -ErrorAction SilentlyContinue; & sc.exe delete PortSentinel | Out-Null }
    throw
}
