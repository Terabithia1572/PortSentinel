$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$lines = [System.Collections.Generic.List[string]]::new()
$os = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$x64 = @(Get-CimInstance Win32_Processor | Where-Object { $_.Architecture -eq 9 }).Count -gt 0
$target = [int]$os.CurrentMajorVersionNumber -ge 10 -and $x64
$lines.Add(('Windows: Edition={0}; Sürüm={1}; Build={2}; x64={3}; Windows 10/11 x64 kurulum hedefi={4}' -f $os.EditionID, $os.DisplayVersion, $os.CurrentBuildNumber, $x64, $target))
if (-not $target) { $lines.Add('Bu paket Windows 10/11 x64 içindir.') }
$lines.Add('Home/Pro/Enterprise/Education edisyonu, güncelleme sürümü ve destek tarihi kurulumu engellemez.')
$cs = Get-CimInstance Win32_ComputerSystem
$lines.Add(('Domain üyesi: {0}; negatif değer MDM/GPO yokluğunun kanıtı değildir.' -f $cs.PartOfDomain))
foreach ($path in @(
    'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions',
    'HKLM:\SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices',
    'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Device Control',
    'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Policy Manager',
    'HKLM:\SOFTWARE\Microsoft\PolicyManager\current\device\Defender',
    'HKLM:\SOFTWARE\Microsoft\Enrollments'
)) {
    try {
    if (Test-Path $path) {
        $values = Get-ItemProperty $path
        $names = @($values.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' } | Select-Object -ExpandProperty Name)
        $children = @(Get-ChildItem $path -ErrorAction SilentlyContinue)
        if ($names.Count -gt 0 -or $children.Count -gt 0) { $lines.Add(('Mevcut yönetim/politika göstergesi: {0}; değerler={1}; alt anahtar={2}. Kaynak ve çatışma yöneticice incelenmeli.' -f $path, ($names -join ','), $children.Count)) }
    }
    } catch { $lines.Add(('Yönetim/politika anahtarı bu servis hesabıyla okunamadı: {0}. Yetki/sahiplik yöneticice incelenmeli.' -f $path)) }
}
try {
    $mp = Get-MpComputerStatus
    $lines.Add(('Defender: Platform={0}; DeviceControlState={1}; DefaultEnforcement={2}; LastUpdated={3}' -f $mp.AMProductVersion, $mp.DeviceControlState, $mp.DeviceControlDefaultEnforcement, $mp.DeviceControlPoliciesLastUpdated))
} catch { $lines.Add('Defender durumu okunamadı: ' + $_.Exception.Message) }
$lines.Add('MDE lisans/onboarding, etkin politika hash eşleşmesi, UASP/boot/handle kabul testleri doğrulanmadı.')
ConvertTo-Json -InputObject @($lines.ToArray()) -Compress
