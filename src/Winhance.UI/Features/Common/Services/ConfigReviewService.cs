using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Manages the app-wide Config Review Mode state.
/// Registered as a Singleton so state persists across page navigation.
/// Eagerly computes diffs when entering review mode so badge counts
/// reflect actual changes from current system state.
/// </summary>
public class ConfigReviewService : IConfigReviewService, IConfigReviewModeService, IConfigReviewDiffService, IConfigReviewBadgeService, IApplicationModeService, IDisposable
{
    private bool _disposed;
    private readonly ILogService _logService;
    private readonly ICatalogSettingsRegistry _catalogSettingsRegistry;
    private readonly ICatalogSettingStateProvider _settingStateProvider;
    private readonly ILocalizationService _localizationService;
    private readonly IWindowsVersionService _windowsVersionService;
    private readonly ConcurrentDictionary<string, ConfigReviewDiff> _diffs = new();
    private readonly ConcurrentDictionary<string, int> _configItemCounts = new();
    private readonly ConcurrentDictionary<string, byte> _featuresInConfig = new();
    private readonly ConcurrentDictionary<string, byte> _visitedFeatures = new();
    private readonly Dictionary<string, BuilderEdit> _builderEdits = new();

    // Action settings that always need confirmation, even when current matches config
    private static readonly HashSet<string> ActionSettingIds = new()
    {
        SettingIds.ThemeModeWindows,
        SettingIds.TaskbarClean,
        SettingIds.StartMenuCleanWin10,
        SettingIds.StartMenuCleanWin11
    };

    public ConfigReviewService(
        ILogService logService,
        ICatalogSettingsRegistry catalogSettingsRegistry,
        ICatalogSettingStateProvider settingStateProvider,
        ILocalizationService localizationService,
        IWindowsVersionService windowsVersionService)
    {
        _logService = logService;
        _catalogSettingsRegistry = catalogSettingsRegistry;
        _settingStateProvider = settingStateProvider;
        _localizationService = localizationService;
        _windowsVersionService = windowsVersionService;

        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _localizationService.LanguageChanged -= OnLanguageChanged;
    }

    public WinhanceMode CurrentMode { get; private set; } = WinhanceMode.Normal;
    public BuilderTarget CurrentBuilderTarget { get; private set; } = BuilderTarget.Config;

    // Legacy view retained for existing callers; now derived from CurrentMode.
    public bool IsInReviewMode => CurrentMode == WinhanceMode.ConfigReview;
    public bool IsWindowsDefaults { get; private set; }
    public UnifiedConfigurationFile? ActiveConfig { get; private set; }
    public int TotalChanges => _diffs.Count;
    public int ApprovedChanges => _diffs.Values.Count(static d => d.IsReviewed && d.IsApproved);
    public int ReviewedChanges => _diffs.Values.Count(static d => d.IsReviewed);
    public int TotalConfigItems { get; private set; }
    public bool IsSoftwareAppsReviewed { get; set; }

    public event EventHandler? ReviewModeChanged;
    public event EventHandler? ApprovalCountChanged;
    public event EventHandler? BadgeStateChanged;
    public event EventHandler? ModeChanged;

