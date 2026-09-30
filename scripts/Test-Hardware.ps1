[CmdletBinding()]
param([Parameter(Mandatory)][string]$Manifest, [switch]$AcknowledgeHardwareTest)
$ErrorActionPreference = 'Stop'
if (-not $AcknowledgeHardwareTest) { throw 'Ayrı laboratuvar donanım testini açıkça seçmek için -AcknowledgeHardwareTest gerekir. Politika değiştirilmez.' }
$root = Split-Path -Parent $PSScriptRoot
$fixtures = @(Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json)
if ($fixtures.Count -eq 0 -or $fixtures.Count -gt 50) { throw 'Manifest 1–50 fixture içermelidir.' }
$devices = @(& (Join-Path $root 'src/PortSentinel.Infrastructure/Windows/Inventory.ps1') | ConvertFrom-Json)
$results = @()
foreach ($fixture in $fixtures) {
    if ($fixture.ExpectedReadable -isnot [bool]) { throw 'ExpectedReadable açık boolean olmalı.' }
    $path = [IO.Path]::GetFullPath([string]$fixture.FixtureFile)
    if ($path -notmatch '^[A-Za-z]:\\' -or $path.Contains('*') -or $path.Contains('?')) { throw 'Fixture yerel, tam ve wildcard içermeyen dosya yolu olmalı.' }
    $device = @($devices | Where-Object { $_.physicalInstanceId -eq $fixture.PhysicalInstanceId -and -not $_.isSystemDisk })
    $letter = $path.Substring(0, 2)
    if ($device.Count -eq 0 -or -not (@($device.volumes | ForEach-Object { $_ }) | Where-Object { $_.driveLetter -eq $letter })) { throw "Fixture fiziksel USB/system-disk kapsam kontrolü başarısız: $($fixture.Name)" }
    $readable = $false; $denied = $false; $errorText = $null; $stream = $null
    try {
        $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        $null = $stream.ReadByte(); $readable = $true
    } catch [UnauthorizedAccessException] { $denied = $true; $errorText = $_.Exception.Message }
    catch { $errorText = $_.Exception.Message }
    finally { if ($null -ne $stream) { $stream.Dispose() } }
    $passed = if ($fixture.ExpectedReadable) { $readable } else { $denied }
    $results += [pscustomobject]@{ Name = $fixture.Name; OccurredUtc = [DateTime]::UtcNow.ToString('o'); PhysicalInstanceId = $fixture.PhysicalInstanceId; FixtureFile = $path; ExpectedReadable = $fixture.ExpectedReadable; Readable = $readable; AccessDenied = $denied; Passed = $passed; Error = $errorText; Coverage = 'Single read only; no boot/raw/write/handle guarantee' }
}
$outputDirectory = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$outputFile = Join-Path $outputDirectory ('hardware-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '.json')
$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $outputFile -Encoding UTF8
$results | Format-List
if (@($results | Where-Object { -not $_.Passed }).Count -gt 0) { throw "Donanım fixture doğrulaması başarısız: $outputFile" }
Write-Host "Yalnız tek dosya okuma denemeleri geçti: $outputFile"
