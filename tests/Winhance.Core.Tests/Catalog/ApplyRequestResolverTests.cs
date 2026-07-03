using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

/// <summary>Tests the pure apply-request -> new-engine-plan bridge. It maps a plain toggle/check-box/selection/Action
/// to an ApplyPlanBuilder plan via its SettingCatalog peer, and returns null (caller falls back to the old apply) for
/// unpaired / reset / numeric / custom-detector / non-index-selection requests.</summary>
public class ApplyRequestResolverTests
{
    private static RegTarget Reg() => new("k", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Test" }, "V", RegistryValueKind.DWord);

    private static SettingState State(string label, int value, bool fallback = false) => new()
    {
        Label = label,
        Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(value) },
        IsFallback = fallback,
    };

    private static Setting ToggleSetting(string id = "t") => new()
    {
        Id = id,
        Display = new() { Name = "n", Description = "d" },
        Targets = new[] { Reg() },
        States = new[] { State("Enabled", 1), State("Disabled", 0, fallback: true) },
    };

    private static Setting SelectionSetting(string id = "t") => new()
    {
        Id = id,
        Display = new() { Name = "n", Description = "d" },
        Targets = new[] { Reg() },
        States = new[] { State("OptA", 0), State("OptB", 1) },
    };

    private static Setting NumericSetting(string id = "t") => new()
    {
        Id = id,
        Display = new() { Name = "n", Description = "d" },
        Targets = new Target[]
        {
            new PowerCfgTarget("pk", "11111111-1111-1111-1111-111111111111",
                "22222222-2222-2222-2222-222222222222", PowerModeSupport.Separate),
        },
        Numeric = new Numeric { Min = 0, Max = 100, Units = "Minutes" },
    };

    private static Setting PowerCfgSelectionSetting(string id = "t") => new()
    {
        Id = id,
        Display = new() { Name = "n", Description = "d" },
        Targets = new Target[]
        {
            new PowerCfgTarget("pk", "11111111-1111-1111-1111-111111111111",
                "22222222-2222-2222-2222-222222222222", PowerModeSupport.Separate),
        },
        States = new[]
        {
            new SettingState { Label = "OptA", Set = new Dictionary<string, StateValue> { ["pk"] = StateValue.Of(10) } },
            new SettingState { Label = "OptB", Set = new Dictionary<string, StateValue> { ["pk"] = StateValue.Of(20) } },
            new SettingState { Label = "OptC", Set = new Dictionary<string, StateValue> { ["pk"] = StateValue.Of(30) } },
        },
    };

    private sealed class FakeDetector : IStateDetector
    {
        public string? Detect(Setting setting, IDetectionContext context) => null;
    }

    // ---- Fallbacks (return null -> old apply) ----

