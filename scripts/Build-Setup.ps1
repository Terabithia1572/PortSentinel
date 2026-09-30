[CmdletBinding()]
param([string]$InnoCompiler, [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$payload = Join-Path $root 'artifacts/setup-payload'
$output = Join-Path $root 'artifacts/installer'
$owner = 'PortSentinel-InnoSetup-69D4AC9F-F550-46D6-AF21-39B2394C2547'
[xml]$buildProperties = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
$appVersion = [string]$buildProperties.Project.PropertyGroup.Version
if ($appVersion -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw 'Uygulama sürümü geçersiz.' }
$installerFileName = "PortSentinel-Setup-$appVersion.exe"
if (-not $InnoCompiler) {
    $candidates = @((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'), (Join-Path $env:ProgramFiles 'Inno Setup 6/ISCC.exe'))
    $InnoCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $InnoCompiler -or -not (Test-Path -LiteralPath $InnoCompiler)) { throw 'Inno Setup 6 ISCC.exe bulunamadı. -InnoCompiler ile derleyici yolunu verin.' }
if (-not $payload.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Paket dizini çalışma alanı dışında.' }
Push-Location $root
try {
    & (Join-Path $PSScriptRoot 'Generate-Icon.ps1')
    & dotnet restore PortSentinel.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore başarısız.' }
    & dotnet build PortSentinel.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build başarısız.' }
    if (-not $SkipTests) {
        & dotnet test PortSentinel.sln -c Release --no-build --no-restore --logger 'trx;LogFilePrefix=setup' --results-directory artifacts/test-results
        if ($LASTEXITCODE -ne 0) { throw 'Test başarısız.' }
        & "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-SetupScripts.ps1')
        if ($LASTEXITCODE -ne 0) { throw 'Windows PowerShell 5.1 kurulum testleri başarısız.' }
    }
    if (Test-Path -LiteralPath $payload) {
        $marker = Join-Path $payload 'installer-owner.txt'
        if (-not (Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw).Trim() -ne $owner) { throw 'Paket dizininin build sahipliği doğrulanamadı; temizleme yapılmadı.' }
        $items = @((Get-Item -LiteralPath $payload -Force)) + @(Get-ChildItem -LiteralPath $payload -Force -Recurse)
        if (@($items | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -ne 0) { throw 'Paket ağacında reparse point var; temizleme yapılmadı.' }
        Remove-Item -LiteralPath $payload -Recurse -Force
    }
    New-Item -ItemType Directory -Path $payload,$output -Force | Out-Null
    Copy-Item -LiteralPath 'installer/installer-owner.txt' -Destination $payload
    foreach ($component in @('Service','Desktop')) {
        # Executable projects declare win-x64; do not propagate a new RID into portable library lock files.
        & dotnet publish "src/PortSentinel.$component" -c Release --self-contained true -p:RuntimeFrameworkVersion=10.0.12 -p:RestoreLockedMode=true -o (Join-Path $payload $component.ToLowerInvariant())
        if ($LASTEXITCODE -ne 0) { throw "Self-contained publish başarısız: $component" }
        $directory = Join-Path $payload $component.ToLowerInvariant()
        foreach ($file in @('coreclr.dll','hostfxr.dll','System.Private.CoreLib.dll')) {
            if (-not (Test-Path -LiteralPath (Join-Path $directory $file))) { throw "Runtime eksik: $component/$file" }
        }
        $config = Get-Content -LiteralPath (Join-Path $directory "PortSentinel.$component.runtimeconfig.json") -Raw | ConvertFrom-Json
        if (-not $config.runtimeOptions.includedFrameworks -or $config.runtimeOptions.framework -or $config.runtimeOptions.frameworks) { throw "Self-contained yapılandırma hatalı: $component" }
    }
    New-Item -ItemType Directory -Path (Join-Path $payload 'scripts'),(Join-Path $payload 'docs'),(Join-Path $payload 'THIRD-PARTY-LICENSES') -Force | Out-Null
    # Windows PowerShell 5.1 için Türkçe metinli betikleri UTF-8 BOM ile paketle.
    foreach ($script in @('Install.ps1','Uninstall.ps1','Invoke-SetupAction.ps1','Test-SetupEnvironment.ps1','Undo-SetupService.ps1','Setup.Common.ps1','Test-ServiceConnection.ps1')) {
        $sourceText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot $script))
        [IO.File]::WriteAllText((Join-Path $payload "scripts/$script"), $sourceText, [Text.UTF8Encoding]::new($true))
    }
    Copy-Item -LiteralPath 'README.md','installer/assets/PortSentinel.ico' -Destination $payload
    Get-ChildItem -LiteralPath 'docs' | Copy-Item -Destination (Join-Path $payload 'docs') -Recurse
    Copy-Item -LiteralPath 'NOTICE.md','CHANGELOG.md' -Destination $payload
    $libraries = @{}
    foreach ($component in @('Service','Desktop')) {
        $assets = Get-Content -LiteralPath "src/PortSentinel.$component/obj/project.assets.json" -Raw | ConvertFrom-Json
        foreach ($library in $assets.libraries.PSObject.Properties) {
            if ($library.Value.type -eq 'package') { $libraries[$library.Name] = $true }
        }
    }
    foreach ($runtime in @('Microsoft.NETCore.App.Runtime.win-x64','Microsoft.WindowsDesktop.App.Runtime.win-x64')) { $libraries["$runtime/10.0.12"] = $true }
    $packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    $notices = foreach ($library in $libraries.Keys | Sort-Object) {
        $parts = $library -split '/'
        $packageDirectory = Join-Path $packageCache "$($parts[0].ToLowerInvariant())/$($parts[1])"
        $nuspec = Join-Path $packageDirectory "$($parts[0].ToLowerInvariant()).nuspec"
        if (-not (Test-Path -LiteralPath $nuspec)) { throw "Lisans metadata'sı bulunamadı: $library" }
        [xml]$metadata = Get-Content -LiteralPath $nuspec -Raw
        $licenseDirectory = Join-Path $payload "THIRD-PARTY-LICENSES/$($parts[0])-$($parts[1])"
        $licenseFiles = @(Get-ChildItem -LiteralPath $packageDirectory -File -Recurse | Where-Object { $_.Name -match '^(LICENSE|LICENCE|NOTICE|THIRD.PARTY.NOTICES)(\..*)?$' })
        if ($metadata.package.metadata.license.type -eq 'file') {
            $relative = [string]$metadata.package.metadata.license.InnerText
            $licensePath = [IO.Path]::GetFullPath((Join-Path $packageDirectory $relative))
            if (-not $licensePath.StartsWith([IO.Path]::GetFullPath($packageDirectory) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Lisans dosyası paket kapsamı dışında.' }
            if (-not (Test-Path -LiteralPath $licensePath)) { throw "Lisans dosyası bulunamadı: $library/$relative" }
            $licenseFiles += Get-Item -LiteralPath $licensePath
        }
        foreach ($file in $licenseFiles | Sort-Object FullName -Unique) {
            $relative = $file.FullName.Substring([IO.Path]::GetFullPath($packageDirectory).Length + 1)
            $destination = Join-Path $licenseDirectory $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destination
        }
        [pscustomobject]@{ Package = $parts[0]; Version = $parts[1]; License = [string]$metadata.package.metadata.license.InnerText; LicenseUrl = [string]$metadata.package.metadata.licenseUrl; Copyright = [string]$metadata.package.metadata.copyright; ProjectUrl = [string]$metadata.package.metadata.projectUrl; LicenseFiles = @($licenseFiles | Select-Object -ExpandProperty Name -Unique) }
    }
    $notices | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $payload 'THIRD-PARTY-NOTICES.json') -Encoding UTF8
    $manifest = @(Get-ChildItem -LiteralPath $payload -Recurse -File | ForEach-Object {
        [pscustomobject]@{ Path = $_.FullName.Substring($payload.Length + 1); SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $payload 'checksums.json') -Encoding UTF8
    & $InnoCompiler '/Qp' "/DAppVersion=$appVersion" "/DPayloadDir=$payload" "/DInstallerOutputDir=$output" (Join-Path $root 'installer/PortSentinel_Setup.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup derlemesi başarısız.' }
    $installer = Join-Path $output $installerFileName
    $hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
    "$hash  $installerFileName" | Set-Content -LiteralPath (Join-Path $output "PortSentinel-Setup-$appVersion.sha256") -Encoding ASCII
    Write-Host "Setup hazır: $installer"
    Write-Host "SHA256: $hash"
} finally { Pop-Location }
