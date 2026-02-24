using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Utilities;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Creates fully-configured SettingItemViewModel instances from setting definitions.
/// </summary>
public class SettingViewModelFactory : ISettingViewModelFactory
{
    private readonly ISettingApplicationService _settingApplicationService;
    private readonly ILogService _logService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IEventBus _eventBus;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IRegeditLauncher _regeditLauncher;
    private readonly IComboBoxSetupService _comboBoxSetupService;
    private readonly IComboBoxResolver _comboBoxResolver;
    private readonly IHardwareDetectionService _hardwareDetectionService;
    private readonly ISettingLocalizationService _settingLocalizationService;
    private readonly ISettingReviewDiffApplier _reviewDiffApplier;

    public SettingViewModelFactory(
        ISettingApplicationService settingApplicationService,
        ILogService logService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IEventBus eventBus,
        IUserPreferencesService userPreferencesService,
        IRegeditLauncher regeditLauncher,
        IComboBoxSetupService comboBoxSetupService,
        IComboBoxResolver comboBoxResolver,
        IHardwareDetectionService hardwareDetectionService,
        ISettingLocalizationService settingLocalizationService,
        ISettingReviewDiffApplier reviewDiffApplier)
    {
        _settingApplicationService = settingApplicationService;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _eventBus = eventBus;
        _userPreferencesService = userPreferencesService;
        _regeditLauncher = regeditLauncher;
        _comboBoxSetupService = comboBoxSetupService;
        _comboBoxResolver = comboBoxResolver;
        _hardwareDetectionService = hardwareDetectionService;
        _settingLocalizationService = settingLocalizationService;
        _reviewDiffApplier = reviewDiffApplier;
    }

    /// <summary>
    /// Creates a fully-configured SettingItemViewModel for the given setting definition and current state.
    /// </summary>
    public async Task<SettingItemViewModel> CreateAsync(
        SettingDefinition setting,
        SettingStateResult currentState,
        ISettingsFeatureViewModel? parentViewModel)
    {
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

                    var acRaw = currentState.RawValues.ToDictionary(kv => kv.Key, kv => kv.Value); acRaw["PowerCfgValue"] = rawAcVal;
                    var dcRaw = currentState.RawValues.ToDictionary(kv => kv.Key, kv => kv.Value); dcRaw["PowerCfgValue"] = rawDcVal;
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

        // If in review mode, apply review diff to the newly created ViewModel
        _reviewDiffApplier.ApplyReviewDiffToViewModel(viewModel, currentState);

        return viewModel;
    }

    private static int ConvertFromSystemUnits(int systemValue, SettingDefinition setting)
    {
        var displayUnits = setting.CustomProperties?.TryGetValue("Units", out var units) == true && units is string unitsStr
            ? unitsStr
            : null;
        return UnitConversionHelper.ConvertFromSystemUnits(systemValue, displayUnits);
    }
}
