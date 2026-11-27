using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Xunit;
using Winhance.TestSupport;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Core.Tests.Catalog;

public class CatalogDiscoveryPrecedenceTests
{
    // A pair not in the dict reads as absent.
    private sealed class Ctx : IDetectionContext
    {
        private readonly Dictionary<(string, string?), object?> _vals;
        public Ctx(Dictionary<(string, string?), object?> vals) => _vals = vals;
        public WinBuild CurrentBuild => new(int.MaxValue);
        public object? GetValue(string keyPath, string? valueName)
            => _vals.TryGetValue((keyPath, valueName), out var v) ? v : null;
        public bool KeyExists(string keyPath) => false;
        public string[] GetSubKeyNames(string keyPath) => Array.Empty<string>();
        public IReadOnlyList<DnsServer> DnsServersOfActiveAdapter() => Array.Empty<DnsServer>();
        public bool IsSystemRestoreEnabled() => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) => null;
        public string? ActivePowerPlanGuid() => null;
    }

    private const string Pref = @"HKEY_CURRENT_USER\Software\X\AdvertisingInfo";
    private const string Gpo = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\X\AdvertisingInfo";
    private const string Mirror = @"HKEY_CURRENT_USER\Software\X\CPSS\Store\AdvertisingInfo";
    private static readonly string[] ThemesPaths = [@"HKEY_CURRENT_USER\Themes"];
    private static readonly string[] TestPaths = [@"HKEY_LOCAL_MACHINE\TEST"];

    // privacy-advertising-id shape: preference key + group-policy override + apply-only mirror.
    private static Setting AdSetting() => new()
    {
        Id = "ad",
        Display = new() { Name = TestKeys.Of("ad"), Description = TestKeys.Of("ad") },
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
                Label = LocKey.Common.Enabled,
                IsFallback = true,
                Set = new Dictionary<string, StateValue> { ["Enabled"] = StateValue.Of(1).OrAbsent(), ["Gp"] = StateValue.Absent },
            },
            new SettingState
            {
                Label = LocKey.Common.Disabled,
                Set = new Dictionary<string, StateValue> { ["Enabled"] = StateValue.Of(0), ["Gp"] = StateValue.Of(1) },
            },
        },
    };

    [Fact]
    public void Detect_WhenPreferenceOnAndMirrorAbsent_ReadsEnabled() // the 29-diff bug case
    {
        var ctx = new Ctx(new() { [(Pref, "Enabled")] = 1 });
        Assert.Equal(LocKey.Common.Enabled.Value, CatalogDiscovery.Detect(AdSetting(), ctx).Label);
    }

    [Fact]
    public void Detect_WhenGroupPolicyOverridesPreference_GroupPolicyWins()
    {
        var ctx = new Ctx(new() { [(Pref, "Enabled")] = 1, [(Gpo, "DisabledByGroupPolicy")] = 1 });
        Assert.Equal(LocKey.Common.Disabled.Value, CatalogDiscovery.Detect(AdSetting(), ctx).Label);
    }

    [Fact]
    public void Detect_WhenOverridesTargetConflictsWithPreference_OverridesTargetWins()
    {
        var setting = AdSetting() with
        {
            Targets = new Target[]
            {
                new RegTarget("Enabled", new[] { Pref }, "Enabled", RegistryValueKind.DWord),
                new RegTarget("Gp", new[] { Mirror }, "Value", RegistryValueKind.DWord) { Overrides = true },
            },
        };
        var ctx = new Ctx(new() { [(Pref, "Enabled")] = 1, [(Mirror, "Value")] = 1 });
        Assert.Equal(LocKey.Common.Disabled.Value, CatalogDiscovery.Detect(setting, ctx).Label);
    }

    [Fact]
    public void Detect_WhenNothingPresent_FallsToTheFallbackState()
    {
        var ctx = new Ctx(new());
        Assert.Equal(LocKey.Common.Enabled.Value, CatalogDiscovery.Detect(AdSetting(), ctx).Label);
    }

    [Fact]
    public void Detect_WhenPreferenceOff_ReadsDisabled()
    {
        var ctx = new Ctx(new() { [(Pref, "Enabled")] = 0 });
        Assert.Equal(LocKey.Common.Disabled.Value, CatalogDiscovery.Detect(AdSetting(), ctx).Label);
    }

    [Fact]
    public void Detect_WithTwoIndependentPreferenceKeys_RequiresAllToMatch() // AND-semantics, e.g. theme-mode-windows
    {
        // apps-theme AND system-theme: each key discriminates a different state, so precedence (decide on one)
        // would be wrong - a mixed machine must stay Custom, not mis-report a concrete state.
        var setting = new Setting
        {
            Id = "theme",
            Display = new() { Name = TestKeys.Of("theme"), Description = TestKeys.Of("theme") },
            Targets = new Target[]
            {
                new RegTarget("Apps", ThemesPaths, "AppsUseLightTheme", RegistryValueKind.DWord),
                new RegTarget("System", ThemesPaths, "SystemUsesLightTheme", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState { Label = TestKeys.Of("Light"), Set = new Dictionary<string, StateValue> { ["Apps"] = StateValue.Of(1), ["System"] = StateValue.Of(1) } },
                new SettingState { Label = TestKeys.Of("Dark"), Set = new Dictionary<string, StateValue> { ["Apps"] = StateValue.Of(0), ["System"] = StateValue.Of(0) } },
            },
        };

        // mixed (light apps, dark taskbar) matches neither state -> Custom (null), NOT "Light"
        var ctx = new Ctx(new() { [(@"HKEY_CURRENT_USER\Themes", "AppsUseLightTheme")] = 1, [(@"HKEY_CURRENT_USER\Themes", "SystemUsesLightTheme")] = 0 });
        Assert.Null(CatalogDiscovery.Detect(setting, ctx).Label);
    }

    [Fact]
    public void Detect_WithSingleTarget_MatchesTheStateForTheValue()
    {
        var setting = new Setting
        {
            Id = "s",
            Display = new() { Name = TestKeys.Of("s"), Description = TestKeys.Of("s") },
            Targets = new Target[] { new RegTarget("Mode", TestPaths, "V", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState { Label = TestKeys.Of("On"), Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(1) } },
                new SettingState { Label = TestKeys.Of("Off"), IsFallback = true, Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(0) } },
            },
        };

        Assert.Equal("On", CatalogDiscovery.Detect(setting, new Ctx(new() { [(@"HKEY_LOCAL_MACHINE\TEST", "V")] = 1 })).Label);
        Assert.Equal("Off", CatalogDiscovery.Detect(setting, new Ctx(new() { [(@"HKEY_LOCAL_MACHINE\TEST", "V")] = 0 })).Label);
    }

    [Fact]
    public void Detect_WhenValuePresentButUnmatched_ReadsCustomDespiteFallback()
    {
        // Enabled=5 is PRESENT but matches neither Of(1).OrAbsent() nor Of(0): detection is honest and
        // reports Custom (null) instead of falling to the IsFallback state.
        var ctx = new Ctx(new() { [(Pref, "Enabled")] = 5 });
        Assert.Null(CatalogDiscovery.Detect(AdSetting(), ctx).Label);
    }

    [Fact]
    public void Detect_WhenValueAbsentAndUnmatched_FallsToTheFallbackState()
    {
        // Absence is what fallbacks are for: deciding value absent + no state pattern matching absence
        // -> the IsFallback label, not Custom.
        var setting = new Setting
        {
            Id = "s",
            Display = new() { Name = TestKeys.Of("s"), Description = TestKeys.Of("s") },
            Targets = new Target[] { new RegTarget("Mode", TestPaths, "V", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState { Label = TestKeys.Of("On"), Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(1) } },
                new SettingState { Label = TestKeys.Of("Off"), IsFallback = true, Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(0) } },
            },
        };

        Assert.Equal("Off", CatalogDiscovery.Detect(setting, new Ctx(new())).Label);
    }

    [Fact]
    public void Detect_WhenValueMatchesFallbackOwnPattern_ReadsTheFallbackState()
    {
        // Matched != fell through: a present value that matches the IsFallback state's OWN pattern
        // (Enabled=1 vs Of(1).OrAbsent()) returns that state via the normal match, never Custom.
        var ctx = new Ctx(new() { [(Pref, "Enabled")] = 1 });
        Assert.Equal(LocKey.Common.Enabled.Value, CatalogDiscovery.Detect(AdSetting(), ctx).Label);
    }
}
