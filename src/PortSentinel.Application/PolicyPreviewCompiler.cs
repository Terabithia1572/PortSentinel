using System.Xml.Linq;
using PortSentinel.Contracts;
using PortSentinel.Domain;

namespace PortSentinel.Application;

public static class PolicyPreviewCompiler
{
    public static PolicyPreviewDto Compile(IReadOnlyList<RegisteredDevice> devices, IReadOnlyList<AccessRule> rules)
    {
        var groupId = "{93f7bf53-b3fd-4c89-a0ab-16f62ab7cb11}";
        var groups = new XElement("PolicyGroups", new XElement("Group", new XAttribute("Id", groupId), new XAttribute("Type", "Device"),
            new XElement("Name", "USB depolama — kapsamı laboratuvarda doğrulayın"), new XElement("MatchType", "MatchAll"),
            new XElement("DescriptorIdList", new XElement("PrimaryId", "RemovableMediaDevices"), new XElement("BusId", "USB"))));
        var allowed = devices.Where(d => rules.Any(r => r.RegisteredDeviceId == d.Id && r.Allowed)).ToArray();
        if (allowed.Any(d => d.Identity.Quality != IdentityQuality.StableSerial || !d.Identity.IsUsbStorage || d.Identity.IsSystemDisk)
            || allowed.GroupBy(d => d.IdentityKey).Any(g => g.Count() != 1))
            throw new BusinessException("UnsafePolicy", "Belirsiz veya kapsam dışı kimlikle XML üretilemez.");
        var policyRules = new XElement("PolicyRules");
        var excluded = new XElement("ExcludedIdList");
        foreach (var d in allowed.OrderBy(d => d.Id))
        {
            var id = d.Id.ToString("B");
            groups.Add(new XElement("Group", new XAttribute("Id", id), new XAttribute("Type", "Device"), new XElement("Name", d.DisplayName),
                new XElement("MatchType", "MatchAll"), new XElement("DescriptorIdList", new XElement("PrimaryId", "RemovableMediaDevices"),
                new XElement("BusId", "USB"), new XElement("SerialNumberId", d.Identity.SerialNumber),
                new XElement("VID_PID", d.Identity.VendorId + "_" + d.Identity.ProductId))));
            excluded.Add(new XElement("GroupId", id));
            policyRules.Add(Rule(d.Id, "Açık izin: " + d.DisplayName, new XElement("IncludedIdList", new XElement("GroupId", id)), new XElement("ExcludedIdList"), "Allow"));
        }
        policyRules.Add(Rule(Guid.Parse("d1c9d123-5899-409e-9817-fb12c2c6bb19"), "İzinsiz USB depolama", new XElement("IncludedIdList", new XElement("GroupId", groupId)), excluded, "Deny"));
        return new(groups.ToString(), policyRules.ToString(),
            "ÖNİZLEME: Windows'a uygulanmadı. Lisans, GPO/MDM çakışması, gerçek SerialNumberId ve USB/UASP kapsamı doğrulanmalıdır. Global default deny açmayın; diğer cihaz aileleri etkilenebilir.");
    }
    private static XElement Rule(Guid id, string name, XElement included, XElement excluded, string type) => new("PolicyRule", new XAttribute("Id", id.ToString("B")),
        new XElement("Name", name), included, excluded, new XElement("Entry", new XAttribute("Id", EntryId(id).ToString("B")),
            new XElement("Type", type), new XElement("Options", 0), new XElement("AccessMask", 63)));
    private static Guid EntryId(Guid id) { var bytes = id.ToByteArray(); bytes[15] ^= 0x80; return new Guid(bytes); }
}
