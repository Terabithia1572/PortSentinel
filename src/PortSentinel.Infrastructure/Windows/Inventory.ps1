$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
function Read-Property($Instance, $Key) {
    $p = Get-PnpDeviceProperty -InstanceId $Instance -KeyName $Key -ErrorAction SilentlyContinue
    if ($null -ne $p) { return $p.Data }
    return $null
}
$results = @()
foreach ($disk in @(Get-CimInstance Win32_DiskDrive)) {
    $node = [string]$disk.PNPDeviceID
    $physical = $null
    $seen = @{}
    for ($depth = 0; $depth -lt 16 -and $node -and -not $seen.ContainsKey($node); $depth++) {
        $seen[$node] = $true
        if ($node -match '^USB\\VID_([0-9A-F]{4})&PID_([0-9A-F]{4})(?:&REV_[0-9A-F]{4})?\\([^\\]+)$') {
            $physical = $node
            $vid = $matches[1]; $pid = $matches[2]; $serial = $matches[3]
            break
        }
        $node = [string](Read-Property $node 'DEVPKEY_Device_Parent')
    }
    if (-not $physical) { continue }
    $capabilities = Read-Property $physical 'DEVPKEY_Device_Capabilities'
    $unique = $null -ne $capabilities -and (([uint32]$capabilities -band 16) -ne 0)
    $volumes = @()
    $system = $false
    $nativeDisk = Get-Disk -Number $disk.Index -ErrorAction SilentlyContinue
    if ($null -eq $nativeDisk) { $system = $true } # Unknown system-disk status is never eligible for permission.
    else { $system = [bool]($nativeDisk.IsBoot -or $nativeDisk.IsSystem) }
    foreach ($partition in @(Get-CimAssociatedInstance -InputObject $disk -Association Win32_DiskDriveToDiskPartition)) {
        $logical = @(Get-CimAssociatedInstance -InputObject $partition -Association Win32_LogicalDiskToPartition)
        if ($logical.Count -eq 0) {
            $volumes += @{ partitionId = [string]$partition.DeviceID; driveLetter = $null; sizeBytes = [long]$partition.Size }
        }
        foreach ($volume in $logical) {
            if ($volume.DeviceID -eq $env:SystemDrive) { $system = $true }
            $volumes += @{ partitionId = [string]$partition.DeviceID; driveLetter = [string]$volume.DeviceID; sizeBytes = [long]$volume.Size }
        }
    }
    $manufacturer = [string](Read-Property $physical 'DEVPKEY_Device_Manufacturer')
    $results += @{
        physicalInstanceId = $physical; serialNumber = $serial; vendorId = $vid; productId = $pid
        hasUniqueId = $unique; isUsbStorage = $true; isSystemDisk = $system
        name = [string]$disk.Model; manufacturer = $manufacturer; sizeBytes = [long]$disk.Size
        transport = $(if ($disk.PNPDeviceID -like 'USBSTOR\*') { 'USBSTOR' } else { 'SCSI / USB ata (UASP adayı)' })
        diskInstanceId = [string]$disk.PNPDeviceID; volumes = @($volumes)
    }
}
ConvertTo-Json -InputObject @($results) -Depth 8 -Compress
