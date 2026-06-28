using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Utilities;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Creates fully-configured SettingItemViewModel instances from setting definitions.
/// </summary>
public class SettingViewModelFactory : ISettingViewModelFactory
{
    private readonly SettingViewModelDependencies _viewModelDeps;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localizationService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly INewBadgeService _newBadgeService;
    private readonly IComboBoxSetupService _comboBoxSetupService;
    private readonly IComboBoxResolver _comboBoxResolver;
    private readonly ISettingViewModelEnricher _enricher;

    public SettingViewModelFactory(
        SettingViewModelDependencies viewModelDeps,
        ILogService logService,
        ILocalizationService localizationService,
        IUserPreferencesService userPreferencesService,
        INewBadgeService newBadgeService,
        IComboBoxSetupService comboBoxSetupService,
        IComboBoxResolver comboBoxResolver,
        ISettingViewModelEnricher enricher)
    {
        _viewModelDeps = viewModelDeps;
        _logService = logService;
        _localizationService = localizationService;
        _userPreferencesService = userPreferencesService;
        _newBadgeService = newBadgeService;
        _comboBoxSetupService = comboBoxSetupService;
        _comboBoxResolver = comboBoxResolver;
        _enricher = enricher;
    }

    /// <summary>
    /// Creates a fully-configured SettingItemViewModel for the given setting definition and current state.
    /// </summary>
    public async Task<SettingItemViewModel> CreateAsync(
        SettingDefinition setting,
        SettingStateResult currentState,
        ISettingsFeatureViewModel? parentViewModel)
    {
        var catalogPeer = SettingCatalog.All.FirstOrDefault(s => s.Id == setting.Id);

        var config = new SettingItemViewModelConfig
        {
            SettingDefinition = setting,
            Setting = catalogPeer,
            ParentFeatureViewModel = parentViewModel,
            SettingId = setting.Id,
            Name = catalogPeer?.Display.Name ?? setting.Name,
            Description = catalogPeer?.Display.Description ?? setting.Description,
            GroupName = catalogPeer?.Display.GroupName ?? setting.GroupName ?? string.Empty,
            Icon = setting.Icon ?? string.Empty,
            IconPack = setting.IconPack ?? "Material",
            InputType = setting.InputType,
            IsSelected = currentState.IsEnabled,
            OnText = _localizationService.GetString("Common_On") ?? "On",
            OffText = _localizationService.GetString("Common_Off") ?? "Off",
            ActionButtonText = _localizationService.GetString("Button_Apply") ?? "Apply"
        };

        var viewModel = new SettingItemViewModel(
            config,
            _viewModelDeps.SettingApplicationService,
            _viewModelDeps.LogService,
            _viewModelDeps.DispatcherService,
            _viewModelDeps.DialogService,
            _localizationService,
            _viewModelDeps.EventBus,
            _userPreferencesService,
            _viewModelDeps.RegeditLauncher,
            _newBadgeService,
            _viewModelDeps.ApplicationModeService);

        // Set lock state for advanced settings
        if (setting.RequiresAdvancedUnlock)
        {
            var unlocked = await _userPreferencesService.GetPreferenceAsync("AdvancedPowerSettingsUnlocked", false);
            viewModel.IsLocked = !unlocked;
        }

        // Populate AC/DC values for PowerModeSupport.Separate settings
        if (viewModel.SupportsSeparateACDC)
        {
            await _enricher.DetectBatteryAsync(viewModel);

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
        if (setting.InputType == InputType.NumericRange && setting.NumericRange != null)
        {
            viewModel.MaxValue = setting.NumericRange.MaxValue;
            viewModel.MinValue = setting.NumericRange.MinValue;
            viewModel.Units = setting.NumericRange.Units ?? "";

            if (currentState.CurrentValue is int intValue)
            {
                viewModel.NumericValue = ConvertFromSystemUnits(intValue, setting);
            }
        }

        // Phase 6.7 Slice 7b-ui-3b: bind the power-plan dropdown to the new GUID model (runtime only). The detection
        // overlay threads the runtime-sourced options + active scheme GUID into the result; build the dropdown directly
        // off them (Value = scheme GUID, no index round-trip - applied via the GUID branch in
        // PowerService.TryApplySpecialSettingAsync, Slice 7b-ui-3a). The custom PowerPlanComboBox drives its per-item
        // visuals (status dot / [Active] badge / delete) off ComboBoxDisplayOption.Tag as PowerPlanComboBoxOption, so
        // synthesize that Tag from the new-model option. Builder mode stays on the OLD index path below so config
        // export's index-based BuilderEdit serialization (ConfigExportService, 6.8 scope) is unchanged.
        var powerPlanHandled = false;
        if (setting.InputType == InputType.Selection
            && setting.Recommendation?.LoadDynamicOptions == true
            && _viewModelDeps.ApplicationModeService?.CurrentMode != WinhanceMode.Builder
            && currentState.DynamicOptions is { } dynamicOptions)
        {
            viewModel.ComboBoxOptions.Clear();

            foreach (var opt in dynamicOptions)
            {
                var label = opt.Label.StartsWith("PowerPlan_")
                    ? _localizationService.GetString(opt.Label)
                    : opt.Label;

                var isActive = currentState.DynamicSelection != null
                    && string.Equals(opt.Value, currentState.DynamicSelection, StringComparison.OrdinalIgnoreCase);

                // The PowerPlanComboBox control + the VM delete path read these off the Tag. ExistsOnSystem/IsActive
                // drive the visuals; SystemPlan.Guid is the delete target (null for a not-installed predefined plan,
                // so its delete button stays hidden). DisplayName carries the raw loc key (the delete dialog
                // re-localizes it), matching the old PowerPlanComboBoxOption Tag.
                var tag = new PowerPlanComboBoxOption
                {
                    DisplayName = opt.Label,
                    ExistsOnSystem = opt.ExistsOnSystem,
                    IsActive = isActive,
                    SystemPlan = opt.ExistsOnSystem
                        ? new Winhance.Core.Features.Optimize.Models.PowerPlan { Guid = opt.Value, Name = label, IsActive = isActive }
                        : null
                };

                viewModel.ComboBoxOptions.Add(new ComboBoxDisplayOption(
                    label,
                    opt.Value,
                    opt.ExistsOnSystem ? "Installed on system" : "Not installed",
                    tag));
            }

            // The stored selection is the active scheme GUID (default to the first option when the active plan is
            // unreadable, mirroring the old index-0 fallback).
            viewModel.SelectedValue = currentState.DynamicSelection ?? dynamicOptions.FirstOrDefault()?.Value;
            _enricher.SetCrossGroupInfoMessage(viewModel, setting);
            viewModel.UpdateStatusBanner(viewModel.SelectedValue);
            powerPlanHandled = true;
        }

        // Set up combo box options for selection settings
        if (setting.InputType == InputType.Selection && !powerPlanHandled)
        {
            try
            {
                var comboBoxResult = await _comboBoxSetupService.SetupComboBoxOptionsAsync(setting, currentState.CurrentValue);
                viewModel.ComboBoxOptions.Clear();

                // Check if this is a PowerPlan setting that needs localization
                var isPowerPlanSetting = setting.Recommendation?.LoadDynamicOptions == true;

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
                _enricher.SetCrossGroupInfoMessage(viewModel, setting);

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

                // Builder/serialization support: when the live state resolves to "Custom"
                // (no predefined option matched), retain the raw values so Builder Save can
                // serialize the custom value without re-reading the system.
                if (viewModel.SelectedValue is int customSelIdx
                    && customSelIdx == ComboBoxConstants.CustomStateIndex
                    && currentState.RawValues != null)
                {
                    viewModel.CapturedCustomStateValues = currentState.RawValues
                        .Where(kv => kv.Value != null)
                        .ToDictionary(kv => kv.Key, kv => kv.Value!);
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
        else if (setting.InputType != InputType.Selection)
        {
            // For non-Selection types, initialize compatibility banner (Selection types handle this in UpdateStatusBanner)
            viewModel.InitializeCompatibilityBanner();
        }

        // If in review mode, apply review diff to the newly created ViewModel
        _enricher.ApplyReviewDiff(viewModel, currentState);

        // Compute initial badge state after all values are populated
        viewModel.ComputeBadgeState();

        return viewModel;
    }

    private static int ConvertFromSystemUnits(int systemValue, SettingDefinition setting)
    {
        var displayUnits = setting.NumericRange?.Units;
        return UnitConversionHelper.ConvertFromSystemUnits(systemValue, displayUnits);
    }
}
