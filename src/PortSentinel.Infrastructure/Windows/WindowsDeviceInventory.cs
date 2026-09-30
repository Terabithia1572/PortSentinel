using System.Text.Json;
using PortSentinel.Application;
using PortSentinel.Domain;

namespace PortSentinel.Infrastructure.Windows;

public sealed class WindowsDeviceInventory(FixedPowerShell powershell) : IDeviceInventory
{
    private sealed record DiskData(string PhysicalInstanceId, string SerialNumber, string VendorId, string ProductId,
        bool HasUniqueId, bool IsUsbStorage, bool IsSystemDisk, string Name, string Manufacturer, long SizeBytes,
        string Transport, string DiskInstanceId, StorageVolume[] Volumes);
    public async Task<IReadOnlyList<DeviceObservation>> ScanAsync(CancellationToken ct)
    {
        var json = await powershell.ExecuteResourceAsync("Inventory", ct);
        var disks = JsonSerializer.Deserialize<DiskData[]>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Envanter JSON geçersiz.");
        if (disks.Length > 200) throw new InvalidDataException("200 disk envanter sınırı aşıldı.");
        return disks.GroupBy(d => d.PhysicalInstanceId, StringComparer.OrdinalIgnoreCase).Select(g =>
        {
            var d = g.First();
            return new DeviceObservation(new() { PhysicalInstanceId = d.PhysicalInstanceId, SerialNumber = d.SerialNumber,
                VendorId = d.VendorId, ProductId = d.ProductId, HasUniqueId = g.All(x => x.HasUniqueId),
                IsUsbStorage = g.All(x => x.IsUsbStorage), IsSystemDisk = g.Any(x => x.IsSystemDisk) },
                d.Name, d.Manufacturer, g.Sum(x => x.SizeBytes), string.Join(" / ", g.Select(x => x.Transport).Distinct()),
                g.Select(x => x.DiskInstanceId).Order().ToArray(), g.SelectMany(x => x.Volumes).Distinct().OrderBy(v => v.PartitionId).ThenBy(v => v.DriveLetter).ToArray());
        }).OrderBy(d => d.Identity.PhysicalInstanceId).ToArray();
    }
}
