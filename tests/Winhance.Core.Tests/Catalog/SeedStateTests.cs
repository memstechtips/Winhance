using FluentAssertions;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Selections;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class SeedStateTests
{
    private static readonly string[] EnvironmentPaths = [@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"];

    private static Setting AnswerFileToggle(StateRole[] enabled, StateRole[] disabled, params Target[] seeds) => new()
    {
        Id = "t",
        Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d") },
        Targets = [new AutounattendElement("K", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/HideEULAPage"), .. seeds],
        States =
        [
            new() { Label = LocKey.Common.Enabled, Roles = enabled, Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Absent } },
            new() { Label = LocKey.Common.Disabled, Roles = disabled, Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of("true") } },
        ],
    };

    [Fact]
    public void The_seed_is_the_Recommended_state_when_the_two_roles_sit_on_different_states()
    {
        var setting = AnswerFileToggle([StateRole.WindowsDefault], [StateRole.Recommended]);

        setting.SeedState!.Label.Should().Be(LocKey.Common.Disabled);
    }

    [Fact]
    public void The_seed_is_the_WindowsDefault_state_when_nothing_is_recommended()
    {
        var setting = AnswerFileToggle([StateRole.WindowsDefault], []);

        setting.SeedState!.Label.Should().Be(LocKey.Common.Enabled);
    }

    [Fact]
    public void A_setting_with_neither_role_has_no_seed()
    {
        AnswerFileToggle([], []).SeedState.Should().BeNull();
    }

    // A build-scoped role names no state for a file written before Windows exists.
    [Fact]
    public void A_build_scoped_role_is_not_a_seed()
    {
        StateRole[] windows11Only = [new StateRole(RoleKind.Recommended) { AppliesTo = [BuildRange.Windows11] }];

        AnswerFileToggle([StateRole.WindowsDefault], windows11Only).SeedState!.Label.Should().Be(LocKey.Common.Enabled);
    }

    [Fact]
    public void A_choice_the_set_does_not_hold_resolves_to_the_seed_and_a_held_one_wins_over_it()
    {
        var setting = AnswerFileToggle([StateRole.WindowsDefault], [StateRole.Recommended]);

        SelectionSet.ChosenState(setting, null)!.Label.Should().Be(LocKey.Common.Disabled);
        SelectionSet.ChosenState(setting, new ChoiceValue.Toggle(true))!.Label.Should().Be(LocKey.Common.Enabled);
    }

    [Fact]
    public void A_read_only_registry_target_on_an_answer_file_setting_seeds_it_from_this_PC()
    {
        var plain = AnswerFileToggle([StateRole.WindowsDefault], []);
        var seeded = AnswerFileToggle(
            [StateRole.WindowsDefault],
            [],
            new RegTarget("this-pc", EnvironmentPaths, "PROCESSOR_ARCHITECTURE", RegistryValueKind.String) { ReadOnly = true });

        plain.SeedsFromThisPc.Should().BeFalse();
        seeded.SeedsFromThisPc.Should().BeTrue();
    }

    [Fact]
    public void A_live_setting_never_seeds_from_this_PC()
    {
        SettingCatalog.ById["power-hibernation-enable"].SeedsFromThisPc.Should().BeFalse();
    }
}
