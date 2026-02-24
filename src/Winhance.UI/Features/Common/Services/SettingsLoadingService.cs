using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Features;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
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
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly ISettingLocalizationService _settingLocalizationService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly SettingViewModelFactory _viewModelFactory;

    public SettingsLoadingService(
        ISystemSettingsDiscoveryService discoveryService,
        IEventBus eventBus,
        ILogService logService,
        IInitializationService initializationService,
        IComboBoxResolver comboBoxResolver,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        ISettingLocalizationService settingLocalizationService,
        IUserPreferencesService userPreferencesService,
        SettingViewModelFactory viewModelFactory)
    {
        _discoveryService = discoveryService;
        _eventBus = eventBus;
        _logService = logService;
        _initializationService = initializationService;
        _comboBoxResolver = comboBoxResolver;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _settingLocalizationService = settingLocalizationService;
        _userPreferencesService = userPreferencesService;
        _viewModelFactory = viewModelFactory;
    }

    public async Task<ObservableCollection<object>> LoadConfiguredSettingsAsync<TDomainService>(
        TDomainService domainService,
        string featureModuleId,
        string progressMessage,
        ISettingsFeatureViewModel? parentViewModel = null)
        where TDomainService : class, IDomainService
    {
        try
        {
            _logService.Log(LogLevel.Info, $"[SettingsLoadingService] Starting to load settings for '{featureModuleId}'");
            _initializationService.StartFeatureInitialization(featureModuleId);

            var settingDefinitions = _compatibleSettingsRegistry.GetFilteredSettings(featureModuleId);
            var localizedSettings = settingDefinitions.Select(s => _settingLocalizationService.LocalizeSetting(s));
            var settingsList = localizedSettings.ToList();

            var settingViewModels = new ObservableCollection<object>();

            // Read technical details preference once for all settings
            var showTechnicalDetails = await _userPreferencesService.GetPreferenceAsync(
                Core.Features.Common.Constants.UserPreferenceKeys.ShowTechnicalDetails, false);

            _logService.Log(LogLevel.Debug, $"Getting batch states for {settingsList.Count} settings in {featureModuleId}");
            var batchStates = await _discoveryService.GetSettingStatesAsync(settingsList);

            // Resolve combo box values for Selection type settings
            foreach (var setting in settingsList.Where(s => s.InputType == InputType.Selection))
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

            _eventBus.Publish(new FeatureComposedEvent(featureModuleId, settingsList));
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
        foreach (var setting in definitions.Where(s => s.InputType == InputType.Selection))
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

        return batchStates;
    }
}
