using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

/// <summary>
/// Machine-INDEPENDENT model-conformance gate for the registry precedence audit. Each case runs a real
/// <see cref="SettingCatalog"/> setting (by Id) through <see cref="CatalogDiscovery.DetectState"/> over a
/// CONSTRUCTED set of readings (clean / recommended-applied / group-policy-present / mirror-split), asserting it
/// resolves to the value Windows would show. Unlike the live old-vs-new harness (a divergence finder tied to one
/// machine's current registry), this pins the effective-value model across the states a single machine never shows -
/// especially the clean/default state, where an untagged absent mirror is a real bug. Where these intentionally
/// differ from the old app's `.Any` detection, the old app is the bug (it reports enabled when any one target is in
/// its enabled state, treating an absent mirror hive as "enabled"); the model reads the highest-precedence present
/// key, mirrors folding HKLM-first.
/// </summary>
public class CatalogDetectionModelConformanceTests
{
    /// <summary>Returns a value per (keyPath, valueName); a pair not in the dict reads as absent. KeyExists is false
    /// (none of the audited settings are key-existence toggles).</summary>
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

    private static readonly IReadOnlyDictionary<string, Setting> Catalog = SettingCatalog.All.ToDictionary(s => s.Id);

    /// <summary>Detects <paramref name="id"/> with the given (keyPath, valueName, value) readings present; everything
    /// else reads absent.</summary>
    private static string? Detect(string id, params (string Path, string? Value, object? Data)[] reads)
    {
        var dict = reads.ToDictionary(r => (r.Path, r.Value), r => r.Data);
        return CatalogDiscovery.DetectState(Catalog[id], new Ctx(dict));
    }

    // ---- Path constants (verbatim from the catalog) -------------------------------------------------------------

    private const string AdPref = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo";
    private const string AdCpss = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\AdvertisingInfo";
    private const string AdGpoHklm = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo";

    private const string DiagToast = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack";
    private const string DiagTelemetryHklm = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection";

    private const string CdmKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string DirectXKey = @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences";
    private const string TabletSvc = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\TabletInputService";
    private const string InkCpss = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\InkingAndTypingPersonalization";
    private const string InkAccepted = @"HKEY_CURRENT_USER\Software\Microsoft\Personalization\Settings";

    // ============================================================================================================
    //  The 6 detection-corrected settings (OrAbsent + ApplyOnly). These must hold ON A CLEAN MACHINE too.
    // ============================================================================================================

    [Fact]
    public void Advertising_id_precedence() // CPSS Value is an apply-only mirror; the GP tier wins
    {
        Assert.Equal("Enabled", Detect("privacy-advertising-id"));                                   // clean -> ads on (default)
        Assert.Equal("Disabled", Detect("privacy-advertising-id", (AdPref, "Enabled", 0)));          // pref off, no policy
        // GP disables while a stale pref says on: policy wins. (OLD `.Any` wrongly reports Enabled here.)
        Assert.Equal("Disabled", Detect("privacy-advertising-id",
            (AdPref, "Enabled", 1), (AdGpoHklm, "DisabledByGroupPolicy", 1)));
        // The CPSS mirror is NOT read: a stale CPSS Value=0 does not flip a clean machine off.
        Assert.Equal("Enabled", Detect("privacy-advertising-id", (AdCpss, "Value", 0)));
    }

    [Fact]
    public void Diagnostics_precedence() // one preference + group-policy telemetry keys
    {
        Assert.Equal("Enabled", Detect("privacy-diagnostics"));                                      // clean -> telemetry on
        Assert.Equal("Disabled", Detect("privacy-diagnostics", (DiagTelemetryHklm, "AllowTelemetry", 0))); // GP off
        // a stale ShowedToastAtLevel=3 pref does not keep it "on" once the GP disables telemetry (OLD bug)
        Assert.Equal("Disabled", Detect("privacy-diagnostics",
            (DiagToast, "ShowedToastAtLevel", 3), (DiagTelemetryHklm, "AllowTelemetry", 0)));
    }

