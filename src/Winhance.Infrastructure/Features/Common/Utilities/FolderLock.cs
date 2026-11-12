using System.ComponentModel;
using System.Security.AccessControl;
using System.Security.Principal;
using Windows.Win32;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Utilities;

// The SYSTEM task runs what is in this folder, so only SYSTEM and Administrators may write to it. Users read and run.
internal static class FolderLock
{
    private static readonly SecurityIdentifier System = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier Users = new(WellKnownSidType.BuiltinUsersSid, null);
    private static readonly SecurityIdentifier TrustedInstaller = new("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464");

    // What the root grants. BuildRootSecurity and the answer file's icacls line are both built from this, so they cannot drift.
    private static readonly (SecurityIdentifier Sid, FileSystemRights Rights, string IcaclsRights)[] RootGrants =
    [
        (System, FileSystemRights.FullControl, "F"),
        (Administrators, FileSystemRights.FullControl, "F"),
        (Users, FileSystemRights.ReadAndExecute, "RX"),
    ];

    private const InheritanceFlags Everything = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

    private const FileSystemRights WriteRights =
        FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.Delete
        | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

    private static readonly string[] RunnableExtensions = [".ps1", ".cmd", ".bat", ".exe", ".dll", ".reg", ".vbs", ".js", ".xml"];

    // Always the installed location: a portable copy still has the SYSTEM task running from here.
    public static string DefaultRoot => AppDataPaths.InstalledMachineRoot;

    // The icacls /grant:r arguments for Everything inheritance, in the form (OI)(CI).
    public static string IcaclsGrantArguments =>
        string.Join(' ', RootGrants.Select(grant => $"'*{grant.Sid.Value}:(OI)(CI){grant.IcaclsRights}'"));

    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    // Counts what is wrong and, when apply is set, fixes it. A reparse point is never followed and always counts: a junction
    // a standard user planted under the folder would otherwise leave the SYSTEM task running whatever it points at.
    public static int Run(string root, bool apply, ILogService log)
    {
        var problems = 0;
        var info = new DirectoryInfo(root);
        if (!info.Exists)
            return 0;

        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            log.LogWarning($"{root} is a link, not locking it");
            return 1;
        }

        if (!RootMatches(info.GetAccessControl()))
        {
            problems++;
            if (apply)
                info.SetAccessControl(BuildRootSecurity());
        }

        problems += WalkChildren(info, apply, log);

        if (problems > 0 && apply)
            log.LogInformation($"Locked {root}: fixed {problems} permission problem(s)");

