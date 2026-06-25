using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Services;

public class SettingsLoadingService : ISettingsLoadingService
{
    private readonly ISystemSettingsDiscoveryService _discoveryService;
    private readonly IEventBus _eventBus;
    private readonly ILogService _logService;
    private readonly IInitializationService _initializationService;
    private readonly IComboBoxResolver _comboBoxResolver;
    private readonly ISettingPreparationPipeline _preparationPipeline;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ISettingViewModelFactory _viewModelFactory;
    private readonly IDetectionShadowRunner _shadowRunner;
    private readonly ICatalogDetectionService _catalogDetectionService;

    public SettingsLoadingService(
        ISystemSettingsDiscoveryService discoveryService,
        IEventBus eventBus,
        ILogService logService,
        IInitializationService initializationService,
        IComboBoxResolver comboBoxResolver,
        ISettingPreparationPipeline preparationPipeline,
        IUserPreferencesService userPreferencesService,
        ISettingViewModelFactory viewModelFactory,
        IDetectionShadowRunner shadowRunner,
        ICatalogDetectionService catalogDetectionService)
    {
        _discoveryService = discoveryService;
        _eventBus = eventBus;
        _logService = logService;
        _initializationService = initializationService;
        _comboBoxResolver = comboBoxResolver;
        _preparationPipeline = preparationPipeline;
        _userPreferencesService = userPreferencesService;
        _viewModelFactory = viewModelFactory;
        _shadowRunner = shadowRunner;
        _catalogDetectionService = catalogDetectionService;
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

            // Resolve combo box values for Selection type settings
            await ResolveComboBoxStatesAsync(settingsList, batchStates);

            // Observe-only shadow of the new catalog detection engine (no-op unless explicitly enabled). Runs
            // after combo-box resolution so the selection baseline is the same value the UI consumes.
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
                var viewModel = await _viewModelFactory.CreateAsync(setting, currentState, parentViewModel);
                viewModel.IsTechnicalDetailsGloballyVisible = showTechnicalDetails;
                settingViewModels.Add(viewModel);
            }

            // Publish tooltip updates from the already-read state data (no second registry read)
            foreach (var kvp in batchStates)
            {
                if (kvp.Value.TooltipData != null)
                {
                    _eventBus.Publish(new TooltipUpdatedEvent(kvp.Key, kvp.Value.TooltipData));
                }
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
        var definitions = settingsList
            .Where(s => s.SettingDefinition != null)
            .Select(s => s.SettingDefinition!)
            .ToList();

        if (definitions.Count == 0)
            return new Dictionary<string, SettingStateResult>();

        var batchStates = await _discoveryService.GetSettingStatesAsync(definitions);

        // Resolve combo box values for Selection type settings
        await ResolveComboBoxStatesAsync(definitions, batchStates);

        // Observe-only shadow of the new catalog detection engine (no-op unless explicitly enabled).
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
        try
        {
            var ids = new HashSet<string>(definitions.Select(d => d.Id));
            var pairedSettings = SettingCatalog.All.Where(s => ids.Contains(s.Id)).ToList();
            if (pairedSettings.Count == 0)
                return;

            var newResults = await _catalogDetectionService.DetectAsync(pairedSettings);

            foreach (var def in definitions)
            {
                if (!batchStates.TryGetValue(def.Id, out var oldState))
                    continue;
                newResults.TryGetValue(def.Id, out var newResult);
                batchStates[def.Id] = CatalogDetectionStateOverlay.Apply(def, oldState, newResult);
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning,
                $"[SettingsLoadingService] Catalog detection overlay failed (keeping old states): {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves combo box values for all Selection-type settings in the batch.
    /// </summary>
    private async Task ResolveComboBoxStatesAsync(
        IEnumerable<SettingDefinition> settings,
        Dictionary<string, SettingStateResult> batchStates)
    {
        foreach (var setting in settings.Where(s => s.InputType == InputType.Selection))
        {
            if (batchStates.TryGetValue(setting.Id, out var state) && state.RawValues != null)
            {
                try
                {
                    var resolvedValue = await _comboBoxResolver.ResolveCurrentValueAsync(setting, state.RawValues as Dictionary<string, object?>);
                    batchStates[setting.Id] = state with { CurrentValue = resolvedValue };
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Failed to resolve combo box value for '{setting.Id}': {ex.Message}");
                }
            }
        }
    }
}
