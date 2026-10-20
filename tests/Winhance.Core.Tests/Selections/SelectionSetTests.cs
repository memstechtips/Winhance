using FluentAssertions;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Selections;
using Xunit;

namespace Winhance.Core.Tests.Selections;

public class SelectionSetTests
{
    private const string WinhanceOnNewPc = "autounattend-desktop-shortcut";
    private const string DesktopFolder = "explorer-customization-thispc-folder-desktop";

    private static SelectionSet Set(params SettingChoice[] choices) => new(choices, [], []);

    [Fact]
    public void Value_finds_a_choice_whichever_side_names_the_retired_id()
    {
        var value = new ChoiceValue.Toggle(false);

        Set(new SettingChoice(DesktopFolder + "-win10", value)).Value(DesktopFolder).Should().Be(value);
        Set(new SettingChoice(DesktopFolder, value)).Value(DesktopFolder + "-win10").Should().Be(value);
    }

    [Fact]
    public void Value_is_null_for_a_setting_the_set_does_not_hold()
    {
        SelectionSet.Empty.Value(WinhanceOnNewPc).Should().BeNull();
    }

    [Fact]
    public void StateOf_reads_the_chosen_option()
    {
        var set = Set(new SettingChoice(WinhanceOnNewPc, new ChoiceValue.Option(0)));

        set.StateOf(WinhanceOnNewPc)!.Label.Should().Be(LocKey.Setting.AutounattendDesktopShortcut.Option0);
    }

    [Fact]
    public void StateOf_falls_back_to_the_seed_state_when_the_set_holds_no_choice()
    {
        SelectionSet.Empty.StateOf(WinhanceOnNewPc)!.Label.Should().Be(LocKey.Setting.AutounattendDesktopShortcut.Option1);
    }

    [Fact]
    public void StateOf_falls_back_to_the_seed_state_for_an_option_the_setting_does_not_have()
    {
        var set = Set(new SettingChoice(WinhanceOnNewPc, new ChoiceValue.Option(99)));

        set.StateOf(WinhanceOnNewPc)!.Label.Should().Be(LocKey.Setting.AutounattendDesktopShortcut.Option1);
    }

    [Fact]
    public void StateOf_is_null_for_an_id_the_catalog_does_not_know()
    {
        SelectionSet.Empty.StateOf("no-such-setting").Should().BeNull();
    }
}
