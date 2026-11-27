using FluentAssertions;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class OwnInstallerTests
{
    private static SelectionSet Set(params SettingChoice[] choices) => new(choices, [], []);

    private static SettingChoice Installers(params (string File, string Switches)[] rows) =>
        new(OwnInstaller.SettingId, new ChoiceValue.List(
        [
            .. rows.Select(row => new ChoiceValue.ListRow(new Dictionary<string, string>
            {
                ["file"] = row.File,
                ["switches"] = row.Switches,
            })),
        ]));

    [Fact]
    public void From_WithInstallerRows_ReadsEachRowWithItsSwitches()
    {
        var set = Set(Installers((@"C:\Tools\Setup.exe", " /S "), (@"C:\Tools\Pack.msi", "")));

        OwnInstaller.From(set).Should().Equal(
            new OwnInstaller(@"C:\Tools\Setup.exe", "/S"),
            new OwnInstaller(@"C:\Tools\Pack.msi", ""));
    }

    [Fact]
    public void From_WithMixedPaths_KeepsOnlyFullInstallerPaths()
    {
        var set = Set(Installers(
            (@"C:\Tools\Setup.EXE", ""),
            (@"C:\Tools\notes.txt", ""),
            ("Setup.exe", ""),
            (@"Tools\Setup.msi", ""),
            ("", "/S")));

        OwnInstaller.From(set).Should().Equal(new OwnInstaller(@"C:\Tools\Setup.EXE", ""));
    }

    [Fact]
    public void From_WhenWinhanceNotAddedToNewPc_IsEmpty()
    {
        var set = Set(
            Installers((@"C:\Tools\Setup.exe", "")),
            new SettingChoice("autounattend-desktop-shortcut", new ChoiceValue.Option(0)));

        OwnInstaller.From(set).Should().BeEmpty();
    }

    [Fact]
    public void From_WithNoInstallerList_IsEmpty()
    {
        OwnInstaller.From(SelectionSet.Empty).Should().BeEmpty();
    }

    [Fact]
    public void StagesApps_UntilTheTickIsCleared_IsTrue()
    {
        OwnInstaller.StagesApps(SelectionSet.Empty).Should().BeTrue();
        OwnInstaller.StagesApps(Set(new SettingChoice(OwnInstaller.StageAppsId, new ChoiceValue.CheckBox(true)))).Should().BeTrue();
        OwnInstaller.StagesApps(Set(new SettingChoice(OwnInstaller.StageAppsId, new ChoiceValue.CheckBox(false)))).Should().BeFalse();
    }
}
