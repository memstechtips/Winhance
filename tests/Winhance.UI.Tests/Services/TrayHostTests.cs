using FluentAssertions;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class TrayHostTests
{
    private static readonly IReadOnlyList<SettingsProfile> Profiles =
    [
        new("builtin:focus", "Winhance Focus", ProfileKind.Focus, null),
        new("My Gaming", "My Gaming", ProfileKind.User, @"C:\Profiles\My Gaming.winhance"),
    ];

    private readonly List<string> _errors = [];

    private LaunchOptions LaunchFor(int item) => LaunchCommandLine.Parse(TrayHost.ArgumentsFor(item, Profiles)!, _errors.Add);

    [Fact]
    public void ArgumentsFor_OpenItem_StartsTheAppWithItsWindow()
    {
        var launch = LaunchFor(TrayController.OpenItem);

        launch.StartsHidden.Should().BeFalse();
        _errors.Should().BeEmpty();
    }

    [Fact]
    public void ArgumentsFor_CheckItem_RunsAManualCheckAndReturnsToTheTray()
    {
        var launch = LaunchFor(TrayController.CheckItem);

        launch.CheckAppUpdates.Should().Be(AppUpdateCheckLaunch.Manual);
        launch.StartInTray.Should().BeTrue();
        launch.RunsTrayHost.Should().BeFalse();
        _errors.Should().BeEmpty();
    }

    [Fact]
    public void ArgumentsFor_ProfileItem_SwitchesToThatProfileById()
    {
        var launch = LaunchFor(TrayController.FirstProfileItem + 1);

        launch.Profile.Should().Be("My Gaming");
        launch.StartInTray.Should().BeTrue();
        _errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ScheduledCheckArguments_RunsAScheduledCheckAndReturnsToTheTray()
    {
        var launch = LaunchCommandLine.Parse(TrayHost.ScheduledCheckArguments, _errors.Add);

        launch.CheckAppUpdates.Should().Be(AppUpdateCheckLaunch.Scheduled);
        launch.StartInTray.Should().BeTrue();
        _errors.Should().BeEmpty();
    }

    [Fact]
    public void ArgumentsFor_UpdateAppsItem_RunsTheInstallingCheckAndReturnsToTheTray()
    {
        var launch = LaunchFor(TrayController.UpdateAppsItem);

        launch.CheckAppUpdates.Should().Be(AppUpdateCheckLaunch.Install);
        launch.StartInTray.Should().BeTrue();
        _errors.Should().BeEmpty();
    }

    [Fact]
    public void ArgumentsFor_StateItem_OpensTheWindow()
    {
        var launch = LaunchFor(TrayController.StateItem);

        launch.StartsHidden.Should().BeFalse();
        _errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ExitFlag_OpensTheWindowAndClosesItTheNormalWay()
    {
        var launch = LaunchCommandLine.Parse([TrayController.ExitFlag], _errors.Add);

        launch.ExitFromTray.Should().BeTrue();
        launch.StartsHidden.Should().BeFalse();
        launch.RunsTrayHost.Should().BeFalse();
        _errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(TrayController.ExitItem)]
    [InlineData(TrayController.FirstProfileItem + 2)]
    [InlineData(-1)]
    public void ArgumentsFor_ExitAndUnknownItems_StartsNothing(int item) =>
        TrayHost.ArgumentsFor(item, Profiles).Should().BeNull();
}
