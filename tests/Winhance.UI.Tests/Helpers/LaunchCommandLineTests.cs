using FluentAssertions;
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
