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

    private static SettingDefinition Def(InputType type, params string[] optionNames)
    {
        var def = new SettingDefinition { Id = "t", Name = "n", Description = "d", InputType = type };
        if (optionNames.Length > 0)
            def = def with
            {
                ComboBox = new ComboBoxMetadata
                {
                    Options = optionNames.Select(o => new ComboBoxOption { DisplayName = o }).ToList(),
                },
            };
        return def;
    }

    private sealed class FakeDetector : IStateDetector
    {
        public string? Detect(Setting setting, IDetectionContext context) => null;
    }

    // ---- Fallbacks (return null -> old apply) ----

    [Fact]
    public void Unpaired_def_returns_null()
    {
        var plan = ApplyRequestResolver.Resolve(Def(InputType.Toggle), enable: true, value: null,
            resetToDefault: false, new[] { ToggleSetting("other") });
        Assert.Null(plan);
    }

    [Fact]
    public void Reset_with_no_default_state_returns_null()
    {
        // ToggleSetting()'s states carry NO WindowsDefault role, so the resolver cannot derive a reset target
        // (no default direction) and falls back to the old apply by returning null.
        var plan = ApplyRequestResolver.Resolve(Def(InputType.Toggle), enable: false, value: null,
            resetToDefault: true, new[] { ToggleSetting() });
        Assert.Null(plan);
    }

    [Fact]
    public void Custom_detector_setting_returns_null()
    {
        var setting = ToggleSetting() with { Detector = new FakeDetector() };
        var plan = ApplyRequestResolver.Resolve(Def(InputType.Toggle), enable: true, value: null,
            resetToDefault: false, new[] { setting });
        Assert.Null(plan);
    }

    [Fact]
    public void Dynamic_option_source_setting_returns_null()
    {
        var setting = SelectionSetting() with { OptionSource = new PowerPlanOptionSource() };
        var plan = ApplyRequestResolver.Resolve(Def(InputType.Selection, "OptA", "OptB"), enable: true, value: 1,
            resetToDefault: false, new[] { setting });
        Assert.Null(plan);
    }

    [Fact]
    public void Numeric_setting_without_numeric_model_returns_null()
    {
        // A NumericRange def whose catalog peer was not authored as a Numeric (no slider model) falls back.
        var plan = ApplyRequestResolver.Resolve(Def(InputType.NumericRange), enable: true, value: 5,
            resetToDefault: false, new[] { ToggleSetting() });
        Assert.Null(plan);
    }

    [Fact]
    public void Numeric_non_dict_value_returns_null()
    {
        // The new engine only handles the AC/DC display-units dictionary shape; a bare int falls back.
        var plan = ApplyRequestResolver.Resolve(Def(InputType.NumericRange), enable: true, value: 5,
            resetToDefault: false, new[] { NumericSetting() });
        Assert.Null(plan);
    }

    [Fact]
    public void Selection_index_out_of_range_returns_null()
    {
        var plan = ApplyRequestResolver.Resolve(Def(InputType.Selection, "OptA", "OptB"), enable: true, value: 7,
            resetToDefault: false, new[] { SelectionSetting() });
        Assert.Null(plan);
    }

    [Fact]
    public void Selection_non_index_value_returns_null()
    {
        var plan = ApplyRequestResolver.Resolve(Def(InputType.Selection, "OptA", "OptB"), enable: true, value: "OptB",
            resetToDefault: false, new[] { SelectionSetting() });
        Assert.Null(plan);
    }

    [Fact]
    public void Selection_label_with_no_matching_state_returns_null()
    {
        // The ComboBox option "Ghost" is not an authored state on the Setting -> fall back rather than throw.
        var plan = ApplyRequestResolver.Resolve(Def(InputType.Selection, "Ghost"), enable: true, value: 0,
            resetToDefault: false, new[] { SelectionSetting() });
        Assert.Null(plan);
    }

    // ---- Handled (produce the same plan ApplyPlanBuilder would) ----

    [Fact]
    public void Toggle_enable_builds_enabled_plan()
    {
        var setting = ToggleSetting();
        var plan = ApplyRequestResolver.Resolve(Def(InputType.Toggle), enable: true, value: null,
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.Build(setting, "Enabled"), plan);
    }

    [Fact]
    public void Toggle_disable_builds_disabled_plan()
    {
        var setting = ToggleSetting();
        var plan = ApplyRequestResolver.Resolve(Def(InputType.Toggle), enable: false, value: null,
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.Build(setting, "Disabled"), plan);
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

        var plan = ApplyRequestResolver.Resolve(Def(InputType.Toggle), enable: false, value: null,
            resetToDefault: true, new[] { setting });

        Assert.Equal(ApplyPlanBuilder.Build(setting, "Disabled", build: null, reset: true), plan);
    }

    [Fact]
    public void CheckBox_builds_like_toggle()
    {
        var setting = ToggleSetting();
        var plan = ApplyRequestResolver.Resolve(Def(InputType.CheckBox), enable: true, value: null,
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.Build(setting, "Enabled"), plan);
    }

    [Fact]
    public void Selection_index_maps_to_option_label_plan()
    {
        var setting = SelectionSetting();
        var plan = ApplyRequestResolver.Resolve(Def(InputType.Selection, "OptA", "OptB"), enable: true, value: 1,
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
        var plan = ApplyRequestResolver.Resolve(Def(InputType.Action), enable: true, value: null,
            resetToDefault: false, new[] { setting });
        Assert.Equal(ApplyPlanBuilder.BuildAction(setting), plan);
    }

    [Fact]
    public void Numeric_acdc_dict_builds_powercfg_numeric_plan()
    {
        var setting = NumericSetting();
        var value = new Dictionary<string, object?> { ["ACValue"] = 10, ["DCValue"] = 5 };
        var plan = ApplyRequestResolver.Resolve(Def(InputType.NumericRange), enable: true, value: value,
            resetToDefault: false, new[] { setting });
        var expected = ApplyPlanBuilder.BuildPowerCfgNumeric(setting, new[]
        {
            new ContextValue(PowerContext.AC, 10),
            new ContextValue(PowerContext.DC, 5),
        });
        Assert.Equal(expected, plan);
    }
}