    [Fact]
    public void Unpaired_def_returns_null()
    {
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: null,
            resetToDefault: false, new[] { ToggleSetting("other") });
        Assert.Null(plan);
    }

    [Fact]
    public void Reset_no_windows_default_toggle_falls_through_to_normal_apply()
    {
        // A toggle with NO WindowsDefault-roled state carries no reset-specific overrides (ResetSet), so its
        // reset IS a normal apply of the requested default direction. The resolver falls through to the toggle
        // resolution with reset:true threaded (a no-op here - no ResetSet). Proven reset-inert + apply-covered +
        // ResetSet-free for the whole population by NoWindowsDefaultResetInertTests.
        var setting = ToggleSetting();
        var plan = ApplyRequestResolver.Resolve("t", enable: false, value: null,
            resetToDefault: true, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.Build(setting, "Disabled", build: null, reset: true), plan);
    }

    [Fact]
    public void Bare_custom_detector_setting_returns_null()
    {
        // A custom-detector setting whose states carry NO apply effects has nothing
        // for the new engine to apply, so it falls back to the old apply. ToggleSetting()'s states are effect-less.
        var setting = ToggleSetting() with { Detector = new FakeDetector() };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: null,
            resetToDefault: false, new[] { setting });
        Assert.Null(plan);
    }

    [Fact]
    public void Custom_detector_reset_with_windows_default_routes_to_new_engine()
    {
        // A custom-detector setting WITH apply effects AND a WindowsDefault state (system-restore-style) now routes its
        // RESET through the new engine: the reset block derives the WindowsDefault state and builds it with reset:true,
        // exactly as ApplyPlanBuilder would - proven equivalent to the old executor reset by
        // CustomDetectorResetApplyEquivalenceTests. The passed enable/value are ignored on a reset; the WindowsDefault
        // direction is authoritative.
        var setting = ToggleSetting() with
        {
            Detector = new FakeDetector(),
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(1) },
                    Effects = new Effect[] { new ScriptEffect("Enable-ComputerRestore", RunContext.System) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(0) },
                    Effects = new Effect[] { new ScriptEffect("Disable-ComputerRestore", RunContext.System) },
                },
            },
        };

        var plan = ApplyRequestResolver.Resolve("t", enable: false, value: null,
            resetToDefault: true, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.Build(setting, "Enabled", build: null, reset: true), plan);
    }

    [Fact]
    public void Custom_detector_reset_with_no_windows_default_returns_null()
    {
        // A custom-detector selection WITH effects but NO WindowsDefault state, reset with a NON-index value
        // (null): it falls through the reset block to the normal selection resolution, which returns null for a
        // non-index value -> old apply. (A reset carrying a valid option index DOES route now - see
        // Reset_no_windows_default_selection_index_falls_through.)
        var setting = SelectionSetting() with
        {
            Detector = new FakeDetector(),
            States = new[]
            {
                new SettingState
                {
                    Label = "OptA",
                    Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(0) },
                    Effects = new Effect[] { new ScriptEffect("a", RunContext.User) },
                },
                new SettingState
                {
                    Label = "OptB",
                    Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(1) },
                    Effects = new Effect[] { new ScriptEffect("b", RunContext.User) },
                },
            },
        };

        var plan = ApplyRequestResolver.Resolve("t", enable: false, value: null,
            resetToDefault: true, new[] { setting });
        Assert.Null(plan);
    }

    [Fact]
    public void Dynamic_option_source_with_non_guid_value_returns_null()
    {
        // A legacy int index needs an async index->GUID lookup the pure resolver can't do -> old apply.
        var setting = SelectionSetting() with { OptionSource = new PowerPlanOptionSource() };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: 1,
            resetToDefault: false, new[] { setting });
        Assert.Null(plan);
    }

    [Fact]
    public void Dynamic_option_source_with_guid_string_builds_activate_op()
    {
        // Live UI selection (Slice 7b-ui-3a): the stored value is the scheme GUID string.
        var setting = SelectionSetting() with { OptionSource = new PowerPlanOptionSource() };
        const string guid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: guid,
            resetToDefault: false, new[] { setting });
        var activate = Assert.IsType<PowerPlanActivateOp>(Assert.Single(plan!));
        Assert.Equal(guid, activate.Guid);
    }

    [Fact]
    public void Dynamic_option_source_with_guid_name_dictionary_builds_activate_op()
    {
        // Config-import shape (ConfigurationApplicationBridgeService): { "Guid": ..., "Name": ... }.
        var setting = SelectionSetting() with { OptionSource = new PowerPlanOptionSource() };
        const string guid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        var value = new Dictionary<string, object> { ["Guid"] = guid, ["Name"] = "Balanced" };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: value,
            resetToDefault: false, new[] { setting });
        var activate = Assert.IsType<PowerPlanActivateOp>(Assert.Single(plan!));
        Assert.Equal(guid, activate.Guid);
    }

    [Fact]
    public void Numeric_non_dict_value_returns_null()
    {
        // The new engine only handles the AC/DC display-units dictionary shape; a bare int falls back.
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: 5,
            resetToDefault: false, new[] { NumericSetting() });
        Assert.Null(plan);
    }

    [Fact]
    public void Selection_index_out_of_range_returns_null()
    {
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: 7,
            resetToDefault: false, new[] { SelectionSetting() });
        Assert.Null(plan);
    }

    [Fact]
    public void Selection_non_index_value_returns_null()
    {
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: "OptB",
            resetToDefault: false, new[] { SelectionSetting() });
        Assert.Null(plan);
    }

    // ---- Handled (produce the same plan ApplyPlanBuilder would) ----

    [Fact]
    public void Toggle_enable_builds_enabled_plan()
    {
        var setting = ToggleSetting();
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: null,
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.Build(setting, "Enabled"), plan);
    }

    [Fact]
    public void Toggle_disable_builds_disabled_plan()
    {
        var setting = ToggleSetting();
        var plan = ApplyRequestResolver.Resolve("t", enable: false, value: null,
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.Build(setting, "Disabled"), plan);
    }

    [Fact]
    public void Custom_detector_setting_with_effects_resolves()
    {
        // A custom-detector setting whose states carry apply effects (system-tray / system-restore, Slice 5) is
        // applied by the new engine: the resolver routes the enable request to the matching state's plan exactly
        // as ApplyPlanBuilder.Build would, instead of falling back to the old apply.
        var setting = ToggleSetting() with
        {
            Detector = new FakeDetector(),
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(1) },
                    Effects = new Effect[] { new ScriptEffect("Enable-ComputerRestore", RunContext.System) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(0) },
                    Effects = new Effect[] { new ScriptEffect("Disable-ComputerRestore", RunContext.System) },
                },
            },
        };

        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: null,
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.Build(setting, "Enabled"), plan);
    }

    [Fact]
    public void Reset_applies_windows_default_state_with_reset_overrides()
    {
        // A toggle whose Disabled state is the WindowsDefault AND carries a ResetSet (its [1,null]-style target
        // DELETEs on reset though it DETECTs "1-or-absent"). The resolver must route reset to that default state
        // and build with reset:true, so the plan equals ApplyPlanBuilder.Build(setting, "Disabled", reset:true).
        var setting = new Setting
        {
            Id = "t",
            Display = new() { Name = "n", Description = "d" },
            Targets = new[] { Reg() },
            States = new[]
            {
                State("Enabled", 1),
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(1).OrAbsent() },
                    ResetSet = new Dictionary<string, StateValue> { ["k"] = StateValue.Absent },
                },
            },
        };

        var plan = ApplyRequestResolver.Resolve("t", enable: false, value: null,
            resetToDefault: true, new[] { setting });

        Assert.Equal(ApplyPlanBuilder.Build(setting, "Disabled", build: null, reset: true), plan);
    }

    [Fact]
    public void CheckBox_builds_like_toggle()
    {
        var setting = ToggleSetting();
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: null,
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.Build(setting, "Enabled"), plan);
    }

    [Fact]
    public void Selection_index_maps_to_option_label_plan()
    {
        var setting = SelectionSetting();
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: 1,
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.Build(setting, "OptB"), plan);
    }

    [Fact]
    public void Action_builds_action_plan()
    {
        var setting = new Setting
        {
            Id = "t",
            Display = new() { Name = "n", Description = "d" },
            Effects = new Effect[] { new ScriptEffect("echo hi", RunContext.System) },
        };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: null,
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.BuildAction(setting), plan);
    }

    [Fact]
    public void Numeric_acdc_dict_builds_powercfg_numeric_plan()
    {
        var setting = NumericSetting();
        var value = new Dictionary<string, object?> { ["ACValue"] = 10, ["DCValue"] = 5 };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: value,
            resetToDefault: false, new[] { setting });
        var expected = ApplyPlanBuilder.BuildPowerCfgNumeric(setting, new[]
        {
            new ContextValue(PowerContext.AC, 10),
            new ContextValue(PowerContext.DC, 5),
        });
        Assert.Equal(expected, plan);
    }

    [Fact]
    public void Powercfg_selection_acdc_tuple_builds_asymmetric_plan()
    {
        // Config-import shape: a (acIndex, dcIndex) ValueTuple. The resolver routes it to BuildPowerCfgSelectionAcDc,
        // which writes the AC option's value to AC and the DC option's value to DC (asymmetric).
        var setting = PowerCfgSelectionSetting();
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: (0, 2),
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.BuildPowerCfgSelectionAcDc(setting, 0, 2), plan);
    }

    [Fact]
    public void Powercfg_selection_acdc_dict_builds_asymmetric_plan()
    {
        // UI AC/DC quick-set shape: { ACValue = acIndex, DCValue = dcIndex }.
        var setting = PowerCfgSelectionSetting();
        var value = new Dictionary<string, object?> { ["ACValue"] = 1, ["DCValue"] = 0 };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: value,
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.BuildPowerCfgSelectionAcDc(setting, 1, 0), plan);
    }

    [Fact]
    public void Powercfg_selection_acdc_out_of_range_index_returns_null()
    {
        // An out-of-range AC/DC index is not representable -> falls back to the old apply.
        var setting = PowerCfgSelectionSetting();
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: (0, 9),
            resetToDefault: false, new[] { setting });
        Assert.Null(plan);
    }

    [Fact]
    public void Mixed_powercfg_and_registry_selection_acdc_returns_null()
    {
        // DEFENSIVE guard: a powercfg selection with a SIBLING RegTarget is not pure - BuildPowerCfgSelectionAcDc
        // writes only the powercfg target and would DROP the registry writes, so it stays on the old apply. (Real
        // powercfg selections never take this shape: their enablement registry is nested in PowerCfgTarget.EnablementKey,
        // out-of-band, so they DO route - see Powercfg_selection_with_enablement_key_routes.)
        var setting = new Setting
        {
            Id = "t",
            Display = new() { Name = "n", Description = "d" },
            Targets = new Target[]
            {
                new PowerCfgTarget("pk", "11111111-1111-1111-1111-111111111111",
                    "22222222-2222-2222-2222-222222222222", PowerModeSupport.Separate),
                Reg(),
            },
            States = new[]
            {
                new SettingState { Label = "OptA", Set = new Dictionary<string, StateValue> { ["pk"] = StateValue.Of(10), ["k"] = StateValue.Of(0) } },
                new SettingState { Label = "OptB", Set = new Dictionary<string, StateValue> { ["pk"] = StateValue.Of(20), ["k"] = StateValue.Of(1) } },
            },
        };
        var value = new Dictionary<string, object?> { ["ACValue"] = 0, ["DCValue"] = 1 };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: value,
            resetToDefault: false, new[] { setting });
        Assert.Null(plan);
    }

    [Fact]
    public void Powercfg_selection_with_enablement_key_routes()
    {
        // A powercfg selection whose PowerCfgTarget carries a nested EnablementKey (the real power-button-action
        // shape) is still PURE (its only target is a PowerCfgTarget) - the enablement registry is applied out-of-band
        // by the existence phase, not the AC/DC write - so its AC/DC apply routes to the new engine.
        var setting = new Setting
        {
            Id = "t",
            Display = new() { Name = "n", Description = "d" },
            Targets = new Target[]
            {
                new PowerCfgTarget("pk", "11111111-1111-1111-1111-111111111111",
                    "22222222-2222-2222-2222-222222222222", PowerModeSupport.Separate) { EnablementKey = Reg() },
            },
            States = new[]
            {
                new SettingState { Label = "OptA", Set = new Dictionary<string, StateValue> { ["pk"] = StateValue.Of(10) } },
                new SettingState { Label = "OptB", Set = new Dictionary<string, StateValue> { ["pk"] = StateValue.Of(20) } },
                new SettingState { Label = "OptC", Set = new Dictionary<string, StateValue> { ["pk"] = StateValue.Of(30) } },
            },
        };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: (0, 2),
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.BuildPowerCfgSelectionAcDc(setting, 0, 2), plan);
    }

    [Fact]
    public void Non_separate_powercfg_selection_acdc_returns_null()
    {
        // The old PowerCfgApplier's AC/DC path is gated on PowerModeSupport.Separate (a non-Separate powercfg selection
        // given an AC/DC value throws NotSupportedException there). The new routing mirrors that gate: a non-Separate
        // powercfg selection falls back to the old apply rather than writing both contexts. (No such setting exists
        // today - all catalog powercfg settings are Separate - this guards the invariant if one is ever added.)
        var setting = new Setting
        {
            Id = "t",
            Display = new() { Name = "n", Description = "d" },
            Targets = new Target[]
            {
                new PowerCfgTarget("pk", "11111111-1111-1111-1111-111111111111",
                    "22222222-2222-2222-2222-222222222222", PowerModeSupport.Both),
            },
            States = new[]
            {
                new SettingState { Label = "OptA", Set = new Dictionary<string, StateValue> { ["pk"] = StateValue.Of(10) } },
                new SettingState { Label = "OptB", Set = new Dictionary<string, StateValue> { ["pk"] = StateValue.Of(20) } },
            },
        };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: (0, 1),
            resetToDefault: false, new[] { setting });
        Assert.Null(plan);
    }

    [Fact]
    public void Non_powercfg_selection_acdc_dict_returns_null()
    {
        // A REGISTRY selection (no PowerCfgTarget) with an AC/DC dict is NOT the powercfg path - it stays on the old
        // apply: an AC/DC dict is Dictionary<string,object?>, distinct from a CustomStateValues Dictionary<string,object>.
        var value = new Dictionary<string, object?> { ["ACValue"] = 0, ["DCValue"] = 1 };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: value,
            resetToDefault: false, new[] { SelectionSetting() });
        Assert.Null(plan);
    }

    [Fact]
    public void Registry_selection_custom_state_dict_routes_to_new_engine()
    {
        // Config-import CustomStateValues (Dictionary<string,object> of raw per-ValueName values for a Custom /
        // no-option state): a PLAIN registry selection routes to BuildRegistryCustomState, which writes each
        // RegTarget whose ValueName is in the dict.
        var setting = SelectionSetting(); // RegTarget Key="k", ValueName="V"
        var customValues = new Dictionary<string, object> { ["V"] = 3 };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: customValues,
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.BuildRegistryCustomState(setting, customValues), plan);
    }

    [Fact]
    public void Registry_selection_custom_state_with_effects_returns_null()
    {
        // A registry selection whose states carry effects (a per-option script) is NOT routed: the old executor
        // ALSO runs the script, which the registry-only builder does not, so it stays on the old apply.
        var setting = SelectionSetting() with
        {
            States = new[]
            {
                new SettingState { Label = "OptA", Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(0) }, Effects = new Effect[] { new ScriptEffect("s", RunContext.User) } },
                new SettingState { Label = "OptB", Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(1) } },
            },
        };
        var customValues = new Dictionary<string, object> { ["V"] = 3 };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: customValues,
            resetToDefault: false, new[] { setting });
        Assert.Null(plan);
    }

    [Fact]
    public void Registry_selection_custom_state_per_nic_returns_null()
    {
        // A per-network-interface registry selection is outside the proven plain-registry custom-state population,
        // so it stays on the old apply.
        var setting = new Setting
        {
            Id = "t",
            Display = new() { Name = "n", Description = "d" },
            Targets = new Target[]
            {
                new RegTarget("k", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Test" }, "V", RegistryValueKind.DWord) { PerNetworkInterface = true },
            },
            States = new[] { State("OptA", 0), State("OptB", 1) },
        };
        var customValues = new Dictionary<string, object> { ["V"] = 3 };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: customValues,
            resetToDefault: false, new[] { setting });
        Assert.Null(plan);
    }

    // ---- Edge-1: retired "-win10" alias ids normalize to the canonical merged setting ----

    [Fact]
    public void Win10_alias_id_normalizes_to_canonical_merged_setting_enabled()
    {
        // Edge-1: on Windows 10 the UI applies a This PC folder setting under its retired "-win10" id. The resolver
        // alias-normalizes it to the canonical MERGED catalog Setting and builds it with the live (Win10) build gate
        // - the same path config-import already uses - instead of missing (unpaired) and falling to the old apply.
        const string aliasId = "explorer-customization-thispc-folder-desktop-win10";
        const string canonicalId = "explorer-customization-thispc-folder-desktop";
        var win10 = new WinBuild(19045);
        var canonical = SettingCatalog.All.First(s => s.Id == canonicalId);

        var plan = ApplyRequestResolver.Resolve(aliasId, enable: true, value: null,
            resetToDefault: false, SettingCatalog.All, win10);

        Assert.Equal(ApplyPlanBuilder.Build(canonical, "Enabled", win10), plan);
    }

    [Fact]
    public void Win10_alias_id_normalizes_to_canonical_merged_setting_disabled()
    {
        // The Disabled direction of the same alias id routes to the canonical setting's Disabled state on Win10.
        const string aliasId = "explorer-customization-thispc-folder-desktop-win10";
        const string canonicalId = "explorer-customization-thispc-folder-desktop";
        var win10 = new WinBuild(19045);
        var canonical = SettingCatalog.All.First(s => s.Id == canonicalId);

        var plan = ApplyRequestResolver.Resolve(aliasId, enable: false, value: null,
            resetToDefault: false, SettingCatalog.All, win10);

        Assert.Equal(ApplyPlanBuilder.Build(canonical, "Disabled", win10), plan);
    }

    // ---- Edge-2: no-WindowsDefault reset falls through to the normal apply resolution ----

    [Fact]
    public void Reset_no_windows_default_selection_index_falls_through()
    {
        // A no-WindowsDefault selection reset carrying a plain option index (the DNS / system-tray bulk-reset
        // shape) applies that index - reset:true threaded (a no-op without a ResetSet).
        var setting = SelectionSetting(); // OptA(0), OptB(1); no WindowsDefault state
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: 1,
            resetToDefault: true, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.Build(setting, "OptB", build: null, reset: true), plan);
    }

    [Fact]
    public void Reset_no_windows_default_powercfg_selection_acdc_dict_falls_through()
    {
        // A no-WindowsDefault powercfg Separate selection reset (the {ACValue,DCValue} index dict the bulk reset
        // dispatches) falls through to the asymmetric AC/DC builder - powercfg carries no ResetSet, so no reset
        // threading is needed there.
        var setting = PowerCfgSelectionSetting();
        var value = new Dictionary<string, object?> { ["ACValue"] = 2, ["DCValue"] = 0 };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: value,
            resetToDefault: true, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.BuildPowerCfgSelectionAcDc(setting, 2, 0), plan);
    }

    [Fact]
    public void Reset_stateless_action_returns_null()
    {
        // A stateless Action has no default STATE to apply on reset (BuildAction runs its one-shot effects, which
        // is NOT a reset), so its reset is not representable and stays on the old apply.
        var setting = new Setting
        {
            Id = "t",
            Display = new() { Name = "n", Description = "d" },
            Effects = new Effect[] { new ScriptEffect("echo hi", RunContext.System) },
        };
        var plan = ApplyRequestResolver.Resolve("t", enable: true, value: null,
            resetToDefault: true, new[] { setting });
        Assert.Null(plan);
    }

    [Fact]
    public void Reset_this_pc_toggle_win11_routes_via_fall_through()
    {
        // Real catalog: a merged This PC toggle has no WindowsDefault state. On Windows 11 the bulk reset
        // dispatches the default direction (Disabled); the resolver falls through to Build the Disabled state with
        // the live (Win11) build gate - the same plan a normal apply produces.
        const string id = "explorer-customization-thispc-folder-desktop";
        var win11 = new WinBuild(22631);
        var canonical = SettingCatalog.All.First(s => s.Id == id);
        var plan = ApplyRequestResolver.Resolve(id, enable: false, value: null,
            resetToDefault: true, SettingCatalog.All, win11);
        Assert.Equal(ApplyPlanBuilder.Build(canonical, "Disabled", win11, reset: true), plan);
    }

    [Fact]
    public void Reset_this_pc_toggle_win10_alias_routes_via_fall_through()
    {
        // On Windows 10 the UI applies the merged This PC toggle under its retired -win10 id; the bulk reset
        // dispatches the default direction (Enabled for the key-existence Win10 variant). Alias-normalize (Edge-1) +
        // the reset fall-through (Edge-2) route it to Build the Enabled state with the Win10 build gate.
        const string aliasId = "explorer-customization-thispc-folder-desktop-win10";
        const string canonicalId = "explorer-customization-thispc-folder-desktop";
        var win10 = new WinBuild(19045);
        var canonical = SettingCatalog.All.First(s => s.Id == canonicalId);
        var plan = ApplyRequestResolver.Resolve(aliasId, enable: true, value: null,
            resetToDefault: true, SettingCatalog.All, win10);
        Assert.Equal(ApplyPlanBuilder.Build(canonical, "Enabled", win10, reset: true), plan);
    }
}
