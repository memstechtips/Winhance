using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class LaunchCommandLineTests
{
    private readonly List<string> _errors = [];

    private LaunchOptions Parse(params string[] args) => LaunchCommandLine.Parse(args, _errors.Add);

    [Fact]
    public void Parse_NoArguments_InstallsNothing()
    {
        var options = Parse();

        options.InstallApps.Should().BeEmpty();
        options.InstallFolder.Should().BeNull();
        options.InstallsApps.Should().BeFalse();
        options.ConfigFile.Should().BeNull();
        _errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_InstallOptions_ReadsWhatTheAnswerFileWrote()
    {
        var written = new InstallOptionsStore { Defaults = InstallOptions.Default with { Architecture = InstallArchitecture.X86 } };

        var options = Parse("--install-apps", "firefox", "--install-options", written.ToArgument());

        options.InstallOptions!.Defaults.Should().Be(written.Defaults);
        _errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_UnreadableInstallOptions_FallBackToThisPcsOwn()
    {
        Parse("--install-apps", "firefox", "--install-options", "not base64!").InstallOptions.Should().BeNull();
    }

    [Fact]
    public void Parse_Config_KeepsAPathWithSpaces()
    {
        var options = Parse("--config", @"C:\Setup Files\My PC.winhance");

        options.ConfigFile.Should().Be(@"C:\Setup Files\My PC.winhance");
        options.InstallsApps.Should().BeFalse();
        _errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ConfigWithInstallApps_ReadsBoth()
    {
        var options = Parse("--config", @"C:\pc.winhance", "--install-apps", "firefox");

        options.ConfigFile.Should().Be(@"C:\pc.winhance");
        options.InstallApps.Should().Equal("firefox");
    }

    [Fact]
    public void Parse_InstallApps_SplitsTheIds()
    {
        var options = Parse("--install-apps", "firefox,7zip");

        options.InstallApps.Should().Equal("firefox", "7zip");
        options.InstallFolder.Should().BeNull();
        options.InstallsApps.Should().BeTrue();
    }

    [Fact]
    public void Parse_InstallApps_TrimsTheIdsAndDropsEmptyOnes()
    {
        var options = Parse("--install-apps", " firefox , 7zip ,");

        options.InstallApps.Should().Equal("firefox", "7zip");
    }

    [Fact]
    public void Parse_InstallFolder_KeepsAPathWithSpaces()
    {
        var options = Parse("--install-folder", @"C:\ProgramData\Winhance\Unattend\Apps\Custom Installers");

        options.InstallFolder.Should().Be(@"C:\ProgramData\Winhance\Unattend\Apps\Custom Installers");
        options.InstallApps.Should().BeEmpty();
        options.InstallsApps.Should().BeTrue();
    }

    [Fact]
    public void Parse_BothOptions_ReadsBoth()
    {
        var options = Parse("--install-apps", "firefox", "--install-folder", @"C:\Custom");

        options.InstallApps.Should().Equal("firefox");
        options.InstallFolder.Should().Be(@"C:\Custom");
    }

    [Fact]
    public void Parse_Tray_StartsInTheTray()
    {
        Parse("--tray").StartInTray.Should().BeTrue();
        Parse().StartInTray.Should().BeFalse();
    }

    [Fact]
    public void Parse_Exit_ClosesTheWindowedAppLikeItsX()
    {
        Parse("--exit").ExitFromTray.Should().BeTrue();
        Parse().ExitFromTray.Should().BeFalse();
    }

    [Fact]
    public void Parse_DiagMemory_SetsTheFlag()
    {
        Parse("--diag-memory").DiagMemory.Should().BeTrue();
        Parse().DiagMemory.Should().BeFalse();
    }

    [Fact]
    public void Parse_TrayAlone_RunsTheTrayHost()
    {
        Parse("--tray").RunsTrayHost.Should().BeTrue();
        Parse().RunsTrayHost.Should().BeFalse();
    }

    [Theory]
    [InlineData("--profile Gaming")]
    [InlineData("--reapply-profile")]
    [InlineData("--check-app-updates Scheduled")]
    [InlineData(@"--config C:\Configs\Gaming.winhance")]
    [InlineData("--install-apps firefox")]
    public void Parse_TrayWithAJob_RunsTheFullApp(string job)
    {
        Parse([.. job.Split(' '), "--tray"]).RunsTrayHost.Should().BeFalse();
        _errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Manual", AppUpdateCheckLaunch.Manual)]
    [InlineData("Scheduled", AppUpdateCheckLaunch.Scheduled)]
    public void Parse_CheckAppUpdates_ChecksInTheBackground(string kind, AppUpdateCheckLaunch expected)
    {
        var options = Parse("--check-app-updates", kind);

        options.CheckAppUpdates.Should().Be(expected);
        options.RunsInBackground.Should().BeTrue();
        options.StartsHidden.Should().BeTrue();
        _errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_Profile_SwitchesInTheBackground()
    {
        var options = Parse("--profile", "My Gaming");

        options.Profile.Should().Be("My Gaming");
        options.SwitchesProfile.Should().BeTrue();
        options.StartsHidden.Should().BeTrue();
        options.StartInTray.Should().BeFalse();
    }

    [Fact]
    public void Parse_ReapplyProfile_SwitchesInTheBackground()
    {
        var options = Parse("--reapply-profile");

        options.ReapplyProfile.Should().BeTrue();
        options.StartsHidden.Should().BeTrue();
        Parse().StartsHidden.Should().BeFalse();
    }

    [Theory]
    [InlineData(@"""C:\Program Files\Winhance\Winhance.exe"" --profile ""My Gaming""")]
    [InlineData(@"--profile ""My Gaming""")]
    public void ParseRedirected_DropsTheExeAndKeepsQuotedNames(string commandLine)
    {
        var options = LaunchCommandLine.ParseRedirected(commandLine, _errors.Add);

        options.Profile.Should().Be("My Gaming");
        _errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_Maintain_SetsTheFlagAndStaysOutOfTheBackgroundModes()
    {
        var options = Parse("--maintain");

        options.Maintain.Should().BeTrue();
        options.RunsInBackground.Should().BeFalse();
        options.StartsHidden.Should().BeFalse();
        options.MaintainsAlone.Should().BeTrue();
        _errors.Should().BeEmpty();
        Parse().Maintain.Should().BeFalse();
    }

    [Theory]
    [InlineData("--tray")]
    [InlineData("--reapply-profile")]
    public void Parse_MaintainWithAnotherSignInJob_DoesNotRunAlone(string other)
    {
        Parse("--maintain", other).MaintainsAlone.Should().BeFalse();
    }

    [Theory]
    [InlineData("--no-such-flag")]
    [InlineData("stray-argument")]
    [InlineData("--install-apps")]
    [InlineData("--config")]
    public void Parse_InvalidCommandLine_LogsTheErrorAndStartsNormally(string arg)
    {
        var options = Parse(arg);

        options.Should().BeSameAs(LaunchOptions.None);
        _errors.Should().NotBeEmpty();
    }
}