        return problems;
    }

    private static int WalkChildren(DirectoryInfo directory, bool apply, ILogService log)
    {
        var problems = 0;
        var entries = TryList(directory);
        if (entries is null)
        {
            problems++;
            if (apply && TakeBack(directory, log))
                entries = TryList(directory);

            if (entries is null)
            {
                log.LogWarning($"Could not read {directory.FullName}, so what is inside it was not checked");
                return problems;
            }
        }

        foreach (var entry in entries)
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                problems++;
                if (apply)
                    RemoveLink(entry, log);
                continue;
            }

            if (entry is DirectoryInfo || IsRunnable(entry.Name))
            {
                try
                {
                    problems += FixChild(entry, apply);
                }
                catch (UnauthorizedAccessException)
                {
                    problems++;
                    if (apply && !TakeBack(entry, log))
                        log.LogWarning($"Could not take back {entry.FullName}");
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException)
                {
                    log.LogDebug($"Could not check permissions on {entry.FullName}: {ex.Message}");
                }
            }

            if (entry is DirectoryInfo subdirectory)
                problems += WalkChildren(subdirectory, apply, log);
        }

        return problems;
    }

    // An entry whose DACL denies Administrators must not slip past the walk, so a refused listing is null, not empty.
    private static List<FileSystemInfo>? TryList(DirectoryInfo directory)
    {
        try
        {
            return directory.EnumerateFileSystemInfos("*", new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = false }).ToList();
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Administrators can take ownership of anything; the owner can then drop the explicit rules, leaving only what the root hands down.
    private static bool TakeBack(FileSystemInfo entry, ILogService log)
    {
        try
        {
            ProcessPrivilege.Enable(PInvoke.SE_TAKE_OWNERSHIP_NAME);
            if (entry is DirectoryInfo directory)
            {
                var owner = new DirectorySecurity();
                owner.SetOwner(Administrators);
                directory.SetAccessControl(owner);
                var inherit = new DirectorySecurity();
                inherit.SetAccessRuleProtection(false, false);
                directory.SetAccessControl(inherit);
            }
            else
            {
                var file = (FileInfo)entry;
                var owner = new FileSecurity();
                owner.SetOwner(Administrators);
                file.SetAccessControl(owner);
                var inherit = new FileSecurity();
                inherit.SetAccessRuleProtection(false, false);
                file.SetAccessControl(inherit);
            }

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or InvalidOperationException or Win32Exception)
        {
            log.LogDebug($"Could not take ownership of {entry.FullName}: {ex.Message}");
            return false;
        }
    }

    // Deleting a link without recursing removes the link and leaves what it points at alone.
    private static void RemoveLink(FileSystemInfo link, ILogService log)
    {
        try
        {
            if (link is DirectoryInfo)
                Directory.Delete(link.FullName, recursive: false);
            else
                File.Delete(link.FullName);

            log.LogWarning($"Removed the link {link.FullName} from the Winhance folder");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            log.LogWarning($"Could not remove the link {link.FullName}: {ex.Message}");
        }
    }

    // A folder somebody else made is not trusted, so anything but a private one is replaced. A link is deleted without
    // following it; a real folder is deleted with its contents, which never follows a link inside it.
    public static bool EnsurePrivateFolder(string path, ILogService log)
    {
        try
        {
            var info = new DirectoryInfo(path);
            if (info.Exists && !IsPrivate(info))
            {
                log.LogWarning($"{path} is not private, replacing it");
                Directory.Delete(path, recursive: !info.Attributes.HasFlag(FileAttributes.ReparsePoint));
                info.Refresh();
            }

            if (!info.Exists)
            {
                if (info.Parent is { Exists: false } parent)
                    parent.Create();
                info.Create(BuildRootSecurity());
            }

            if (IsPrivate(info))
                return true;

            log.LogWarning($"{path} is not private after it was created");
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.LogWarning($"Could not make {path} private: {ex.Message}");
            return false;
        }
    }

    private static bool IsPrivate(DirectoryInfo info)
    {
        info.Refresh();
        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return false;

        var security = info.GetAccessControl();
        return OwnerIsTrusted(security) && RootMatches(security);
    }

    internal static DirectorySecurity BuildRootSecurity()
    {
        var security = new DirectorySecurity();
        security.SetOwner(Administrators);
        security.SetAccessRuleProtection(true, false);
        foreach (var (sid, rights, _) in RootGrants)
            security.AddAccessRule(new FileSystemAccessRule(sid, rights, Everything, PropagationFlags.None, AccessControlType.Allow));
        return security;
    }

    internal static bool RootMatches(DirectorySecurity actual)
    {
        if (!actual.AreAccessRulesProtected || actual.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier != Administrators)
            return false;

        var expected = BuildRootSecurity().GetAccessRules(true, false, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToList();
        var rules = actual.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToList();
        return rules.Count == expected.Count && expected.All(want => rules.Any(have =>
            have.AccessControlType == AccessControlType.Allow
            && !have.IsInherited
            && have.IdentityReference == want.IdentityReference
            && have.InheritanceFlags == want.InheritanceFlags
            && have.PropagationFlags == want.PropagationFlags
            && (have.FileSystemRights & ~FileSystemRights.Synchronize) == (want.FileSystemRights & ~FileSystemRights.Synchronize)));
    }

    internal static bool IsRunnable(string fileName) =>
        RunnableExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);

    // Grants of write, modify or full control to anyone who is not SYSTEM, Administrators or TrustedInstaller. Inherited rules
    // count only when asked for, and then an inherit-only rule is skipped: it does not apply to the entry it sits on.
    internal static List<FileSystemAccessRule> UnsafeRules(FileSystemSecurity security, bool includeInherited = false) =>
        security.GetAccessRules(true, includeInherited, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Where(rule => rule.AccessControlType == AccessControlType.Allow
                && (rule.FileSystemRights & WriteRights) != 0
                && !(includeInherited && rule.PropagationFlags.HasFlag(PropagationFlags.InheritOnly))
                && !IsTrusted((SecurityIdentifier)rule.IdentityReference))
            .ToList();

    // For a file or folder that something elevated runs: an untrusted owner can rewrite the DACL, and any inherited rule
    // that lets another account write defeats the folder. A path that cannot be read counts as changeable.
    public static bool CanBeChangedByOthers(string path)
    {
        try
        {
            FileSystemSecurity security = Directory.Exists(path)
                ? new DirectoryInfo(path).GetAccessControl()
                : new FileInfo(path).GetAccessControl();
            return !OwnerIsTrusted(security) || UnsafeRules(security, includeInherited: true).Count > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return true;
        }
    }

    private static bool OwnerIsTrusted(FileSystemSecurity security) =>
        security.GetOwner(typeof(SecurityIdentifier)) is SecurityIdentifier owner && IsTrusted(owner);

    internal static bool IsTrusted(SecurityIdentifier sid) =>
        sid == System || sid == Administrators || sid == TrustedInstaller;

    private static int FixChild(FileSystemInfo entry, bool apply)
    {
        FileSystemSecurity security = entry is DirectoryInfo directory ? directory.GetAccessControl() : ((FileInfo)entry).GetAccessControl();
        var unsafeRules = UnsafeRules(security);
        var untrustedOwner = !OwnerIsTrusted(security);
        var blocksInheritance = security.AreAccessRulesProtected;
        if (unsafeRules.Count == 0 && !untrustedOwner && !blocksInheritance)
            return 0;

        if (apply)
        {
            if (blocksInheritance)
                security.SetAccessRuleProtection(false, true);
            foreach (var rule in unsafeRules)
                security.RemoveAccessRuleSpecific(rule);
            if (untrustedOwner)
                security.SetOwner(Administrators);

            if (entry is DirectoryInfo dir)
                dir.SetAccessControl((DirectorySecurity)security);
            else
                ((FileInfo)entry).SetAccessControl((FileSecurity)security);
        }

        return 1;
    }
}
