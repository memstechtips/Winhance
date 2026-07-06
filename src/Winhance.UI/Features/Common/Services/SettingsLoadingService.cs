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
    private readonly ISettingPreparationPipeline _preparationPipeline;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ISettingViewModelFactory _viewModelFactory;
    private readonly ISettingLocalizationService _settingLocalizationService;
    private readonly IApplicationModeService _applicationModeService;

    public SettingsLoadingService(
        ICatalogSettingStateProvider settingStateProvider,
        ILogService logService,
        IInitializationService initializationService,
        ISettingPreparationPipeline preparationPipeline,
        IUserPreferencesService userPreferencesService,
        ISettingViewModelFactory viewModelFactory,
        ISettingLocalizationService settingLocalizationService,
        IApplicationModeService applicationModeService)
    {
        _settingStateProvider = settingStateProvider;
        _logService = logService;
        _initializationService = initializationService;
        _preparationPipeline = preparationPipeline;
        _userPreferencesService = userPreferencesService;
        _viewModelFactory = viewModelFactory;
        _settingLocalizationService = settingLocalizationService;
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

            var settingsList = _preparationPipeline.PrepareSettings(featureModuleId);

            var settingViewModels = new ObservableCollection<SettingItemViewModel>();

            // Read technical details preference once for all settings
            var showTechnicalDetails = await _userPreferencesService.GetPreferenceAsync(
                Core.Features.Common.Constants.UserPreferenceKeys.ShowTechnicalDetails, false);

            _logService.Log(LogLevel.Debug, $"Getting batch states for {settingsList.Count} settings in {featureModuleId}");
            // Slice 6: the new-engine full-state provider IS the old-discovery+overlay result (completeness-proven:
            // every setting pairs), so it replaces both and retires the observe-only shadow. Custom-state now comes
            // from the typed fields (SettingViewModelFactory rebuilds CapturedCustomStateValues via the reconstructor).
            var batchStates = await _settingStateProvider.GetStatesAsync(settingsList);

            // Create ViewModels for all settings (skip settings whose backing resource doesn't exist)
            foreach (var setting in settingsList)
            {
                if (batchStates.TryGetValue(setting.Id, out var settingState) && !settingState.Success)
                {
                    _logService.Log(LogLevel.Debug, $"Skipping setting '{setting.Id}': {settingState.ErrorMessage}");
                    continue;
                }

                var currentState = batchStates.TryGetValue(setting.Id, out var s) ? s : new SettingStateResult();

                // Bridge to the new model: pair the old SettingDefinition to its catalog Setting (by id, after
                // normalizing the 6 "-win10" ThisPC aliases) and build the VM from that. The catalog is complete,
                // so a missing peer is a real gap - skip the VM rather than crash the page.
                var paired = SettingCatalog.All.FirstOrDefault(c => c.Id == SettingIdAliases.Normalize(setting.Id));
                if (paired is null)
                {
                    _logService.Log(LogLevel.Warning, $"No catalog Setting for '{setting.Id}'; skipping VM.");
                    continue;
                }

                var crossGroupInfoMessage = _settingLocalizationService.BuildCrossGroupInfoMessage(setting);

                // Builder mode keeps the index-valued power-plan dropdown (config export's index-based BuilderEdit).
                // G1b: build it here from the new engine's DynamicOptions (the same runtime options the live GUID-valued
                // dropdown uses), index-valued + the rich PowerPlanComboBoxOption Tag the bespoke control reads -
                // retiring the old IComboBoxSetupService precompute. The factory's builder block localizes the
                // PowerPlan_ DisplayText, so the bridge passes the raw loc key.
                ComboBoxSetupResult? builderComboBoxOptions =
                    (_applicationModeService?.CurrentMode == WinhanceMode.Builder && setting.Recommendation?.LoadDynamicOptions == true)
                        ? BuildBuilderPowerPlanOptions(currentState)
                        : null;

                var viewModel = await _viewModelFactory.CreateAsync(paired, setting.InputType, currentState, parentViewModel, crossGroupInfoMessage, builderComboBoxOptions, setting.VersionCompatibilityMessage);
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

        // The VM no longer carries its SettingDefinition (Phase 6.7 Slice 11). Re-source the definitions for this
        // refresh from the preparation pipeline, keyed by each VM's owning feature module and filtered to the VMs
        // on screen - the same pipeline + filter as the initial load, so the definitions are identical.
        var wantedIds = new HashSet<string>(settingsList.Select(s => s.SettingId));
        var definitions = settingsList
            .Select(s => s.ParentFeatureViewModel?.ModuleId)
            .Where(m => !string.IsNullOrEmpty(m))
            .Distinct()
            .SelectMany(m => _preparationPipeline.PrepareSettings(m!))
            .Where(d => wantedIds.Contains(d.Id))
            .GroupBy(d => d.Id)
            .Select(g => g.First())
            .ToList();

        if (definitions.Count == 0)
            return new Dictionary<string, SettingStateResult>();

        // Slice 6: read from the new-engine full-state provider (drop-in for old discovery + overlay).
        var batchStates = await _settingStateProvider.GetStatesAsync(definitions);

        return batchStates;
    }

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
