using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Utilities;
using Xunit;

namespace Winhance.Infrastructure.Tests.Utilities;

public class FolderLockTests
{
    private static readonly SecurityIdentifier Users = new(WellKnownSidType.BuiltinUsersSid, null);
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier Trusted = new("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464");

    private static FileSystemAccessRule Grant(SecurityIdentifier sid, FileSystemRights rights) =>
        new(sid, rights, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);

    [Fact]
    public void RootMatches_TheSecurityItBuilds_IsTrue()
    {
        FolderLock.RootMatches(FolderLock.BuildRootSecurity()).Should().BeTrue();
    }

    [Fact]
    public void RootMatches_UsersCanModify_IsFalse()
    {
        var security = FolderLock.BuildRootSecurity();
        security.AddAccessRule(Grant(Users, FileSystemRights.Modify));

        FolderLock.RootMatches(security).Should().BeFalse();
    }

    [Fact]
    public void RootMatches_InheritanceNotProtected_IsFalse()
    {
        var security = FolderLock.BuildRootSecurity();
        security.SetAccessRuleProtection(false, true);

        FolderLock.RootMatches(security).Should().BeFalse();
    }

    [Fact]
    public void RootMatches_OwnerIsNotAdministrators_IsFalse()
    {
        var security = FolderLock.BuildRootSecurity();
        security.SetOwner(Users);

        FolderLock.RootMatches(security).Should().BeFalse();
    }

    [Fact]
    public void Run_ChildJunction_IsCountedAndNotFollowed()
    {
        var parent = Path.Combine(Path.GetTempPath(), "winhance-lock-" + Guid.NewGuid().ToString("N"));
        var plainRoot = Path.Combine(parent, "plain");
        var root = Path.Combine(parent, "root");
        var target = Path.Combine(parent, "target");
        var link = Path.Combine(root, "Scripts");
        try
        {
            Directory.CreateDirectory(plainRoot);
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "unsafe.ps1"), "Write-Host hi");
            MakeJunction(link, target);
            var log = new Mock<ILogService>().Object;

            var baseline = FolderLock.Run(plainRoot, apply: false, log);
            var problems = FolderLock.Run(root, apply: false, log);

