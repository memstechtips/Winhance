using System;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Extensions
{
    /// <summary>
    /// Extension methods for setting view models.
    /// </summary>
    public static class SettingViewModelExtensions
    {
        /// <summary>
        /// Safely converts an ApplicationSettingViewModel to an ApplicationSettingViewModel if possible.
        /// </summary>
        /// <param name="setting">The setting to convert.</param>
        /// <returns>The setting as an ApplicationSettingViewModel, or null if conversion is not possible.</returns>
        public static ApplicationSettingViewModel? AsApplicationSettingViewModel(this ApplicationSettingViewModel setting)
        {
            return setting;
        }
        
        /// <summary>
        /// Creates a new ApplicationSettingViewModel with properties copied from an ApplicationSettingViewModel.
        /// </summary>
        /// <param name="setting">The setting to convert.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dependencyManager">The dependency manager.</param>
        /// <param name="viewModelLocator">The view model locator.</param>
        /// <param name="settingsRegistry">The settings registry.</param>
        /// <returns>A new ApplicationSettingViewModel with properties copied from the input setting.</returns>
        public static ApplicationSettingViewModel ToApplicationSettingViewModel(
            this ApplicationSettingViewModel setting,
            IRegistryService registryService,
            IDialogService? dialogService,
            ILogService logService,
            IDependencyManager? dependencyManager = null,
            IViewModelLocator? viewModelLocator = null,
            ISettingsRegistry? settingsRegistry = null)
        {
            if (setting is ApplicationSettingViewModel applicationSetting)
            {
                return applicationSetting;
            }
            
            var result = new ApplicationSettingViewModel(
                registryService, 
                dialogService, 
                logService, 
                dependencyManager)
            {
                Id = setting.Id,
                Name = setting.Name,
                Description = setting.Description,
                IsSelected = setting.IsSelected,
                GroupName = setting.GroupName,
                IsGroupHeader = setting.IsGroupHeader,
                IsGroupedSetting = setting.IsGroupedSetting,
                ControlType = setting.ControlType,
                SliderSteps = setting.SliderSteps,
                SliderValue = setting.SliderValue,
                Status = setting.Status,
                CurrentValue = setting.CurrentValue,
                StatusMessage = setting.StatusMessage,
                RegistrySetting = setting.RegistrySetting,
                LinkedRegistrySettings = setting.LinkedRegistrySettings,
                Dependencies = setting.Dependencies
            };
            
            // Copy child settings if any
            foreach (var child in setting.ChildSettings)
            {
                result.ChildSettings.Add(child.ToApplicationSettingViewModel(
                    registryService, 
                    dialogService, 
                    logService, 
                    dependencyManager, 
                    viewModelLocator, 
                    settingsRegistry));
            }
            
            return result;
        }
    }
}
