using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Features;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Services
{
    public class SettingsLoadingService(
        ISystemSettingsDiscoveryService discoveryService,
        ISettingApplicationService settingApplicationService,
        IEventBus eventBus,
        ILogService logService,
        IComboBoxSetupService comboBoxSetupService,
        IDomainServiceRouter domainServiceRouter,
        ISettingsConfirmationService confirmationService,
        IGlobalSettingsRegistry globalSettingsRegistry,
        IInitializationService initializationService,
        IPowerPlanComboBoxService powerPlanComboBoxService,
        IComboBoxResolver comboBoxResolver,
        IUserPreferencesService userPreferencesService,
        IDialogService dialogService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry) : ISettingsLoadingService
    {

        public async Task<ObservableCollection<object>> LoadConfiguredSettingsAsync<TDomainService>(
            TDomainService domainService,
            string featureModuleId,
            string progressMessage,
            ISettingsFeatureViewModel? parentViewModel = null)
            where TDomainService : class, IDomainService
        {
            try
            {
                logService.Log(LogLevel.Info, $"[SettingsLoadingService] Starting to load settings for '{featureModuleId}'");
                initializationService.StartFeatureInitialization(featureModuleId);

                var settingDefinitions = await domainService.GetSettingsAsync();
                var settingsList = settingDefinitions.ToList();

                domainServiceRouter.AddSettingMappings(featureModuleId, settingsList.Select(s => s.Id));
                globalSettingsRegistry.RegisterSettings(featureModuleId, settingsList);

                var settingViewModels = new ObservableCollection<object>();
                
                logService.Log(LogLevel.Debug, $"Getting batch states for {settingsList.Count} settings in {featureModuleId}");
                var batchStates = await discoveryService.GetSettingStatesAsync(settingsList);
                var comboBoxTasks = new Dictionary<string, Task<(SettingItemViewModel viewModel, bool success)>>();

                // Resolve combo box values for Selection type settings
                foreach (var setting in settingsList.Where(s => s.InputType == InputType.Selection))
                {
                    if (batchStates.TryGetValue(setting.Id, out var state) && state.RawValues != null)
                    {
                        try
                        {
                            var resolvedValue = await comboBoxResolver.ResolveCurrentValueAsync(setting, state.RawValues);
                            state.CurrentValue = resolvedValue;
                        }
                        catch (Exception ex)
                        {
                            logService.Log(LogLevel.Warning, $"Failed to resolve combo box value for '{setting.Id}': {ex.Message}");
                        }
                    }
                }
                
                var comboBoxSettings = settingsList.Where(s => s.InputType == InputType.Selection);
                foreach (var setting in comboBoxSettings)
                {
                    var viewModel = await CreateSettingViewModelAsync(setting, batchStates, parentViewModel);
                    var currentState = batchStates.TryGetValue(setting.Id, out var state) ? state : new SettingStateResult();

                    comboBoxTasks[setting.Id] = Task.Run(async () =>
                    {
                        try
                        {
                            await viewModel.SetupComboBoxAsync(setting, currentState.CurrentValue, comboBoxSetupService, logService, currentState.RawValues);
                            return (viewModel, success: true);
                        }
                        catch
                        {
                            return (viewModel, success: false);
                        }
                    });
                }

                int comboBoxFailures = 0;
                foreach (var setting in settingsList)
                {
                    if (setting.InputType == InputType.Selection)
                    {
                        var (viewModel, success) = await comboBoxTasks[setting.Id];
                        if (!success) comboBoxFailures++;
                        settingViewModels.Add(viewModel);
                    }
                    else
                    {
                        var viewModel = await CreateSettingViewModelAsync(setting, batchStates, parentViewModel);
                        settingViewModels.Add(viewModel);
                    }
                }
                
                if (comboBoxFailures > 0)
                {
                    logService.Log(LogLevel.Warning, $"ComboBox setup failures: {comboBoxFailures}/{comboBoxSettings.Count()} for {featureModuleId}");
                }

                eventBus.Publish(new FeatureComposedEvent(featureModuleId, settingsList));
                logService.Log(LogLevel.Info, $"[SettingsLoadingService] Finished loading {settingViewModels.Count} settings for '{featureModuleId}' - About to complete initialization");
                initializationService.CompleteFeatureInitialization(featureModuleId);
                logService.Log(LogLevel.Info, $"[SettingsLoadingService] Initialization completed for '{featureModuleId}'");
                return settingViewModels;
            }
            catch (Exception ex)
            {
                initializationService.CompleteFeatureInitialization(featureModuleId);
                logService.Log(LogLevel.Error, $"Error loading settings for {featureModuleId}: {ex.Message}");
                throw;
            }
        }

        private async Task<SettingItemViewModel> CreateSettingViewModelAsync(SettingDefinition setting, Dictionary<string, SettingStateResult> batchStates, ISettingsFeatureViewModel? parentViewModel)
        {
            var currentState = batchStates.TryGetValue(setting.Id, out var state) ? state : new SettingStateResult();

            var viewModel = new SettingItemViewModel(settingApplicationService, eventBus, logService, confirmationService, domainServiceRouter, initializationService, comboBoxSetupService, discoveryService, userPreferencesService, dialogService, compatibleSettingsRegistry)
            {
                SettingDefinition = setting,
                ParentFeatureViewModel = parentViewModel,
                SettingId = setting.Id,
                Name = setting.Name,
                Description = setting.Description,
                GroupName = setting.GroupName,
                InputType = setting.InputType,
                Icon = setting.Icon,
                IconPack = setting.IconPack ?? "Material",
                RequiresConfirmation = setting.RequiresConfirmation,
                ConfirmationTitle = setting.ConfirmationTitle,
                ConfirmationMessage = setting.ConfirmationMessage,
                ActionCommandName = setting.ActionCommand,
                IsSelected = currentState.IsEnabled
            };

            if (setting.InputType != InputType.Selection)
            {
                viewModel.SelectedValue = currentState.CurrentValue;
            }

            if (setting.RequiresAdvancedUnlock)
            {
                var unlocked = await userPreferencesService.GetPreferenceAsync<bool>("AdvancedPowerSettingsUnlocked", false);
                viewModel.IsLocked = !unlocked;
            }

            if (setting.InputType == InputType.NumericRange)
            {
                viewModel.SetupNumericUpDown(setting, currentState.CurrentValue, currentState.RawValues);
            }
            else if (setting.InputType == InputType.Toggle)
            {
                viewModel.CompleteInitialization();
            }

            if (setting.CustomProperties?.TryGetValue(
                Core.Features.Common.Constants.CustomPropertyKeys.VersionCompatibilityMessage, out var compatMessage) == true &&
                compatMessage is string messageText)
            {
                viewModel.WarningText = messageText;
            }

            viewModel.IsRegistryValueNotSet = currentState.IsRegistryValueNotSet;

            return viewModel;
        }
    }
}
