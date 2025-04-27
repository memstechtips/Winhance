using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Enums;
using Winhance.Core.Features.Customize.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// View model for a customization setting.
    /// </summary>
    public partial class CustomizationSettingViewModel : ApplicationSettingViewModel
    {
        private readonly IViewModelLocator? _viewModelLocator;
        private readonly ISettingsRegistry? _settingsRegistry;

        /// <summary>
        /// Gets or sets the customization category.
        /// </summary>
        public CustomizationCategory Category { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomizationSettingViewModel"/> class.
        /// </summary>
        public CustomizationSettingViewModel()
            : base()
        {
            // Register property changes through the helper method
            SetupPropertyChangeHandlers();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomizationSettingViewModel"/> class.
        /// </summary>
        /// <param name="registryService">The registry service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dependencyManager">The dependency manager.</param>
        public CustomizationSettingViewModel(
            IRegistryService registryService,
            IDialogService? dialogService,
            ILogService logService,
            IDependencyManager? dependencyManager = null)
            : this(registryService, dialogService, logService, dependencyManager, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomizationSettingViewModel"/> class.
        /// </summary>
        /// <param name="registryService">The registry service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dependencyManager">The dependency manager.</param>
        /// <param name="viewModelLocator">The view model locator.</param>
        /// <param name="settingsRegistry">The settings registry.</param>
        public CustomizationSettingViewModel(
            IRegistryService registryService,
            IDialogService? dialogService,
            ILogService logService,
            IDependencyManager? dependencyManager = null,
            IViewModelLocator? viewModelLocator = null,
            ISettingsRegistry? settingsRegistry = null)
            : base(registryService, dialogService, logService, dependencyManager)
        {
            _viewModelLocator = viewModelLocator;
            _settingsRegistry = settingsRegistry;

            // Register property changes through the helper method
            SetupPropertyChangeHandlers();
        }

        /// <summary>
        /// Sets up property change handlers.
        /// </summary>
        private void SetupPropertyChangeHandlers()
        {
            // Register for property changes to track settings
            this.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(Id) && !string.IsNullOrEmpty(Id))
                {
                    // Register this setting in the settings registry if available
                    if (_settingsRegistry != null)
                    {
                        _settingsRegistry.RegisterSetting(this);
                        _logService?.Log(LogLevel.Info, $"Registered customization setting {Id} in settings registry");
                    }
                }
            };
        }

        /// <summary>
        /// Creates a view model from a customization setting.
        /// </summary>
        /// <param name="setting">The customization setting.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dependencyManager">The dependency manager.</param>
        /// <param name="viewModelLocator">The view model locator.</param>
        /// <param name="settingsRegistry">The settings registry.</param>
        /// <returns>A new view model.</returns>
        public static CustomizationSettingViewModel FromSetting(
            CustomizationSetting setting,
            IRegistryService registryService,
            IDialogService? dialogService,
            ILogService logService,
            IDependencyManager? dependencyManager = null,
            IViewModelLocator? viewModelLocator = null,
            ISettingsRegistry? settingsRegistry = null)
        {
            var viewModel = new CustomizationSettingViewModel(
                registryService, 
                dialogService, 
                logService, 
                dependencyManager,
                viewModelLocator,
                settingsRegistry)
            {
                Id = setting.Id,
                Name = setting.Name,
                Description = setting.Description,
                IsSelected = false, // Always initialize as unchecked
                GroupName = setting.GroupName,
                Dependencies = setting.Dependencies,
                ControlType = setting.ControlType,
                SliderSteps = setting.SliderSteps,
                Category = setting.Category
            };

            // Set up the registry settings
            if (setting.RegistrySettings.Count == 1)
            {
                // Single registry setting
                viewModel.RegistrySetting = setting.RegistrySettings[0];
            }
            else if (setting.RegistrySettings.Count > 0)
            {
                // Linked registry settings
                viewModel.LinkedRegistrySettings = setting.CreateLinkedRegistrySettings();
            }

            // Register the setting in the settings registry if available
            if (settingsRegistry != null && !string.IsNullOrEmpty(viewModel.Id))
            {
                settingsRegistry.RegisterSetting(viewModel);
                logService.Log(LogLevel.Info, $"Registered customization setting {viewModel.Id} in settings registry during creation");
            }

            return viewModel;
        }

        /// <summary>
        /// Gets all settings from all view models in the application.
        /// </summary>
        /// <returns>A list of all settings, or null if none found.</returns>
        protected override List<ISettingItem>? GetAllSettings()
        {
            try
            {
                _logService?.Log(LogLevel.Info, $"Getting all settings for dependency check");
                
                // First check if we have a settings registry
                if (_settingsRegistry != null)
                {
                    var settings = _settingsRegistry.GetAllSettings();
                    if (settings.Count > 0)
                    {
                        _logService?.Log(LogLevel.Info, $"Found {settings.Count} settings in settings registry");
                        return settings;
                    }
                }
                
                // If the settings registry is empty or not available, try to use the view model locator
                if (_viewModelLocator != null)
                {
                    var customizeViewModel = _viewModelLocator.FindViewModel<Winhance.WPF.Features.Customize.ViewModels.CustomizeViewModel>();
                    if (customizeViewModel != null)
                    {
                        // Try to get settings from the customize view model's properties
                        var settingsList = new List<ISettingItem>();
                        var properties = customizeViewModel.GetType().GetProperties()
                            .Where(p => p.Name.EndsWith("Settings") || p.Name.EndsWith("ViewModel"));
                        
                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(customizeViewModel);
                            if (value is IEnumerable<ISettingItem> settings)
                            {
                                settingsList.AddRange(settings);
                                _logService?.Log(LogLevel.Info, $"Found {settings.Count()} settings in {prop.Name}");
                            }
                            else if (value != null && prop.Name.EndsWith("ViewModel"))
                            {
                                // Check if this view model has a Settings property
                                var nestedProps = value.GetType().GetProperties()
                                    .Where(p => p.Name == "Settings" || p.Name.EndsWith("Settings"));
                                
                                foreach (var nestedProp in nestedProps)
                                {
                                    var nestedValue = nestedProp.GetValue(value);
                                    if (nestedValue is IEnumerable<ISettingItem> nestedSettings)
                                    {
                                        settingsList.AddRange(nestedSettings);
                                        _logService?.Log(LogLevel.Info, $"Found {nestedSettings.Count()} settings in {prop.Name}.{nestedProp.Name}");
                                    }
                                }
                            }
                        }
                        
                        if (settingsList.Count > 0)
                        {
                            _logService?.Log(LogLevel.Info, $"Found {settingsList.Count} settings through view model locator");
                            
                            // Add these settings to the registry for future use
                            if (_settingsRegistry != null)
                            {
                                foreach (var setting in settingsList)
                                {
                                    _settingsRegistry.RegisterSetting(setting);
                                }
                            }
                            
                            return settingsList;
                        }
                    }
                }
                
                // If all else fails, fall back to the base implementation
                return base.GetAllSettings();
            }
            catch (Exception ex)
            {
                _logService?.Log(LogLevel.Error, $"Error getting all settings: {ex.Message}");
                return null;
            }
        }
    }
}
