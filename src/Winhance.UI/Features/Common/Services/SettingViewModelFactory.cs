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
        SettingStateResult currentState,
        ISettingsFeatureViewModel? parentViewModel,
        string? crossGroupInfoMessage,
        ComboBoxSetupResult? builderComboBoxOptions,
        string? compatibilityMessage,
        WinBuild build = default)
    {
        // Derive the InputType from the catalog Setting's Control; it feeds the dispatch below and
        // config.InputType.
        var inputType = ControlToInputType(setting.Control);

        var config = new SettingItemViewModelConfig
        {
            Setting = setting,
            Build = build,
            ParentFeatureViewModel = parentViewModel,
            SettingId = setting.Id,
            // Localize the catalog Display fields via the canonical keys (raw Display fallback).
            Name = LocalizeOrFallback($"Setting_{setting.Id}_Name", setting.Display.Name) ?? setting.Display.Name,
            Description = LocalizeOrFallback($"Setting_{setting.Id}_Description", setting.Display.Description) ?? setting.Display.Description,
            GroupName = setting.Display.GroupName != null ? LocalizeGroupName(setting.Display.GroupName) : string.Empty,
            Icon = setting.Display.Icon?.Glyph ?? string.Empty,
            IconPack = setting.Display.Icon?.Pack == IconPack.Fluent ? "Fluent" : "Material",
            InputType = inputType,
            IsSelected = currentState.IsEnabled,
            Outcome = currentState.Outcome,
            OnText = _localizationService.GetString("Common_On") ?? "On",
            OffText = _localizationService.GetString("Common_Off") ?? "Off",
            ActionButtonText = _localizationService.GetString("Button_Apply") ?? "Apply",
            OptionWarnings = BuildCatalogOptionWarnings(setting)
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

        // Cross-group promo banner text is precomputed by the loading bridge and passed in.
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

        // Bind the runtime power-plan dropdown to the GUID model. Detection threads the runtime-sourced
        // options + active scheme GUID into the result; the VM builds the dropdown directly off them (Value =
        // scheme GUID; the custom PowerPlanComboBox reads the per-item Tag). This build lives on the VM
        // (TryApplyDynamicPowerPlanOptions) so the SAME code runs on initial load (here) and on refresh
        // (UpdateStateFromSystemState). Returns false in Builder mode, which keeps the index-valued dropdown below.
        var powerPlanHandled = viewModel.TryApplyDynamicPowerPlanOptions(currentState);

        // Builder mode keeps the index-valued power-plan dropdown. The loading bridge precomputes the options
        // and passes the result here; translate the PowerPlan_ loc keys, then select + banner off the result.
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
                    // Option build: the options come from Setting.States, localized via the
                    // Setting_{id}_Option_{i} keys (loc-key-only; state.Label is the fallback). The current index
                    // is the detection-resolved CurrentValue (1:1 with States, -1 == Custom).
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
                    && customSelIdx == ComboBoxConstants.CustomStateIndex)
                {
                    // Rebuild the captured custom-state from the typed fields.
                    var captured = CustomStateValueReconstructor.Build(setting, currentState)
                        .Where(kv => kv.Value != null)
                        .ToDictionary(kv => kv.Key, kv => kv.Value!);
                    if (captured.Count > 0)
                        viewModel.CapturedCustomStateValues = captured;
                }

                // Resolve AC/DC Selection values AFTER ComboBox options are populated
                // (ComboBox needs items before SelectedValue can match). AC/DC index: match the
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
            // it via UpdateStatusBanner's compat fallback).
            viewModel.ShowCompatibilityBanner();
        }

        // If in review mode, apply review diff to the newly created ViewModel
        _enricher.ApplyReviewDiff(viewModel, currentState);

        // Compute initial badge state after all values are populated
        viewModel.ComputeBadgeState();

        // Initial Custom-state banner (Informational; a compatibility Warning applied above outranks it).
        viewModel.UpdateDetectionOutcomeBanner();

        // Build the technical-details panel from the Setting model + the now-populated current state
        // (the panel is VM-driven, not TooltipUpdatedEvent-driven).
        viewModel.RefreshTechnicalDetails();

        return viewModel;
    }

    private static int ConvertFromSystemUnits(int systemValue, Setting setting)
    {
        var displayUnits = setting.Numeric?.Units;
        return UnitConversionHelper.ConvertFromSystemUnits(systemValue, displayUnits);
    }

    /// <summary>
    /// Builds a selection's combobox options from the <see cref="Setting"/> model:
    /// one option per <see cref="SettingState"/>, localized via the canonical Setting_{id}_Option_{i}
    /// (and _OptionTooltip_{i}) keys with <c>state.Label</c> as the fallback, and the recommended/default
    /// flags derived from the state's roles. Appends the synthetic "Custom" option (Setting_{id}_Option_Custom
    /// or the generic Common_CustomState) when the current index is the Custom sentinel.
    /// </summary>
    private void BuildCatalogSelectionOptions(Setting setting, int currentIndex, ObservableCollection<ComboBoxDisplayOption> options)
    {
        var states = setting.States;
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            // When the state Label is itself a shared localization key
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

    // Per-option warnings sourced from the catalog Setting's States. Localized via the canonical
    // Setting_{id}_OptionWarning_{i} key with the raw state.Warning as the fallback. Index-aligned with the
    // options BuildCatalogSelectionOptions builds (one per State), which is how the status banner indexes the
    // list. Null for a stateless setting (e.g. the dynamic power-plan).
    private IReadOnlyList<string?>? BuildCatalogOptionWarnings(Setting setting)
    {
        if (setting.States.Count == 0)
            return null;

        var warnings = new List<string?>(setting.States.Count);
        for (int i = 0; i < setting.States.Count; i++)
        {
            var raw = setting.States[i].Warning;
            warnings.Add(string.IsNullOrEmpty(raw)
                ? raw
                : LocalizeOrFallback($"Setting_{setting.Id}_OptionWarning_{i}", raw));
        }
        return warnings;
    }

    // Resolves the localized group name: try the compacted group key (SettingGroup_{name without
    // spaces/ampersands}); if it resolves use it, else fall back to the snake-case key with the raw group name
    // as the final fallback. Keyed off the group NAME.
    private string LocalizeGroupName(string groupName)
    {
        var compact = LocalizeOrFallback(SettingLocalizationKeys.GroupCompact(groupName), null);
        if (compact != null)
            return compact;
        return LocalizeOrFallback(SettingLocalizationKeys.GroupSnake(groupName), groupName) ?? groupName;
    }

    // Returns the localized string for the key, or the fallback when the key is missing
    // (ILocalizationService.GetString returns the "[key]" marker on a miss).
    private string? LocalizeOrFallback(string key, string? fallback)
    {
        var s = _localizationService.GetString(key);
        return (s.Length >= 2 && s[0] == '[' && s[^1] == ']') ? fallback : s;
    }

    // Maps a raw powercfg value (the AC or DC reading) to the State index whose Set[powerKey] accepts it, for
    // a separate-AC/DC powercfg selection. Returns null when no option matches (treated as Custom).
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

    /// <summary>Twin of ConfigExportService / ConfigReviewService / AutounattendXmlGeneratorService
    /// .ControlToInputType: derives the VM-facing InputType from the catalog Setting's derived Control.</summary>
    private static InputType ControlToInputType(ControlKind control) => control switch
    {
        ControlKind.Selection or ControlKind.PowerPlan => InputType.Selection,
        ControlKind.Slider => InputType.NumericRange,
        ControlKind.Action => InputType.Action,
        _ => InputType.Toggle,
    };
}