    [Fact]
    public void Lock_screen_overlay_reads_primary_key() // SubscribedContent mirror is apply-only
    {
        Assert.Equal("Enabled", Detect("privacy-lock-screen-overlay"));                              // clean -> overlay on
        Assert.Equal("Enabled", Detect("privacy-lock-screen-overlay", (CdmKey, "RotatingLockScreenOverlayEnabled", 1)));
        Assert.Equal("Disabled", Detect("privacy-lock-screen-overlay", (CdmKey, "RotatingLockScreenOverlayEnabled", 0)));
        // a stale SubscribedContent=1 does not flip an off machine back on (mirror not read)
        Assert.Equal("Disabled", Detect("privacy-lock-screen-overlay",
            (CdmKey, "RotatingLockScreenOverlayEnabled", 0), (CdmKey, "SubscribedContent-338387Enabled", 1)));
    }

    [Fact]
    public void DirectX_flip_model_defaults_on_when_absent() // composite sub-key; DefaultValue "1"
    {
        Assert.Equal("Enabled", Detect("gaming-directx-flip-model"));                                          // sub-key absent -> default on
        Assert.Equal("Enabled", Detect("gaming-directx-flip-model", (DirectXKey, "DirectXUserGlobalSettings", "SwapEffectUpgradeEnable=1")));
        Assert.Equal("Disabled", Detect("gaming-directx-flip-model", (DirectXKey, "DirectXUserGlobalSettings", "SwapEffectUpgradeEnable=0")));
    }

    [Fact]
    public void DirectX_vrr_defaults_on_when_absent()
    {
        Assert.Equal("Enabled", Detect("gaming-directx-vrr-optimizations"));
        Assert.Equal("Disabled", Detect("gaming-directx-vrr-optimizations", (DirectXKey, "DirectXUserGlobalSettings", "VRROptimizeEnable=0")));
    }

    [Fact]
    public void Touch_keyboard_service_reads_start_type() // IsInputAppPreloadEnabled is an apply-only secondary
    {
        Assert.Equal("ServiceOption_Manual", Detect("gaming-touch-keyboard-service"));                        // absent -> default (Manual)
        Assert.Equal("ServiceOption_DisabledRecommended", Detect("gaming-touch-keyboard-service", (TabletSvc, "Start", 4)));
        Assert.Equal("ServiceOption_Manual", Detect("gaming-touch-keyboard-service", (TabletSvc, "Start", 3)));
        Assert.Equal("ServiceOption_Automatic", Detect("gaming-touch-keyboard-service", (TabletSvc, "Start", 2)));
        Assert.Null(Detect("gaming-touch-keyboard-service", (TabletSvc, "Start", 1)));                        // unrecognised -> Custom
    }

    [Fact]
    public void Inking_typing_dictionary_cpss_wins_else_accepted_policy() // CPSS Value (Win11) > AcceptedPrivacyPolicy (Win10)
    {
        // CPSS Value is tagged a policy tier (the connected-privacy store the Win11 Settings app binds to); the two
        // InputPersonalization keys are apply-only enforcement keys. Default (all absent) reads OFF.
        Assert.Equal("Disabled", Detect("privacy-inking-typing-dictionary"));                                 // clean -> off (default)
        Assert.Equal("Enabled", Detect("privacy-inking-typing-dictionary", (InkCpss, "Value", 1)));           // Win11 CPSS on
        // CPSS is authoritative on Win11: Value=0 wins even over a stale AcceptedPrivacyPolicy=1
        Assert.Equal("Disabled", Detect("privacy-inking-typing-dictionary",
            (InkCpss, "Value", 0), (InkAccepted, "AcceptedPrivacyPolicy", 1)));
        // Win10 (no CPSS store): AcceptedPrivacyPolicy decides
        Assert.Equal("Enabled", Detect("privacy-inking-typing-dictionary", (InkAccepted, "AcceptedPrivacyPolicy", 1)));
        Assert.Equal("Disabled", Detect("privacy-inking-typing-dictionary", (InkAccepted, "AcceptedPrivacyPolicy", 0)));
    }

