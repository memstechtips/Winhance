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
    public void Value_RetiredIdOnEitherSide_FindsTheChoice()
    {
        var value = new ChoiceValue.Toggle(false);

        Set(new SettingChoice(DesktopFolder + "-win10", value)).Value(DesktopFolder).Should().Be(value);
        Set(new SettingChoice(DesktopFolder, value)).Value(DesktopFolder + "-win10").Should().Be(value);
    }

    [Fact]
    public void Value_SettingTheSetDoesNotHold_ReturnsNull()
    {
        SelectionSet.Empty.Value(WinhanceOnNewPc).Should().BeNull();
    }

    [Fact]
    public void StateOf_ChosenOption_ReadsIt()
    {
        var set = Set(new SettingChoice(WinhanceOnNewPc, new ChoiceValue.Option(0)));

        set.StateOf(WinhanceOnNewPc)!.Label.Should().Be(LocKey.Setting.AutounattendDesktopShortcut.Option0);
    }

    [Fact]
    public void StateOf_SetHoldsNoChoice_FallsBackToTheSeedState()
    {
        SelectionSet.Empty.StateOf(WinhanceOnNewPc)!.Label.Should().Be(LocKey.Setting.AutounattendDesktopShortcut.Option1);
    }

    [Fact]
    public void StateOf_OptionTheSettingDoesNotHave_FallsBackToTheSeedState()
    {
        var set = Set(new SettingChoice(WinhanceOnNewPc, new ChoiceValue.Option(99)));

        set.StateOf(WinhanceOnNewPc)!.Label.Should().Be(LocKey.Setting.AutounattendDesktopShortcut.Option1);
    }

    [Fact]
    public void StateOf_IdTheCatalogDoesNotKnow_ReturnsNull()
    {
        SelectionSet.Empty.StateOf("no-such-setting").Should().BeNull();
    }
}
