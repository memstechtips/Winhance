using FluentAssertions;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Xunit;
using Winhance.TestSupport;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Core.Tests.Catalog;

public class AnswerFileOnlyShapeTests
{
    private static readonly string[] TestPaths = [@"HKEY_CURRENT_USER\Software\T"];

    private static Setting With(IReadOnlyList<Target> targets, IReadOnlyList<SettingState> states) => new()
    {
        Id = "t",
        Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d") },
        Targets = targets,
        States = states,
    };

    private static SettingState State(LocKey label) =>
        new() { Label = label, Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of("true") } };

    [Fact]
    public void An_element_target_alone_is_answer_file_only()
    {
        var setting = With(
            [new AutounattendElement("K", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/HideEULAPage")],
            [State(LocKey.Common.Enabled), State(LocKey.Common.Disabled)]);

        setting.IsAnswerFileOnly.Should().BeTrue();
    }

    [Fact]
    public void A_registry_target_makes_it_a_live_setting()
    {
        var setting = With(
            [new RegTarget("K", TestPaths, "V", RegistryValueKind.DWord)],
            [State(LocKey.Common.Enabled), State(LocKey.Common.Disabled)]);

        setting.IsAnswerFileOnly.Should().BeFalse();
    }

    [Fact]
    public void A_read_only_registry_target_beside_an_element_keeps_it_answer_file_only()
    {
        var setting = With(
            [
                new AutounattendElement("K", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/HideEULAPage"),
                new RegTarget("seed", TestPaths, "V", RegistryValueKind.String) { ReadOnly = true },
            ],
            [State(LocKey.Common.Enabled), State(LocKey.Common.Disabled)]);

        setting.IsAnswerFileOnly.Should().BeTrue();
    }

    [Fact]
    public void Read_only_registry_targets_alone_write_nothing_so_it_is_not_answer_file_only()
    {
        var setting = With(
            [new RegTarget("seed", TestPaths, "V", RegistryValueKind.String) { ReadOnly = true }],
            [State(LocKey.Common.Enabled), State(LocKey.Common.Disabled)]);

        setting.IsAnswerFileOnly.Should().BeFalse();
    }

    [Fact]
    public void A_setup_command_with_no_targets_is_answer_file_only()
    {
        var setting = With(
            [],
            [
                new SettingState { Label = LocKey.Common.Enabled },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Effects = [new AutounattendCommand("specialize", "Microsoft-Windows-Deployment", "cmd.exe /c echo")],
                },
            ]);

        setting.IsAnswerFileOnly.Should().BeTrue();
    }

    [Fact]
    public void An_action_with_no_targets_and_no_state_effects_is_not()
    {
        var setting = With([], []) with { Effects = [new ScriptEffect("s", RunContext.System)] };

        setting.IsAnswerFileOnly.Should().BeFalse();
    }

    [Fact]
    public void A_script_effect_on_a_state_keeps_it_answer_file_only()
    {
        var setting = With(
            [],
            [
                new SettingState { Label = LocKey.Common.Enabled, Effects = [new ScriptEffect("New-Item x", RunContext.System)] },
                new SettingState { Label = LocKey.Common.Disabled },
            ]);

        setting.IsAnswerFileOnly.Should().BeTrue();
    }

    // A ReadOnly HibernateEnabled target would make hibernation answer-file-only and drop it off the Optimize page.
    [Fact]
    public void Hibernation_carries_scripts_but_writes_a_target_so_it_stays_live()
    {
        SettingCatalog.ById["power-hibernation-enable"].IsAnswerFileOnly.Should().BeFalse();
    }
}
