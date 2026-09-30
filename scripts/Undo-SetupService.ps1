[CmdletBinding()]
param([Parameter(Mandatory)][string]$LogPath)
$ErrorActionPreference = 'Stop'
try {
    # Yalnız kurucu bu çalıştırmada servis kaydını başarıyla oluşturmuşsa çağırır.
    $target = [IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'PortSentinel'))
    $exe = Join-Path $target 'service/PortSentinel.Service.exe'
    $receipt = Get-Content -LiteralPath (Join-Path $env:ProgramData 'PortSentinel/install-receipt.json') -Raw | ConvertFrom-Json
    if ($receipt.Product -ne 'PortSentinel' -or $receipt.InstalledBy -ne 'InnoSetup' -or $receipt.ServiceBinary -ne $exe -or @($receipt.WindowsPolicyChanges).Count -ne 0) { throw 'Geri alma sahipliği doğrulanamadı.' }
    $service = Get-CimInstance Win32_Service -Filter "Name='PortSentinel'"
    if ($service) {
        if ($service.PathName.Trim('"') -ne $exe) { throw 'Servis başka araçla değişmiş; geri alma yapılmadı.' }
        Stop-Service -Name PortSentinel
        & sc.exe delete PortSentinel | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Bu kurulumun servis kaydı geri alınamadı.' }
    }
    $message = 'Bu kurulumun oluşturduğu servis kaydı geri alındı. ProgramData ve Windows politikaları korundu.'
    [IO.File]::WriteAllText($LogPath, $message, [Text.UTF8Encoding]::new($true))
    exit 0
} catch {
    [IO.File]::WriteAllText($LogPath, ($_ | Out-String), [Text.UTF8Encoding]::new($true))
    exit 1
}
