using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Features;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Services;

public class SettingsLoadingService : ISettingsLoadingService
{
    private readonly ISystemSettingsDiscoveryService _discoveryService;
    private readonly ISettingApplicationService _settingApplicationService;
    private readonly IEventBus _eventBus;
    private readonly ILogService _logService;
    private readonly IComboBoxSetupService _comboBoxSetupService;
    private readonly IDomainServiceRouter _domainServiceRouter;
    private readonly IInitializationService _initializationService;
    private readonly IComboBoxResolver _comboBoxResolver;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly ISettingLocalizationService _settingLocalizationService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILocalizationService _localizationService;
    private readonly IDialogService _dialogService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IConfigReviewModeService _configReviewModeService;
    private readonly IConfigReviewDiffService _configReviewDiffService;
    private readonly IHardwareDetectionService _hardwareDetectionService;
    private readonly IRegeditLauncher _regeditLauncher;

    public SettingsLoadingService(
        ISystemSettingsDiscoveryService discoveryService,
        ISettingApplicationService settingApplicationService,
        IEventBus eventBus,
        ILogService logService,
        IComboBoxSetupService comboBoxSetupService,
        IDomainServiceRouter domainServiceRouter,
        IInitializationService initializationService,
        IComboBoxResolver comboBoxResolver,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        ISettingLocalizationService settingLocalizationService,
        IDispatcherService dispatcherService,
        ILocalizationService localizationService,
        IDialogService dialogService,
        IUserPreferencesService userPreferencesService,
        IConfigReviewModeService configReviewModeService,
        IConfigReviewDiffService configReviewDiffService,
        IHardwareDetectionService hardwareDetectionService,
        IRegeditLauncher regeditLauncher)
    {
        _discoveryService = discoveryService;
        _settingApplicationService = settingApplicationService;
        _eventBus = eventBus;
        _logService = logService;
        _comboBoxSetupService = comboBoxSetupService;
        _domainServiceRouter = domainServiceRouter;
        _initializationService = initializationService;
        _comboBoxResolver = comboBoxResolver;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _settingLocalizationService = settingLocalizationService;
        _dispatcherService = dispatcherService;
        _localizationService = localizationService;
        _dialogService = dialogService;
        _userPreferencesService = userPreferencesService;
        _configReviewModeService = configReviewModeService;
        _configReviewDiffService = configReviewDiffService;
        _hardwareDetectionService = hardwareDetectionService;
        _regeditLauncher = regeditLauncher;
    }

    public async Task<ObservableCollection<object>> LoadConfiguredSettingsAsync<TDomainService>(
        TDomainService domainService,
        string featureModuleId,
        string progressMessage,
        ISettingsFeatureViewModel? parentViewModel = null)
        where TDomainService : class, IDomainService
    {
        try
        {
            _logService.Log(LogLevel.Info, $"[SettingsLoadingService] Starting to load settings for '{featureModuleId}'");
            _initializationService.StartFeatureInitialization(featureModuleId);

            var settingDefinitions = _compatibleSettingsRegistry.GetFilteredSettings(featureModuleId);
            var localizedSettings = settingDefinitions.Select(s => _settingLocalizationService.LocalizeSetting(s));
            var settingsList = localizedSettings.ToList();

            var settingViewModels = new ObservableCollection<object>();

            // Read technical details preference once for all settings
            var showTechnicalDetails = await _userPreferencesService.GetPreferenceAsync(
                Core.Features.Common.Constants.UserPreferenceKeys.ShowTechnicalDetails, false);

            _logService.Log(LogLevel.Debug, $"Getting batch states for {settingsList.Count} settings in {featureModuleId}");
            var batchStates = await _discoveryService.GetSettingStatesAsync(settingsList);
            var comboBoxTasks = new Dictionary<string, Task<(SettingItemViewModel viewModel, bool success)>>();

            // Resolve combo box values for Selection type settings
            foreach (var setting in settingsList.Where(s => s.InputType == InputType.Selection))
            {
                if (batchStates.TryGetValue(setting.Id, out var state) && state.RawValues != null)
                {
                    try
                    {
                        var resolvedValue = await _comboBoxResolver.ResolveCurrentValueAsync(setting, state.RawValues);
                        state.CurrentValue = resolvedValue;
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to resolve combo box value for '{setting.Id}': {ex.Message}");
                    }
                }
            }

            // Create ViewModels for all settings (skip settings whose backing resource doesn't exist)
            foreach (var setting in settingsList)
            {
                if (batchStates.TryGetValue(setting.Id, out var settingState) && !settingState.Success)
                {
                    _logService.Log(LogLevel.Debug, $"Skipping setting '{setting.Id}': {settingState.ErrorMessage}");
                    continue;
                }

                var viewModel = await CreateSettingViewModelAsync(setting, batchStates, parentViewModel);
                viewModel.IsTechnicalDetailsGloballyVisible = showTechnicalDetails;
                settingViewModels.Add(viewModel);
            }

            _eventBus.Publish(new FeatureComposedEvent(featureModuleId, settingsList));
            _logService.Log(LogLevel.Info, $"[SettingsLoadingService] Finished loading {settingViewModels.Count} settings for '{featureModuleId}'");
            _initializationService.CompleteFeatureInitialization(featureModuleId);

            return settingViewModels;
        }
        catch (Exception ex)
        {
            _initializationService.CompleteFeatureInitialization(featureModuleId);
            _logService.Log(LogLevel.Error, $"Error loading settings for {featureModuleId}: {ex.Message}");
            throw;
        }
    }

