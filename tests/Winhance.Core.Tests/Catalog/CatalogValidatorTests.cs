using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.TestSupport;
using Xunit;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Core.Tests.Catalog;

public class CatalogValidatorTests
{
    private static readonly string[] TestPaths = [@"HKEY_LOCAL_MACHINE\TEST"];

    private static RegTarget Reg(string key, string valueName) =>
        new(key, TestPaths, valueName, RegistryValueKind.DWord);

    private static AutounattendElement Element(string key) =>
        new(key, "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/" + key);

    private static SettingState St(LocKey label, Dictionary<string, StateValue> set,
        bool fallback = false, params StateRole[] roles) =>
        new() { Label = label, Set = set, Roles = roles, IsFallback = fallback };

    private static Setting Make(IReadOnlyList<Target> targets, IReadOnlyList<SettingState> states,
        IReadOnlyList<PowerContext>? contexts = null, IStateDetector? detector = null) =>
        new()
        {
            Id = "test-setting", Display = new() { Name = TestKeys.Of("Test"), Description = TestKeys.Of("Test") },
            Targets = targets, States = states, Detector = detector,
            Contexts = contexts ?? new[] { PowerContext.Always },
        };

    private static SettingState Named(string label, string key) =>
        St(TestKeys.Of(label), new() { [FakeOptionProvider.TargetKey] = StateValue.Of(key) });

    [Fact]
    public void Every_setting_in_the_catalog_passes_its_own_rules()
    {
        var errors = SettingCatalog.All
            .SelectMany(CatalogValidator.Validate)
            .Select(e => $"{e.SettingId}: {e.Message}")
            .ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public void A_keyed_selection_with_one_target_and_no_states_is_valid()
    {
        Assert.Empty(CatalogValidator.Validate(FakeOptionProvider.SettingFor()));
    }

    [Fact]
    public void A_keyed_selection_may_name_its_options_as_states_by_key_alone()
    {
        var s = FakeOptionProvider.SettingFor() with
        {
            Targets = [.. FakeOptionProvider.SettingFor().Targets, Reg("Type", "BackgroundType") with { ReadOnly = true }],
            States = [Named("A", "alpha"), Named("B", "beta")],
        };

        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void A_keyed_state_that_gives_its_key_target_no_string_is_an_error()
    {
        var s = FakeOptionProvider.SettingFor() with
        {
            States = [St(TestKeys.Of("A"), new() { [FakeOptionProvider.TargetKey] = StateValue.Of(1) })],
        };

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("names no key"));
    }

    [Fact]
    public void A_keyed_selection_with_no_key_target_cannot_name_a_state()
    {
        var s = FakeOptionProvider.SettingFor() with
        {
            Targets = [new RegTarget(FakeOptionProvider.TargetKey, TestPaths, "V", RegistryValueKind.String) { From = OptionValue.Color }],
            States = [Named("A", "alpha")],
        };

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("names no key"));
    }

