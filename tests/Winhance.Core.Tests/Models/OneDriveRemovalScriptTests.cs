using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class OneDriveRemovalScriptTests
{
    [Fact]
    public void GetScript_CleansTheAppDataFolderAndStartMenuLinkOfEveryProfile()
    {
        var script = OneDriveRemovalScript.GetScript();
        var start = script.IndexOf("# 3.2:", StringComparison.Ordinal);
        var cleanup = script[start..script.IndexOf("# 3.4:", start, StringComparison.Ordinal)];

        cleanup.Should().Contain("Get-ChildItem -Path \"$root\\Users\" -Directory -Force");
        cleanup.Should().Contain("foreach ($userProfile in $userProfiles)");
        cleanup.Should().Contain("\\AppData\\Local\\Microsoft\\OneDrive");
        cleanup.Should().Contain("\\Start Menu\\Programs\\OneDrive.lnk");
        cleanup.Should().NotContain("$userProfilePath");
    }

    [Fact]
    public void GetScript_TakesTheImageAndHiveRootsWithLiveDefaults()
    {
        var script = OneDriveRemovalScript.GetScript();

        script.Should().Contain("param([string]$Image = '',");
        script.Should().Contain("[string]$SoftwareRoot = 'HKEY_LOCAL_MACHINE\\SOFTWARE'");
        script.Should().Contain("[string]$UserRoot = 'HKEY_CURRENT_USER'");
        script.Should().Contain("$root = if ($Offline) { $Image.TrimEnd('\\') } else { $env:SystemDrive }");
    }

    [Fact]
    public void GetScript_OnAnImage_SkipsTheLiveOnlySteps()
    {
        var script = OneDriveRemovalScript.GetScript();

        script.Should().Contain("Where-Object { -not $Offline }");
        script.Should().Contain("if ($remaining -and -not $Offline) {");
        script.Should().Contain("if (-not $Offline -and (Test-Path $folder)) {");
        script.Should().Contain("Remove-OneDriveUserKeys $UserRoot");
        script.Should().Contain("$defaultUser = if ($Offline) { $UserRoot } else { 'HKU\\Default' }");
    }

    [Fact]
    public void GetScript_LockedAppDataFolder_IsQueuedForDeletionOnRebootWithoutTouchingExplorer()
    {
        var script = OneDriveRemovalScript.GetScript();

        script.Should().Contain("MoveFileEx($Path, [NullString]::Value, 4)");
        script.IndexOf("Schedule-DeleteOnReboot $_.FullName", StringComparison.Ordinal)
            .Should().BeGreaterThan(script.IndexOf("Remove-Item $folder -Recurse -Force", StringComparison.Ordinal));
    }

    [Fact]
    public void GetScript_NeverDeletesTheUsersSyncedOneDriveFolder()
    {
        var script = OneDriveRemovalScript.GetScript();

        script.Should().Contain("foreach ($folder in \"$($userProfile.FullName)\\AppData\\Local\\Microsoft\\OneDrive\", \"$($userProfile.FullName)\\AppData\\Roaming\\Microsoft\\OneDrive\")");
        script.Should().NotContain("$($userProfile.FullName)\\OneDrive");
    }

    [Fact]
    public void GetScript_CleansTheRegistryOfEveryUserIncludingProfilesThatAreNotLoaded()
    {
        var script = OneDriveRemovalScript.GetScript();
        var start = script.IndexOf("# 3.1c:", StringComparison.Ordinal);
        var cleanup = script[start..script.IndexOf("# 3.2:", start, StringComparison.Ordinal)];

        cleanup.Should().Contain("Registry::HKEY_USERS");
        cleanup.Should().Contain("'^S-1-5-21-'");
        cleanup.Should().Contain("-notlike '*_Classes'");
        cleanup.Should().Contain("ProfileList");
        cleanup.Should().Contain("reg.exe load HKU\\WinhanceTemp");
        cleanup.Should().Contain("[gc]::Collect()");
        cleanup.Should().Contain("reg.exe unload HKU\\WinhanceTemp");
        cleanup.Should().Contain("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\OneDriveSetup.exe");
        cleanup.IndexOf("[gc]::Collect()", StringComparison.Ordinal)
            .Should().BeLessThan(cleanup.IndexOf("reg.exe unload", StringComparison.Ordinal));
        script.Should().NotContain("HKU\\$userSID\\SOFTWARE\\Microsoft\\OneDrive");
    }

    [Fact]
    public void GetScript_NeverRunsAnUninstallStringFromAUserHive()
    {
        var script = OneDriveRemovalScript.GetScript();

        script.Should().NotContain("Invoke-RegistryUninstall \"HKU");
        script.Should().NotContain("'HKU')");
        script.Should().NotContain("Get-UserSID");
        script.Should().NotContain("Get-TargetUser");
    }

    [Fact]
    public void GetScript_RunsTheHkcuUninstallerOnlyWhenNotSystem()
    {
        var script = OneDriveRemovalScript.GetScript();
        var call = script.IndexOf("Invoke-RegistryUninstall 'HKCU", StringComparison.Ordinal);
        var guard = script.LastIndexOf("if (", call, StringComparison.Ordinal);

        call.Should().BeGreaterThan(0);
        script[guard..call].Should().Contain("-not $runningAsSystem");
        script.Should().Contain("$runningAsSystem = -not $Offline -and ($env:USERNAME -eq 'SYSTEM'");
    }

    [Fact]
    public void GetScript_StopsOneDriveBeforeTheCleanup()
    {
        var script = OneDriveRemovalScript.GetScript();
        var stop = script.IndexOf("if (-not $Offline) { Stop-Process -Name '*OneDrive*'", StringComparison.Ordinal);

        stop.Should().BeGreaterThan(script.IndexOf("# Step 3:", StringComparison.Ordinal));
        stop.Should().BeLessThan(script.IndexOf("# 3.2:", StringComparison.Ordinal));
    }

    [Fact]
    public void GetScript_LiveRun_NeverTakesOwnershipInsideAUserProfile()
    {
        var script = OneDriveRemovalScript.GetScript();
        var start = script.IndexOf("# 3.2:", StringComparison.Ordinal);
        var perProfile = script[start..script.IndexOf("$systemLink =", start, StringComparison.Ordinal)];
        var takeown = perProfile.IndexOf("takeown /f $folder", StringComparison.Ordinal);

        takeown.Should().BeGreaterThan(0);
        perProfile[..takeown].TrimEnd().Should().EndWith("if ($Offline) {");
        perProfile.Should().Contain("icacls $folder /grant '*S-1-5-32-544:F' /t");
    }

    [Fact]
    public void GetScript_SkipsAProfileFolderBehindALink()
    {
        var script = OneDriveRemovalScript.GetScript();

        script.Should().Contain("function Test-NoLink([string]$Path, [string]$Stop)");
        script.Should().Contain("$p.Attributes -band 'ReparsePoint'");
        script.Should().Contain("if (-not (Test-NoLink $folder $userProfile.FullName)) {");
        script.IndexOf("Test-NoLink $folder", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("Remove-Item $folder -Recurse -Force", StringComparison.Ordinal));
    }

    [Fact]
    public void GetScript_NeverTakesOwnershipOfTheProgramDataFolder()
    {
        var script = OneDriveRemovalScript.GetScript();
        var takeown = script.IndexOf("takeown /f $path", StringComparison.Ordinal);

        script.Should().Contain("$userMadePath = \"$root\\ProgramData\\Microsoft OneDrive\"");
        script[..takeown].TrimEnd().Should().EndWith("if ($path -ne $userMadePath) {");
        script.IndexOf("Remove-Item $path -Recurse -Force", StringComparison.Ordinal).Should().BeGreaterThan(takeown);
    }
}
