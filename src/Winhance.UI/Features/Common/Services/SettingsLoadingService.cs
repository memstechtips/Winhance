using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Services;

public class SettingsLoadingService : ISettingsLoadingService
{
    private readonly ICatalogSettingStateProvider _settingStateProvider;
    private readonly ILogService _logService;
    private readonly IInitializationService _initializationService;
    private readonly ICatalogSettingsRegistry _catalogSettingsRegistry;
    private readonly IWindowsVersionFilterService _windowsVersionFilterService;
    private readonly IWindowsVersionService _windowsVersionService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ISettingViewModelFactory _viewModelFactory;
    private readonly ISettingLocalizationService _settingLocalizationService;
    private readonly ILocalizationService _localization;
    private readonly IApplicationModeService _applicationModeService;

    public SettingsLoadingService(
        ICatalogSettingStateProvider settingStateProvider,
        ILogService logService,
        IInitializationService initializationService,
        ICatalogSettingsRegistry catalogSettingsRegistry,
        IWindowsVersionFilterService windowsVersionFilterService,
        IWindowsVersionService windowsVersionService,
        IUserPreferencesService userPreferencesService,
        ISettingViewModelFactory viewModelFactory,
        ISettingLocalizationService settingLocalizationService,
        ILocalizationService localization,
        IApplicationModeService applicationModeService)
    {
        _settingStateProvider = settingStateProvider;
        _logService = logService;
        _initializationService = initializationService;
        _catalogSettingsRegistry = catalogSettingsRegistry;
        _windowsVersionFilterService = windowsVersionFilterService;
        _windowsVersionService = windowsVersionService;
        _userPreferencesService = userPreferencesService;
        _viewModelFactory = viewModelFactory;
        _settingLocalizationService = settingLocalizationService;
        _localization = localization;
        _applicationModeService = applicationModeService;
    }

