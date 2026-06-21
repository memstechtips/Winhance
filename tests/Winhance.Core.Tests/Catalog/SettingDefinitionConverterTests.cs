using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class SettingDefinitionConverterTests
{
    private static SettingDefinition ToggleDef(params RegistrySetting[] regs) => new()
    {
        Id = "t", Name = "n", Description = "d", InputType = InputType.Toggle,
        RegistrySettings = regs,
    };

    [Fact]
    public void Single_target_toggle_maps_enabled_disabled()
    {
        var def = ToggleDef(new RegistrySetting
        {
            KeyPath = @"HKLM\A", ValueName = "V",
            EnabledValue = new object?[] { 1 }, DisabledValue = new object?[] { 0 },
            RecommendedValue = null, DefaultValue = 0, ValueType = RegistryValueKind.DWord,
        });

        var s = SettingDefinitionConverter.ConvertToggle(def);

        var target = Assert.IsType<RegTarget>(Assert.Single(s.Targets));
        Assert.Equal("V", target.Key);
        Assert.Equal(new[] { @"HKLM\A" }, target.Paths);

        var enabled = s.States.Single(x => x.Label == "Enabled");
        var disabled = s.States.Single(x => x.Label == "Disabled");
        Assert.True(enabled.Set["V"].Matches(1, present: true));
        Assert.True(disabled.Set["V"].Matches(0, present: true));
    }

    [Fact]
    public void Mirror_paths_fold_into_one_target()
    {
        var def = ToggleDef(
            new RegistrySetting { KeyPath = @"HKCU\A", ValueName = "Hide", EnabledValue = new object?[] { 1 }, DisabledValue = new object?[] { null }, RecommendedValue = 1, DefaultValue = null, ValueType = RegistryValueKind.DWord },
            new RegistrySetting { KeyPath = @"HKLM\B", ValueName = "Hide", EnabledValue = new object?[] { 1 }, DisabledValue = new object?[] { null }, RecommendedValue = 1, DefaultValue = null, ValueType = RegistryValueKind.DWord });

        var s = SettingDefinitionConverter.ConvertToggle(def);

        var target = Assert.IsType<RegTarget>(Assert.Single(s.Targets));   // one target, two paths
        Assert.Equal(2, target.Paths.Count);
        // Disabled = [null] -> Absent
        Assert.True(s.States.Single(x => x.Label == "Disabled").Set["Hide"].Matches(null, present: false));
    }

    [Fact]
    public void Value_or_absent_array_becomes_or_absent()
    {
        var def = ToggleDef(new RegistrySetting
        {
            KeyPath = @"HKLM\A", ValueName = "V",
            EnabledValue = new object?[] { 1, null }, DisabledValue = new object?[] { 0 },
            RecommendedValue = 1, DefaultValue = 0, ValueType = RegistryValueKind.DWord,
        });
        var s = SettingDefinitionConverter.ConvertToggle(def);
        var enabled = s.States.Single(x => x.Label == "Enabled").Set["V"];
        Assert.True(enabled.Matches(1, present: true));
        Assert.True(enabled.Matches(null, present: false)); // OrAbsent
    }

    [Fact]
    public void Disabled_state_is_a_fallback_so_unrecognised_values_are_not_custom()
    {
        var def = ToggleDef(new RegistrySetting
        {
            KeyPath = @"HKLM\A", ValueName = "V",
            EnabledValue = new object?[] { 1 }, DisabledValue = new object?[] { 0 },
            RecommendedValue = null, DefaultValue = 0, ValueType = RegistryValueKind.DWord,
        });

        var s = SettingDefinitionConverter.ConvertToggle(def);

        var enabled = s.States.Single(x => x.Label == "Enabled");
        var disabled = s.States.Single(x => x.Label == "Disabled");
        Assert.False(enabled.IsFallback);   // On is exact (EnabledValue)
        Assert.True(disabled.IsFallback);    // Off is the catch-all (binary toggle, never Custom)

        // And the detection engine resolves an unrecognised live value (3, neither 1 nor 0) to Disabled,
        // not Custom -- reproducing the old binary-toggle behaviour.
        var readings = new DictReadings();
        readings.Set("V", 3, present: true);
        Assert.Equal("Disabled", StateDetectionEngine.Detect(s.States, readings));
    }

    [Fact]
    public void KeyExistence_standard_shape_enabled_when_key_present()
    {
        // ValueName == null with EnabledValue/DisabledValue both null: standard shape - the key being
        // present means Enabled, absent means Disabled.
        var def = ToggleDef(new RegistrySetting
        {
            KeyPath = @"HKLM\NameSpace\X", ValueName = null,
            EnabledValue = null, DisabledValue = null,
            RecommendedValue = null, DefaultValue = null, ValueType = RegistryValueKind.None,
        });

        var s = SettingDefinitionConverter.ConvertToggle(def);
        var enabled = s.States.Single(x => x.Label == "Enabled").Set["KeyExists"];
        var disabled = s.States.Single(x => x.Label == "Disabled").Set["KeyExists"];

        Assert.True(enabled.Matches(null, present: true));     // key present -> Enabled
        Assert.False(enabled.Matches(null, present: false));
        Assert.True(disabled.Matches(null, present: false));   // key absent -> Disabled
    }

    [Fact]
    public void KeyExistence_enabledvalue_null_sentinel_array_is_still_standard_shape()
    {
        // EnabledValue = [null] still carries the null sentinel, so it is standard (not inverted).
        var def = ToggleDef(new RegistrySetting
        {
            KeyPath = @"HKLM\Delegate\X", ValueName = null,
            EnabledValue = new object?[] { null }, DisabledValue = null,
            RecommendedValue = null, DefaultValue = null, ValueType = RegistryValueKind.None,
        });

        var s = SettingDefinitionConverter.ConvertToggle(def);

        Assert.True(s.States.Single(x => x.Label == "Enabled").Set["KeyExists"].Matches(null, present: true));
        Assert.True(s.States.Single(x => x.Label == "Disabled").Set["KeyExists"].Matches(null, present: false));
    }

    [Fact]
    public void KeyExistence_inverted_shape_enabled_when_key_absent()
    {
        // Inverted: DisabledValue carries the null sentinel and EnabledValue does not -> the key being
        // absent means Enabled, present means Disabled.
        var def = ToggleDef(new RegistrySetting
        {
            KeyPath = @"HKLM\Inverted\X", ValueName = null,
            EnabledValue = new object?[] { "x" }, DisabledValue = new object?[] { null },
            RecommendedValue = null, DefaultValue = null, ValueType = RegistryValueKind.None,
        });

        var s = SettingDefinitionConverter.ConvertToggle(def);

        Assert.True(s.States.Single(x => x.Label == "Enabled").Set["KeyExists"].Matches(null, present: false));
        Assert.True(s.States.Single(x => x.Label == "Disabled").Set["KeyExists"].Matches(null, present: true));
    }

    private static SettingDefinition SelectionDef(bool resolveUnmatched, RegistrySetting reg, params ComboBoxOption[] options) => new()
    {
        Id = "s", Name = "n", Description = "d", InputType = InputType.Selection,
        ResolveUnmatchedToDefault = resolveUnmatched,
        RegistrySettings = new[] { reg },
        ComboBox = new ComboBoxMetadata { Options = options },
    };

    [Fact]
    public void Selection_maps_each_option_to_a_state_with_value_set_roles_and_fallback()
    {
        var def = SelectionDef(
            resolveUnmatched: true,
            new RegistrySetting { KeyPath = @"HKCU\E", ValueName = "link", RecommendedValue = null, DefaultValue = null, ValueType = RegistryValueKind.DWord },
            new ComboBoxOption { DisplayName = "Keep", ValueMappings = new Dictionary<string, object?> { ["link"] = null }, IsRecommended = true, IsDefault = true },
            new ComboBoxOption { DisplayName = "Remove", ValueMappings = new Dictionary<string, object?> { ["link"] = 0 } });

        var s = SettingDefinitionConverter.ConvertSelection(def);

        Assert.Equal(2, s.States.Count);
        var keep = s.States.Single(x => x.Label == "Keep");
        var remove = s.States.Single(x => x.Label == "Remove");

        // Keep maps link=null -> Absent; Remove maps link=0 -> Of(0).
        Assert.True(keep.Set["link"].Matches(null, present: false));
        Assert.False(keep.Set["link"].Matches(0, present: true));
        Assert.True(remove.Set["link"].Matches(0, present: true));
        Assert.False(remove.Set["link"].Matches(null, present: false));

        Assert.True(keep.HasRole(RoleKind.Recommended));
        Assert.True(keep.HasRole(RoleKind.WindowsDefault));
        Assert.True(keep.IsFallback);     // ResolveUnmatchedToDefault + IsDefault
        Assert.False(remove.IsFallback);
    }

    [Fact]
    public void Selection_mapping_equal_to_default_value_also_accepts_absence()
    {
        var def = SelectionDef(
            resolveUnmatched: false,
            new RegistrySetting { KeyPath = @"HKLM\E", ValueName = "Mode", RecommendedValue = null, DefaultValue = 1, ValueType = RegistryValueKind.DWord },
            new ComboBoxOption { DisplayName = "On", ValueMappings = new Dictionary<string, object?> { ["Mode"] = 1 }, IsDefault = true },
            new ComboBoxOption { DisplayName = "Off", ValueMappings = new Dictionary<string, object?> { ["Mode"] = 0 } });

        var s = SettingDefinitionConverter.ConvertSelection(def);
        var on = s.States.Single(x => x.Label == "On").Set["Mode"];
        var off = s.States.Single(x => x.Label == "Off").Set["Mode"];

        // "On" maps Mode=1, which equals DefaultValue=1, so an absent read (which the old resolver reads as
        // the default) also resolves to On.
        Assert.True(on.Matches(1, present: true));
        Assert.True(on.Matches(null, present: false));   // OrAbsent
        // "Off" maps Mode=0, not the default, so an absent read does not match Off.
        Assert.True(off.Matches(0, present: true));
        Assert.False(off.Matches(null, present: false));
    }

    [Fact]
    public void Selection_without_resolve_unmatched_resolves_unrecognised_value_to_custom()
    {
        var def = SelectionDef(
            resolveUnmatched: false,
            new RegistrySetting { KeyPath = @"HKLM\E", ValueName = "Mode", RecommendedValue = null, DefaultValue = null, ValueType = RegistryValueKind.DWord },
            new ComboBoxOption { DisplayName = "A", ValueMappings = new Dictionary<string, object?> { ["Mode"] = 0 }, IsDefault = true },
            new ComboBoxOption { DisplayName = "B", ValueMappings = new Dictionary<string, object?> { ["Mode"] = 1 } });

        var s = SettingDefinitionConverter.ConvertSelection(def);

        Assert.All(s.States, st => Assert.False(st.IsFallback));
        var readings = new DictReadings();
        readings.Set("Mode", 9, present: true);
        Assert.Null(StateDetectionEngine.Detect(s.States, readings));   // unrecognised -> Custom, not a default
    }
}
