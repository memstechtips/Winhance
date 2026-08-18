using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class CatalogValidatorTests
{
    private static readonly string[] TestPaths = [@"HKEY_LOCAL_MACHINE\TEST"];

    private static RegTarget Reg(string key, string valueName) =>
        new(key, TestPaths, valueName, RegistryValueKind.DWord);

    private static SettingState St(string label, Dictionary<string, StateValue> set,
        bool fallback = false, params StateRole[] roles) =>
        new() { Label = label, Set = set, Roles = roles, IsFallback = fallback };

    private static Setting Make(IReadOnlyList<Target> targets, IReadOnlyList<SettingState> states,
        IReadOnlyList<PowerContext>? contexts = null, IStateDetector? detector = null) =>
        new()
        {
            Id = "test-setting", Display = new() { Name = "Test", Description = "Test" },
            Targets = targets, States = states, Detector = detector,
            Contexts = contexts ?? new[] { PowerContext.Always },
        };

    [Fact]
    public void Valid_setting_has_no_errors()
    {
        var s = Make(
            new[] { Reg("Mode", "SearchboxTaskbarMode") },
            new[]
            {
                St("Hide", new() { ["Mode"] = StateValue.Of(0) }, roles: new StateRole(RoleKind.Recommended)),
                St("Box",  new() { ["Mode"] = StateValue.Of(2) }, roles: new StateRole(RoleKind.WindowsDefault)),
            });
        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void Two_fallback_states_is_an_error()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St("A", new() { ["K"] = StateValue.Of(1) }, fallback: true),
                St("B", new() { ["K"] = StateValue.Of(2) }, fallback: true),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("IsFallback"));
    }

    [Fact]
    public void Two_recommended_in_same_context_is_an_error()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St("A", new() { ["K"] = StateValue.Of(1) }, roles: new StateRole(RoleKind.Recommended)),
                St("B", new() { ["K"] = StateValue.Of(2) }, roles: new StateRole(RoleKind.Recommended)),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Recommended"));
    }

    [Fact]
    public void Recommended_in_different_contexts_is_fine()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St("A", new() { ["K"] = StateValue.Of(1) }, roles: new StateRole(RoleKind.Recommended, PowerContext.AC)),
                St("B", new() { ["K"] = StateValue.Of(2) }, roles: new StateRole(RoleKind.Recommended, PowerContext.DC)),
            },
            contexts: new[] { PowerContext.AC, PowerContext.DC });
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("Recommended"));
    }

    [Fact]
    public void Empty_set_non_fallback_state_is_an_error()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[] { St("Broken", new()) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("undetectable"));
    }

    [Fact]
    public void Empty_set_fallback_state_is_allowed()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St("Known", new() { ["K"] = StateValue.Of(1) }),
                St("Default", new(), fallback: true),
            });
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("undetectable"));
    }

    [Fact]
    public void State_missing_a_target_key_is_an_error()
    {
        var s = Make(
            new[] { Reg("Start", "Start"), Reg("Preload", "IsInputAppPreloadEnabled") },
            new[] { St("Manual", new() { ["Start"] = StateValue.Of(3) }) }); // missing "Preload"
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("missing target key"));
    }

    [Fact]
    public void State_with_unknown_target_key_is_an_error()
    {
        var s = Make(
            new[] { Reg("Start", "Start") },
            new[] { St("X", new() { ["Start"] = StateValue.Of(3), ["Ghost"] = StateValue.Of(1) }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("unknown target key"));
    }

    [Fact]
    public void Duplicate_target_key_is_an_error()
    {
        var s = Make(
            new[] { Reg("K", "A"), Reg("K", "B") },
            new[] { St("X", new() { ["K"] = StateValue.Of(1) }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Duplicate target key"));
    }

    [Fact]
    public void Custom_detector_skips_target_coverage_check()
    {
        var detector = new FakeDetector();
        var s = Make(
            new[] { Reg("Start", "Start"), Reg("Preload", "P") },
            new[] { St("X", new() { ["Start"] = StateValue.Of(3) }) }, // would be "missing Preload" without detector
            detector: detector);
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("missing target key"));
    }

    [Fact]
    public void Fallback_state_may_carry_a_partial_set()
    {
        // A fallback is the last-resort catch-all, so a partial representative Set is allowed -
        // it must NOT trip the "missing target key" rule that non-fallback states do.
        var s = Make(
            new[] { Reg("Start", "Start"), Reg("Preload", "P") },
            new[]
            {
                St("Known",   new() { ["Start"] = StateValue.Of(3), ["Preload"] = StateValue.Of(1) }),
                St("Default", new() { ["Start"] = StateValue.Of(2) }, fallback: true), // only 1 of 2 keys
            });
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("missing target key"));
    }

    [Fact]
    public void Fallback_state_with_an_unknown_key_is_still_an_error()
    {
        // Exempt from "missing", but a typo'd/unknown key is always caught - fallback or not.
        var s = Make(
            new[] { Reg("Start", "Start") },
            new[]
            {
                St("Known",   new() { ["Start"] = StateValue.Of(3) }),
                St("Default", new() { ["Start"] = StateValue.Of(2), ["Ghost"] = StateValue.Of(1) }, fallback: true),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("unknown target key"));
    }

    [Fact]
    public void Action_ZeroStateWithEffect_IsValid()
    {
        var s = new Setting
        {
            Id = "act-ok",
            Display = new() { Name = "n", Description = "d", GroupName = "g" },
            Effects = new Effect[] { new ScriptEffect("echo hi", RunContext.System) },
        };
        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void Action_EffectsWithStates_IsRejected()
    {
        var s = new Setting
        {
            Id = "act-bad-states",
            Display = new() { Name = "n", Description = "d", GroupName = "g" },
            Effects = new Effect[] { new ScriptEffect("echo hi", RunContext.System) },
            States = new[] { new SettingState { Label = "Enabled", IsFallback = true } },
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("stateless Actions"));
    }

    [Fact]
    public void Action_EffectsWithTargets_IsRejected()
    {
        var s = new Setting
        {
            Id = "act-bad-targets",
            Display = new() { Name = "n", Description = "d", GroupName = "g" },
            Effects = new Effect[] { new ScriptEffect("echo hi", RunContext.System) },
            Targets = new Target[] { Reg("k", "k") },
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("stateless Actions"));
    }

    [Fact]
    public void Setting_NoStatesNoTargetsNoDetectorNoEffects_IsRejected()
    {
        var s = new Setting
        {
            Id = "does-nothing",
            Display = new() { Name = "n", Description = "d", GroupName = "g" },
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("detects nothing and does nothing"));
    }

    [Fact]
    public void NumericRangeShape_ZeroStateWithTargetNoEffects_IsValid()
    {
        var s = new Setting
        {
            Id = "range-ok",
            Display = new() { Name = "n", Description = "d", GroupName = "g" },
            Targets = new Target[] { Reg("v", "v") },
        };
        Assert.Empty(CatalogValidator.Validate(s));
    }

    // ---- IsDetectOnly: a state detection can resolve to but the user cannot choose ----------------

    private static Setting WithId(string id, IReadOnlyList<SettingState> states) =>
        new()
        {
            Id = id, Display = new() { Name = "n", Description = "d" },
            Targets = new Target[] { Reg("K", "V") }, States = states,
        };

    [Fact]
    public void DetectOnly_state_carrying_the_Recommended_role_is_an_error()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St("A", new() { ["K"] = StateValue.Of(1) }),
                new SettingState { Label = "Neutral", IsFallback = true, IsDetectOnly = true,
                    Roles = new[] { StateRole.Recommended } },
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("IsDetectOnly"));
    }

    [Fact]
    public void DetectOnly_state_carrying_a_BUILD_SCOPED_WindowsDefault_role_is_an_error()
    {
        // HasRole deliberately ignores build-scoped roles, so the rule reads Roles directly. Without
        // that, an OS-divergent default could be hung on an unchoosable state and nothing would notice.
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St("A", new() { ["K"] = StateValue.Of(1) }),
                new SettingState { Label = "Neutral", IsFallback = true, IsDetectOnly = true,
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } } },
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("IsDetectOnly"));
    }

    [Fact]
    public void DetectOnly_state_with_no_role_and_no_Set_is_valid()
    {
        // The shape the theme master's neutral state uses: fallback exempts it from the empty-Set rule.
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St("A", new() { ["K"] = StateValue.Of(1) }, roles: new StateRole(RoleKind.Recommended)),
                new SettingState { Label = "Neutral", IsFallback = true, IsDetectOnly = true },
            });
        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void Controls_naming_a_DetectOnly_state_on_the_child_is_an_error()
    {
        var child = WithId("child", new[]
        {
            St("On", new() { ["K"] = StateValue.Of(1) }),
            new SettingState { Label = "Neutral", IsFallback = true, IsDetectOnly = true },
        });
        var master = WithId("master", new[]
        {
            new SettingState
            {
                Label = "Preset",
                Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) },
                Controls = new Dictionary<string, string> { ["child"] = "Neutral" },
            },
        });

        Assert.Contains(CatalogValidator.ValidateCatalog(new[] { master, child }),
            e => e.Message.Contains("detect-only state"));
    }

    [Fact]
    public void Link_RequiredState_naming_a_DetectOnly_state_is_an_error()
    {
        var other = WithId("other", new[]
        {
            St("On", new() { ["K"] = StateValue.Of(1) }),
            new SettingState { Label = "Neutral", IsFallback = true, IsDetectOnly = true },
        });
        var owner = WithId("owner", new[]
        {
            new SettingState
            {
                Label = "On",
                Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) },
                Links = new[] { new Link("other", LinkKind.Requires, "Neutral") },
            },
        });

        Assert.Contains(CatalogValidator.ValidateCatalog(new[] { owner, other }),
            e => e.Message.Contains("detect-only state"));
    }

    [Fact]
    public void Controls_naming_a_choosable_state_on_the_child_is_valid()
    {
        // Non-vacuity for the two rules above: the same shape pointed at a real option raises nothing.
        var child = WithId("child", new[]
        {
            St("On", new() { ["K"] = StateValue.Of(1) }),
            new SettingState { Label = "Neutral", IsFallback = true, IsDetectOnly = true },
        });
        var master = WithId("master", new[]
        {
            new SettingState
            {
                Label = "Preset",
                Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) },
                Controls = new Dictionary<string, string> { ["child"] = "On" },
            },
        });

        Assert.DoesNotContain(CatalogValidator.ValidateCatalog(new[] { master, child }),
            e => e.Message.Contains("detect-only state"));
    }

    private sealed class FakeDetector : IStateDetector
    {
        public string? Detect(Setting setting, IDetectionContext context) => null;
    }
}
