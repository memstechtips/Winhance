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
    public void From_reads_each_row_as_an_installer_with_its_switches()
    {
        var set = Set(Installers((@"C:\Tools\Setup.exe", " /S "), (@"C:\Tools\Pack.msi", "")));

        OwnInstaller.From(set).Should().Equal(
            new OwnInstaller(@"C:\Tools\Setup.exe", "/S"),
            new OwnInstaller(@"C:\Tools\Pack.msi", ""));
    }

    [Fact]
    public void From_believes_only_a_full_path_to_an_installer()
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
    public void From_is_empty_when_Winhance_is_not_added_to_the_new_PC()
    {
        var set = Set(
            Installers((@"C:\Tools\Setup.exe", "")),
            new SettingChoice("autounattend-desktop-shortcut", new ChoiceValue.Option(0)));

        OwnInstaller.From(set).Should().BeEmpty();
    }

    [Fact]
    public void From_is_empty_for_a_set_that_holds_no_installer_list()
    {
        OwnInstaller.From(SelectionSet.Empty).Should().BeEmpty();
    }

    [Fact]
    public void StagesApps_is_true_until_the_tick_is_cleared()
    {
        OwnInstaller.StagesApps(SelectionSet.Empty).Should().BeTrue();
        OwnInstaller.StagesApps(Set(new SettingChoice(OwnInstaller.StageAppsId, new ChoiceValue.CheckBox(true)))).Should().BeTrue();
        OwnInstaller.StagesApps(Set(new SettingChoice(OwnInstaller.StageAppsId, new ChoiceValue.CheckBox(false)))).Should().BeFalse();
    }
}
