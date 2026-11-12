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
    public void IsAnswerFileOnly_ElementTargetAlone_ReturnsTrue()
    {
        var setting = With(
            [new AutounattendElement("K", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/HideEULAPage")],
            [State(LocKey.Common.Enabled), State(LocKey.Common.Disabled)]);

        setting.IsAnswerFileOnly.Should().BeTrue();
    }

    [Fact]
    public void IsAnswerFileOnly_RegistryTarget_ReturnsFalse()
    {
        var setting = With(
            [new RegTarget("K", TestPaths, "V", RegistryValueKind.DWord)],
            [State(LocKey.Common.Enabled), State(LocKey.Common.Disabled)]);

        setting.IsAnswerFileOnly.Should().BeFalse();
    }

    [Fact]
    public void IsAnswerFileOnly_ReadOnlyRegistryTargetBesideElement_ReturnsTrue()
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
    public void IsAnswerFileOnly_ReadOnlyRegistryTargetsAlone_ReturnsFalse()
    {
        var setting = With(
            [new RegTarget("seed", TestPaths, "V", RegistryValueKind.String) { ReadOnly = true }],
            [State(LocKey.Common.Enabled), State(LocKey.Common.Disabled)]);

        setting.IsAnswerFileOnly.Should().BeFalse();
    }

    [Fact]
    public void IsAnswerFileOnly_SetupCommandWithNoTargets_ReturnsTrue()
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
    public void IsAnswerFileOnly_ActionWithNoTargetsOrStateEffects_ReturnsFalse()
    {
        var setting = With([], []) with { Effects = [new ScriptEffect("s", RunContext.System)] };

        setting.IsAnswerFileOnly.Should().BeFalse();
    }

    [Fact]
    public void IsAnswerFileOnly_ScriptEffectOnAState_ReturnsTrue()
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
    public void IsAnswerFileOnly_HibernationWithScriptsAndATarget_ReturnsFalse()
    {
        SettingCatalog.ById["power-hibernation-enable"].IsAnswerFileOnly.Should().BeFalse();
    }
}
