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
    public void SeedState_RolesOnDifferentStates_IsTheRecommendedState()
    {
        var setting = AnswerFileToggle([StateRole.WindowsDefault], [StateRole.Recommended]);

        setting.SeedState!.Label.Should().Be(LocKey.Common.Disabled);
    }

    [Fact]
    public void SeedState_NothingRecommended_IsTheWindowsDefaultState()
    {
        var setting = AnswerFileToggle([StateRole.WindowsDefault], []);

        setting.SeedState!.Label.Should().Be(LocKey.Common.Enabled);
    }

    [Fact]
    public void SeedState_NeitherRole_IsNull()
    {
        AnswerFileToggle([], []).SeedState.Should().BeNull();
    }

    // A build-scoped role names no state for a file written before Windows exists.
    [Fact]
    public void SeedState_BuildScopedRole_IsNotUsed()
    {
        StateRole[] windows11Only = [new StateRole(RoleKind.Recommended) { AppliesTo = [BuildRange.Windows11] }];

        AnswerFileToggle([StateRole.WindowsDefault], windows11Only).SeedState!.Label.Should().Be(LocKey.Common.Enabled);
    }

    [Fact]
    public void ChosenState_ChoiceTheSetDoesNotHold_ResolvesToTheSeedAndAHeldOneWins()
    {
        var setting = AnswerFileToggle([StateRole.WindowsDefault], [StateRole.Recommended]);

        SelectionSet.ChosenState(setting, null)!.Label.Should().Be(LocKey.Common.Disabled);
        SelectionSet.ChosenState(setting, new ChoiceValue.Toggle(true))!.Label.Should().Be(LocKey.Common.Enabled);
    }

    [Fact]
    public void SeedsFromThisPc_ReadOnlyRegistryTarget_IsTrue()
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
    public void SeedsFromThisPc_LiveSetting_IsFalse()
    {
        SettingCatalog.ById["power-hibernation-enable"].SeedsFromThisPc.Should().BeFalse();
    }
}