    [Fact]
    public void Two_keyed_states_with_the_same_key_in_any_case_are_an_error()
    {
        var s = FakeOptionProvider.SettingFor() with { States = [Named("A", "alpha"), Named("B", "ALPHA")] };

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("repeats the key 'ALPHA'"));
    }

    [Fact]
    public void A_keyed_state_may_not_carry_a_role_an_effect_a_control_or_a_link()
    {
        var named = Named("A", "alpha");
        var states = new[]
        {
            named with { Roles = [StateRole.Recommended] },
            named with { Effects = [new ScriptEffect("x", RunContext.User)] },
            named with { Controls = new Dictionary<string, LocKey> { ["other"] = TestKeys.Of("On") } },
            named with { Links = [new Link("other", LinkKind.Requires, TestKeys.Of("On"))] },
        };

        foreach (var state in states)
        {
            var s = FakeOptionProvider.SettingFor() with { States = [state] };
            Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("may not carry roles, effects, controls or links"));
        }
    }

    [Fact]
    public void A_power_plan_list_with_no_target_is_valid()
    {
        var s = FakeOptionProvider.SettingFor() with
        {
            Options = new(OptionSource.PowerPlans),
            Targets = System.Array.Empty<Target>(),
        };

        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void Any_other_keyed_selection_must_write_a_registry_target()
    {
        var none = FakeOptionProvider.SettingFor() with { Targets = System.Array.Empty<Target>() };
        var readOnly = FakeOptionProvider.SettingFor() with { Targets = [Reg("Type", "BackgroundType") with { ReadOnly = true }] };

        Assert.Contains(CatalogValidator.Validate(none), e => e.Message.Contains("at least one registry target that is not ReadOnly"));
        Assert.Contains(CatalogValidator.Validate(readOnly), e => e.Message.Contains("at least one registry target that is not ReadOnly"));
    }

    [Fact]
    public void A_keyed_selection_with_a_numeric_range_is_an_error()
    {
        var s = FakeOptionProvider.SettingFor() with { Numeric = new() { Min = 0, Max = 100 } };

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Numeric range"));
    }

    [Fact]
    public void A_target_that_takes_a_computed_value_belongs_to_a_keyed_selection()
    {
        var keyed = FakeOptionProvider.SettingFor() with
        {
            Targets = [.. FakeOptionProvider.SettingFor().Targets, Reg("Locale", "Locale") with { From = OptionValue.LocaleId }],
        };
        var live = Make(
            new[] { Reg("Mode", "Mode") with { From = OptionValue.LocaleId } },
            new[]
            {
                St(TestKeys.Of("Hide"), new() { ["Mode"] = StateValue.Of(0) }),
                St(TestKeys.Of("Box"), new() { ["Mode"] = StateValue.Of(2) }),
            });

        Assert.Empty(CatalogValidator.Validate(keyed));
        Assert.Contains(CatalogValidator.Validate(live), e => e.Message.Contains("takes From LocaleId"));
    }

    [Theory]
    [InlineData(OptionSource.TimeZones)]
    [InlineData(OptionSource.KeyboardLayouts)]
    public void A_list_read_from_registry_subkeys_must_name_their_path_and_caption(OptionSource source)
    {
        const string message = "must name their Path and LabelValue";
        var named = FakeOptionProvider.SettingFor() with { Options = new(source) { Path = @"HKEY_LOCAL_MACHINE\TEST", LabelValue = "Display" } };

        Assert.DoesNotContain(CatalogValidator.Validate(named), e => e.Message.Contains(message));
        Assert.Contains(CatalogValidator.Validate(named with { Options = named.Options! with { Path = null } }), e => e.Message.Contains(message));
        Assert.Contains(CatalogValidator.Validate(named with { Options = named.Options! with { LabelValue = null } }), e => e.Message.Contains(message));
    }

    [Fact]
    public void A_list_not_read_from_registry_subkeys_may_not_name_a_path_or_caption()
    {
        var withPath = FakeOptionProvider.SettingFor() with { Options = new(OptionSource.RegionalFormats) { Path = @"HKEY_LOCAL_MACHINE\TEST" } };
        var withCaption = FakeOptionProvider.SettingFor() with { Options = new(OptionSource.RegionalFormats) { LabelValue = "Display" } };

        Assert.Contains(CatalogValidator.Validate(withPath), e => e.Message.Contains("Path and LabelValue would be silently ignored"));
        Assert.Contains(CatalogValidator.Validate(withCaption), e => e.Message.Contains("Path and LabelValue would be silently ignored"));
    }

    [Fact]
    public void Only_a_colour_list_declares_fixed_keys()
    {
        var colors = FakeOptionProvider.SettingFor() with { Options = new(OptionSource.Colors) { Keys = ["#000000"] } };
        var formats = FakeOptionProvider.SettingFor() with { Options = new(OptionSource.RegionalFormats) { Keys = ["en-US"] } };

        Assert.DoesNotContain(CatalogValidator.Validate(colors), e => e.Message.Contains("fixed Keys"));
        Assert.Contains(CatalogValidator.Validate(formats), e => e.Message.Contains("Only a Colors list declares fixed Keys"));
    }

    [Fact]
    public void Valid_setting_has_no_errors()
    {
        var s = Make(
            new[] { Reg("Mode", "SearchboxTaskbarMode") },
            new[]
            {
                St(TestKeys.Of("Hide"), new() { ["Mode"] = StateValue.Of(0) }, roles: new StateRole(RoleKind.Recommended)),
                St(TestKeys.Of("Box"),  new() { ["Mode"] = StateValue.Of(2) }, roles: new StateRole(RoleKind.WindowsDefault)),
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
                St(TestKeys.Of("A"), new() { ["K"] = StateValue.Of(1) }, fallback: true),
                St(TestKeys.Of("B"), new() { ["K"] = StateValue.Of(2) }, fallback: true),
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
                St(TestKeys.Of("A"), new() { ["K"] = StateValue.Of(1) }, roles: new StateRole(RoleKind.Recommended)),
                St(TestKeys.Of("B"), new() { ["K"] = StateValue.Of(2) }, roles: new StateRole(RoleKind.Recommended)),
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
                St(TestKeys.Of("A"), new() { ["K"] = StateValue.Of(1) }, roles: new StateRole(RoleKind.Recommended, PowerContext.AC)),
                St(TestKeys.Of("B"), new() { ["K"] = StateValue.Of(2) }, roles: new StateRole(RoleKind.Recommended, PowerContext.DC)),
            },
            contexts: new[] { PowerContext.AC, PowerContext.DC });
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("Recommended"));
    }

    [Fact]
    public void Empty_set_non_fallback_state_is_an_error()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[] { St(TestKeys.Of("Broken"), new()) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("undetectable"));
    }

    [Fact]
    public void Empty_set_fallback_state_is_allowed()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St(TestKeys.Of("Known"), new() { ["K"] = StateValue.Of(1) }),
                St(TestKeys.Of("Default"), new(), fallback: true),
            });
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("undetectable"));
    }

    [Fact]
    public void Checked_or_Unchecked_outside_the_exact_pair_is_an_error()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St(LocKey.Common.Checked, new() { ["K"] = StateValue.Of(1) }),
                St(TestKeys.Of("Off"), new() { ["K"] = StateValue.Of(0) }),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Checked/Unchecked"));
    }

    [Fact]
    public void State_missing_a_target_key_is_an_error()
    {
        var s = Make(
            new[] { Reg("Start", "Start"), Reg("Preload", "IsInputAppPreloadEnabled") },
            new[] { St(TestKeys.Of("Manual"), new() { ["Start"] = StateValue.Of(3) }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("missing target key"));
    }

    [Fact]
    public void State_with_unknown_target_key_is_an_error()
    {
        var s = Make(
            new[] { Reg("Start", "Start") },
            new[] { St(TestKeys.Of("X"), new() { ["Start"] = StateValue.Of(3), ["Ghost"] = StateValue.Of(1) }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("unknown target key"));
    }

    [Fact]
    public void Duplicate_target_key_is_an_error()
    {
        var s = Make(
            new[] { Reg("K", "A"), Reg("K", "B") },
            new[] { St(TestKeys.Of("X"), new() { ["K"] = StateValue.Of(1) }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Duplicate target key"));
    }

    [Fact]
    public void Custom_detector_skips_target_coverage_check()
    {
        var detector = new FakeDetector();
        var s = Make(
            new[] { Reg("Start", "Start"), Reg("Preload", "P") },
            new[] { St(TestKeys.Of("X"), new() { ["Start"] = StateValue.Of(3) }) }, // would be "missing Preload" without detector
            detector: detector);
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("missing target key"));
    }

    [Fact]
    public void State_with_no_set_and_no_detector_is_an_error()
    {
        var s = Make(
            new[] { Reg("Start", "Start") },
            new[] { St(TestKeys.Of("X"), new()) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("empty Set"));
    }

    [Fact]
    public void Custom_detector_lets_a_state_carry_no_set()
    {
        var s = Make(
            new[] { Reg("Start", "Start") },
            new[] { St(TestKeys.Of("X"), new()) },
            detector: new FakeDetector());
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("empty Set"));
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
                St(TestKeys.Of("Known"),   new() { ["Start"] = StateValue.Of(3), ["Preload"] = StateValue.Of(1) }),
                St(TestKeys.Of("Default"), new() { ["Start"] = StateValue.Of(2) }, fallback: true),
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
                St(TestKeys.Of("Known"),   new() { ["Start"] = StateValue.Of(3) }),
                St(TestKeys.Of("Default"), new() { ["Start"] = StateValue.Of(2), ["Ghost"] = StateValue.Of(1) }, fallback: true),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("unknown target key"));
    }

    [Fact]
    public void Action_ZeroStateWithEffect_IsValid()
    {
        var s = new Setting
        {
            Id = "act-ok",
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d"), GroupName = TestKeys.Of("g") },
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
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d"), GroupName = TestKeys.Of("g") },
            Effects = new Effect[] { new ScriptEffect("echo hi", RunContext.System) },
            States = new[] { new SettingState { Label = LocKey.Common.Enabled, IsFallback = true } },
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("stateless Actions"));
    }

    [Fact]
    public void Action_EffectsWithTargets_IsRejected()
    {
        var s = new Setting
        {
            Id = "act-bad-targets",
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d"), GroupName = TestKeys.Of("g") },
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
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d"), GroupName = TestKeys.Of("g") },
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("detects nothing and does nothing"));
    }

    [Fact]
    public void NumericRangeShape_ZeroStateWithTargetNoEffects_IsValid()
    {
        var s = new Setting
        {
            Id = "range-ok",
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d"), GroupName = TestKeys.Of("g") },
            Targets = new Target[] { Reg("v", "v") },
        };
        Assert.Empty(CatalogValidator.Validate(s));
    }

    private static Setting WithId(string id, IReadOnlyList<SettingState> states) =>
        new()
        {
            Id = id, Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d") },
            Targets = new Target[] { Reg("K", "V") }, States = states,
        };

    [Fact]
    public void DetectOnly_state_carrying_the_Recommended_role_is_an_error()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St(TestKeys.Of("A"), new() { ["K"] = StateValue.Of(1) }),
                new SettingState { Label = TestKeys.Of("Neutral"), IsFallback = true, IsDetectOnly = true,
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
                St(TestKeys.Of("A"), new() { ["K"] = StateValue.Of(1) }),
                new SettingState { Label = TestKeys.Of("Neutral"), IsFallback = true, IsDetectOnly = true,
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
                St(TestKeys.Of("A"), new() { ["K"] = StateValue.Of(1) }, roles: new StateRole(RoleKind.Recommended)),
                new SettingState { Label = TestKeys.Of("Neutral"), IsFallback = true, IsDetectOnly = true },
            });
        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void Controls_naming_a_DetectOnly_state_on_the_child_is_an_error()
    {
        var child = WithId("child", new[]
        {
            St(TestKeys.Of("On"), new() { ["K"] = StateValue.Of(1) }),
            new SettingState { Label = TestKeys.Of("Neutral"), IsFallback = true, IsDetectOnly = true },
        });
        var master = WithId("master", new[]
        {
            new SettingState
            {
                Label = TestKeys.Of("Preset"),
                Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) },
                Controls = new Dictionary<string, LocKey> { ["child"] = TestKeys.Of("Neutral") },
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
            St(TestKeys.Of("On"), new() { ["K"] = StateValue.Of(1) }),
            new SettingState { Label = TestKeys.Of("Neutral"), IsFallback = true, IsDetectOnly = true },
        });
        var owner = WithId("owner", new[]
        {
            new SettingState
            {
                Label = TestKeys.Of("On"),
                Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) },
                Links = new[] { new Link("other", LinkKind.Requires, TestKeys.Of("Neutral")) },
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
            St(TestKeys.Of("On"), new() { ["K"] = StateValue.Of(1) }),
            new SettingState { Label = TestKeys.Of("Neutral"), IsFallback = true, IsDetectOnly = true },
        });
        var master = WithId("master", new[]
        {
            new SettingState
            {
                Label = TestKeys.Of("Preset"),
                Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) },
                Controls = new Dictionary<string, LocKey> { ["child"] = TestKeys.Of("On") },
            },
        });

        Assert.DoesNotContain(CatalogValidator.ValidateCatalog(new[] { master, child }),
            e => e.Message.Contains("detect-only state"));
    }

    [Fact]
    public void An_answer_file_only_state_may_write_nothing()
    {
        var s = Make(
            new[] { Element("K") },
            new[]
            {
                St(LocKey.Common.Enabled, new(), roles: new StateRole(RoleKind.WindowsDefault)),
                St(LocKey.Common.Disabled, new() { ["K"] = StateValue.Of("true") }),
            });
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("undetectable"));
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("missing target key"));
    }

    [Fact]
    public void An_answer_file_only_setting_needs_exactly_one_windows_default_state()
    {
        var s = Make(
            new[] { Element("K") },
            new[] { St(LocKey.Common.Enabled, new()), St(LocKey.Common.Disabled, new() { ["K"] = StateValue.Of("true") }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("WindowsDefault state to seed from"));
    }

    [Fact]
    public void A_live_setting_may_not_carry_an_answer_file_target()
    {
        var s = Make(
            new Target[] { Reg("R", "V"), Element("K") },
            new[]
            {
                St(LocKey.Common.Enabled, new() { ["R"] = StateValue.Of(1), ["K"] = StateValue.Of("true") }, roles: new StateRole(RoleKind.WindowsDefault)),
                St(LocKey.Common.Disabled, new() { ["R"] = StateValue.Of(0), ["K"] = StateValue.Absent }),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("mixes"));
    }

    [Fact]
    public void A_live_setting_may_not_fire_a_setup_command()
    {
        var s = Make(
            new[] { Reg("R", "V") },
            new[]
            {
                St(LocKey.Common.Enabled, new() { ["R"] = StateValue.Of(1) }, roles: new StateRole(RoleKind.WindowsDefault)),
                St(LocKey.Common.Disabled, new() { ["R"] = StateValue.Of(0) }) with
                {
                    Effects = new[] { new AutounattendCommand("specialize", "Microsoft-Windows-Deployment", "cmd.exe /c echo") },
                },
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("mixes"));
    }

    [Fact]
    public void A_setting_level_machine_effect_keeps_an_element_only_setting_live()
    {
        var s = Make(new[] { Element("K") }, Array.Empty<SettingState>()) with
        {
            Effects = new[] { new RegistryWriteEffect("HKCU\\S", "V", RegistryValueKind.DWord, 1) },
        };
        Assert.False(s.IsAnswerFileOnly);
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("mixes"));
    }

    [Fact]
    public void A_setting_level_setup_command_on_a_live_target_is_mixed()
    {
        var s = Make(new[] { Reg("R", "V") }, Array.Empty<SettingState>()) with
        {
            Effects = new[] { new AutounattendCommand("specialize", "Microsoft-Windows-Deployment", "cmd.exe /c echo") },
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("mixes"));
    }

    [Fact]
    public void An_answer_file_only_state_may_omit_an_element_it_does_not_write()
    {
        var s = Make(
            new[] { Element("K1"), Element("K2") },
            new[]
            {
                St(LocKey.Common.Enabled, new() { ["K1"] = StateValue.Of("true") }, roles: new StateRole(RoleKind.WindowsDefault)),
                St(LocKey.Common.Disabled, new() { ["K1"] = StateValue.Absent, ["K2"] = StateValue.Of("true") }),
            });
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("missing target key"));
    }

    [Fact]
    public void Exists_is_not_a_value_an_answer_file_element_can_take()
    {
        var s = Make(
            new[] { Element("K") },
            new[]
            {
                St(LocKey.Common.Enabled, new() { ["K"] = StateValue.Exists }, roles: new StateRole(RoleKind.WindowsDefault)),
                St(LocKey.Common.Disabled, new() { ["K"] = StateValue.Absent }),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Exists"));
    }

    private static Setting Typed(IReadOnlyList<Target> targets, IReadOnlyList<SettingState> states) =>
        Make(targets, states) with { TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything."))) };

    [Fact]
    public void A_text_setting_may_not_declare_states()
    {
        var s = Typed(new[] { Element("K") }, new[] { St(LocKey.Common.Enabled, new() { ["K"] = StateValue.Of("true") }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("may not declare states"));
    }

    [Fact]
    public void A_text_setting_may_not_declare_a_numeric_range()
    {
        var s = Typed(new[] { Element("K") }, Array.Empty<SettingState>()) with { Numeric = new Numeric { Min = 0, Max = 10 } };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Numeric range"));
    }

    [Fact]
    public void A_text_setting_may_not_declare_an_option_list()
    {
        var s = FakeOptionProvider.SettingFor() with { TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything."))) };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("option list"));
    }

    [Fact]
    public void A_text_setting_needs_somewhere_to_put_what_was_typed()
    {
        var s = Typed(Array.Empty<Target>(), Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("at least one target to write to"));
    }

    [Fact]
    public void A_text_setting_may_not_write_to_the_registry()
    {
        var s = Typed(new[] { Reg("R", "V") }, Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("answer-file target"));
    }

    [Fact]
    public void A_text_setting_may_read_this_PC_through_the_read_only_target_its_seed_names()
    {
        var s = Typed(new Target[] { Element("K"), Reg("this-pc", "Hostname") with { ReadOnly = true } }, Array.Empty<SettingState>())
            with { TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything.")), SeedKey: "this-pc") };

        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("answer-file target") || e.Message.Contains("SeedKey"));
    }

    [Fact]
    public void A_text_seed_must_name_a_read_only_registry_target_on_the_setting()
    {
        var s = Typed(new[] { Element("K") }, Array.Empty<SettingState>())
            with { TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything.")), SeedKey: "nope") };

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("SeedKey names 'nope'"));
    }

    [Fact]
    public void A_text_setting_may_write_the_desktop_slideshow_instead_of_the_answer_file()
    {
        var s = Typed(
            new Target[]
            {
                new DesktopSlideshowTarget("album"),
                Reg("BackgroundType", "BackgroundType") with { ReadOnly = true },
                Reg("SlideshowDirectoryPath1", "SlideshowDirectoryPath1") with { ReadOnly = true },
            },
            Array.Empty<SettingState>()) with
        {
            TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything.")), SeedKey: "album"),
            CustomStateScripts = [new ScriptEffect("Set-Slideshow '{{value}}'", RunContext.User)],
        };

        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void A_slideshow_box_without_its_script_is_an_error()
    {
        var s = Typed(new Target[] { new DesktopSlideshowTarget("album") }, Array.Empty<SettingState>());

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("CustomStateScripts"));
    }

    [Fact]
    public void A_slideshow_box_without_the_values_its_album_is_read_from_is_an_error()
    {
        var s = Typed(new Target[] { new DesktopSlideshowTarget("album") }, Array.Empty<SettingState>());

        var errors = CatalogValidator.Validate(s);

        Assert.Contains(errors, e => e.Message.Contains("keyed 'BackgroundType'"));
        Assert.Contains(errors, e => e.Message.Contains("keyed 'SlideshowDirectoryPath1'"));
    }

    [Fact]
    public void A_read_only_registry_target_belongs_to_a_setting_that_seeds_itself()
    {
        var live = Make(new[] { Reg("R", "V") with { ReadOnly = true } }, new[] { St(LocKey.Common.Enabled, new() { ["R"] = StateValue.Of(1) }) });
        Assert.Contains(CatalogValidator.Validate(live), e => e.Message.Contains("is ReadOnly, which seeds"));
    }

    [Fact]
    public void A_read_only_registry_target_may_sit_on_a_keyed_selection()
    {
        var s = FakeOptionProvider.SettingFor() with
        {
            Targets = [.. FakeOptionProvider.SettingFor().Targets, Reg("Type", "BackgroundType") with { ReadOnly = true }],
        };

        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("is ReadOnly"));
    }

    [Fact]
    public void A_keyed_selection_may_declare_an_answer_file_element_beside_its_live_targets()
    {
        var s = FakeOptionProvider.SettingFor() with
        {
            Targets = [.. FakeOptionProvider.SettingFor().Targets, Element("zone-element")],
        };

        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("mixes an answer-file mechanism"));
    }

    [Fact]
    public void A_registry_target_cleared_on_apply_must_be_read_only()
    {
        var s = Listed(new Target[] { Element("K"), Reg("R", "V") with { ClearedOnApply = true } }, Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("ClearedOnApply but not ReadOnly"));
    }

    [Fact]
    public void A_registry_target_cannot_be_read_only_and_apply_only_at_once()
    {
        var s = Listed(new Target[] { Element("K"), Reg("R", "V") with { ReadOnly = true, ApplyOnly = true } }, Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("both ReadOnly and ApplyOnly"));
    }

    [Fact]
    public void A_list_setting_may_carry_read_only_registry_targets_beside_its_element()
    {
        var s = Listed(new Target[] { Element("K"), Reg("name", "USERNAME") with { ReadOnly = true } }, Array.Empty<SettingState>());
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("exactly one answer-file element"));
    }

    [Fact]
    public void A_list_seed_names_read_only_registry_targets_on_the_setting()
    {
        var s = Listed(new Target[] { Element("K"), Reg("name", "USERNAME") with { ReadOnly = true } }, Array.Empty<SettingState>())
            with
            {
                List = new(
                    [new Field("name", FieldKind.Text, TestKeys.Of("Name"))],
                    Seed: new Dictionary<string, string> { ["name"] = "name", ["display-name"] = "display-name" }),
            };
        var errors = CatalogValidator.Validate(s);
        Assert.Contains(errors, e => e.Message.Contains("List.Seed names 'display-name'"));
        Assert.DoesNotContain(errors, e => e.Message.Contains("List.Seed names 'name'"));
    }

    [Fact]
    public void A_list_setting_needs_a_field_to_type_into()
    {
        var s = Make(new[] { Element("K") }, Array.Empty<SettingState>()) with { List = new([]) };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("at least one field"));
    }

    [Fact]
    public void Two_fields_may_not_share_a_key()
    {
        var s = Make(new[] { Element("K") }, Array.Empty<SettingState>()) with
        {
            List = new([new Field("name", FieldKind.Text, TestKeys.Of("Name")), new Field("name", FieldKind.Password, TestKeys.Of("Again"))]),
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Duplicate field key 'name'"));
    }

    private static Setting Listed(IReadOnlyList<Target> targets, IReadOnlyList<SettingState> states) =>
        Make(targets, states) with { List = new([new Field("name", FieldKind.Text, TestKeys.Of("Name"))]) };

    [Fact]
    public void A_list_setting_may_not_declare_states()
    {
        var s = Listed(new[] { Element("K") }, new[] { St(LocKey.Common.Enabled, new() { ["K"] = StateValue.Of("true") }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("may not declare states"));
    }

    [Fact]
    public void A_list_setting_needs_the_element_it_writes_into()
    {
        var s = Listed(Array.Empty<Target>(), Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("exactly one answer-file element"));
    }

    [Fact]
    public void A_list_setting_may_not_write_to_the_registry()
    {
        var s = Listed(new[] { Reg("R", "V") }, Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("exactly one answer-file element"));
    }

    [Fact]
    public void A_list_setting_writes_one_element_not_two()
    {
        var s = Listed(new[] { Element("K1"), Element("K2") }, Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("exactly one answer-file element"));
    }

    [Fact]
    public void A_list_setting_may_not_carry_a_text_rule_as_well()
    {
        var s = Listed(new[] { Element("K") }, Array.Empty<SettingState>()) with
        {
            TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything."))),
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("silently ignored"));
    }

    [Fact]
    public void A_list_setting_may_not_carry_an_option_list_as_well()
    {
        var s = FakeOptionProvider.SettingFor() with { List = new([new Field("name", FieldKind.Text, TestKeys.Of("Name"))]) };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("option list"));
    }

    private sealed class FakeDetector : IStateDetector
    {
        public string? Detect(Setting setting, IDetectionContext context) => null;
    }
}
