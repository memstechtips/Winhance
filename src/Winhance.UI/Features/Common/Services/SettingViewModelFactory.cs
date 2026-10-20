using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Common.TechnicalDetails;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Utilities;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.Common.Services;

public class SettingViewModelFactory : ISettingViewModelFactory
{
    private readonly SettingViewModelDependencies _viewModelDeps;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localizationService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly INewBadgeService _newBadgeService;
    private readonly ISettingViewModelEnricher _enricher;
    private readonly IPowerSettingsQueryService _power;

    public SettingViewModelFactory(
        SettingViewModelDependencies viewModelDeps,
        ILogService logService,
        ILocalizationService localizationService,
        IUserPreferencesService userPreferencesService,
        INewBadgeService newBadgeService,
        ISettingViewModelEnricher enricher,
        IPowerSettingsQueryService power)
    {
        _viewModelDeps = viewModelDeps;
        _logService = logService;
        _localizationService = localizationService;
        _userPreferencesService = userPreferencesService;
        _newBadgeService = newBadgeService;
        _enricher = enricher;
        _power = power;
    }

    public async Task<SettingItemViewModel> CreateAsync(
        Setting setting,
        SettingStateResult currentState,
        ISettingsFeatureViewModel? parentViewModel,
        string? crossGroupInfoMessage,
        string? compatibilityMessage,
        WinBuild build = default)
    {
        InputType inputType = ConfigFileMapper.InputTypeFor(setting);

        var config = new SettingItemViewModelConfig
        {
            Setting = setting,
            Build = build,
            ParentFeatureViewModel = parentViewModel,
            SettingId = setting.Id,
            Name = Localized(setting.Display.Name),
            Description = Localized(setting.Display.Description),
            GroupName = setting.Display.GroupName is { } group ? Localized(group) : string.Empty,
            Icon = setting.Display.Icon?.Glyph ?? string.Empty,
            IconPack = setting.Display.Icon?.Pack.ToString() ?? "Material",
            InputType = inputType,
            IsSelected = currentState.IsEnabled,
            Outcome = currentState.Outcome,
            OnText = inputType == InputType.CheckBox
                ? _localizationService.GetStringOrDefault(TechnicalDetailKeys.Checked, "Checked")
                : _localizationService.GetStringOrDefault("Common_On", "On"),
            OffText = inputType == InputType.CheckBox
                ? _localizationService.GetStringOrDefault(TechnicalDetailKeys.Unchecked, "Unchecked")
                : _localizationService.GetStringOrDefault("Common_Off", "Off"),
            ActionButtonText = _localizationService.GetStringOrDefault("Button_Apply", "Apply"),
            OptionWarnings = BuildCatalogOptionWarnings(setting)
        };

        var viewModel = new SettingItemViewModel(
            config,
            _viewModelDeps.WriteStrategySelector,
            _viewModelDeps.LogService,
            _viewModelDeps.DispatcherService,
            _viewModelDeps.DialogService,
            _localizationService,
            _userPreferencesService,
            _viewModelDeps.RegeditLauncher,
            _newBadgeService,
            _viewModelDeps.ApplicationModeService,
            _viewModelDeps.FilePickerService);

        // Typed fields open read-only; the pencil unlocks them.
        viewModel.IsLocked = setting.Fields.Count > 0;
        viewModel.RecordKnownState(currentState);
        viewModel.CrossGroupInfoMessage = crossGroupInfoMessage;
        viewModel.CompatibilityMessage = compatibilityMessage;
        viewModel.CompatibilitySeverity = setting.Availability.Allows(build) ? InfoBarSeverity.Informational : InfoBarSeverity.Warning;

        if (setting.Availability.RequiresAdvancedUnlock)
        {
            var unlocked = await _userPreferencesService.GetPreferenceAsync("AdvancedPowerSettingsUnlocked", false);
            viewModel.IsLocked = !unlocked;
        }

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
        }

        if (inputType != InputType.Selection)
        {
            viewModel.SelectedValue = currentState.CurrentValue;
        }

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

        if (inputType == InputType.TextBox)
        {
            viewModel.SeedText(currentState.CurrentValue as string ?? setting.TextBox?.Default ?? string.Empty);
        }

        if (inputType == InputType.List)
        {
            viewModel.ListAddLabel = setting.List!.Add is { } add ? Localized(add) : string.Empty;
            viewModel.ListRemoveLabel = setting.List.Remove is { } remove ? Localized(remove) : string.Empty;
            viewModel.SavePasswordsLabel = Localized(LocKey.AutounattendAccounts.SavePasswords);
            viewModel.SavePasswordsNote = Localized(LocKey.AutounattendAccounts.SavePasswordsNote);

            if (currentState.CurrentValue is ChoiceValue.List seeded)
                viewModel.SeedList(seeded);
        }

