using FluentAssertions;
using Winhance.Core.Features.Autounattend;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Models;
using Xunit;

namespace Winhance.Core.Tests.Autounattend;

public class WinhanceOnNewPcTests
{
    private static readonly AppChoice Firefox = new("firefox", "Firefox", null, null, null, "Mozilla.Firefox");

    private static readonly AppChoice Cortana = new("windows-app-cortana", "Cortana", ["Microsoft.549981C3F5F10"], null, null, null);

    private static SettingChoice Card(int index) => new(WinhanceOnNewPc.SettingId, new ChoiceValue.Option(index));

    private static SettingChoice Installer(string file) =>
        new(OwnInstaller.SettingId, new ChoiceValue.List(
            [new ChoiceValue.ListRow(new Dictionary<string, string> { ["file"] = file, ["switches"] = "" })]));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Enforce_WhenFileInstallsApps_SetsWinhanceToInstallAutomatically(int chosen)
    {
        var set = new SelectionSet([Card(chosen)], [], [Firefox]);

        var enforced = WinhanceOnNewPc.Enforce(set);

        enforced.StateOf(WinhanceOnNewPc.SettingId)!.Label.Should().Be(WinhanceOnNewPc.InstallsAutomatically);
        enforced.Settings.Where(c => c.SettingId == WinhanceOnNewPc.SettingId).Should().ContainSingle();
    }

    [Fact]
    public void Enforce_WithOwnInstallers_CountsThemAsAppsAndKeepsThem()
    {
        var set = new SelectionSet([Card(0), Installer(@"C:\Tools\Setup.exe")], [], []);

        var enforced = WinhanceOnNewPc.Enforce(set);

        enforced.StateOf(WinhanceOnNewPc.SettingId)!.Label.Should().Be(WinhanceOnNewPc.InstallsAutomatically);
        OwnInstaller.From(enforced).Should().Equal(new OwnInstaller(@"C:\Tools\Setup.exe", ""));
    }

    [Fact]
    public void Enforce_WhenSetNeverHeldTheChoice_AddsIt()
    {
        var enforced = WinhanceOnNewPc.Enforce(new SelectionSet([], [], [Firefox]));

        enforced.StateOf(WinhanceOnNewPc.SettingId)!.Label.Should().Be(WinhanceOnNewPc.InstallsAutomatically);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Enforce_WithoutApps_LeavesTheChoiceAlone(int chosen)
    {
        var set = new SelectionSet([Card(chosen)], [], []);

        WinhanceOnNewPc.Enforce(set).Should().BeSameAs(set);
    }

    private static SettingChoice KeepRemoved(int index) => new(WinhanceOnNewPc.KeepRemovedId, new ChoiceValue.Option(index));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Enforce_WhenWinhanceKeepsRemovedAppsRemoved_InstallsWinhance(int chosen)
    {
        var set = new SelectionSet([Card(chosen), KeepRemoved(2)], [Cortana], []);

        var enforced = WinhanceOnNewPc.Enforce(set);

        enforced.StateOf(WinhanceOnNewPc.SettingId)!.Label.Should().Be(WinhanceOnNewPc.InstallsAutomatically);
        enforced.Settings.Where(c => c.SettingId == WinhanceOnNewPc.SettingId).Should().ContainSingle();
        enforced.Settings.Should().Contain(KeepRemoved(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Enforce_WhenRemovedAppsStayRemovedAnotherWay_LeavesTheChoiceAlone(int keepRemoved)
    {
        var set = new SelectionSet([Card(0), KeepRemoved(keepRemoved)], [Cortana], []);

        WinhanceOnNewPc.Enforce(set).Should().BeSameAs(set);
    }

    [Fact]
    public void Enforce_WhenNoAppsAreRemovedForWinhanceToKeep_LeavesTheChoiceAlone()
    {
        var set = new SelectionSet([Card(0), KeepRemoved(2)], [], []);

        WinhanceOnNewPc.Enforce(set).Should().BeSameAs(set);
        WinhanceOnNewPc.KeepsRemovedAppsWithWinhance(set).Should().BeFalse();
    }

    [Fact]
    public void InstallsAutomatically_Always_IsAStateOfTheCard()
    {
        WinhanceOnNewPc.InstallsAutomatically.Should().Be(LocKey.Setting.AutounattendDesktopShortcut.Option2);
        new SelectionSet([Card(2)], [], []).StateOf(WinhanceOnNewPc.SettingId)!.Label
            .Should().Be(WinhanceOnNewPc.InstallsAutomatically);
    }
}