            problems.Should().Be(baseline + 1);
            File.Exists(Path.Combine(target, "unsafe.ps1")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(link))
                Directory.Delete(link, recursive: false);
            if (Directory.Exists(parent))
                Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Run_SubfolderThatDeniesListing_IsCountedNotSkipped()
    {
        var parent = Path.Combine(Path.GetTempPath(), "winhance-lock-" + Guid.NewGuid().ToString("N"));
        var plainSub = Path.Combine(parent, "plain", "sub");
        var lockedSub = Path.Combine(parent, "locked", "sub");
        var me = WindowsIdentity.GetCurrent().User!;
        var deny = new FileSystemAccessRule(me, FileSystemRights.ListDirectory, AccessControlType.Deny);
        try
        {
            Directory.CreateDirectory(plainSub);
            Directory.CreateDirectory(lockedSub);
            var security = new DirectoryInfo(lockedSub).GetAccessControl();
            security.AddAccessRule(deny);
            new DirectoryInfo(lockedSub).SetAccessControl(security);
            var log = new Mock<ILogService>().Object;

            var baseline = FolderLock.Run(Path.Combine(parent, "plain"), apply: false, log);
            var problems = FolderLock.Run(Path.Combine(parent, "locked"), apply: false, log);

            problems.Should().BeGreaterThan(baseline);
        }
        finally
        {
            var info = new DirectoryInfo(lockedSub);
            if (info.Exists)
            {
                var security = info.GetAccessControl();
                security.RemoveAccessRuleSpecific(deny);
                info.SetAccessControl(security);
            }

            if (Directory.Exists(parent))
                Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void IcaclsGrantArguments_GrantTheSameSidsAndRightsAsTheRootSecurity()
    {
        var granted = FolderLock.IcaclsGrantArguments.Split(' ')
            .Select(token => token.Trim('\'').TrimStart('*').Split(':'))
            .Select(parts => (Sid: parts[0], Rights: (parts[1][("(OI)(CI)".Length)..] switch
            {
                "F" => FileSystemRights.FullControl,
                "RX" => FileSystemRights.ReadAndExecute,
                var other => throw new InvalidOperationException($"Unmapped icacls right {other}"),
            }) & ~FileSystemRights.Synchronize))
            .ToList();
        var expected = FolderLock.BuildRootSecurity().GetAccessRules(true, false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Select(rule => (Sid: ((SecurityIdentifier)rule.IdentityReference).Value, Rights: rule.FileSystemRights & ~FileSystemRights.Synchronize))
            .ToList();

        granted.Should().BeEquivalentTo(expected);
    }

    private static void MakeJunction(string link, string target)
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0);
    }

    [Fact]
    public void UnsafeRules_InheritedUserModify_IsReportedOnlyWhenInheritedRulesCount()
    {
        var parent = TempParent();
        var child = Path.Combine(parent, "child");
        try
        {
            var security = new DirectorySecurity();
            security.AddAccessRule(Grant(Users, FileSystemRights.Modify));
            new DirectoryInfo(parent).SetAccessControl(security);
            Directory.CreateDirectory(child);
            var childSecurity = new DirectoryInfo(child).GetAccessControl();

            FolderLock.UnsafeRules(childSecurity).Should().BeEmpty();
            FolderLock.UnsafeRules(childSecurity, includeInherited: true).Should().Contain(rule => rule.IdentityReference == Users);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void UnsafeRules_InheritOnlyUserModify_IsNotReportedForInheritedRules()
    {
        var security = new DirectorySecurity();
        security.AddAccessRule(new FileSystemAccessRule(
            Users, FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.InheritOnly, AccessControlType.Allow));

        FolderLock.UnsafeRules(security, includeInherited: true).Should().BeEmpty();
    }

    [Fact]
    public void CanBeChangedByOthers_ProtectedSystemFile_IsFalse()
    {
        FolderLock.CanBeChangedByOthers(Path.Combine(Environment.SystemDirectory, "cmd.exe")).Should().BeFalse();
    }

    [Fact]
    public void CanBeChangedByOthers_FolderUsersCanWrite_IsTrue()
    {
        var parent = TempParent();
        try
        {
            var security = new DirectoryInfo(parent).GetAccessControl();
            security.AddAccessRule(Grant(Users, FileSystemRights.Modify));
            new DirectoryInfo(parent).SetAccessControl(security);

            FolderLock.CanBeChangedByOthers(parent).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    private static string TempParent() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "winhance-lock-" + Guid.NewGuid().ToString("N"))).FullName;

    [Theory]
    [InlineData("Keep.ps1", true)]
    [InlineData("run.CMD", true)]
    [InlineData("tool.exe", true)]
    [InlineData("a.dll", true)]
    [InlineData("settings.xml", true)]
    [InlineData("Winhancements.txt", false)]
    [InlineData("data.json", false)]
    [InlineData("noextension", false)]
    public void IsRunnable_MatchesTheScriptAndBinaryExtensions(string name, bool expected)
    {
        FolderLock.IsRunnable(name).Should().Be(expected);
    }

    [Fact]
    public void UnsafeRules_FlagsWriteGrantsToUsersOnly()
    {
        var security = new DirectorySecurity();
        security.AddAccessRule(Grant(Users, FileSystemRights.Write));
        security.AddAccessRule(Grant(Administrators, FileSystemRights.FullControl));
        security.AddAccessRule(Grant(Trusted, FileSystemRights.FullControl));

        var unsafeRules = FolderLock.UnsafeRules(security);

        unsafeRules.Should().ContainSingle().Which.IdentityReference.Should().Be(Users);
    }

    [Fact]
    public void UnsafeRules_ReadOnlyGrantToUsers_IsFine()
    {
        var security = new DirectorySecurity();
        security.AddAccessRule(Grant(Users, FileSystemRights.ReadAndExecute));

        FolderLock.UnsafeRules(security).Should().BeEmpty();
    }

    [Fact]
    public void IsTrusted_SystemAdministratorsAndTrustedInstallerOnly()
    {
        FolderLock.IsTrusted(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null)).Should().BeTrue();
        FolderLock.IsTrusted(Administrators).Should().BeTrue();
        FolderLock.IsTrusted(Trusted).Should().BeTrue();
        FolderLock.IsTrusted(Users).Should().BeFalse();
    }
}
