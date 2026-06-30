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
    private readonly ISettingViewModelEnricher _enricher;

    public SettingViewModelFactory(
        SettingViewModelDependencies viewModelDeps,
        ILogService logService,
        ILocalizationService localizationService,
        IUserPreferencesService userPreferencesService,
        INewBadgeService newBadgeService,
        ISettingViewModelEnricher enricher)
    {
        _viewModelDeps = viewModelDeps;
        _logService = logService;
        _localizationService = localizationService;
        _userPreferencesService = userPreferencesService;
        _newBadgeService = newBadgeService;
        _enricher = enricher;
    }

    /// <summary>
    /// Creates a fully-configured SettingItemViewModel for the given setting definition and current state.
    /// </summary>
    public async Task<SettingItemViewModel> CreateAsync(
        Setting setting,
        InputType inputType,
        SettingStateResult currentState,
        ISettingsFeatureViewModel? parentViewModel,
        IReadOnlyList<string?>? optionWarnings,
        string? crossGroupInfoMessage,
        ComboBoxSetupResult? builderComboBoxOptions,
        string? compatibilityMessage)
    {
        var config = new SettingItemViewModelConfig
        {
            Setting = setting,
            ParentFeatureViewModel = parentViewModel,
            SettingId = setting.Id,
            Name = setting.Display.Name,
            Description = setting.Display.Description,
            GroupName = setting.Display.GroupName ?? string.Empty,
            Icon = setting.Display.Icon?.Glyph ?? string.Empty,
            IconPack = setting.Display.Icon?.Pack == IconPack.Fluent ? "Fluent" : "Material",
            InputType = inputType,
            IsSelected = currentState.IsEnabled,
            OnText = _localizationService.GetString("Common_On") ?? "On",
            OffText = _localizationService.GetString("Common_Off") ?? "Off",
            ActionButtonText = _localizationService.GetString("Button_Apply") ?? "Apply",
            OptionWarnings = optionWarnings
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

        // Cross-group promo banner text is precomputed by the loading bridge (which holds the old
        // definition) and passed in, so the factory/VM stay off the old model.
        viewModel.CrossGroupInfoMessage = crossGroupInfoMessage;
        viewModel.CompatibilityMessage = compatibilityMessage;

        // Set lock state for advanced settings
        if (setting.Availability.RequiresAdvancedUnlock)
        {
            var unlocked = await _userPreferencesService.GetPreferenceAsync("AdvancedPowerSettingsUnlocked", false);
            viewModel.IsLocked = !unlocked;
        }

        // Populate AC/DC values for PowerModeSupport.Separate settings
        if (viewModel.SupportsSeparateACDC)
        {
            await _enricher.DetectBatteryAsync(viewModel);

            if (inputType == InputType.NumericRange)
            {
                if (currentState.AcValue is int acInt)
                    viewModel.AcNumericValue = ConvertFromSystemUnits(acInt, setting);
                if (currentState.DcValue is int dcInt)
                    viewModel.DcNumericValue = ConvertFromSystemUnits(dcInt, setting);
            }
            // Note: AC/DC Selection values are set AFTER ComboBox options are populated (below)
        }

        if (inputType != InputType.Selection)
        {
            viewModel.SelectedValue = currentState.CurrentValue;
        }

        // Set up numeric range settings
        if (inputType == InputType.NumericRange && setting.Numeric != null)
        {
            viewModel.MaxValue = setting.Numeric.Max;
            viewModel.MinValue = setting.Numeric.Min;
            viewModel.Units = setting.Numeric.Units ?? "";

            if (currentState.CurrentValue is int intValue)
            {
                viewModel.NumericValue = ConvertFromSystemUnits(intValue, setting);
            }
        }

        // Phase 6.7 Slice 7b-ui-3b / Phase 6.8 Cluster C: bind the runtime power-plan dropdown to the new GUID model.
        // The detection overlay threads the runtime-sourced options + active scheme GUID into the result; the VM builds
        // the dropdown directly off them (Value = scheme GUID; the custom PowerPlanComboBox reads the per-item Tag).
        // This build now lives on the VM (TryApplyDynamicPowerPlanOptions) so the SAME code runs on initial load (here)
        // and on refresh (UpdateStateFromSystemState) - the detection-driven path that retires the old
        // RefreshPowerPlanComboBoxAsync / IPowerPlanComboBoxService. Returns false in Builder mode, which keeps the OLD
        // index-valued dropdown below (config-export's index-based BuilderEdit serialization is unchanged).
        var powerPlanHandled = viewModel.TryApplyDynamicPowerPlanOptions(currentState);

        // Builder mode keeps the OLD index-valued power-plan dropdown (config export's index-based BuilderEdit,
        // 6.8 scope). The loading bridge precomputes the options via the old IComboBoxSetupService (it still holds the
        // old definition) and passes the result here; mirror the old unpaired else branch - translate the
        // PowerPlan_ loc keys, then select + banner off the result.
        if (inputType == InputType.Selection
            && setting.OptionSource is not null
            && _viewModelDeps.ApplicationModeService?.CurrentMode == WinhanceMode.Builder
            && builderComboBoxOptions is { } cbr)
        {
            viewModel.ComboBoxOptions.Clear();
            foreach (var option in cbr.Options)
            {
                if (option.DisplayText.StartsWith("PowerPlan_"))
                    option.DisplayText = _localizationService.GetString(option.DisplayText);
                viewModel.ComboBoxOptions.Add(option);
            }
            viewModel.SelectedValue = cbr.SelectedValue ?? currentState.CurrentValue;
            viewModel.UpdateStatusBanner(viewModel.SelectedValue);
            powerPlanHandled = true;
        }

        // Set up combo box options for selection settings
        if (inputType == InputType.Selection && !powerPlanHandled)
        {
            try
            {
                viewModel.ComboBoxOptions.Clear();
                object? resolvedSelection = null;

                if (setting is { States.Count: > 0 })
                {
                    // New-model-native option build: the options come from Setting.States, localized via the
                    // Setting_{id}_Option_{i} keys (loc-key-only; state.Label is the fallback). The current index is
                    // the detection-resolved CurrentValue (1:1 with States, -1 == Custom). Every selection is paired
                    // now, so this is the only path (the old unpaired IComboBoxSetupService fallback is retired).
                    int currentIndex = currentState.CurrentValue is int ci ? ci : ComboBoxConstants.CustomStateIndex;
                    BuildCatalogSelectionOptions(setting, currentIndex, viewModel.ComboBoxOptions);
                    resolvedSelection = currentState.CurrentValue ?? currentIndex;
                }

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
                // (ComboBox needs items before SelectedValue can match). New-model-native AC/DC index: match the
                // typed AC/DC powercfg reading against each option's per-context State value (Set[powerKey]).
                // -1 (Custom) on no match is unreachable (0 orphan / 0 duplicate powercfg option values).
                if (viewModel.SupportsSeparateACDC
                    && setting is { States.Count: > 0 }
                    && setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault() is { } powerTarget)
                {
                    viewModel.AcValue = currentState.AcValue is int acInt
                        ? FindStateIndexForPowerCfgValue(setting, powerTarget.Key, acInt) ?? ComboBoxConstants.CustomStateIndex
                        : 0;
                    viewModel.DcValue = currentState.DcValue is int dcInt
                        ? FindStateIndexForPowerCfgValue(setting, powerTarget.Key, dcInt) ?? ComboBoxConstants.CustomStateIndex
                        : 0;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed to setup combo box for '{setting.Id}': {ex.Message}");
            }
        }
        else if (inputType != InputType.Selection)
        {
            // For non-Selection types, surface the Windows-version compatibility banner here (Selection types get
            // it via UpdateStatusBanner's compat fallback). Mirrors the old InitializeCompatibilityBanner call site.
            viewModel.ShowCompatibilityBanner();
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

    private static int ConvertFromSystemUnits(int systemValue, Setting setting)
    {
        var displayUnits = setting.Numeric?.Units;
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
