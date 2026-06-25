using System;
using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class CatalogDiscoveryPrecedenceTests
{
    /// <summary>Returns a value per (keyPath, valueName); a pair not in the dict reads as absent.</summary>
    private sealed class Ctx : IDetectionContext
    {
        private readonly Dictionary<(string, string?), object?> _vals;
        public Ctx(Dictionary<(string, string?), object?> vals) => _vals = vals;
        public WinBuild CurrentBuild => new(int.MaxValue);
        public object? GetValue(string keyPath, string? valueName)
            => _vals.TryGetValue((keyPath, valueName), out var v) ? v : null;
        public bool KeyExists(string keyPath) => false;
        public string[] GetSubKeyNames(string keyPath) => Array.Empty<string>();
        public string? PrimaryDnsV4OfActiveAdapter() => null;
        public bool IsSystemRestoreEnabled() => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) => null;
        public string? ActivePowerPlanGuid() => null;
    }

    private const string Pref = @"HKEY_CURRENT_USER\Software\X\AdvertisingInfo";
    private const string Gpo = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\X\AdvertisingInfo";
    private const string Mirror = @"HKEY_CURRENT_USER\Software\X\CPSS\Store\AdvertisingInfo";

    /// <summary>privacy-advertising-id shape: preference key + group-policy override + apply-only mirror.</summary>
    private static Setting AdSetting() => new()
    {
        Id = "ad",
        Display = new() { Name = "ad", Description = "ad" },
        Targets = new Target[]
        {
            new RegTarget("Enabled", new[] { Pref }, "Enabled", RegistryValueKind.DWord),
            new RegTarget("Gp", new[] { Gpo }, "DisabledByGroupPolicy", RegistryValueKind.DWord) { IsGroupPolicy = true },
            new RegTarget("Mirror", new[] { Mirror }, "Value", RegistryValueKind.DWord) { ApplyOnly = true },
        },
        States = new[]
        {
            new SettingState
            {
                Label = "Enabled",
                IsFallback = true,
                Set = new Dictionary<string, StateValue> { ["Enabled"] = StateValue.Of(1).OrAbsent(), ["Gp"] = StateValue.Absent },
            },
            new SettingState
            {
                Label = "Disabled",
                Set = new Dictionary<string, StateValue> { ["Enabled"] = StateValue.Of(0), ["Gp"] = StateValue.Of(1) },
            },
        },
    };

    [Fact]
    public void Preference_on_with_absent_mirror_reads_enabled() // the 29-diff bug case
    {
        var ctx = new Ctx(new() { [(Pref, "Enabled")] = 1 }); // Enabled=1; GP + mirror absent
        Assert.Equal("Enabled", CatalogDiscovery.DetectState(AdSetting(), ctx));
    }

    [Fact]
    public void Group_policy_override_wins_over_preference()
    {
        var ctx = new Ctx(new() { [(Pref, "Enabled")] = 1, [(Gpo, "DisabledByGroupPolicy")] = 1 });
        Assert.Equal("Disabled", CatalogDiscovery.DetectState(AdSetting(), ctx)); // GP forces off despite Enabled=1
    }

    [Fact]
    public void Nothing_present_falls_to_default_on_fallback()
    {
        var ctx = new Ctx(new()); // everything absent
        Assert.Equal("Enabled", CatalogDiscovery.DetectState(AdSetting(), ctx)); // default-on via .OrAbsent fallback
    }

    [Fact]
    public void Preference_off_reads_disabled()
    {
        var ctx = new Ctx(new() { [(Pref, "Enabled")] = 0 });
        Assert.Equal("Disabled", CatalogDiscovery.DetectState(AdSetting(), ctx));
    }

    [Fact]
    public void Two_independent_preference_keys_keep_all_match() // AND-semantics, e.g. theme-mode-windows
    {
        // apps-theme AND system-theme: each key discriminates a different state, so precedence (decide on one)
        // would be wrong - a mixed machine must stay Custom, not mis-report a concrete state.
        var setting = new Setting
        {
            Id = "theme",
            Display = new() { Name = "theme", Description = "theme" },
            Targets = new Target[]
            {
                new RegTarget("Apps", new[] { @"HKEY_CURRENT_USER\Themes" }, "AppsUseLightTheme", RegistryValueKind.DWord),
                new RegTarget("System", new[] { @"HKEY_CURRENT_USER\Themes" }, "SystemUsesLightTheme", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState { Label = "Light", Set = new Dictionary<string, StateValue> { ["Apps"] = StateValue.Of(1), ["System"] = StateValue.Of(1) } },
                new SettingState { Label = "Dark", Set = new Dictionary<string, StateValue> { ["Apps"] = StateValue.Of(0), ["System"] = StateValue.Of(0) } },
            },
        };

        // mixed (light apps, dark taskbar) matches neither state -> Custom (null), NOT "Light"
        var ctx = new Ctx(new() { [(@"HKEY_CURRENT_USER\Themes", "AppsUseLightTheme")] = 1, [(@"HKEY_CURRENT_USER\Themes", "SystemUsesLightTheme")] = 0 });
        Assert.Null(CatalogDiscovery.DetectState(setting, ctx));
    }

    [Fact]
    public void Single_target_behaves_as_before()
    {
        var setting = new Setting
        {
            Id = "s",
            Display = new() { Name = "s", Description = "s" },
            Targets = new Target[] { new RegTarget("Mode", new[] { @"HKEY_LOCAL_MACHINE\TEST" }, "V", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState { Label = "On", Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(1) } },
                new SettingState { Label = "Off", IsFallback = true, Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(0) } },
            },
        };

        Assert.Equal("On", CatalogDiscovery.DetectState(setting, new Ctx(new() { [(@"HKEY_LOCAL_MACHINE\TEST", "V")] = 1 })));
        Assert.Equal("Off", CatalogDiscovery.DetectState(setting, new Ctx(new() { [(@"HKEY_LOCAL_MACHINE\TEST", "V")] = 0 })));
    }

    private const string DiagPref = @"HKEY_CURRENT_USER\Software\X\Diagnostics\DiagTrack";
    private const string DiagGpo = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\X\DataCollection";

    /// <summary>diagnostics shape: a preference key (the deciding key when nothing is present) whose Enabled value is
    /// a concrete value with NO absent-accept, plus a group-policy override. The Enabled state carries WindowsDefault.</summary>
    private static Setting DiagSetting() => new()
    {
        Id = "diag",
        Display = new() { Name = "diag", Description = "diag" },
        Targets = new Target[]
        {
            new RegTarget("Toast", new[] { DiagPref }, "ShowedToastAtLevel", RegistryValueKind.DWord),
            new RegTarget("Telemetry", new[] { DiagGpo }, "AllowTelemetry", RegistryValueKind.DWord) { IsGroupPolicy = true },
        },
        States = new[]
        {
            new SettingState
            {
                Label = "Enabled",
                Roles = new[] { StateRole.WindowsDefault },
                Set = new Dictionary<string, StateValue> { ["Toast"] = StateValue.Of(3), ["Telemetry"] = StateValue.Of(3) },
            },
            new SettingState
            {
                Label = "Disabled",
                Roles = new[] { StateRole.Recommended },
                IsFallback = true,
                Set = new Dictionary<string, StateValue> { ["Toast"] = StateValue.Of(1), ["Telemetry"] = StateValue.Of(0) },
            },
        },
    };

    [Fact]
    public void Nothing_present_resolves_to_windows_default_state() // Gap 2: clean machine -> default, not the catch-all
    {
        // No key present and the deciding pref's Enabled entry is Of(3) (no absent): without Gap 2 this falls to the
        // IsFallback "Disabled" catch-all, which is wrong - a clean machine has telemetry on (the Windows default).
        Assert.Equal("Enabled", CatalogDiscovery.DetectState(DiagSetting(), new Ctx(new())));
    }

    [Fact]
    public void Group_policy_off_decides_over_the_default()
    {
        var ctx = new Ctx(new() { [(DiagGpo, "AllowTelemetry")] = 0 }); // GP disables telemetry
        Assert.Equal("Disabled", CatalogDiscovery.DetectState(DiagSetting(), ctx));
    }

    [Fact]
    public void Preference_off_decides_when_no_group_policy_present()
    {
        var ctx = new Ctx(new() { [(DiagPref, "ShowedToastAtLevel")] = 1 }); // only the pref is set, to the disabled value
        Assert.Equal("Disabled", CatalogDiscovery.DetectState(DiagSetting(), ctx));
    }

    [Fact]
    public void Nothing_present_without_windows_default_role_stays_on_fallback() // Gap 2 is gated: no role -> old behaviour
    {
        var setting = new Setting
        {
            Id = "nd",
            Display = new() { Name = "nd", Description = "nd" },
            Targets = new Target[] { new RegTarget("Mode", new[] { @"HKEY_LOCAL_MACHINE\TESTND" }, "V", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState { Label = "On", Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(1) } },
                new SettingState { Label = "Off", IsFallback = true, Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(0) } },
            },
        };

        Assert.Equal("Off", CatalogDiscovery.DetectState(setting, new Ctx(new())));
    }

    [Fact]
    public void Selection_decides_by_authoritative_key_and_defaults_when_absent() // touch-keyboard shape
    {
        var setting = new Setting
        {
            Id = "svc",
            Display = new() { Name = "svc", Description = "svc" },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SVC" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState { Label = "DisabledRec", Roles = new[] { StateRole.Recommended }, Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(4) } },
                new SettingState { Label = "Manual", Roles = new[] { StateRole.WindowsDefault }, Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(3) } },
                new SettingState { Label = "Automatic", Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(2) } },
            },
        };

        Assert.Equal("Manual", CatalogDiscovery.DetectState(setting, new Ctx(new() { [(@"HKEY_LOCAL_MACHINE\SVC", "Start")] = 3 })));
        Assert.Equal("DisabledRec", CatalogDiscovery.DetectState(setting, new Ctx(new() { [(@"HKEY_LOCAL_MACHINE\SVC", "Start")] = 4 })));
        Assert.Equal("Manual", CatalogDiscovery.DetectState(setting, new Ctx(new())));                                  // absent -> WindowsDefault
        Assert.Null(CatalogDiscovery.DetectState(setting, new Ctx(new() { [(@"HKEY_LOCAL_MACHINE\SVC", "Start")] = 1 }))); // unrecognised present -> Custom
    }
}
