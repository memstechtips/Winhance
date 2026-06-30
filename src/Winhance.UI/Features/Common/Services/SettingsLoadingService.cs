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
    private readonly ISystemSettingsDiscoveryService _discoveryService;
    private readonly ILogService _logService;
    private readonly IInitializationService _initializationService;
    private readonly ISettingPreparationPipeline _preparationPipeline;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ISettingViewModelFactory _viewModelFactory;
    private readonly IDetectionShadowRunner _shadowRunner;
    private readonly ICatalogDetectionService _catalogDetectionService;
    private readonly ISettingLocalizationService _settingLocalizationService;
    private readonly IComboBoxSetupService _comboBoxSetupService;
    private readonly IApplicationModeService _applicationModeService;

    public SettingsLoadingService(
        ISystemSettingsDiscoveryService discoveryService,
        ILogService logService,
        IInitializationService initializationService,
        ISettingPreparationPipeline preparationPipeline,
        IUserPreferencesService userPreferencesService,
        ISettingViewModelFactory viewModelFactory,
        IDetectionShadowRunner shadowRunner,
        ICatalogDetectionService catalogDetectionService,
        ISettingLocalizationService settingLocalizationService,
        IComboBoxSetupService comboBoxSetupService,
        IApplicationModeService applicationModeService)
    {
        _discoveryService = discoveryService;
        _logService = logService;
        _initializationService = initializationService;
        _preparationPipeline = preparationPipeline;
        _userPreferencesService = userPreferencesService;
        _viewModelFactory = viewModelFactory;
        _shadowRunner = shadowRunner;
        _catalogDetectionService = catalogDetectionService;
        _settingLocalizationService = settingLocalizationService;
        _comboBoxSetupService = comboBoxSetupService;
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
            var batchStates = await _discoveryService.GetSettingStatesAsync(settingsList);

            // Observe-only shadow of the new catalog detection engine (no-op unless explicitly enabled). The
            // selection baseline is GetSettingStatesAsync's resolved option index (its ResolveRawValuesToIndex),
            // the same value the UI consumes - the redundant IComboBoxResolver re-resolution was retired (G1a).
            await _shadowRunner.RunAsync(settingsList, batchStates);

            // Detection cutover: the new catalog engine decides each setting's primary state (toggle on/off,
            // selection option). Auxiliary data (RawValues, TooltipData, AC/DC) stays as the old discovery read it.
            await OverlayCatalogStatesAsync(settingsList, batchStates);

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

                var optionWarnings = setting.ComboBox?.Options?.Select(o => o.Warning).ToList();
                var crossGroupInfoMessage = _settingLocalizationService.BuildCrossGroupInfoMessage(setting);

                // Builder mode keeps the old index-valued power-plan dropdown (config export's index-based BuilderEdit,
                // 6.8 scope). Precompute its options here in the bridge - we still hold the SettingDefinition the old
                // IComboBoxSetupService needs - and hand the result to the factory.
                ComboBoxSetupResult? builderComboBoxOptions =
                    (_applicationModeService?.CurrentMode == WinhanceMode.Builder && setting.Recommendation?.LoadDynamicOptions == true)
                        ? await _comboBoxSetupService.SetupComboBoxOptionsAsync(setting, currentState.CurrentValue)
                        : null;

                var viewModel = await _viewModelFactory.CreateAsync(paired, setting.InputType, currentState, parentViewModel, optionWarnings, crossGroupInfoMessage, builderComboBoxOptions, setting.VersionCompatibilityMessage);
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

        var batchStates = await _discoveryService.GetSettingStatesAsync(definitions);

        // Observe-only shadow of the new catalog detection engine (no-op unless explicitly enabled). Selection
        // indices come from GetSettingStatesAsync (the redundant IComboBoxResolver re-resolution was retired - G1a).
        await _shadowRunner.RunAsync(definitions, batchStates);

        // Detection cutover: the new catalog engine decides each setting's primary state.
        await OverlayCatalogStatesAsync(definitions, batchStates);

        return batchStates;
    }

    /// <summary>
    /// Overlays the new catalog detection engine's authoritative primary state (a toggle's on/off, a selection's
    /// chosen option index) onto the batch states the UI consumes, for every setting that has a catalog peer. The
    /// old result's auxiliary data (RawValues, TooltipData, the AC/DC split) is preserved, and unpaired settings keep
    /// their old state. Any failure is logged and leaves the old states in place, so detection never hard-fails a page.
    /// </summary>
    private async Task OverlayCatalogStatesAsync(
        IReadOnlyList<SettingDefinition> definitions,
        Dictionary<string, SettingStateResult> batchStates)
    {
        await CatalogDetectionOverlayHelper.OverlayAsync(definitions, batchStates, _catalogDetectionService, _logService);
    }
}
