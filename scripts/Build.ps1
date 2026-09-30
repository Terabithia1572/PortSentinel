[CmdletBinding()]
param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    & dotnet restore PortSentinel.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore başarısız.' }
    & dotnet build PortSentinel.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build başarısız.' }
    if (-not $SkipTests) {
        & dotnet test PortSentinel.sln -c Release --no-build --no-restore --logger 'trx;LogFilePrefix=portsentinel' --results-directory artifacts/test-results
        if ($LASTEXITCODE -ne 0) { throw 'Test başarısız.' }
    }
    # Re-publishing to an existing directory must not retain assemblies from an earlier RID/package graph.
    $publishRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts/publish'))
    if (-not $publishRoot.StartsWith([IO.Path]::GetFullPath($root) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Publish dizini çalışma alanı dışında.' }
    if (Test-Path -LiteralPath $publishRoot) {
        if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'checksums.json'))) { throw 'Mevcut publish dizininin build sahipliği doğrulanamadı; temizleme yapılmadı.' }
        $items = @((Get-Item -LiteralPath $publishRoot -Force)) + @(Get-ChildItem -LiteralPath $publishRoot -Force -Recurse)
        if (@($items | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -gt 0) { throw 'Publish ağacında reparse point var; temizleme yapılmadı.' }
        Remove-Item -LiteralPath $publishRoot -Recurse -Force
    }
    foreach ($project in @('Service', 'Desktop')) {
        & dotnet publish "src/PortSentinel.$project" -c Release --no-restore --self-contained false -o "artifacts/publish/$($project.ToLowerInvariant())"
        if ($LASTEXITCODE -ne 0) { throw "Publish başarısız: $project" }
    }
    New-Item -ItemType Directory -Path 'artifacts/publish/scripts','artifacts/publish/docs' -Force | Out-Null
    Copy-Item -LiteralPath 'scripts/Install.ps1','scripts/Uninstall.ps1' -Destination 'artifacts/publish/scripts'
    Copy-Item -LiteralPath 'README.md' -Destination 'artifacts/publish'
    Get-ChildItem -LiteralPath 'docs' | Copy-Item -Destination 'artifacts/publish/docs' -Recurse
    Copy-Item -LiteralPath 'NOTICE.md','CHANGELOG.md' -Destination 'artifacts/publish'
    $libraries = @{}
    foreach ($component in @('service','desktop')) {
        $deps = Get-Content "artifacts/publish/$component/PortSentinel.$((Get-Culture).TextInfo.ToTitleCase($component)).deps.json" -Raw | ConvertFrom-Json
        foreach ($library in $deps.libraries.PSObject.Properties) {
            if ($library.Value.type -eq 'package') { $libraries[$library.Name] = $true }
        }
    }
    $packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    $notices = foreach ($library in $libraries.Keys | Sort-Object) {
        $parts = $library -split '/'
        $nuspec = Join-Path $packageCache "$($parts[0].ToLowerInvariant())/$($parts[1])/$($parts[0].ToLowerInvariant()).nuspec"
        if (-not (Test-Path -LiteralPath $nuspec)) { throw "Lisans metadata'sı bulunamadı: $library" }
        [xml]$metadata = Get-Content -LiteralPath $nuspec -Raw
        [pscustomobject]@{ Package = $parts[0]; Version = $parts[1]; License = $metadata.package.metadata.license.InnerText; LicenseUrl = [string]$metadata.package.metadata.licenseUrl; Copyright = [string]$metadata.package.metadata.copyright; ProjectUrl = [string]$metadata.package.metadata.projectUrl }
    }
    $notices | ConvertTo-Json -Depth 5 | Set-Content 'artifacts/publish/THIRD-PARTY-NOTICES.json' -Encoding UTF8
    $files = @(Get-ChildItem 'artifacts/publish' -Recurse -File | Where-Object { $_.Name -ne 'checksums.json' } | ForEach-Object {
        [pscustomobject]@{ Path = $_.FullName.Substring((Join-Path $root 'artifacts/publish').Length + 1); SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $files | ConvertTo-Json -Depth 4 | Set-Content 'artifacts/publish/checksums.json' -Encoding UTF8
    Write-Host 'win-x64 framework-dependent paket hazır: artifacts/publish. .NET 10 x64 Desktop Runtime gerekir.'
} finally { Pop-Location }
