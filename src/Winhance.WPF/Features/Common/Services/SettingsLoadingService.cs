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
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Service for loading and setting up feature settings across ViewModels.
    /// Centralizes the common loading logic to eliminate duplication.
    /// Lives in WPF layer as it creates UI objects (SettingItemViewModels).
    /// </summary>
    public class SettingsLoadingService : ISettingsLoadingService
    {
        private readonly ISettingApplicationService _settingApplicationService;
        private readonly ITaskProgressService _progressService;
        private readonly IEventBus _eventBus;
        private readonly ILogService _logService;
        private readonly IComboBoxSetupService _comboBoxSetupService;
        private readonly IDomainServiceRouter _domainServiceRouter;

        public SettingsLoadingService(
            ISettingApplicationService settingApplicationService,
            ITaskProgressService progressService,
            IEventBus eventBus,
            ILogService logService,
            IComboBoxSetupService comboBoxSetupService,
            IDomainServiceRouter DomainServiceRouter)
        {
            _settingApplicationService = settingApplicationService ?? throw new ArgumentNullException(nameof(settingApplicationService));
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _comboBoxSetupService = comboBoxSetupService ?? throw new ArgumentNullException(nameof(comboBoxSetupService));
            _domainServiceRouter = DomainServiceRouter ?? throw new ArgumentNullException(nameof(DomainServiceRouter));
        }

        /// <summary>
        /// Loads and configures settings for a feature using the provided domain service.
        /// Handles progress tracking, exception handling, event publishing, and SettingItemViewModel creation.
        /// Returns a collection of fully configured SettingItemViewModels ready for binding.
        /// </summary>
        public async Task<ObservableCollection<object>> LoadConfiguredSettingsAsync<TDomainService>(
            TDomainService domainService,
            string featureModuleId,
            string progressMessage)
            where TDomainService : class
        {
            _logService.Log(
                LogLevel.Info,
                $"SettingsLoadingService: Starting LoadConfiguredSettingsAsync for {featureModuleId}"
            );

            try
            {
                _progressService.StartTask(progressMessage);

                // Use reflection to call GetSettingsAsync on the domain service
                var getSettingsMethod = domainService.GetType().GetMethod("GetSettingsAsync");
                if (getSettingsMethod == null)
                {
                    throw new InvalidOperationException($"Domain service {typeof(TDomainService).Name} does not have a GetSettingsAsync method");
                }

                // Invoke the method and get the task
                var task = getSettingsMethod.Invoke(domainService, null) as Task;
                if (task == null)
                {
                    throw new InvalidOperationException($"GetSettingsAsync method on {typeof(TDomainService).Name} did not return a Task");
                }

                // Wait for completion and extract result
                await task;
                var resultProperty = task.GetType().GetProperty("Result");
                if (resultProperty == null)
                {
                    throw new InvalidOperationException($"GetSettingsAsync method on {typeof(TDomainService).Name} did not return a Task with Result property");
                }

                var SettingDefinitions = resultProperty.GetValue(task) as IEnumerable<SettingDefinition>;
                if (SettingDefinitions == null)
                {
                    throw new InvalidOperationException($"GetSettingsAsync method on {typeof(TDomainService).Name} did not return IEnumerable<SettingDefinition>");
                }

                var settingsList = new List<SettingDefinition>(SettingDefinitions);

                // Build mapping synchronously
                _domainServiceRouter.AddSettingMappings(featureModuleId, settingsList.Select(s => s.Id));

                // Create and configure SettingItemViewModels
                var settingViewModels = new ObservableCollection<object>();
                foreach (var setting in settingsList)
                {
                    // Create SettingItemViewModel for each setting
                    var settingViewModel = new SettingItemViewModel(_settingApplicationService, _eventBus, _logService)
                    {
                        SettingId = setting.Id,
                        Name = setting.Name,
                        Description = setting.Description,
                        GroupName = setting.GroupName,
                        InputType = setting.InputType,
                        Icon = setting.Icon,
                        RequiresConfirmation = setting.RequiresConfirmation,
                        ConfirmationTitle = setting.ConfirmationTitle,
                        ConfirmationMessage = setting.ConfirmationMessage,
                        ActionCommandName = setting.ActionCommand
                    };

                    // Get current state from system
                    var currentState = await _settingApplicationService.GetSettingStateAsync(setting.Id);
                    settingViewModel.IsSelected = currentState.IsEnabled;

                    // Handle ComboBox setup using centralized service
                    if (setting.InputType == SettingInputType.Selection)
                    {
                        settingViewModel.SetupComboBox(setting, currentState.CurrentValue, _comboBoxSetupService, _logService);
                    }
                    else
                    {
                        // For non-ComboBox controls, set SelectedValue normally
                        settingViewModel.SelectedValue = currentState.CurrentValue;
                    }
                    
                    settingViewModels.Add(settingViewModel);
                }

                _logService.Log(
                    LogLevel.Info,
                    $"SettingsLoadingService: Loaded and configured {settingViewModels.Count} settings for {featureModuleId}"
                );

                // Publish FeatureComposedEvent after ViewModels are created
                _eventBus.Publish(new FeatureComposedEvent(featureModuleId, settingsList));

                _progressService.CompleteTask();
                return settingViewModels;
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading configured settings for {featureModuleId}: {ex.Message}");
                throw;
            }
        }
    }
}
