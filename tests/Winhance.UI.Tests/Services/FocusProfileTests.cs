using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class FocusProfileTests
{
    private static readonly string[] ReadableFocusIds = ["processor-min-state", "gaming-game-mode", "gaming-task-sqm"];
    private static readonly string[] UnreadPowerIds = ["processor-energy-performance-preference", "usb-selective-suspend", "pci-link-state-power-management"];

    // A reading for every Focus setting, shaped the way the snapshot reads each control.
    private static List<SettingChoice> AMachineReadingEverything() =>
        FocusProfile.Ids
            .Select(id => SettingCatalog.Find(id) is { } setting
                ? new SettingChoice(id, setting.Control switch
                {
                    ControlKind.Slider => new ChoiceValue.AcDcNumber(5, 7),
                    ControlKind.Selection => new ChoiceValue.AcDcOption(1, 1),
                    _ => new ChoiceValue.Toggle(false),
                })
                : null)
            .OfType<SettingChoice>()
            .ToList();

    [Fact]
    public void Overlay_EveryFocusValue_IsAStateOfItsSettingAndTakesEffectWithoutARestart()
    {
        var problems = new List<string>();
        foreach (var id in FocusProfile.Ids.Where(id => SettingCatalog.Find(id) is null))
            problems.Add($"{id}: not in the catalog");

        var overlay = FocusProfile.Overlay(AMachineReadingEverything());
        foreach (var choice in overlay)
        {
            var setting = SettingCatalog.Find(choice.SettingId)!;
            if (!IsAStateOf(setting, choice.Value))
                problems.Add($"{choice.SettingId}: {choice.Value} is not one of its states");
            if (setting.Apply.RequiresReboot)
                problems.Add($"{choice.SettingId}: needs a reboot");
            if (RestartTarget.IsExplorer(setting.Apply.Restart))
                problems.Add($"{choice.SettingId}: restarts Explorer");
        }

        problems.Should().BeEmpty();
        overlay.Select(c => c.SettingId).Should().BeEquivalentTo(FocusProfile.Ids);
    }

    [Fact]
    public void Overlay_PowerSetting_ChangesOnACOnlyAndKeepsTheDCTheMachineReads()
    {
        var overlay = FocusProfile.Overlay(
        [
            new("processor-min-state", new ChoiceValue.AcDcNumber(5, 30)),
            new("usb-selective-suspend", new ChoiceValue.AcDcOption(1, 1)),
        ]).ToDictionary(c => c.SettingId, c => c.Value);

        overlay["processor-min-state"].Should().Be(new ChoiceValue.AcDcNumber(100, 30));
        overlay["usb-selective-suspend"].Should().Be(new ChoiceValue.AcDcOption(0, 1));
    }

    [Fact]
    public void Overlay_PowerSettingTheMachineCouldNotRead_IsLeftOut()
    {
        var ids = FocusProfile.Overlay([new("processor-min-state", new ChoiceValue.AcDcNumber(5, 30))]).Select(c => c.SettingId).ToList();

        ids.Should().Contain(ReadableFocusIds);
        ids.Should().NotContain(UnreadPowerIds);
    }

    private static bool IsAStateOf(Setting setting, ChoiceValue value) => value switch
    {
        ChoiceValue.Toggle toggle => setting.Control == ControlKind.Toggle
            && setting.States.Any(s => s.Label == TwoState.Label(ControlKind.Toggle, toggle.On)),
        ChoiceValue.Option option => setting.Control == ControlKind.Selection && IsIndex(setting, option.Index),
        ChoiceValue.AcDcOption option => setting.Control == ControlKind.Selection
            && IsIndex(setting, option.AcIndex) && IsIndex(setting, option.DcIndex),
        ChoiceValue.AcDcNumber number => setting.Numeric is { } numeric
            && number.Ac >= numeric.Min && number.Ac <= numeric.Max && number.Dc >= numeric.Min && number.Dc <= numeric.Max,
        _ => false,
    };

    private static bool IsIndex(Setting setting, int index) => index >= 0 && index < setting.States.Count;
}