        viewModel.TryApplyKeyedOptions(currentState);

        // A keyed selection's States are named keys, never indices, so a keyed card with no detected options stays empty.
        if (inputType == InputType.Selection && setting.Control != ControlKind.KeyedSelection)
        {
            try
            {
                viewModel.ComboBoxOptions.Clear();
                object? resolvedSelection = null;

                if (setting is { States.Count: > 0 })
                {
                    int currentIndex = currentState.CurrentValue is int ci ? ci : ComboBoxConstants.CustomStateIndex;
                    BuildCatalogSelectionOptions(setting, viewModel.ComboBoxOptions, await WindowsPowerOptionsAsync(setting));
                    resolvedSelection = currentState.CurrentValue ?? currentIndex;
                }

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

        // Everything above seeded this card from the live machine. If the user already authored
        // this setting in the current session, that is the value the card must show and the value
        // Save will write, so it goes on last - this is the seeding half of the same invariant
        // UpdateStateFromSystemState honours on refresh.
        viewModel.ApplyAuthoredOverlay();

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

    // The list contains ONLY real, choosable options. An unresolved selection (CurrentValue == -1) is shown by the
    // card's outcome overlay, not by a synthetic list entry: a fake "Custom" item made an unreadable value read as
    // a deliberate choice, and it was pickable. An IsDetectOnly state is left out for the same reason from the
    // other direction. SKIP, NEVER RENUMBER: every option keeps its own STATE index as its Value - that index is
    // what the drop-down-closed handler applies, what a saved .winhance config persists, and what the review diff compares.
    // Windows names the choices of a power setting itself, in the language it is installed in, and lists only the
    // ones this machine has: a CPU that cannot boost aggressively does not offer those modes. So a power card shows
    // Windows' own wording and, on the machine it is about to change, drops the states this PC does not offer.
    // Keyed by the possible-setting INDEX, which is what a catalog state carries.
    // An authoring mode keeps every state, because the file is for a PC whose hardware this one cannot speak for.
    // The catalog still owns the values, which is what the apply and the answer file write and a config carries.
    private async Task<IReadOnlyDictionary<int, string>?> WindowsPowerOptionsAsync(Setting setting)
    {
        if (setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault() is not { } target)
            return null;

        try
        {
            var offered = await _power.GetPossibleValuesAsync(target.SubgroupGuid, target.SettingGuid)
                .ConfigureAwait(false);
            return offered.Count > 0
                ? offered.GroupBy(option => option.Index).ToDictionary(group => group.Key, group => group.First().Name)
                : null;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning,
                $"Could not read the options Windows offers for {setting.Id}: {ex.Message}");
            return null;
        }
    }

    private void BuildCatalogSelectionOptions(
        Setting setting,
        ObservableCollection<ComboBoxDisplayOption> options,
        IReadOnlyDictionary<int, string>? windowsOptions = null)
    {
        var states = setting.States;
        var powerKey = setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault()?.Key;
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (state.IsDetectOnly)
                continue;
            string? windowsName = null;
            if (windowsOptions is not null && powerKey is not null)
            {
                if (state.Set.TryGetValue(powerKey, out var stateValue)
                    && stateValue.WritePayload is int index
                    && windowsOptions.TryGetValue(index, out var offeredName))
                {
                    windowsName = offeredName;
                }
                else if (!_viewModelDeps.ApplicationModeService.Capabilities().AuthorsIntent)
                {
                    continue;   // this PC has no such value, and the card is about to change this PC
                }
            }
            var label = windowsName ?? Localized(state.Label);
            var tooltip = state.Tooltip is { } tip ? Localized(tip) : null;
            options.Add(new ComboBoxDisplayOption(label, i, tooltip)
            {
                IsRecommended = state.HasRole(RoleKind.Recommended),
                IsDefault = state.HasRole(RoleKind.WindowsDefault),
                IsSubjectivePreference = setting.Display.IsSubjectivePreference,
            });
        }
    }

    // One entry per State, detect-only ones included: the status banner indexes it by the selected value, the state index.
    private IReadOnlyList<OptionWarning?>? BuildCatalogOptionWarnings(Setting setting)
    {
        if (setting.States.Count == 0)
            return null;

        return setting.States
            .Select(state => state.Warning is { } warning
                ? new OptionWarning(Localized(warning), state.IsDetectOnly)
                : null)
            .ToList();
    }

    private string Localized(LocKey key) => _localizationService.GetStringOrDefault(key.Value, key.Value);

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
}
