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
        Assert.Equal("Enabled", CatalogDiscovery.Detect(AdSetting(), ctx).Label);
    }

    [Fact]
    public void Group_policy_override_wins_over_preference()
    {
        var ctx = new Ctx(new() { [(Pref, "Enabled")] = 1, [(Gpo, "DisabledByGroupPolicy")] = 1 });
        Assert.Equal("Disabled", CatalogDiscovery.Detect(AdSetting(), ctx).Label); // GP forces off despite Enabled=1
    }

    [Fact]
    public void Nothing_present_falls_to_default_on_fallback()
    {
        var ctx = new Ctx(new()); // everything absent
        Assert.Equal("Enabled", CatalogDiscovery.Detect(AdSetting(), ctx).Label); // default-on via .OrAbsent fallback
    }

    [Fact]
    public void Preference_off_reads_disabled()
    {
        var ctx = new Ctx(new() { [(Pref, "Enabled")] = 0 });
        Assert.Equal("Disabled", CatalogDiscovery.Detect(AdSetting(), ctx).Label);
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
        Assert.Null(CatalogDiscovery.Detect(setting, ctx).Label);
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

        Assert.Equal("On", CatalogDiscovery.Detect(setting, new Ctx(new() { [(@"HKEY_LOCAL_MACHINE\TEST", "V")] = 1 })).Label);
        Assert.Equal("Off", CatalogDiscovery.Detect(setting, new Ctx(new() { [(@"HKEY_LOCAL_MACHINE\TEST", "V")] = 0 })).Label);
    }

    [Fact]
    public void Present_but_unmatched_value_reads_custom_despite_fallback()
    {
        // Enabled=5 is PRESENT but matches neither Of(1).OrAbsent() nor Of(0): detection is honest and
        // reports Custom (null) instead of falling to the IsFallback state.
        var ctx = new Ctx(new() { [(Pref, "Enabled")] = 5 });
        Assert.Null(CatalogDiscovery.Detect(AdSetting(), ctx).Label);
    }

    [Fact]
    public void Absent_unmatched_value_still_falls_to_fallback()
    {
        // Absence is what fallbacks are for: deciding value absent + no state pattern matching absence
        // -> the IsFallback label, not Custom.
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

        Assert.Equal("Off", CatalogDiscovery.Detect(setting, new Ctx(new())).Label);
    }

    [Fact]
    public void Present_value_matching_fallbacks_own_pattern_reads_fallback()
    {
        // Matched != fell through: a present value that matches the IsFallback state's OWN pattern
        // (Enabled=1 vs Of(1).OrAbsent()) returns that state via the normal match, never Custom.
        var ctx = new Ctx(new() { [(Pref, "Enabled")] = 1 });
        Assert.Equal("Enabled", CatalogDiscovery.Detect(AdSetting(), ctx).Label);
    }
}