    public async Task EnterReviewModeAsync(UnifiedConfigurationFile config, bool isWindowsDefaults = false)
    {
        // Fully tear down whatever mode we're leaving (clears Builder edits / prior review
        // state) before seeding review. Review entry is the one async transition, so it
        // drives the teardown itself rather than routing through SetMode.
        LeaveCurrentMode();

        ActiveConfig = config;
        IsWindowsDefaults = isWindowsDefaults;
        _diffs.Clear();
        _configItemCounts.Clear();
        _featuresInConfig.Clear();
        _visitedFeatures.Clear();
        CurrentMode = WinhanceMode.ConfigReview;

        // First compute total config item counts and populate _featuresInConfig
        ComputeConfigItemCounts(config);

        // Eagerly compute diffs for all Optimize and Customize settings
        await ComputeEagerDiffsAsync(config);

        // Auto-mark features with 0 diffs as visited (nothing to review)
        foreach (var featureId in _featuresInConfig.Keys)
        {
            if (FeatureDefinitions.OptimizeFeatures.Contains(featureId) ||
                FeatureDefinitions.CustomizeFeatures.Contains(featureId))
            {
                if (GetFeatureDiffCount(featureId) == 0)
                {
                    _visitedFeatures.TryAdd(featureId, 0);
                }
            }
        }

        _logService.Log(LogLevel.Info,
            $"[ConfigReviewService] Entered review mode with {TotalConfigItems} total config items, {TotalChanges} actual diffs");
        // Ordering is load-bearing: ReviewModeChanged must fire before ModeChanged so the
        // orchestration service can still see the pre-review mode when deciding whether to
        // reapply diffs in place (Normal -> Review) or reload stale Builder VMs first.
        ReviewModeChanged?.Invoke(this, EventArgs.Empty);
        BadgeStateChanged?.Invoke(this, EventArgs.Empty);
        ModeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ExitReviewMode() => SetMode(WinhanceMode.Normal);

    public void EnterBuilderMode(BuilderTarget target) => SetMode(WinhanceMode.Builder, target);

    /// <summary>
    /// The single entry point for synchronous mode transitions (Normal, Builder). Fully
    /// exits whatever mode is active — clearing its state and raising its "exited" events so
    /// subscribers clean up — before entering <paramref name="target"/>. This is the state
    /// machine's chokepoint: no public method sets <see cref="CurrentMode"/> directly, so the
    /// modes can never bleed into each other regardless of which transition a caller requests.
    /// Review entry is async and routes its teardown through <see cref="LeaveCurrentMode"/>.
    /// </summary>
    private void SetMode(WinhanceMode target, BuilderTarget builderTarget = BuilderTarget.Config)
    {
        LeaveCurrentMode();

        if (target == WinhanceMode.Builder)
        {
            CurrentBuilderTarget = builderTarget;
        }

        CurrentMode = target;
        _logService.Log(LogLevel.Info, target == WinhanceMode.Builder
            ? $"[ConfigReviewService] Entered Builder mode (target: {builderTarget})"
            : $"[ConfigReviewService] Entered {target} mode");
        ModeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Completely exits the active mode: clears its in-memory state and raises the
    /// mode-specific "exited" events (so e.g. the orchestration service clears per-setting
    /// review state). Leaves <see cref="CurrentMode"/> at Normal and does NOT raise
    /// <see cref="ModeChanged"/> — the caller owns entering the next mode and raising that.
    /// No-op when already Normal.
    /// </summary>
    private void LeaveCurrentMode()
    {
        switch (CurrentMode)
        {
            case WinhanceMode.ConfigReview:
                ClearReviewArtifacts();
                // Flip out of review BEFORE notifying so subscribers see IsInReviewMode == false.
                CurrentMode = WinhanceMode.Normal;
                ReviewModeChanged?.Invoke(this, EventArgs.Empty);
                BadgeStateChanged?.Invoke(this, EventArgs.Empty);
                break;

            case WinhanceMode.Builder:
                _builderEdits.Clear();
                break;
        }
    }

    /// <summary>
    /// Resets the in-memory review artifacts (diffs, counts, active config). Does not
    /// touch <see cref="CurrentMode"/> or raise events — callers own the transition.
    /// </summary>
    private void ClearReviewArtifacts()
    {
        ActiveConfig = null;
        _diffs.Clear();
        _configItemCounts.Clear();
        _featuresInConfig.Clear();
        _visitedFeatures.Clear();
        TotalConfigItems = 0;
        IsWindowsDefaults = false;
    }

    public void RecordBuilderEdit(BuilderEdit edit)
    {
        if (edit == null || string.IsNullOrEmpty(edit.SettingId))
        {
            return;
        }

        _builderEdits[edit.SettingId] = edit;
    }

    public IReadOnlyCollection<BuilderEdit> GetBuilderEdits()
    {
        return _builderEdits.Values.ToList();
    }

    public void SetBuilderTarget(BuilderTarget target)
    {
        if (CurrentMode != WinhanceMode.Builder || CurrentBuilderTarget == target)
        {
            return;
        }

        CurrentBuilderTarget = target;
        _logService.Log(LogLevel.Info, $"[ConfigReviewService] Builder target switched to {target}");
        ModeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void EnterNormalMode()
    {
        if (CurrentMode == WinhanceMode.Normal)
        {
            return;
        }

        SetMode(WinhanceMode.Normal);
    }

    public ConfigReviewDiff? GetDiffForSetting(string settingId)
    {
        return _diffs.TryGetValue(settingId, out var diff) ? diff : null;
    }

    public void SetSettingApproval(string settingId, bool approved)
    {
        if (_diffs.TryGetValue(settingId, out var diff))
        {
            _diffs[settingId] = diff with { IsReviewed = true, IsApproved = approved };
            ApprovalCountChanged?.Invoke(this, EventArgs.Empty);
            BadgeStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetActionApproval(string settingId, bool approved)
    {
        if (_diffs.TryGetValue(settingId, out var diff))
        {
            _diffs[settingId] = diff with { IsActionReviewed = true, IsActionApproved = approved };
        }
    }

    public IReadOnlyList<ConfigReviewDiff> GetApprovedDiffs()
    {
        return _diffs.Values.Where(d => d.IsReviewed && d.IsApproved).ToList().AsReadOnly();
    }

    public void RegisterDiff(ConfigReviewDiff diff)
    {
        _diffs[diff.SettingId] = diff;
        _logService.Log(
            LogLevel.Debug,
            $"[ConfigReviewService] Registered diff for '{diff.SettingId}': {diff.CurrentValueDisplay} -> {diff.ConfigValueDisplay}");
        ApprovalCountChanged?.Invoke(this, EventArgs.Empty);
        BadgeStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyBadgeStateChanged()
    {
        BadgeStateChanged?.Invoke(this, EventArgs.Empty);
        ApprovalCountChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkFeatureVisited(string featureId)
    {
        if (_visitedFeatures.TryAdd(featureId, 0))
        {
            _logService.Log(LogLevel.Debug,
                $"[ConfigReviewService] Feature '{featureId}' marked as visited");
            BadgeStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int GetNavBadgeCount(string sectionTag)
    {
        if (!IsInReviewMode) return 0;

        return sectionTag switch
        {
            "SoftwareApps" => GetFeatureConfigItemCount(FeatureIds.WindowsApps)
                            + GetFeatureConfigItemCount(FeatureIds.ExternalApps),
            "Optimize" => FeatureDefinitions.OptimizeFeatures
                .Sum(f => GetFeaturePendingDiffCount(f)),
            "Customize" => FeatureDefinitions.CustomizeFeatures
                .Sum(f => GetFeaturePendingDiffCount(f)),
            _ => 0
        };
    }

    private int GetFeatureConfigItemCount(string featureId)
    {
        return _configItemCounts.TryGetValue(featureId, out var count) ? count : 0;
    }

    public int GetFeatureDiffCount(string featureId)
    {
        return _diffs.Values.Count(d => d.FeatureModuleId == featureId);
    }

    public int GetFeaturePendingDiffCount(string featureId)
    {
        return _diffs.Values.Count(d => d.FeatureModuleId == featureId && !d.IsReviewed);
    }

    public bool IsFeatureInConfig(string featureId)
    {
        return _featuresInConfig.ContainsKey(featureId);
    }

    public bool IsSectionFullyReviewed(string sectionTag)
    {
        if (!IsInReviewMode) return false;

        // SoftwareApps uses action choice state from the ViewModel
        if (sectionTag == "SoftwareApps")
        {
            return IsSoftwareAppsReviewed;
        }

        var featureIds = sectionTag switch
        {
            "Optimize" => FeatureDefinitions.OptimizeFeatures.ToArray(),
            "Customize" => FeatureDefinitions.CustomizeFeatures.ToArray(),
            _ => Array.Empty<string>()
        };

        // Only consider features that are in the config
        var relevantFeatures = featureIds.Where(f => _featuresInConfig.ContainsKey(f)).ToList();
        if (relevantFeatures.Count == 0) return false;

        return relevantFeatures.All(IsFeatureFullyReviewed);
    }

    public bool IsFeatureFullyReviewed(string featureId)
    {
        if (!IsInReviewMode) return false;
        if (!_featuresInConfig.ContainsKey(featureId)) return false;

        // Features with 0 diffs that are in config = fully reviewed (nothing to change)
        var featureDiffs = _diffs.Values.Where(d => d.FeatureModuleId == featureId).ToList();
        if (featureDiffs.Count == 0)
        {
            return true; // No diffs means already matching config
        }

        // All diffs must be explicitly reviewed (accept or reject)
        return featureDiffs.All(d => d.IsReviewed);
    }

    private void ComputeConfigItemCounts(UnifiedConfigurationFile config)
    {
        int total = 0;

        // WindowsApps
        if (config.WindowsApps.IsIncluded && config.WindowsApps.Items.Count > 0)
        {
            _configItemCounts[FeatureIds.WindowsApps] = config.WindowsApps.Items.Count;
            _featuresInConfig.TryAdd(FeatureIds.WindowsApps, 0);
            total += config.WindowsApps.Items.Count;
        }

        // ExternalApps
        if (config.ExternalApps.IsIncluded && config.ExternalApps.Items.Count > 0)
        {
            _configItemCounts[FeatureIds.ExternalApps] = config.ExternalApps.Items.Count;
            _featuresInConfig.TryAdd(FeatureIds.ExternalApps, 0);
            total += config.ExternalApps.Items.Count;
        }

        // Optimize features
        foreach (var kvp in config.Optimize.Features)
        {
            if (kvp.Value.IsIncluded && kvp.Value.Items.Count > 0)
            {
                _configItemCounts[kvp.Key] = kvp.Value.Items.Count;
                _featuresInConfig.TryAdd(kvp.Key, 0);
                total += kvp.Value.Items.Count;
            }
        }

        // Customize features
        foreach (var kvp in config.Customize.Features)
        {
            if (kvp.Value.IsIncluded && kvp.Value.Items.Count > 0)
            {
                _configItemCounts[kvp.Key] = kvp.Value.Items.Count;
                _featuresInConfig.TryAdd(kvp.Key, 0);
                total += kvp.Value.Items.Count;
            }
        }

        TotalConfigItems = total;
    }

    /// <summary>
    /// Eagerly computes diffs for all Optimize and Customize settings by batch-loading
    /// current system state and comparing against config values.
    /// </summary>
    private async Task ComputeEagerDiffsAsync(UnifiedConfigurationFile config)
    {
        var onText = _localizationService.GetString("Common_On") ?? "On";
        var offText = _localizationService.GetString("Common_Off") ?? "Off";

        // Process Optimize features
        foreach (var feature in config.Optimize.Features)
        {
            if (!feature.Value.IsIncluded || feature.Value.Items.Count == 0) continue;
            await ComputeFeatureDiffsAsync(feature.Key, feature.Value.Items, onText, offText);
        }

        // Process Customize features
        foreach (var feature in config.Customize.Features)
        {
            if (!feature.Value.IsIncluded || feature.Value.Items.Count == 0) continue;
            await ComputeFeatureDiffsAsync(feature.Key, feature.Value.Items, onText, offText);
        }
    }

    private async Task ComputeFeatureDiffsAsync(
        string featureId,
        IReadOnlyList<ConfigurationItem> configItems,
        string onText,
        string offText)
    {
        try
        {
            // Get the catalog settings for this feature. Review always wants the compatibility filter ON,
            // and GetByFeature's default scope is current-OS.
            var settings = _catalogSettingsRegistry.GetByFeature(featureId);
            var settingMap = settings.ToDictionary(s => s.Id);

            // Batch-load current system states
            var settingList = settings.ToList();
            // Read state from the full-state provider via its catalog Setting overload. This service reads no
            // RawValues; the provider resolves CurrentValue/IsEnabled/DynamicSelection/AcValue/DcValue/Readings.
            var batchStates = await _settingStateProvider.GetStatesAsync(settingList);

            // Mirror the settings-page render predicate so the review never counts/diffs a setting the user
            // cannot see (which would leave the review uncompleteable). The page skips a setting whose live
            // state the provider could not resolve (SettingsLoadingService: !state.Success) and drops an
            // orphaned sub-setting whose UiParentId parent is not itself rendered (a sub-setting lives only
            // inside its parent's expander). Both sides use the same strict GetByFeature scope, so only these
            // two exclusions differ. UiParentId nesting is one level in the catalog.
            var detectedIds = new HashSet<string>(
                settingList
                    .Where(s => !(batchStates.TryGetValue(s.Id, out var st) && !st.Success))
                    .Select(s => s.Id));
            var renderedIds = new HashSet<string>(
                settingList
                    .Where(s => detectedIds.Contains(s.Id)
                        && (string.IsNullOrEmpty(s.UiParentId) || detectedIds.Contains(s.UiParentId)))
                    .Select(s => s.Id));

            foreach (var configItem in configItems)
            {
                if (!settingMap.TryGetValue(configItem.Id, out var setting))
                    continue;

                // Skip settings the settings page will not render (failed detection / orphaned sub-setting)
                // so nothing uncompleteable is counted toward the review completion gate.
                if (!renderedIds.Contains(setting.Id))
                    continue;

                var currentState = batchStates.TryGetValue(configItem.Id, out var state)
                    ? state
                    : new SettingStateResult();

                // Check if this is a special action setting
                bool isActionSetting = ActionSettingIds.Contains(configItem.Id);

                // For start-menu-clean, only register the one matching the current Windows version
                if (configItem.Id == SettingIds.StartMenuCleanWin10 && _windowsVersionService.IsWindows11())
                    continue;
                if (configItem.Id == SettingIds.StartMenuCleanWin11 && !_windowsVersionService.IsWindows11())
                    continue;

                // Compute diff
                var (hasDiff, currentDisplay, configDisplay, currentKey, configKey) = await ComputeEagerDiffAsync(
                    setting, configItem, currentState, onText, offText).ConfigureAwait(false);

                if (hasDiff || isActionSetting)
                {
                    var diff = new ConfigReviewDiff
                    {
                        SettingId = configItem.Id,
                        SettingName = setting.Display.Name,
                        FeatureModuleId = featureId,
                        CurrentValueDisplay = currentDisplay,
                        ConfigValueDisplay = configDisplay,
                        CurrentDisplayKey = currentKey,
                        ConfigDisplayKey = configKey,
                        ConfigItem = configItem,
                        IsApproved = false,
                        IsReviewed = false,
                        IsActionSetting = isActionSetting,
                    };

                    if (isActionSetting)
                    {
                        diff = diff with { ActionConfirmationMessage = GetActionConfirmationMessage(configItem) };
                    }

                    _diffs[configItem.Id] = diff;

                    _logService.Log(LogLevel.Debug,
                        $"[ConfigReviewService] Eager diff for '{configItem.Id}' in '{featureId}': " +
                        $"{(isActionSetting ? "[Action] " : "")}{currentDisplay} -> {configDisplay}");
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error,
                $"[ConfigReviewService] Error computing eager diffs for '{featureId}': {ex.Message}");
        }
    }

    private string GetActionConfirmationMessage(ConfigurationItem configItem)
    {
        return configItem.Id switch
        {
            SettingIds.ThemeModeWindows => GetThemeWallpaperMessage(configItem),
            SettingIds.TaskbarClean => _localizationService.GetString("Review_Mode_Action_CleanTaskbar")
                ?? "Clean the taskbar as part of this configuration?",
            SettingIds.StartMenuCleanWin10 or SettingIds.StartMenuCleanWin11 =>
                _localizationService.GetString("Review_Mode_Action_CleanStartMenu")
                ?? "Clean the start menu as part of this configuration?",
            _ => string.Empty
        };
    }

    private string GetThemeWallpaperMessage(ConfigurationItem configItem)
    {
        var themeNameKey = configItem.SelectedIndex == 0 ? "Theme_LightNative" : "Theme_DarkNative";
        var themeName = _localizationService.GetString(themeNameKey) ?? (configItem.SelectedIndex == 0 ? "Light" : "Dark");
        var format = _localizationService.GetString("Review_Mode_Action_ThemeWallpaper")
            ?? "Apply the default {0} wallpaper?";
        return string.Format(format, themeName);
    }

    /// <summary>
    /// Computes diff between current system state and config value for a catalog setting.
    /// Works with the catalog Setting + SettingStateResult (no ViewModel required).
    /// Returns display strings, plus raw keys for re-localization on language change.
    /// </summary>
    private async Task<(bool hasDiff, string currentDisplay, string configDisplay, string? currentKey, string? configKey)> ComputeEagerDiffAsync(
        Setting setting,
        ConfigurationItem configItem,
        SettingStateResult currentState,
        string onText,
        string offText)
    {
        // Dispatch off the catalog Control (Selection incl. power-plan -> the Selection value path).
        // ControlKind.Toggle covers Toggle + CheckBox (no setting is CheckBox).
        var control = setting.Control;
        switch (control)
        {
            case ControlKind.Toggle:
            {
                var currentBool = currentState.IsEnabled;
                var configBool = configItem.IsSelected ?? false;
                if (currentBool != configBool)
                {
                    var currentKey = currentBool ? "Common_On" : "Common_Off";
                    var configKey = configBool ? "Common_On" : "Common_Off";
                    return (true, currentBool ? onText : offText, configBool ? onText : offText, currentKey, configKey);
                }
                return (false, string.Empty, string.Empty, null, null);
            }

            case ControlKind.Selection:
            case ControlKind.PowerPlan:
            {
                // Resolve the current index via combo box setup for accurate display
                var comboResult = BuildComboBoxOptions(setting, currentState.CurrentValue);
                var currentIndex = comboResult.SelectedValue is int resolvedIdx ? resolvedIdx
                    : (currentState.CurrentValue is int idx ? idx : -1);
                // Special handling: PowerPlan - compare by scheme GUID (locale-independent)
                if (configItem.PowerPlanGuid != null)
                {
                    // Read the active scheme GUID from DynamicSelection (the active plan GUID, lowercased).
                    // NormalizeGuid lowercases both sides for the comparison.
                    string? currentGuid = currentState.DynamicSelection;

                    // The current plan NAME reads the typed DynamicSelectionName (the active plan's raw OS name).
                    string? currentPlanName = currentState.DynamicSelectionName;
                    string? configPlanName = configItem.PowerPlanName;

                    _logService.Log(LogLevel.Debug,
                        $"[ConfigReviewService] PowerPlan comparison: currentGuid='{currentGuid}', configGuid='{configItem.PowerPlanGuid}', " +
                        $"currentName='{currentPlanName}', configName='{configPlanName}'");

                    // Normalize GUIDs for comparison (handle format differences like braces, case)
                    bool guidsMatch = !string.IsNullOrEmpty(currentGuid) &&
                        NormalizeGuid(currentGuid) == NormalizeGuid(configItem.PowerPlanGuid);

                    if (guidsMatch)
                    {
                        _logService.Log(LogLevel.Debug, "[ConfigReviewService] PowerPlan: GUIDs match directly");
                        return (false, string.Empty, string.Empty, null, null);
                    }

                    // Fallback: check if both plans resolve to the same known predefined plan
                    // This handles the case where Winhance Power Plan was created with a different GUID
                    var currentPredefined = ResolveToPredefinedPlan(currentGuid, currentPlanName);
                    var configPredefined = ResolveToPredefinedPlan(configItem.PowerPlanGuid, configPlanName);

                    _logService.Log(LogLevel.Debug,
                        $"[ConfigReviewService] PowerPlan resolve: current='{currentPredefined?.Name}' ({currentPredefined?.Guid}), " +
                        $"config='{configPredefined?.Name}' ({configPredefined?.Guid})");

                    if (currentPredefined != null && configPredefined != null &&
                        NormalizeGuid(currentPredefined.Guid) == NormalizeGuid(configPredefined.Guid))
                    {
                        _logService.Log(LogLevel.Debug, "[ConfigReviewService] PowerPlan: Both resolve to same predefined plan");
                        return (false, string.Empty, string.Empty, null, null);
                    }

                    // Get raw keys for re-localization (localization key for predefined plans, plain name for custom)
                    var currentRawKey = GetPowerPlanLocalizationKey(currentGuid) ?? currentPlanName ?? "Unknown";
                    var configRawKey = GetPowerPlanLocalizationKey(configItem.PowerPlanGuid) ?? configPlanName ?? "Custom";

                    var currentDisplayName = LocalizePowerPlanByGuid(currentGuid)
                        ?? currentPlanName ?? "Unknown";
                    var configDisplayName = LocalizePowerPlanByGuid(configItem.PowerPlanGuid)
                        ?? configPlanName ?? "Custom";

                    _logService.Log(LogLevel.Debug,
                        $"[ConfigReviewService] PowerPlan: Diff detected - '{currentDisplayName}' -> '{configDisplayName}'");
                    return (true, currentDisplayName, configDisplayName, currentRawKey, configRawKey);
                }

                // Special handling: CustomStateValues
                if (configItem.CustomStateValues != null)
                {
                    var currentRawKey = DisplayKeyForStateIndex(setting, comboResult, currentIndex);
                    var currentDisplayName = currentRawKey != null
                        ? LocalizeComboBoxDisplayText(currentRawKey)
                        : await GetComboBoxDisplayNameFromCatalogAsync(setting, currentIndex, currentState).ConfigureAwait(false);
                    var configDisplayName = configItem.PowerPlanName ?? "Custom";
                    if (!string.Equals(currentDisplayName, configDisplayName, StringComparison.OrdinalIgnoreCase))
                        return (true, currentDisplayName, configDisplayName, currentRawKey, configDisplayName);
                    return (false, string.Empty, string.Empty, null, null);
                }

                if (configItem.SelectedIndex == null)
                    return (false, string.Empty, string.Empty, null, null);

                var configIndex = configItem.SelectedIndex.Value;
                if (currentIndex != configIndex)
                {
                    var rawCurrentKey = DisplayKeyForStateIndex(setting, comboResult, currentIndex);
                    var rawConfigKey = DisplayKeyForStateIndex(setting, comboResult, configIndex);
                    var currentDisplayName = rawCurrentKey != null
                        ? LocalizeComboBoxDisplayText(rawCurrentKey) : currentIndex.ToString();
                    var configDisplayName = rawConfigKey != null
                        ? LocalizeComboBoxDisplayText(rawConfigKey) : configIndex.ToString();
                    return (true, currentDisplayName, configDisplayName, rawCurrentKey, rawConfigKey);
                }
                return (false, string.Empty, string.Empty, null, null);
            }

            case ControlKind.Slider:
            {
                var currentVal = currentState.CurrentValue is int cv ? cv : 0;
                if (configItem.PowerSettings != null)
                {
                    if (configItem.PowerSettings.TryGetValue("ACValue", out var acVal) && acVal is int acInt)
                    {
                        if (currentVal != acInt)
                            return (true, currentVal.ToString(), acInt.ToString(), null, null);
                    }
                }
                return (false, string.Empty, string.Empty, null, null);
            }

            default:
                return (false, string.Empty, string.Empty, null, null);
        }
    }

    /// <summary>
    /// Builds the combo box display options for a non-power-plan Selection from its catalog States. The
    /// current index is read straight off <paramref name="currentValue"/> (already the resolved option index);
    /// power-plan settings are handled separately via the PowerPlanGuid branch and never reach this method.
    /// </summary>
    private static ComboBoxSetupResult BuildComboBoxOptions(Setting setting, object? currentValue)
    {
        var result = new ComboBoxSetupResult();

        // Build the review combo-box options from the catalog Setting's States (one per option). Only
        // DisplayText (from State.Label) and SelectedValue are read by the review diff (ComputeEagerDiffAsync /
        // GetComboBoxDisplayNameFromCatalogAsync); Tooltip/IsRecommended/IsDefault/IsSubjectivePreference are
        // populated for the option object but are NOT read in this flow.
        if (setting.States.Count == 0)
            return result; // e.g. power-plan-selection (dynamic options; handled by the PowerPlanGuid branch)

        int currentIndex = currentValue is int idx ? idx : 0;
        var isCustomState = currentIndex == ComboBoxConstants.CustomStateIndex;
        var states = setting.States;

        for (int i = 0; i < states.Count; i++)
        {
            // Twin of SettingViewModelFactory.BuildCatalogSelectionOptions: a detect-only state is not a
            // choice, so it is not an option. SKIP, NEVER RENUMBER - each surviving option keeps its own
            // STATE index as its Value, which is what a saved config's SelectedIndex means. Read the list
            // back through OptionForStateIndex, never by position.
            if (states[i].IsDetectOnly)
                continue;
            result.Options.Add(new ComboBoxDisplayOption(states[i].Label, i, states[i].Tooltip)
            {
                IsRecommended = states[i].HasRole(RoleKind.Recommended),
                IsDefault = states[i].HasRole(RoleKind.WindowsDefault),
                IsSubjectivePreference = setting.Display.IsSubjectivePreference,
            });
        }

        // No synthetic "Custom" entry: every read of Options is guarded by `index >= 0` and the sentinel is
        // -1, so the entry was unreachable dead code - and it carried a hardcoded English "Custom" that no
        // language could translate. SelectedValue still carries the sentinel, which is what callers read.
        result.SelectedValue = isCustomState ? ComboBoxConstants.CustomStateIndex : currentIndex;
        result.Success = true;
        return result;
    }

    /// <summary>The option carrying STATE index <paramref name="stateIndex"/> as its Value, or null when
    /// none does. Options are keyed by state index, NOT by their position in the list: BuildComboBoxOptions
    /// skips detect-only states without renumbering the survivors, so a positional read would return the
    /// wrong option as soon as a skipped state is not the last one - and would silently hand back an option
    /// for an index that has none.</summary>
    private static ComboBoxDisplayOption? OptionForStateIndex(ComboBoxSetupResult result, int stateIndex) =>
        stateIndex < 0 ? null : result.Options.FirstOrDefault(o => o.Value is int v && v == stateIndex);

    /// <summary>The raw display key for STATE index <paramref name="stateIndex"/>: the option's DisplayText
    /// when the state is a choice, and the state's own Label when it is DETECT-ONLY - such a state has no
    /// option (it is not a choice) but is a real, named state, and rendering the bare index for it would
    /// print "2" where the card shows "Mixed". Null when the index names no state at all.</summary>
    private static string? DisplayKeyForStateIndex(Setting setting, ComboBoxSetupResult result, int stateIndex)
    {
        if (OptionForStateIndex(result, stateIndex) is { } option)
            return option.DisplayText;
        return stateIndex >= 0 && stateIndex < setting.States.Count && setting.States[stateIndex].IsDetectOnly
            ? setting.States[stateIndex].Label
            : null;
    }

    /// <summary>
    /// Gets a display name for a combo box index using the catalog Setting's combo box setup.
    /// </summary>
    private async Task<string> GetComboBoxDisplayNameFromCatalogAsync(
        Setting setting,
        int index,
        SettingStateResult currentState)
    {
        try
        {
            var result = BuildComboBoxOptions(setting, currentState.CurrentValue);
            if (DisplayKeyForStateIndex(setting, result, index) is { } key)
            {
                return LocalizeComboBoxDisplayText(key);
            }

            // If index is negative, try to use the resolved selected value from the combo box setup
            if (index < 0 && result.SelectedValue is int resolvedIndex
                && DisplayKeyForStateIndex(setting, result, resolvedIndex) is { } resolvedKey)
            {
                return LocalizeComboBoxDisplayText(resolvedKey);
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning,
                $"[ConfigReviewService] Failed to get combo box display name for '{setting.Id}' index {index}: {ex.Message}");
        }
        return index >= 0 ? index.ToString() : "Unknown";
    }

    /// <summary>
    /// Localizes combo box display text by attempting resolution through the localization service.
    /// Keys like "PowerPlan_Balanced_Name" or "ServiceOption_Disabled" resolve to localized strings;
    /// plain text like "Programs" is not a key, so it passes through unchanged.
    /// </summary>
    private string LocalizeComboBoxDisplayText(string displayText)
    {
        if (string.IsNullOrEmpty(displayText))
            return "Unknown";

        return _localizationService.TryGetString(displayText, out var localized) && !string.IsNullOrEmpty(localized)
            ? localized
            : displayText;
    }

    /// <summary>
    /// Handles language changes by re-localizing all diff display strings.
    /// Runs synchronously so updated diffs are ready before ViewModels reload settings.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (!IsInReviewMode) return;
        RelocalizeDisplayStrings();
    }

    /// <summary>
    /// Re-localizes all diff display strings using stored raw keys.
    /// Called when the UI language changes during active review mode.
    /// </summary>
    private void RelocalizeDisplayStrings()
    {
        foreach (var key in _diffs.Keys)
        {
            if (!_diffs.TryGetValue(key, out var diff))
                continue;
            var updated = diff;
            if (diff.CurrentDisplayKey != null)
                updated = updated with { CurrentValueDisplay = LocalizeComboBoxDisplayText(diff.CurrentDisplayKey) };
            if (diff.ConfigDisplayKey != null)
                updated = updated with { ConfigValueDisplay = LocalizeComboBoxDisplayText(diff.ConfigDisplayKey) };
            if (diff.IsActionSetting && diff.ConfigItem != null)
                updated = updated with { ActionConfirmationMessage = GetActionConfirmationMessage(diff.ConfigItem) };
            _diffs[key] = updated;
        }
    }

    /// <summary>
    /// Gets the localization key for a predefined power plan by GUID, or null if not predefined.
    /// </summary>
    private static string? GetPowerPlanLocalizationKey(string? guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;
        var normalizedGuid = NormalizeGuid(guid);
        var predefined = PowerPlanCatalog.BuiltInPowerPlans.FirstOrDefault(
            p => NormalizeGuid(p.Guid) == normalizedGuid);
        return predefined?.LocalizationKey;
    }

    /// <summary>
    /// Resolves a power plan to a predefined plan entry using GUID first, then name matching.
    /// Returns null if no match is found.
    /// </summary>
    private static PredefinedPowerPlan? ResolveToPredefinedPlan(string? guid, string? name)
    {
        var plans = PowerPlanCatalog.BuiltInPowerPlans;

        // 1. Try GUID match first (most reliable, locale-independent)
        if (!string.IsNullOrEmpty(guid))
        {
            var normalizedGuid = NormalizeGuid(guid);
            var byGuid = plans.FirstOrDefault(p => NormalizeGuid(p.Guid) == normalizedGuid);
            if (byGuid != null) return byGuid;
        }

        // 2. Fall back to name matching for plans with different runtime GUIDs
        if (!string.IsNullOrEmpty(name))
        {
            // Winhance Power Plan: any plan name containing "Winhance" (language-independent brand name)
            if (name.Contains("Winhance", StringComparison.OrdinalIgnoreCase))
            {
                return plans.FirstOrDefault(p =>
                    p.Name.Contains("Winhance", StringComparison.OrdinalIgnoreCase));
            }
        }

        return null;
    }

    /// <summary>
    /// Looks up a power plan by GUID in predefined plans and returns the localized display name.
    /// Returns null if no matching predefined plan is found.
    /// </summary>
    private string? LocalizePowerPlanByGuid(string? guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;

        var normalizedGuid = NormalizeGuid(guid);
        var predefined = PowerPlanCatalog.BuiltInPowerPlans.FirstOrDefault(
            p => NormalizeGuid(p.Guid) == normalizedGuid);

        if (predefined == null) return null;

        var localized = _localizationService.GetString(predefined.LocalizationKey);
        return !string.IsNullOrEmpty(localized) ? localized : predefined.Name;
    }

    /// <summary>
    /// Normalizes a GUID string for comparison by parsing and re-formatting.
    /// Handles differences in casing, braces, and formatting.
    /// </summary>
    private static string NormalizeGuid(string? guid)
    {
        if (string.IsNullOrEmpty(guid)) return string.Empty;
        return Guid.TryParse(guid, out var parsed) ? parsed.ToString("D").ToLowerInvariant() : guid.ToLowerInvariant();
    }
}