    public async Task<ObservableCollection<SettingItemViewModel>> LoadConfiguredSettingsAsync(
        string featureModuleId,
        string progressMessage,
        ISettingsFeatureViewModel? parentViewModel = null)
    {
        try
        {
            _logService.Log(LogLevel.Info, $"[SettingsLoadingService] Starting to load settings for '{featureModuleId}'");
            _initializationService.StartFeatureInitialization(featureModuleId);

            var settingsList = _catalogSettingsRegistry.GetByFeature(featureModuleId, includeOtherOsVersions: !_windowsVersionFilterService.IsFilterEnabled);

            var settingViewModels = new ObservableCollection<SettingItemViewModel>();

            // Read technical details preference once for all settings
            var showTechnicalDetails = await _userPreferencesService.GetPreferenceAsync(
                Core.Features.Common.Constants.UserPreferenceKeys.ShowTechnicalDetails, false);

            _logService.Log(LogLevel.Debug, $"Getting batch states for {settingsList.Count} settings in {featureModuleId}");
            // Slice 6: the new-engine full-state provider IS the old-discovery+overlay result, so it replaces both
            // and retires the observe-only shadow. Custom-state now comes from the typed fields
            // (SettingViewModelFactory rebuilds CapturedCustomStateValues via the reconstructor).
            var batchStates = await _settingStateProvider.GetStatesAsync(settingsList);

            // L4b: the compatibility message is derived from the catalog Availability against the live build
            // (AvailabilityCompatibility reproduces the retired def decoration); read the build once per load.
            var liveBuild = LiveBuild();

            // Create ViewModels for all settings (skip settings whose backing resource doesn't exist)
            foreach (var setting in settingsList)
            {
                if (batchStates.TryGetValue(setting.Id, out var settingState) && !settingState.Success)
                {
                    _logService.Log(LogLevel.Debug, $"Skipping setting '{setting.Id}': {settingState.ErrorMessage}");
                    continue;
                }

                var currentState = batchStates.TryGetValue(setting.Id, out var s) ? s : new SettingStateResult();

                var crossGroupInfoMessage = _settingLocalizationService.BuildCrossGroupInfoMessage(setting);

                // Builder mode keeps the index-valued power-plan dropdown (config export's index-based BuilderEdit).
                // G1b: build it here from the new engine's DynamicOptions (the same runtime options the live GUID-valued
                // dropdown uses), index-valued + the rich PowerPlanComboBoxOption Tag the bespoke control reads -
                // retiring the old IComboBoxSetupService precompute. The factory's builder block localizes the
                // PowerPlan_ DisplayText, so this service passes the raw loc key.
                ComboBoxSetupResult? builderComboBoxOptions =
                    (_applicationModeService?.CurrentMode == WinhanceMode.Builder && setting.OptionSource is not null)
                        ? BuildBuilderPowerPlanOptions(currentState)
                        : null;

                var viewModel = await _viewModelFactory.CreateAsync(setting, currentState, parentViewModel, crossGroupInfoMessage, builderComboBoxOptions, LocalizeCompatibilityMessage(AvailabilityCompatibility.DeriveCompatibilityMessage(setting.Availability, liveBuild)));
                viewModel.IsTechnicalDetailsGloballyVisible = showTechnicalDetails;
                settingViewModels.Add(viewModel);
            }

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

    public async Task<Dictionary<string, SettingStateResult>> RefreshSettingStatesAsync(
        IEnumerable<SettingItemViewModel> settings)
    {
        var settingsList = settings.ToList();

        // The VM no longer carries its setting model (Phase 6.7 Slice 11). Re-source the catalog Settings for this
        // refresh from the catalog registry, keyed by each VM's owning feature module and filtered to the VMs
        // on screen - the same registry + scope as the initial load, so the settings are identical.
        var wantedIds = new HashSet<string>(settingsList.Select(s => s.SettingId));
        var catalogSettings = settingsList
            .Select(s => s.ParentFeatureViewModel?.ModuleId)
            .Where(m => !string.IsNullOrEmpty(m))
            .Distinct()
            .SelectMany(m => _catalogSettingsRegistry.GetByFeature(m!, includeOtherOsVersions: !_windowsVersionFilterService.IsFilterEnabled))
            .Where(c => wantedIds.Contains(c.Id))
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .ToList();

        if (catalogSettings.Count == 0)
            return new Dictionary<string, SettingStateResult>();

        // Slice 6: read from the new-engine full-state provider (drop-in for old discovery + overlay).
        var batchStates = await _settingStateProvider.GetStatesAsync(catalogSettings);

        return batchStates;
    }

    // Slice B2/L4b: the compatibility message arrives raw from the catalog derivation
    // (AvailabilityCompatibility.DeriveCompatibilityMessage; format: "Compatibility_Key|Arg1|Arg2..."). Localize it
    // here before passing it to the factory - lifted verbatim from the retired LocalizeSetting. A non-Compatibility_
    // message (incl. null) is returned unchanged, exactly as the old service left it.
    private string? LocalizeCompatibilityMessage(string? message)
    {
        if (message is { } compatKey && compatKey.StartsWith("Compatibility_"))
        {
            var parts = compatKey.Split('|');
            var key = parts[0];
            if (parts.Length > 1)
            {
                var args = parts.Skip(1).ToArray();
                try
                {
                    var format = _localization.GetString(key);
                    return string.Format(format, args);
                }
                catch
                {
                    return _localization.GetString(key);
                }
            }
            return _localization.GetString(key);
        }
        return message;
    }

    /// <summary>The live Windows build for compatibility-message derivation. Read ONCE per load (cached in a
    /// local before the VM loop), not per setting.</summary>
    private WinBuild LiveBuild() =>
        new(_windowsVersionService.GetWindowsBuildNumber(), _windowsVersionService.GetWindowsBuildRevision());

    /// <summary>
    /// Builds the Builder-mode power-plan dropdown (INDEX-valued, for config-export's index-based BuilderEdit) from the
    /// new engine's runtime options, retiring the old IComboBoxSetupService precompute. Faithful to
    /// PowerPlanComboBoxService.SetupPowerPlanComboBoxAsync: PowerPlanOptions.Build (which produces these DynamicOptions)
    /// reproduces the old GetPowerPlanOptionsAsync option set + OrderBy(label) sort, so each option's list index equals
    /// the old option Index. The rich PowerPlanComboBoxOption Tag mirrors
    /// SettingItemViewModel.TryApplyDynamicPowerPlanOptions (the live dropdown the bespoke PowerPlanComboBox control
    /// already reads): ExistsOnSystem/IsActive drive the control visuals, SystemPlan.Guid is the delete target, and the
    /// option's DisplayName (the raw PowerPlan_ loc key) is re-localized by the delete dialog; SystemPlan.Name is not
    /// consumed. DisplayText stays the raw loc key - the factory's builder block localizes it. Returns an empty (but
    /// non-null) result when there are no runtime options, matching the old service's empty-result contract.
    /// </summary>
    private static ComboBoxSetupResult BuildBuilderPowerPlanOptions(SettingStateResult state)
    {
        var result = new ComboBoxSetupResult { Success = true };
        if (state.DynamicOptions is not { } dynamicOptions)
            return result;

        int activeIndex = 0;
        bool foundActive = false;
        for (int i = 0; i < dynamicOptions.Count; i++)
        {
            var opt = dynamicOptions[i];
            var isActive = state.DynamicSelection != null
                && string.Equals(opt.Value, state.DynamicSelection, StringComparison.OrdinalIgnoreCase);
            // FIRST match wins, matching the old GetCurrentPowerPlanIndexAsync's `return i` (each option's Tag still
            // carries its own per-option isActive below).
            if (isActive && !foundActive)
            {
                activeIndex = i;
                foundActive = true;
            }

            var tag = new PowerPlanComboBoxOption
            {
                DisplayName = opt.Label,
                ExistsOnSystem = opt.ExistsOnSystem,
                IsActive = isActive,
                SystemPlan = opt.ExistsOnSystem
                    ? new Winhance.Core.Features.Optimize.Models.PowerPlan { Guid = opt.Value, Name = opt.Label, IsActive = isActive }
                    : null,
            };

            // Value = the option index (BuilderEdit serializes the int index); DisplayText = the raw PowerPlan_ loc key.
            result.Options.Add(new ComboBoxDisplayOption(
                opt.Label,
                i,
                opt.ExistsOnSystem ? "Installed on system" : "Not installed",
                tag));
        }

        result.SelectedValue = activeIndex;
        return result;
    }
}
