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
    public void Selection_option_script_maps_to_that_states_effect()
    {
        // The option's Script field selects which shared script body runs (mirrors the old apply): Enabled ->
        // EnabledScript, Disabled -> DisabledScript (here null -> no effect), None -> no effect.
        var def = new SettingDefinition
        {
            Id = "s", Name = "n", Description = "d", InputType = InputType.Selection,
            RegistrySettings = new[]
            {
                new RegistrySetting { KeyPath = @"HKLM\E", ValueName = "Mode", RecommendedValue = null, DefaultValue = null, ValueType = RegistryValueKind.DWord },
            },
            PowerShellScripts = new[]
            {
                new PowerShellScriptSetting { EnabledScript = "ENABLE-BODY", DisabledScript = null, RunContext = RunContext.User },
            },
            ComboBox = new ComboBoxMetadata
            {
                Options = new[]
                {
                    new ComboBoxOption { DisplayName = "On",  ValueMappings = new Dictionary<string, object?> { ["Mode"] = 1 }, Script = ScriptOption.Enabled },
                    new ComboBoxOption { DisplayName = "Off", ValueMappings = new Dictionary<string, object?> { ["Mode"] = 0 }, Script = ScriptOption.Disabled },
                    new ComboBoxOption { DisplayName = "Leave", ValueMappings = new Dictionary<string, object?> { ["Mode"] = 2 }, Script = ScriptOption.None },
                },
            },
        };

        var s = SettingDefinitionConverter.ConvertSelection(def);

        var on = s.States.Single(x => x.Label == "On");
        var onEffect = Assert.IsType<ScriptEffect>(Assert.Single(on.Effects));
        Assert.Equal("ENABLE-BODY", onEffect.Script);
        Assert.Equal(RunContext.User, onEffect.Run);

        // Off -> DisabledScript is null -> no effect; None -> no effect.
        Assert.Empty(s.States.Single(x => x.Label == "Off").Effects);
        Assert.Empty(s.States.Single(x => x.Label == "Leave").Effects);
    }

    [Fact]
    public void Selection_option_script_substitutes_script_variables()
    {
        var def = new SettingDefinition
        {
            Id = "s", Name = "n", Description = "d", InputType = InputType.Selection,
            RegistrySettings = new[]
            {
                new RegistrySetting { KeyPath = @"HKLM\E", ValueName = "Mode", RecommendedValue = null, DefaultValue = null, ValueType = RegistryValueKind.DWord },
            },
            PowerShellScripts = new[]
            {
                new PowerShellScriptSetting { EnabledScript = "set {{ip}} now", RunContext = RunContext.System },
            },
            ComboBox = new ComboBoxMetadata
            {
                Options = new[]
                {
                    new ComboBoxOption
                    {
                        DisplayName = "Cloudflare",
                        ValueMappings = new Dictionary<string, object?> { ["Mode"] = 1 },
                        Script = ScriptOption.Enabled,
                        ScriptVariables = new Dictionary<string, string> { ["ip"] = "1.1.1.1" },
                    },
                },
            },
        };

        var s = SettingDefinitionConverter.ConvertSelection(def);
        var effect = Assert.IsType<ScriptEffect>(Assert.Single(s.States.Single(x => x.Label == "Cloudflare").Effects));
        Assert.Equal("set 1.1.1.1 now", effect.Script);
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

    [Fact]
    public void Selection_single_target_resolves_absent_to_default_option_without_resolve_unmatched()
    {
        // Mirrors start-all-apps-view: single DWORD target, an IsDefault option, ResolveUnmatchedToDefault off.
        // Old detection sends an all-absent read to the IsDefault option; the new engine must too (not Custom).
        var def = SelectionDef(
            resolveUnmatched: false,
            new RegistrySetting { KeyPath = @"HKCU\Start", ValueName = "Mode", RecommendedValue = null, DefaultValue = null, ValueType = RegistryValueKind.DWord },
            new ComboBoxOption { DisplayName = "Category", ValueMappings = new Dictionary<string, object?> { ["Mode"] = 0 }, IsDefault = true },
            new ComboBoxOption { DisplayName = "Grid", ValueMappings = new Dictionary<string, object?> { ["Mode"] = 1 } },
            new ComboBoxOption { DisplayName = "List", ValueMappings = new Dictionary<string, object?> { ["Mode"] = 2 } });

        var s = SettingDefinitionConverter.ConvertSelection(def);
        var readings = new DictReadings();

        readings.Set("Mode", null, present: false);                    // value absent -> the IsDefault option
        Assert.Equal("Category", StateDetectionEngine.Detect(s.States, readings));

        readings.Set("Mode", 1, present: true);                        // a recognised value still wins its option
        Assert.Equal("Grid", StateDetectionEngine.Detect(s.States, readings));

        readings.Set("Mode", 7, present: true);                        // present but unrecognised is still Custom
        Assert.Null(StateDetectionEngine.Detect(s.States, readings));
    }

    [Fact]
    public void ScheduledTask_toggle_maps_enabled_disabled_states_and_roles()
    {
        var def = new SettingDefinition
        {
            Id = "t", Name = "n", Description = "d", InputType = InputType.Toggle,
            ScheduledTaskSettings = new[]
            {
                new ScheduledTaskSetting { Id = "x", TaskPath = @"\MS\Task", RecommendedState = false, DefaultState = true },
            },
        };

        var s = SettingDefinitionConverter.ConvertScheduledTaskToggle(def);

        var target = Assert.IsType<TaskTarget>(Assert.Single(s.Targets));
        Assert.Equal(@"\MS\Task", target.TaskPath);

        var enabled = s.States.Single(x => x.Label == "Enabled");
        var disabled = s.States.Single(x => x.Label == "Disabled");
        Assert.True(enabled.Set["Task"].Matches(true, present: true));
        Assert.True(disabled.Set["Task"].Matches(false, present: true));
        Assert.True(disabled.IsFallback);

        // RecommendedState=false -> Disabled is recommended; DefaultState=true -> Enabled is the Windows default.
        Assert.True(disabled.HasRole(RoleKind.Recommended));
        Assert.True(enabled.HasRole(RoleKind.WindowsDefault));
    }

    /// <summary>Fake context for the tray detector: the first <c>promoted</c> of <c>subKeys</c> have
    /// IsPromoted=1, the rest 0.</summary>
    private sealed class TrayCtx : IDetectionContext
    {
        public WinBuild CurrentBuild => new(int.MaxValue);
        private readonly string[] _subKeys;
        private readonly int _promoted;
        public TrayCtx(string[] subKeys, int promoted) { _subKeys = subKeys; _promoted = promoted; }
        public string[] GetSubKeyNames(string keyPath) => _subKeys;
        public object? GetValue(string keyPath, string? valueName)
        {
            for (int i = 0; i < _subKeys.Length; i++)
                if (keyPath.EndsWith("\\" + _subKeys[i]))
                    return i < _promoted ? 1 : 0;
            return null;
        }
        public bool KeyExists(string keyPath) => false;
        public string? PrimaryDnsV4OfActiveAdapter() => null;
        public bool IsSystemRestoreEnabled() => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
    }

    [Fact]
    public void SystemTray_converts_to_a_detector_using_the_script_keyed_labels()
    {
        var def = new SettingDefinition
        {
            Id = "t", Name = "n", Description = "d", InputType = InputType.Selection,
            DetectionType = DetectionType.SystemTrayIcons,
            ComboBox = new ComboBoxMetadata
            {
                Options = new[]
                {
                    new ComboBoxOption { DisplayName = "Show all icons", Script = ScriptOption.Enabled, IsRecommended = true },
                    new ComboBoxOption { DisplayName = "Hide all icons", Script = ScriptOption.Disabled },
                    new ComboBoxOption { DisplayName = "Custom", Script = ScriptOption.None, IsDefault = true },
                },
            },
        };

        var s = SettingDefinitionConverter.ConvertSystemTray(def);
        Assert.IsType<SystemTrayDetector>(s.Detector);

        var keys = new[] { "a", "b", "c" };
        // all promoted -> the Script.Enabled label; none -> the Script.Disabled label; mixed -> Custom.
        Assert.Equal("Show all icons", CatalogDiscovery.DetectState(s, new TrayCtx(keys, promoted: 3)));
        Assert.Equal("Hide all icons", CatalogDiscovery.DetectState(s, new TrayCtx(keys, promoted: 0)));
        Assert.Null(CatalogDiscovery.DetectState(s, new TrayCtx(keys, promoted: 1)));
    }

    private sealed class RestoreCtx : IDetectionContext
    {
        public WinBuild CurrentBuild => new(int.MaxValue);
        private readonly bool _enabled;
        public RestoreCtx(bool enabled) => _enabled = enabled;
        public bool IsSystemRestoreEnabled() => _enabled;
        public object? GetValue(string keyPath, string? valueName) => null;
        public string[] GetSubKeyNames(string keyPath) => System.Array.Empty<string>();
        public bool KeyExists(string keyPath) => false;
        public string? PrimaryDnsV4OfActiveAdapter() => null;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
    }

    [Fact]
    public void SystemRestore_converts_to_a_detector_mapping_enabled_disabled()
    {
        var def = new SettingDefinition
        {
            Id = "t", Name = "n", Description = "d", InputType = InputType.Toggle,
            DetectionType = DetectionType.SystemRestore,
            RecommendedToggleState = true, DefaultToggleState = true,
        };

        var s = SettingDefinitionConverter.ConvertSystemRestore(def);
        Assert.IsType<SystemRestoreDetector>(s.Detector);

        Assert.Equal("Enabled", CatalogDiscovery.DetectState(s, new RestoreCtx(enabled: true)));
        Assert.Equal("Disabled", CatalogDiscovery.DetectState(s, new RestoreCtx(enabled: false)));
    }

    private sealed class DnsCtx : IDetectionContext
    {
        public WinBuild CurrentBuild => new(int.MaxValue);
        private readonly string? _primary;
        public DnsCtx(string? primary) => _primary = primary;
        public string? PrimaryDnsV4OfActiveAdapter() => _primary;
        public object? GetValue(string keyPath, string? valueName) => null;
        public string[] GetSubKeyNames(string keyPath) => System.Array.Empty<string>();
        public bool KeyExists(string keyPath) => false;
        public bool IsSystemRestoreEnabled() => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
    }

    [Fact]
    public void DnsServer_converts_to_a_detector_with_first_wins_ip_map()
    {
        // Mirrors gaming-dns-server: option 0 is automatic (no ScriptVariables); two later options share the
        // same primary IP (1.1.1.1), and the old first-match loop returns the earlier one.
        var def = new SettingDefinition
        {
            Id = "t", Name = "n", Description = "d", InputType = InputType.Selection,
            DetectionType = DetectionType.DnsServer,
            ComboBox = new ComboBoxMetadata
            {
                Options = new[]
                {
                    new ComboBoxOption { DisplayName = "Automatic", IsDefault = true, IsRecommended = true },
                    new ComboBoxOption { DisplayName = "Cloudflare", ScriptVariables = new Dictionary<string, string> { ["primary"] = "1.1.1.1" } },
                    new ComboBoxOption { DisplayName = "Google", ScriptVariables = new Dictionary<string, string> { ["primary"] = "8.8.8.8" } },
                    new ComboBoxOption { DisplayName = "Cloudflare DoH", ScriptVariables = new Dictionary<string, string> { ["primary"] = "1.1.1.1" } },
                },
            },
        };

        var s = SettingDefinitionConverter.ConvertDnsServer(def);
        Assert.IsType<DnsServerDetector>(s.Detector);

        Assert.Equal("Automatic", CatalogDiscovery.DetectState(s, new DnsCtx(null)));        // DHCP / no adapter
        Assert.Equal("Google", CatalogDiscovery.DetectState(s, new DnsCtx("8.8.8.8")));
        Assert.Equal("Cloudflare", CatalogDiscovery.DetectState(s, new DnsCtx("1.1.1.1")));  // first-wins over DoH
        Assert.Null(CatalogDiscovery.DetectState(s, new DnsCtx("5.5.5.5")));                  // unknown -> Custom
    }
}