    // ============================================================================================================
    //  Single GPO-mirror toggles (no catalog change). Clean -> default-on; a disable applied to ONE policy hive
    //  reads Disabled (mirror folds HKLM-first) - the exact case where OLD `.Any` falsely reports Enabled.
    // ============================================================================================================

    [Theory]
    // id, hkcu policy path, value name, the "disabled" value (the recommended write)
    [InlineData("privacy-activity-history", @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0)]
    [InlineData("privacy-allow-cortana", @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0)]
    [InlineData("privacy-onedrive-auto-backup", @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\OneDrive", "KFMBlockOptIn", 1)]
    [InlineData("security-workplace-join-messages", @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WorkplaceJoin", "BlockAADWorkplaceJoin", 1)]
    [InlineData("updates-driver-controls", @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", 1)]
    [InlineData("updates-restart-options", @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoRebootWithLoggedOnUsers", 1)]
    [InlineData("privacy-turn-off-copilot", @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1)]
    [InlineData("updates-store-auto-download", @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\WindowsStore", "AutoDownload", 2)]
    [InlineData("gaming-storage-sense", @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\StorageSense", "AllowStorageSenseGlobal", 0)]
    [InlineData("taskbar-widgets", @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0)]
    [InlineData("taskbar-news-and-interests", @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds", 0)]
    public void Single_gpo_mirror_clean_is_enabled_and_one_hive_disable_is_disabled(
        string id, string hkcuPath, string valueName, int disabledValue)
    {
        Assert.Equal("Enabled", Detect(id));                                                  // clean -> default-on
        Assert.Equal("Disabled", Detect(id, (hkcuPath, valueName, disabledValue)));           // recommended applied to the user hive
    }

    // ============================================================================================================
    //  Multi-target precedence toggles, already correct (no catalog change): clean -> default-on; a preference or
    //  policy in its off value -> Disabled.
    // ============================================================================================================

    [Fact]
    public void Feedback_frequency_clean_on_and_policy_off()
    {
        Assert.Equal("Enabled", Detect("privacy-feedback-frequency"));
        Assert.Equal("Disabled", Detect("privacy-feedback-frequency",
            (@"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1)));
    }

    [Fact]
    public void Autoplay_clean_on_and_policy_off()
    {
        Assert.Equal("Enabled", Detect("explorer-autoplay"));
        Assert.Equal("Disabled", Detect("explorer-autoplay",
            (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 255)));
        Assert.Equal("Disabled", Detect("explorer-autoplay",
            (@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", 1)));
    }

    [Fact]
    public void Bing_search_results_clean_on_and_policy_off()
    {
        Assert.Equal("Enabled", Detect("start-disable-bing-search-results"));
        Assert.Equal("Disabled", Detect("start-disable-bing-search-results",
            (@"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1)));
        Assert.Equal("Disabled", Detect("start-disable-bing-search-results",
            (@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0)));
    }

    // ============================================================================================================
    //  Explorer ThisPC-tree toggles (no catalog change): the only discriminating key is IsPinnedToNameSpaceTree
    //  (Of(1).OrAbsent() vs Of(0)); the other two keys accept absence in both states.
    // ============================================================================================================

    [Theory]
    [InlineData("explorer-customization-gallery", @"HKEY_CURRENT_USER\Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}")]
    [InlineData("explorer-customization-home-folder", @"HKEY_CURRENT_USER\Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}")]
    public void Explorer_thispc_tree_clean_on_unpinned_off(string id, string clsidPath)
    {
        Assert.Equal("Enabled", Detect(id));                                                   // clean -> shown (default)
        Assert.Equal("Disabled", Detect(id, (clsidPath, "System.IsPinnedToNameSpaceTree", 0))); // unpinned -> hidden
    }
}
