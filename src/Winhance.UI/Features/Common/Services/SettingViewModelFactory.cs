using System.Collections.ObjectModel;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
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
                viewModel.ComboBoxOptions.Clear();
                object? resolvedSelection;

                if (catalogPeer is { States.Count: > 0 } pairedSelection)
                {
                    // Phase 6.7 P1 - new-model-native option build: the options come from Setting.States,
                    // localized via the Setting_{id}_Option_{i} keys (loc-key-only; state.Label is the
                    // fallback). The current index is the detection-resolved CurrentValue (1:1 with States,
                    // -1 == Custom). Retires the old IComboBoxSetupService for paired selections; the old
                    // service stays only as the unpaired fallback below.
                    int currentIndex = currentState.CurrentValue is int ci ? ci : ComboBoxConstants.CustomStateIndex;
                    BuildCatalogSelectionOptions(pairedSelection, currentIndex, viewModel.ComboBoxOptions);
                    resolvedSelection = currentState.CurrentValue ?? currentIndex;
                }
                else
                {
                    // Unpaired (no catalog peer yet): fall back to the old combobox setup service.
                    var comboBoxResult = await _comboBoxSetupService.SetupComboBoxOptionsAsync(setting, currentState.CurrentValue);
                    var isPowerPlanSetting = setting.Recommendation?.LoadDynamicOptions == true;
                    foreach (var option in comboBoxResult.Options)
                    {
                        // Translate PowerPlan localization keys
                        if (isPowerPlanSetting && option.DisplayText.StartsWith("PowerPlan_"))
                            option.DisplayText = _localizationService.GetString(option.DisplayText);
                        viewModel.ComboBoxOptions.Add(option);
                    }
                    resolvedSelection = comboBoxResult.SelectedValue ?? currentState.CurrentValue;
                }

                // Build cross-group info message if this setting has CrossGroupChildSettings
                _enricher.SetCrossGroupInfoMessage(viewModel, setting);

                // Set the selected value from the resolved option build or current state
                if (resolvedSelection != null)
                {
                    viewModel.SelectedValue = resolvedSelection;
                    viewModel.UpdateStatusBanner(resolvedSelection);
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

                    if (catalogPeer is { States.Count: > 0 } pairedPower
                        && pairedPower.Targets.OfType<PowerCfgTarget>().FirstOrDefault() is { } powerTarget)
                    {
                        // Phase 6.7 P2 - new-model-native AC/DC index: match the raw powercfg value against
                        // each option's per-context State value (Set[powerKey]), retiring the old
                        // IComboBoxResolver for paired separate-AC/DC selections. -1 (Custom) on no match is
                        // unreachable (Slice 6 proved 0 orphan / 0 duplicate powercfg option values). The raw
                        // ACValue/DCValue still come from RawValues (threaded there from the new engine by the
                        // overlay); that last RawValues read is retired with the result-shape swap in Slice 10.
                        viewModel.AcValue = rawAcVal is int acInt
                            ? FindStateIndexForPowerCfgValue(pairedPower, powerTarget.Key, acInt) ?? ComboBoxConstants.CustomStateIndex
                            : 0;
                        viewModel.DcValue = rawDcVal is int dcInt
                            ? FindStateIndexForPowerCfgValue(pairedPower, powerTarget.Key, dcInt) ?? ComboBoxConstants.CustomStateIndex
                            : 0;
                    }
                    else
                    {
                        // Unpaired (no catalog peer): the old resolver matches against ComboBox.Options ValueMappings.
                        var acRaw = currentState.RawValues.ToDictionary(kv => kv.Key, kv => kv.Value); acRaw["PowerCfgValue"] = rawAcVal;
                        var dcRaw = currentState.RawValues.ToDictionary(kv => kv.Key, kv => kv.Value); dcRaw["PowerCfgValue"] = rawDcVal;
                        var acIndex = await _comboBoxResolver.ResolveCurrentValueAsync(setting, acRaw);
                        var dcIndex = await _comboBoxResolver.ResolveCurrentValueAsync(setting, dcRaw);

                        viewModel.AcValue = acIndex is int ai ? ai : 0;
                        viewModel.DcValue = dcIndex is int di ? di : 0;
                    }
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

        // Build the technical-details panel from the new Setting model + the now-populated current state
        // (Phase 6.7 Slice 9 - the panel is VM-driven, not TooltipUpdatedEvent-driven).
        viewModel.RefreshTechnicalDetails();

        return viewModel;
    }

    private static int ConvertFromSystemUnits(int systemValue, SettingDefinition setting)
    {
        var displayUnits = setting.NumericRange?.Units;
        return UnitConversionHelper.ConvertFromSystemUnits(systemValue, displayUnits);
    }

    /// <summary>
    /// Builds a selection's combobox options from the new <see cref="Setting"/> model (Phase 6.7 P1):
    /// one option per <see cref="SettingState"/>, localized via the canonical Setting_{id}_Option_{i}
    /// (and _OptionTooltip_{i}) keys with <c>state.Label</c> as the fallback, and the recommended/default
    /// flags derived from the state's roles. Appends the synthetic "Custom" option (Setting_{id}_Option_Custom
    /// or the generic Common_CustomState) when the current index is the Custom sentinel. This replaces the old
    /// IComboBoxSetupService for catalog-paired selections.
    /// </summary>
    private void BuildCatalogSelectionOptions(Setting setting, int currentIndex, ObservableCollection<ComboBoxDisplayOption> options)
    {
        var states = setting.States;
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            // Mirror the old localizer: when the state Label is itself a shared localization key
            // (Template_* / ServiceOption_* / Setting_* / PowerPlan_*), look it up AS the key; otherwise
            // build the per-setting Setting_{id}_Option_{i} key. state.Label is the final raw fallback.
            var displayKey = SettingLocalizationKeys.IsLocalizationKey(state.Label)
                ? state.Label
                : $"Setting_{setting.Id}_Option_{i}";
            var label = LocalizeOrFallback(displayKey, state.Label) ?? state.Label;
            var tooltip = LocalizeOrFallback($"Setting_{setting.Id}_OptionTooltip_{i}", null);
            options.Add(new ComboBoxDisplayOption(label, i, tooltip)
            {
                IsRecommended = state.HasRole(RoleKind.Recommended),
                IsDefault = state.HasRole(RoleKind.WindowsDefault),
                IsSubjectivePreference = setting.Display.IsSubjectivePreference,
            });
        }

        if (currentIndex == ComboBoxConstants.CustomStateIndex)
        {
            var custom = LocalizeOrFallback($"Setting_{setting.Id}_Option_Custom",
                LocalizeOrFallback("Common_CustomState", "Custom"));
            options.Add(new ComboBoxDisplayOption(custom ?? "Custom", ComboBoxConstants.CustomStateIndex, null));
        }
    }

    // Returns the localized string for the key, or the fallback when the key is missing
    // (ILocalizationService.GetString returns the "[key]" marker on a miss).
    private string? LocalizeOrFallback(string key, string? fallback)
    {
        var s = _localizationService.GetString(key);
        return (s.Length >= 2 && s[0] == '[' && s[^1] == ']') ? fallback : s;
    }

    // Maps a raw powercfg value (the AC or DC reading) to the new-model State index whose Set[powerKey]
    // accepts it - the new-model equivalent of the old IComboBoxResolver's ValueMappings match for a
    // separate-AC/DC powercfg selection. Returns null when no option matches (treated as Custom).
    private static int? FindStateIndexForPowerCfgValue(Setting setting, string powerKey, int rawValue)
    {
        var states = setting.States;
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Set.TryGetValue(powerKey, out var stateValue)
                && stateValue.Matches(rawValue, present: true))
                return i;
        }
        return null;
    }
}
