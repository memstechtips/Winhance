using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class CatalogValidatorTests
{
    private static RegTarget Reg(string key, string valueName) =>
        new(key, new[] { @"HKEY_LOCAL_MACHINE\TEST" }, valueName, RegistryValueKind.DWord);

    private static SettingState St(string label, Dictionary<string, StateValue> set,
        bool fallback = false, params StateRole[] roles) =>
        new() { Label = label, Set = set, Roles = roles, IsFallback = fallback };

    private static Setting Make(IReadOnlyList<Target> targets, IReadOnlyList<SettingState> states,
        IReadOnlyList<PowerContext>? contexts = null, IStateDetector? detector = null) =>
        new()
        {
            Id = "test-setting", Name = "Test", Description = "Test",
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
        // A fallback is the last-resort catch-all, so a partial representative Set is allowed —
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
        // Exempt from "missing", but a typo'd/unknown key is always caught — fallback or not.
        var s = Make(
            new[] { Reg("Start", "Start") },
            new[]
            {
                St("Known",   new() { ["Start"] = StateValue.Of(3) }),
                St("Default", new() { ["Start"] = StateValue.Of(2), ["Ghost"] = StateValue.Of(1) }, fallback: true),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("unknown target key"));
    }

    private sealed class FakeDetector : IStateDetector
    {
        public string? Detect(IStateReadings readings) => null;
    }
}