    public async Task<SettingItemViewModel> CreateSettingViewModelAsync(
        SettingDefinition setting,
        Dictionary<string, SettingStateResult> batchStates,
        ISettingsFeatureViewModel? parentViewModel)
    {
        var currentState = batchStates.TryGetValue(setting.Id, out var state) ? state : new SettingStateResult();

        var viewModel = new SettingItemViewModel(
            _settingApplicationService,
            _logService,
            _dispatcherService,
            _dialogService,
            _localizationService,
            _eventBus,
            _userPreferencesService,
            _regeditLauncher)
        {
            SettingDefinition = setting,
            ParentFeatureViewModel = parentViewModel,
            SettingId = setting.Id,
            Name = setting.Name,
            Description = setting.Description,
            GroupName = setting.GroupName ?? string.Empty,
            Icon = setting.Icon ?? string.Empty,
            IconPack = setting.IconPack ?? "Material",
            InputType = setting.InputType,
            IsSelected = currentState.IsEnabled,
            OnText = _localizationService.GetString("Common_On") ?? "On",
            OffText = _localizationService.GetString("Common_Off") ?? "Off",
            ActionButtonText = _localizationService.GetString("Button_Apply") ?? "Apply"
        };

        // Set lock state for advanced settings
        if (setting.RequiresAdvancedUnlock)
        {
            var unlocked = await _userPreferencesService.GetPreferenceAsync("AdvancedPowerSettingsUnlocked", false);
            viewModel.IsLocked = !unlocked;
        }

        // Populate AC/DC values for PowerModeSupport.Separate settings
        if (viewModel.SupportsSeparateACDC)
        {
            viewModel.HasBattery = await _hardwareDetectionService.HasBatteryAsync();

            if (setting.InputType == InputType.NumericRange && currentState.RawValues != null)
            {
                if (currentState.RawValues.TryGetValue("ACValue", out var acVal) && acVal is int acInt)
                    viewModel.AcNumericValue = ConvertFromSystemUnits(acInt, setting);
                if (currentState.RawValues.TryGetValue("DCValue", out var dcVal) && dcVal is int dcInt)
                    viewModel.DcNumericValue = ConvertFromSystemUnits(dcInt, setting);
            }
            // Note: AC/DC Selection values are set AFTER ComboBox options are populated (below)
        }

        if (setting.InputType != InputType.Selection)
        {
            viewModel.SelectedValue = currentState.CurrentValue;
        }

        // Set up numeric range settings
        if (setting.InputType == InputType.NumericRange && setting.CustomProperties != null)
        {
            viewModel.MaxValue = setting.CustomProperties.TryGetValue("MaxValue", out var max) ? (int)max : int.MaxValue;
            viewModel.MinValue = setting.CustomProperties.TryGetValue("MinValue", out var min) ? (int)min : 0;
            viewModel.Units = setting.CustomProperties.TryGetValue("Units", out var units) ? (string)units : "";

            if (currentState.CurrentValue is int intValue)
            {
                viewModel.NumericValue = ConvertFromSystemUnits(intValue, setting);
            }
        }

        // Set up combo box options for selection settings
        if (setting.InputType == InputType.Selection)
        {
            try
            {
                var comboBoxResult = await _comboBoxSetupService.SetupComboBoxOptionsAsync(setting, currentState.CurrentValue);
                viewModel.ComboBoxOptions.Clear();

                // Check if this is a PowerPlan setting that needs localization
                var isPowerPlanSetting = setting.CustomProperties?.ContainsKey("LoadDynamicOptions") == true;

                foreach (var option in comboBoxResult.Options)
                {
                    // Translate PowerPlan localization keys
                    if (isPowerPlanSetting && option.DisplayText.StartsWith("PowerPlan_"))
                    {
                        option.DisplayText = _localizationService.GetString(option.DisplayText);
                    }

                    viewModel.ComboBoxOptions.Add(option);
                }

                // Build cross-group info message if this setting has CrossGroupChildSettings
                viewModel.CrossGroupInfoMessage = _settingLocalizationService.BuildCrossGroupInfoMessage(setting);

                // Set the selected value from the setup result or current state
                if (comboBoxResult.SelectedValue != null)
                {
                    viewModel.SelectedValue = comboBoxResult.SelectedValue;
                    viewModel.UpdateStatusBanner(comboBoxResult.SelectedValue);
                }
                else if (currentState.CurrentValue != null)
                {
                    viewModel.SelectedValue = currentState.CurrentValue;
                    viewModel.UpdateStatusBanner(currentState.CurrentValue);
                }

                // Resolve AC/DC Selection values AFTER ComboBox options are populated
                // (ComboBox needs items before SelectedValue can match)
                if (viewModel.SupportsSeparateACDC && currentState.RawValues != null)
                {
                    var rawAcVal = currentState.RawValues.GetValueOrDefault("ACValue");
                    var rawDcVal = currentState.RawValues.GetValueOrDefault("DCValue");

                    var acRaw = new Dictionary<string, object?>(currentState.RawValues) { ["PowerCfgValue"] = rawAcVal };
                    var dcRaw = new Dictionary<string, object?>(currentState.RawValues) { ["PowerCfgValue"] = rawDcVal };
                    var acIndex = await _comboBoxResolver.ResolveCurrentValueAsync(setting, acRaw);
                    var dcIndex = await _comboBoxResolver.ResolveCurrentValueAsync(setting, dcRaw);

                    viewModel.AcValue = acIndex is int ai ? ai : 0;
                    viewModel.DcValue = dcIndex is int di ? di : 0;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed to setup combo box for '{setting.Id}': {ex.Message}");
            }
        }
        else
        {
            // For non-Selection types, initialize compatibility banner (Selection types handle this in UpdateStatusBanner)
            viewModel.InitializeCompatibilityBanner();
        }

        // If in review mode, check for diffs and apply review state
        if (_configReviewModeService.IsInReviewMode)
        {
            ApplyReviewDiffToViewModel(viewModel, currentState);
        }

        return viewModel;
    }

    /// <summary>
    /// Checks for an eagerly-computed diff from ConfigReviewService, or falls back to
    /// computing a diff against the active config. Sets review mode properties on the ViewModel.
    /// </summary>
    public void ApplyReviewDiffToViewModel(SettingItemViewModel viewModel, SettingStateResult currentState)
    {
        var config = _configReviewModeService.ActiveConfig;
        if (config == null) return;

        viewModel.IsInReviewMode = true;

        // Check if an eager diff already exists from ConfigReviewService
        var existingDiff = _configReviewDiffService.GetDiffForSetting(viewModel.SettingId);
        if (existingDiff != null)
        {
            // Use the pre-computed diff
            if (existingDiff.IsActionSetting && !string.IsNullOrEmpty(existingDiff.ActionConfirmationMessage))
            {
                // Action settings show their custom confirmation message
                viewModel.HasReviewDiff = true;
                viewModel.ReviewDiffMessage = existingDiff.ActionConfirmationMessage;
            }
            else
            {
                var diffFormat = _localizationService.GetString("Review_Mode_Diff_Toggle") ?? "Current: {0} \u2192 Config: {1}";
                viewModel.HasReviewDiff = true;
                viewModel.ReviewDiffMessage = string.Format(diffFormat, existingDiff.CurrentValueDisplay, existingDiff.ConfigValueDisplay);
            }

            // Restore review decision state
            if (existingDiff.IsReviewed)
            {
                if (existingDiff.IsApproved)
                    viewModel.IsReviewApproved = true;
                else
                    viewModel.IsReviewRejected = true;
            }

            // Subscribe to approval changes from this ViewModel
            viewModel.ReviewApprovalChanged += (sender, approved) =>
            {
                _configReviewDiffService.SetSettingApproval(viewModel.SettingId, approved);
            };
            return;
        }

        // No eager diff exists - setting not in config or no change
        // Find this setting in the config to determine if it's in the config at all
        var (configItem, featureModuleId) = FindConfigItemForSetting(viewModel.SettingId, config);
        if (configItem == null)
        {
            // Setting not in config - just mark as in review mode (controls disabled)
            return;
        }

        // Compute diff based on input type (fallback for settings not caught by eager computation)
        var (hasDiff, currentDisplay, configDisplay) = ComputeDiff(viewModel, configItem, currentState);

        if (hasDiff)
        {
            var diffFormat = _localizationService.GetString("Review_Mode_Diff_Toggle") ?? "Current: {0} \u2192 Config: {1}";
            viewModel.HasReviewDiff = true;
            viewModel.ReviewDiffMessage = string.Format(diffFormat, currentDisplay, configDisplay);
            viewModel.IsReviewApproved = false;

            // Register the diff with the service for tracking
            var diff = new ConfigReviewDiff
            {
                SettingId = viewModel.SettingId,
                SettingName = viewModel.Name,
                FeatureModuleId = featureModuleId ?? string.Empty,
                CurrentValueDisplay = currentDisplay,
                ConfigValueDisplay = configDisplay,
                ConfigItem = configItem,
                IsApproved = false,
                InputType = viewModel.InputType
            };
            _configReviewDiffService.RegisterDiff(diff);

            // Subscribe to approval changes from this ViewModel
            viewModel.ReviewApprovalChanged += (sender, approved) =>
            {
                _configReviewDiffService.SetSettingApproval(viewModel.SettingId, approved);
            };
        }
    }

    private (ConfigurationItem? item, string? featureId) FindConfigItemForSetting(string settingId, UnifiedConfigurationFile config)
    {
        // Search in Optimize features
        foreach (var feature in config.Optimize.Features)
        {
            var item = feature.Value.Items.FirstOrDefault(i => i.Id == settingId);
            if (item != null) return (item, feature.Key);
        }

        // Search in Customize features
        foreach (var feature in config.Customize.Features)
        {
            var item = feature.Value.Items.FirstOrDefault(i => i.Id == settingId);
            if (item != null) return (item, feature.Key);
        }

        return (null, null);
    }

    private (bool hasDiff, string currentDisplay, string configDisplay) ComputeDiff(
        SettingItemViewModel viewModel,
        ConfigurationItem configItem,
        SettingStateResult currentState)
    {
        var onText = _localizationService.GetString("Common_On") ?? "On";
        var offText = _localizationService.GetString("Common_Off") ?? "Off";

        switch (viewModel.InputType)
        {
            case InputType.Toggle:
            case InputType.CheckBox:
            {
                var currentBool = currentState.IsEnabled;
                var configBool = configItem.IsSelected ?? false;
                if (currentBool != configBool)
                {
                    return (true, currentBool ? onText : offText, configBool ? onText : offText);
                }
                return (false, string.Empty, string.Empty);
            }

            case InputType.Selection:
            {
                // Compare by SelectedIndex
                var currentIndex = viewModel.SelectedValue is int idx ? idx : -1;

                // Special handling: CustomStateValues or PowerPlan means "custom" config
                if (configItem.CustomStateValues != null || configItem.PowerPlanGuid != null)
                {
                    var currentDisplayName = GetComboBoxDisplayName(viewModel, currentIndex);
                    var configDisplayName = configItem.PowerPlanName ?? "Custom";
                    if (!string.Equals(currentDisplayName, configDisplayName, StringComparison.OrdinalIgnoreCase))
                        return (true, currentDisplayName, configDisplayName);
                    return (false, string.Empty, string.Empty);
                }

                // If config has no SelectedIndex, skip diff for this setting
                if (configItem.SelectedIndex == null)
                    return (false, string.Empty, string.Empty);

                var configIndex = configItem.SelectedIndex.Value;

                if (currentIndex != configIndex)
                {
                    var currentDisplayName = GetComboBoxDisplayName(viewModel, currentIndex);
                    var configDisplayName = GetComboBoxDisplayName(viewModel, configIndex);
                    return (true, currentDisplayName, configDisplayName);
                }
                return (false, string.Empty, string.Empty);
            }

            case InputType.NumericRange:
            {
                var currentVal = currentState.CurrentValue is int cv ? cv : viewModel.NumericValue;
                // For numeric range, the config value might be in PowerSettings or a direct value
                // Try to extract from the config item
                if (configItem.PowerSettings != null)
                {
                    // Power settings have AC/DC values - show AC for display
                    if (configItem.PowerSettings.TryGetValue("ACValue", out var acVal) && acVal is int acInt)
                    {
                        if (currentVal != acInt)
                            return (true, currentVal.ToString(), acInt.ToString());
                    }
                }
                return (false, string.Empty, string.Empty);
            }

            default:
                return (false, string.Empty, string.Empty);
        }
    }

    private string GetComboBoxDisplayName(SettingItemViewModel viewModel, int index)
    {
        if (index >= 0 && index < viewModel.ComboBoxOptions.Count)
        {
            return viewModel.ComboBoxOptions[index].DisplayText ?? index.ToString();
        }
        return index.ToString();
    }

    public async Task<Dictionary<string, SettingStateResult>> RefreshSettingStatesAsync(
        IEnumerable<SettingItemViewModel> settings)
    {
        var settingsList = settings.ToList();
        var definitions = settingsList
            .Where(s => s.SettingDefinition != null)
            .Select(s => s.SettingDefinition!)
            .ToList();

        if (definitions.Count == 0)
            return new Dictionary<string, SettingStateResult>();

        var batchStates = await _discoveryService.GetSettingStatesAsync(definitions);

        // Resolve combo box values for Selection type settings
        foreach (var setting in definitions.Where(s => s.InputType == InputType.Selection))
        {
            if (batchStates.TryGetValue(setting.Id, out var state) && state.RawValues != null)
            {
                try
                {
                    var resolvedValue = await _comboBoxResolver.ResolveCurrentValueAsync(setting, state.RawValues);
                    state.CurrentValue = resolvedValue;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Failed to resolve combo box value for '{setting.Id}': {ex.Message}");
                }
            }
        }

        return batchStates;
    }

    /// <summary>
    /// Converts a raw powercfg API value to display units based on the setting's CustomProperties.
    /// For example, converts 1200 seconds to 20 minutes when display units are "Minutes".
    /// </summary>
    private static int ConvertFromSystemUnits(int systemValue, SettingDefinition setting)
    {
        var displayUnits = setting.CustomProperties?.TryGetValue("Units", out var units) == true && units is string unitsStr
            ? unitsStr
            : null;

        return displayUnits?.ToLowerInvariant() switch
        {
            "minutes" => systemValue / 60,
            "hours" => systemValue / 3600,
            "milliseconds" => systemValue * 1000,
            _ => systemValue
        };
    }
}
