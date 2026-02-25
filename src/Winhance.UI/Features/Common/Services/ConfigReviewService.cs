using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
public class ConfigReviewService : IConfigReviewService, IConfigReviewModeService, IConfigReviewDiffService, IConfigReviewBadgeService, IDisposable
{
    private bool _disposed;
    private readonly ILogService _logService;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly ISystemSettingsDiscoveryService _discoveryService;
    private readonly IComboBoxSetupService _comboBoxSetupService;
    private readonly IComboBoxResolver _comboBoxResolver;
    private readonly ILocalizationService _localizationService;
    private readonly IWindowsVersionService _windowsVersionService;
    private readonly Dictionary<string, ConfigReviewDiff> _diffs = new();
    private readonly Dictionary<string, int> _configItemCounts = new();
    private readonly HashSet<string> _featuresInConfig = new();
    private readonly HashSet<string> _visitedFeatures = new();

    // Action settings that always need confirmation, even when current matches config
    private static readonly HashSet<string> ActionSettingIds = new()
    {
        "theme-mode-windows",
        "taskbar-clean",
        "start-menu-clean-10",
        "start-menu-clean-11"
    };

    public ConfigReviewService(
        ILogService logService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        ISystemSettingsDiscoveryService discoveryService,
        IComboBoxSetupService comboBoxSetupService,
        IComboBoxResolver comboBoxResolver,
        ILocalizationService localizationService,
        IWindowsVersionService windowsVersionService)
    {
        _logService = logService;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _discoveryService = discoveryService;
        _comboBoxSetupService = comboBoxSetupService;
        _comboBoxResolver = comboBoxResolver;
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

    public bool IsInReviewMode { get; private set; }
    public UnifiedConfigurationFile? ActiveConfig { get; private set; }
    public int TotalChanges => _diffs.Count;
    public int ApprovedChanges => _diffs.Values.Count(d => d.IsReviewed && d.IsApproved);
    public int ReviewedChanges => _diffs.Values.Count(d => d.IsReviewed);
    public int TotalConfigItems { get; private set; }
    public bool IsSoftwareAppsReviewed { get; set; }

    public event EventHandler? ReviewModeChanged;
    public event EventHandler? ApprovalCountChanged;
    public event EventHandler? BadgeStateChanged;

    public async Task EnterReviewModeAsync(UnifiedConfigurationFile config)
    {
        ActiveConfig = config;
        _diffs.Clear();
        _configItemCounts.Clear();
        _featuresInConfig.Clear();
        _visitedFeatures.Clear();
        IsInReviewMode = true;

        // First compute total config item counts and populate _featuresInConfig
        ComputeConfigItemCounts(config);

        // Eagerly compute diffs for all Optimize and Customize settings
        await ComputeEagerDiffsAsync(config);

        // Auto-mark features with 0 diffs as visited (nothing to review)
        foreach (var featureId in _featuresInConfig.ToList())
        {
            if (FeatureDefinitions.OptimizeFeatures.Contains(featureId) ||
                FeatureDefinitions.CustomizeFeatures.Contains(featureId))
            {
                if (GetFeatureDiffCount(featureId) == 0)
                {
                    _visitedFeatures.Add(featureId);
                }
            }
        }

        _logService.Log(LogLevel.Info,
            $"[ConfigReviewService] Entered review mode with {TotalConfigItems} total config items, {TotalChanges} actual diffs");
        ReviewModeChanged?.Invoke(this, EventArgs.Empty);
        BadgeStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ExitReviewMode()
    {
        ActiveConfig = null;
        _diffs.Clear();
        _configItemCounts.Clear();
        _featuresInConfig.Clear();
        _visitedFeatures.Clear();
        TotalConfigItems = 0;
        IsInReviewMode = false;
        _logService.Log(LogLevel.Info, "[ConfigReviewService] Exited review mode");
        ReviewModeChanged?.Invoke(this, EventArgs.Empty);
        BadgeStateChanged?.Invoke(this, EventArgs.Empty);
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
        if (_visitedFeatures.Add(featureId))
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
        return _featuresInConfig.Contains(featureId);
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
        var relevantFeatures = featureIds.Where(f => _featuresInConfig.Contains(f)).ToList();
        if (relevantFeatures.Count == 0) return false;

        return relevantFeatures.All(IsFeatureFullyReviewed);
    }

    public bool IsFeatureFullyReviewed(string featureId)
    {
        if (!IsInReviewMode) return false;
        if (!_featuresInConfig.Contains(featureId)) return false;

        // Features with 0 diffs that are in config = fully reviewed (nothing to change)
        var featureDiffs = _diffs.Values.Where(d => d.FeatureModuleId == featureId).ToList();
        if (featureDiffs.Count == 0)
        {
            return true; // No diffs means already matching config
        }

        // Must be visited AND all diffs explicitly reviewed (accept or reject)
        if (!_visitedFeatures.Contains(featureId)) return false;
        return featureDiffs.All(d => d.IsReviewed);
    }

    private void ComputeConfigItemCounts(UnifiedConfigurationFile config)
    {
        int total = 0;

        // WindowsApps
        if (config.WindowsApps.IsIncluded && config.WindowsApps.Items.Count > 0)
        {
            _configItemCounts[FeatureIds.WindowsApps] = config.WindowsApps.Items.Count;
            _featuresInConfig.Add(FeatureIds.WindowsApps);
            total += config.WindowsApps.Items.Count;
        }

        // ExternalApps
        if (config.ExternalApps.IsIncluded && config.ExternalApps.Items.Count > 0)
        {
            _configItemCounts[FeatureIds.ExternalApps] = config.ExternalApps.Items.Count;
            _featuresInConfig.Add(FeatureIds.ExternalApps);
            total += config.ExternalApps.Items.Count;
        }

        // Optimize features
        foreach (var kvp in config.Optimize.Features)
        {
            if (kvp.Value.IsIncluded && kvp.Value.Items.Count > 0)
            {
                _configItemCounts[kvp.Key] = kvp.Value.Items.Count;
                _featuresInConfig.Add(kvp.Key);
                total += kvp.Value.Items.Count;
            }
        }

        // Customize features
        foreach (var kvp in config.Customize.Features)
        {
            if (kvp.Value.IsIncluded && kvp.Value.Items.Count > 0)
            {
                _configItemCounts[kvp.Key] = kvp.Value.Items.Count;
                _featuresInConfig.Add(kvp.Key);
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
            // Get setting definitions for this feature
            var settingDefinitions = _compatibleSettingsRegistry.GetFilteredSettings(featureId);
            var settingDefMap = settingDefinitions.ToDictionary(s => s.Id);

            // Batch-load current system states
            var batchStates = await _discoveryService.GetSettingStatesAsync(settingDefinitions.ToList());

            // Resolve combo box values for Selection-type settings
            foreach (var setting in settingDefinitions.Where(s => s.InputType == InputType.Selection))
            {
                if (batchStates.TryGetValue(setting.Id, out var state) && state.RawValues != null)
                {
                    try
                    {
                        var resolvedValue = await _comboBoxResolver.ResolveCurrentValueAsync(setting, state.RawValues as Dictionary<string, object?>);
                        batchStates[setting.Id] = state with { CurrentValue = resolvedValue };
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Warning,
                            $"[ConfigReviewService] Failed to resolve combo box for '{setting.Id}': {ex.Message}");
                    }
                }
            }

            foreach (var configItem in configItems)
            {
                if (!settingDefMap.TryGetValue(configItem.Id, out var settingDef))
                    continue;

                var currentState = batchStates.TryGetValue(configItem.Id, out var state)
                    ? state
                    : new SettingStateResult();

                // Check if this is a special action setting
                bool isActionSetting = ActionSettingIds.Contains(configItem.Id);

                // For start-menu-clean, only register the one matching the current Windows version
                if (configItem.Id == "start-menu-clean-10" && _windowsVersionService.IsWindows11())
                    continue;
                if (configItem.Id == "start-menu-clean-11" && !_windowsVersionService.IsWindows11())
                    continue;

                // Compute diff
                var (hasDiff, currentDisplay, configDisplay, currentKey, configKey) = await ComputeEagerDiffAsync(
                    settingDef, configItem, currentState, onText, offText).ConfigureAwait(false);

                if (hasDiff || isActionSetting)
                {
                    var diff = new ConfigReviewDiff
                    {
                        SettingId = configItem.Id,
                        SettingName = settingDef.Name,
                        FeatureModuleId = featureId,
                        CurrentValueDisplay = currentDisplay,
                        ConfigValueDisplay = configDisplay,
                        CurrentDisplayKey = currentKey,
                        ConfigDisplayKey = configKey,
                        ConfigItem = configItem,
                        IsApproved = false,
                        IsReviewed = false,
                        InputType = settingDef.InputType,
                        IsActionSetting = isActionSetting,
                    };

                    if (isActionSetting)
                    {
                        diff = diff with { ActionConfirmationMessage = GetActionConfirmationMessage(configItem.Id) };
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

    private string GetActionConfirmationMessage(string settingId)
    {
        return settingId switch
        {
            "theme-mode-windows" => _localizationService.GetString("Review_Mode_Action_ThemeWallpaper")
                ?? "Apply the default wallpaper for this theme? (Recommended)",
            "taskbar-clean" => _localizationService.GetString("Review_Mode_Action_CleanTaskbar")
                ?? "Clean the taskbar as part of this configuration?",
            "start-menu-clean-10" or "start-menu-clean-11" =>
                _localizationService.GetString("Review_Mode_Action_CleanStartMenu")
                ?? "Clean the start menu as part of this configuration?",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Computes diff between current system state and config value for a setting definition.
    /// Works with SettingDefinition + SettingStateResult (no ViewModel required).
    /// Returns display strings, plus raw keys for re-localization on language change.
    /// </summary>
    private async Task<(bool hasDiff, string currentDisplay, string configDisplay, string? currentKey, string? configKey)> ComputeEagerDiffAsync(
        SettingDefinition settingDef,
        ConfigurationItem configItem,
        SettingStateResult currentState,
        string onText,
        string offText)
    {
        switch (settingDef.InputType)
        {
            case InputType.Toggle:
            case InputType.CheckBox:
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

            case InputType.Selection:
            {
                // Resolve the current index via combo box setup for accurate display
                var comboResult = await _comboBoxSetupService.SetupComboBoxOptionsAsync(settingDef, currentState.CurrentValue).ConfigureAwait(false);
                var currentIndex = comboResult.SelectedValue is int resolvedIdx ? resolvedIdx
                    : (currentState.CurrentValue is int idx ? idx : -1);
                // Special handling: PowerPlan - compare by GUID from RawValues (locale-independent)
                if (configItem.PowerPlanGuid != null)
                {
                    // Get current active plan GUID directly from raw discovery values
                    string? currentGuid = null;
                    if (currentState.RawValues?.TryGetValue("ActivePowerPlanGuid", out var rawGuid) == true)
                        currentGuid = rawGuid?.ToString();

                    string? currentPlanName = currentState.RawValues?.TryGetValue("ActivePowerPlan", out var rawName) == true
                        ? rawName?.ToString() : null;
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
                    var currentRawKey = currentIndex >= 0 && currentIndex < comboResult.Options.Count
                        ? comboResult.Options[currentIndex].DisplayText : null;
                    var currentDisplayName = currentRawKey != null
                        ? LocalizeComboBoxDisplayText(currentRawKey)
                        : await GetComboBoxDisplayNameFromDefAsync(settingDef, currentIndex, currentState).ConfigureAwait(false);
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
                    var rawCurrentKey = currentIndex >= 0 && currentIndex < comboResult.Options.Count
                        ? comboResult.Options[currentIndex].DisplayText : null;
                    var rawConfigKey = configIndex >= 0 && configIndex < comboResult.Options.Count
                        ? comboResult.Options[configIndex].DisplayText : null;
                    var currentDisplayName = rawCurrentKey != null
                        ? LocalizeComboBoxDisplayText(rawCurrentKey) : currentIndex.ToString();
                    var configDisplayName = rawConfigKey != null
                        ? LocalizeComboBoxDisplayText(rawConfigKey) : configIndex.ToString();
                    return (true, currentDisplayName, configDisplayName, rawCurrentKey, rawConfigKey);
                }
                return (false, string.Empty, string.Empty, null, null);
            }

            case InputType.NumericRange:
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
    /// Gets a display name for a combo box index using the setting definition's combo box setup.
    /// </summary>
    private async Task<string> GetComboBoxDisplayNameFromDefAsync(
        SettingDefinition settingDef,
        int index,
        SettingStateResult currentState)
    {
        try
        {
            var result = await _comboBoxSetupService.SetupComboBoxOptionsAsync(settingDef, currentState.CurrentValue).ConfigureAwait(false);
            if (index >= 0 && index < result.Options.Count)
            {
                return LocalizeComboBoxDisplayText(result.Options[index].DisplayText ?? index.ToString());
            }

            // If index is negative, try to use the resolved selected value from the combo box setup
            if (index < 0 && result.SelectedValue is int resolvedIndex &&
                resolvedIndex >= 0 && resolvedIndex < result.Options.Count)
            {
                return LocalizeComboBoxDisplayText(result.Options[resolvedIndex].DisplayText ?? resolvedIndex.ToString());
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning,
                $"[ConfigReviewService] Failed to get combo box display name for '{settingDef.Id}' index {index}: {ex.Message}");
        }
        return index >= 0 ? index.ToString() : "Unknown";
    }

    /// <summary>
    /// Localizes combo box display text by attempting resolution through the localization service.
    /// Keys like "PowerPlan_Balanced_Name" or "ServiceOption_Disabled" resolve to localized strings;
    /// plain text like "Programs" passes through unchanged (GetString returns "[key]" for missing keys).
    /// </summary>
    private string LocalizeComboBoxDisplayText(string displayText)
    {
        if (string.IsNullOrEmpty(displayText))
            return "Unknown";

        var localized = _localizationService.GetString(displayText);
        if (!string.IsNullOrEmpty(localized) && !(localized.StartsWith("[") && localized.EndsWith("]")))
            return localized;

        return displayText;
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
        foreach (var key in _diffs.Keys.ToList())
        {
            var diff = _diffs[key];
            var updated = diff;
            if (diff.CurrentDisplayKey != null)
                updated = updated with { CurrentValueDisplay = LocalizeComboBoxDisplayText(diff.CurrentDisplayKey) };
            if (diff.ConfigDisplayKey != null)
                updated = updated with { ConfigValueDisplay = LocalizeComboBoxDisplayText(diff.ConfigDisplayKey) };
            if (diff.IsActionSetting)
                updated = updated with { ActionConfirmationMessage = GetActionConfirmationMessage(diff.SettingId) };
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
        var predefined = PowerPlanDefinitions.BuiltInPowerPlans.FirstOrDefault(
            p => NormalizeGuid(p.Guid) == normalizedGuid);
        return predefined?.LocalizationKey;
    }

    /// <summary>
    /// Resolves a power plan to a predefined plan entry using GUID first, then name matching.
    /// Returns null if no match is found.
    /// </summary>
    private static PredefinedPowerPlan? ResolveToPredefinedPlan(string? guid, string? name)
    {
        var plans = PowerPlanDefinitions.BuiltInPowerPlans;

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
        var predefined = PowerPlanDefinitions.BuiltInPowerPlans.FirstOrDefault(
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
