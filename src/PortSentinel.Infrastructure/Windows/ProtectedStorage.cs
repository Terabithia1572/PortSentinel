using System.Security.AccessControl;
using System.Security.Principal;

namespace PortSentinel.Infrastructure.Windows;

public static class ProtectedStorage
{
    public static void Verify(string directory)
    {
        var info = new DirectoryInfo(directory);
        if (!info.Exists) throw new InvalidOperationException("Korumalı ProgramData dizini yok. Önce yönetici kurulumunu çalıştırın.");
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Veri dizini reparse point olamaz.");
        var acl = info.GetAccessControl();
        var safe = new HashSet<string> { "S-1-5-18", "S-1-5-19", "S-1-5-32-544" };
        const FileSystemRights writes = FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.ChangePermissions
            | FileSystemRights.TakeOwnership | FileSystemRights.DeleteSubdirectoriesAndFiles;
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && (rule.FileSystemRights & writes) != 0
                && !safe.Contains(rule.IdentityReference.Value))
                throw new InvalidOperationException("Veri dizininde standart kullanıcı yazma izni var. Kurulum ACL'lerini onarın.");
        foreach (var file in info.EnumerateFiles())
        {
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Veri dosyası reparse point olamaz.");
            foreach (FileSystemAccessRule rule in file.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)))
                if (rule.AccessControlType == AccessControlType.Allow && (rule.FileSystemRights & writes) != 0 && !safe.Contains(rule.IdentityReference.Value))
                    throw new InvalidOperationException("Veri dosyası ACL güvenlik kontrolü başarısız.");
        }
    }
}
