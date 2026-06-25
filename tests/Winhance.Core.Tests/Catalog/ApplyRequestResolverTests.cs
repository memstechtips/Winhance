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
    public void Reset_to_default_returns_null()
    {
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
    public void Numeric_range_returns_null()
    {
        var plan = ApplyRequestResolver.Resolve(Def(InputType.NumericRange), enable: true, value: 5,
            resetToDefault: false, new[] { ToggleSetting() });
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
}
