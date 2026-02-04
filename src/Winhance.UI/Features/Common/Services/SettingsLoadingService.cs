using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Features;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Service for loading settings and creating ViewModels.
/// </summary>
public class SettingsLoadingService : ISettingsLoadingService
{
    private readonly ISystemSettingsDiscoveryService _discoveryService;
    private readonly ISettingApplicationService _settingApplicationService;
    private readonly IEventBus _eventBus;
    private readonly ILogService _logService;
    private readonly IComboBoxSetupService _comboBoxSetupService;
    private readonly IDomainServiceRouter _domainServiceRouter;
    private readonly IInitializationService _initializationService;
    private readonly IComboBoxResolver _comboBoxResolver;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly ISettingLocalizationService _settingLocalizationService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILocalizationService _localizationService;
    private readonly IDialogService _dialogService;

    public SettingsLoadingService(
        ISystemSettingsDiscoveryService discoveryService,
        ISettingApplicationService settingApplicationService,
        IEventBus eventBus,
        ILogService logService,
        IComboBoxSetupService comboBoxSetupService,
        IDomainServiceRouter domainServiceRouter,
        IInitializationService initializationService,
        IComboBoxResolver comboBoxResolver,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        ISettingLocalizationService settingLocalizationService,
        IDispatcherService dispatcherService,
        ILocalizationService localizationService,
        IDialogService dialogService)
    {
        _discoveryService = discoveryService;
        _settingApplicationService = settingApplicationService;
        _eventBus = eventBus;
        _logService = logService;
        _comboBoxSetupService = comboBoxSetupService;
        _domainServiceRouter = domainServiceRouter;
        _initializationService = initializationService;
        _comboBoxResolver = comboBoxResolver;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _settingLocalizationService = settingLocalizationService;
        _dispatcherService = dispatcherService;
        _localizationService = localizationService;
        _dialogService = dialogService;
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

            _logService.Log(LogLevel.Debug, $"Getting batch states for {settingsList.Count} settings in {featureModuleId}");
            var batchStates = await _discoveryService.GetSettingStatesAsync(settingsList);
            var comboBoxTasks = new Dictionary<string, Task<(SettingItemViewModel viewModel, bool success)>>();

            // Resolve combo box values for Selection type settings
            foreach (var setting in settingsList.Where(s => s.InputType == InputType.Selection))
            {
                if (batchStates.TryGetValue(setting.Id, out var state) && state.RawValues != null)
                {
                    try
                    {
                        var resolvedValue = await _comboBoxResolver.ResolveCurrentValueAsync(setting, state.RawValues);
                        state.CurrentValue = resolvedValue;
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to resolve combo box value for '{setting.Id}': {ex.Message}");
                    }
                }
            }

            // Create ViewModels for all settings
            foreach (var setting in settingsList)
            {
                var viewModel = await CreateSettingViewModelAsync(setting, batchStates, parentViewModel);
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

    public async Task<SettingItemViewModel> CreateSettingViewModelAsync(
        SettingDefinition setting,
        Dictionary<string, SettingStateResult> batchStates,
        ISettingsFeatureViewModel? parentViewModel)
    {
        var currentState = batchStates.TryGetValue(setting.Id, out var state) ? state : new SettingStateResult();

        var viewModel = new SettingItemViewModel(
            _settingApplicationService,
            _logService,
            _dispatcherService,
            _dialogService,
            _localizationService)
        {
            SettingDefinition = setting,
            ParentFeatureViewModel = parentViewModel,
            SettingId = setting.Id,
            Name = setting.Name,
            Description = setting.Description,
            GroupName = setting.GroupName ?? string.Empty,
            Icon = setting.Icon ?? string.Empty,
            IconPack = setting.IconPack ?? "Material",
            InputType = setting.InputType,
            IsSelected = currentState.IsEnabled,
            OnText = _localizationService.GetString("Common_On") ?? "On",
            OffText = _localizationService.GetString("Common_Off") ?? "Off",
            ActionButtonText = _localizationService.GetString("Dialog_Button_Apply") ?? "Apply"
        };

        if (setting.InputType != InputType.Selection)
        {
            viewModel.SelectedValue = currentState.CurrentValue;
        }

        // Set up numeric range settings
        if (setting.InputType == InputType.NumericRange && setting.CustomProperties != null)
        {
            viewModel.MaxValue = setting.CustomProperties.TryGetValue("MaxValue", out var max) ? (int)max : int.MaxValue;
            viewModel.MinValue = setting.CustomProperties.TryGetValue("MinValue", out var min) ? (int)min : 0;
            viewModel.Units = setting.CustomProperties.TryGetValue("Units", out var units) ? (string)units : "";

            if (currentState.CurrentValue is int intValue)
            {
                viewModel.NumericValue = intValue;
            }
        }

        // Set up combo box options for selection settings
        if (setting.InputType == InputType.Selection)
        {
            try
            {
                var comboBoxResult = await _comboBoxSetupService.SetupComboBoxOptionsAsync(setting, currentState.CurrentValue);
                viewModel.ComboBoxOptions.Clear();
                foreach (var option in comboBoxResult.Options)
                {
                    viewModel.ComboBoxOptions.Add(option);
                }

                // Set the selected value from the setup result or current state
                if (comboBoxResult.SelectedValue != null)
                {
                    viewModel.SelectedValue = comboBoxResult.SelectedValue;
                }
                else if (currentState.CurrentValue != null)
                {
                    viewModel.SelectedValue = currentState.CurrentValue;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed to setup combo box for '{setting.Id}': {ex.Message}");
            }
        }

        return viewModel;
    }
}
