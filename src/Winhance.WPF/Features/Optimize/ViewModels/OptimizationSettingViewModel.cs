using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.Common.Extensions;
using Microsoft.Win32;
using Winhance.Core.Features.Optimize.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// View model for an optimization setting.
    /// </summary>
    public partial class OptimizationSettingViewModel : ApplicationSettingViewModel
    {
        private readonly IViewModelLocator? _viewModelLocator;
        private readonly ISettingsRegistry? _settingsRegistry;

        /// <summary>
        /// Gets or sets a value indicating whether the setting is visible.
        /// </summary>
        [ObservableProperty]
        private bool _isVisible = true;

        /// <summary>
        /// Creates an OptimizationSettingViewModel from an OptimizationSetting.
        /// </summary>
        /// <param name="setting">The optimization setting.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dependencyManager">The dependency manager.</param>
        /// <param name="viewModelLocator">The view model locator.</param>
        /// <param name="settingsRegistry">The settings registry.</param>
        /// <param name="powerPlanService">The power plan service.</param>
        /// <returns>A new OptimizationSettingViewModel.</returns>
        public static OptimizationSettingViewModel FromSetting(
            Winhance.Core.Features.Optimize.Models.OptimizationSetting setting,
            IRegistryService registryService,
            IDialogService? dialogService,
            ILogService logService,
            IDependencyManager? dependencyManager = null,
            IViewModelLocator? viewModelLocator = null,
            ISettingsRegistry? settingsRegistry = null,
            Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService? powerPlanService = null)
        {
            var viewModel = new OptimizationSettingViewModel(
                registryService,
                dialogService,
                logService,
                dependencyManager,
                viewModelLocator,
                settingsRegistry,
                powerPlanService)
            {
                Id = setting.Id,
                Name = setting.Name,
                Description = setting.Description,
                IsSelected = false, // Always initialize as unchecked
                GroupName = setting.GroupName,
                Dependencies = setting.Dependencies,
                ControlType = ControlType.BinaryToggle // Default to binary toggle
            };

            // Set up the registry settings
            if (setting.RegistrySettings.Count == 1)
            {
                // Single registry setting
                viewModel.RegistrySetting = setting.RegistrySettings[0];
                logService.Log(LogLevel.Info, $"Setting up single registry setting for {setting.Name}: {setting.RegistrySettings[0].Hive}\\{setting.RegistrySettings[0].SubKey}\\{setting.RegistrySettings[0].Name}");
            }
            else if (setting.RegistrySettings.Count > 1)
            {
                // Linked registry settings
                viewModel.LinkedRegistrySettings = setting.CreateLinkedRegistrySettings();
                logService.Log(LogLevel.Info, $"Setting up linked registry settings for {setting.Name} with {setting.RegistrySettings.Count} entries and logic {setting.LinkedSettingsLogic}");
                
                // Log details about each registry entry for debugging
                foreach (var regSetting in setting.RegistrySettings)
                {
                    logService.Log(LogLevel.Info, $"Linked registry entry: {regSetting.Hive}\\{regSetting.SubKey}\\{regSetting.Name}, IsPrimary={regSetting.IsPrimary}");
                }
            }
            else
            {
                logService.Log(LogLevel.Warning, $"No registry settings found for {setting.Name}");
            }

            // Register the setting in the settings registry if available
            if (settingsRegistry != null && !string.IsNullOrEmpty(viewModel.Id))
            {
                settingsRegistry.RegisterSetting(viewModel);
                logService.Log(LogLevel.Info, $"Registered setting {viewModel.Id} in settings registry during creation");
            }

            return viewModel;
        }

        [ObservableProperty]
        private string _actionVerb = string.Empty;

        [ObservableProperty]
        private string _subject = string.Empty;

        [ObservableProperty]
        private string _fullName = string.Empty;

        partial void OnActionVerbChanged(string value) => UpdateFullName();
        partial void OnSubjectChanged(string value) => UpdateFullName();
        // Handle Name property changes through the PropertyChanged event in the constructor

        private void UpdateFullName()
        {
            FullName = string.IsNullOrEmpty(ActionVerb) ? Name : $"{ActionVerb} {Subject}";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizationSettingViewModel"/> class.
        /// </summary>
        public OptimizationSettingViewModel()
            : base()
        {
            // Initialize FullName
            UpdateFullName();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizationSettingViewModel"/> class.
        /// </summary>
        /// <param name="registryService">The registry service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dependencyManager">The dependency manager.</param>
        public OptimizationSettingViewModel(
            IRegistryService registryService,
            IDialogService? dialogService,
            ILogService logService,
            IDependencyManager? dependencyManager = null,
            Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService? powerPlanService = null)
            : this(registryService, dialogService, logService, dependencyManager, null, null, powerPlanService)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizationSettingViewModel"/> class.
        /// </summary>
        /// <param name="registryService">The registry service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dependencyManager">The dependency manager.</param>
        /// <param name="viewModelLocator">The view model locator.</param>
        /// <param name="settingsRegistry">The settings registry.</param>
        public OptimizationSettingViewModel(
            IRegistryService registryService,
            IDialogService? dialogService,
            ILogService logService,
            IDependencyManager? dependencyManager = null,
            IViewModelLocator? viewModelLocator = null,
            ISettingsRegistry? settingsRegistry = null,
            Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService? powerPlanService = null)
            : base(registryService, dialogService, logService, dependencyManager, powerPlanService)
        {
            _viewModelLocator = viewModelLocator;
            _settingsRegistry = settingsRegistry;

            // Set up property changed handlers
            this.PropertyChanged += (s, e) => {
                // Handle Name property changes for FullName updates
                if (e.PropertyName == nameof(Name))
                {
                    UpdateFullName();
                }
                
                // Handle Id property changes for registering in the settings registry
                if (e.PropertyName == nameof(Id) && !string.IsNullOrEmpty(Id))
                {
                    // Register this setting in the settings registry if available
                    if (_settingsRegistry != null)
                    {
                        _settingsRegistry.RegisterSetting(this);
                        _logService?.Log(LogLevel.Info, $"Registered setting {Id} in settings registry");
                    }
                }
                
                // Handle IsSelected property changes for child settings and applying settings
                if (e.PropertyName == nameof(IsSelected))
                {
                    // If this is a grouped setting, update all child settings
                    if (IsGroupedSetting && ChildSettings.Count > 0 && !IsUpdatingFromCode)
                    {
                        _isUpdatingFromCode = true;
                        try
                        {
                            foreach (var child in ChildSettings)
                            {
                                child.IsSelected = IsSelected;
                            }
                        }
                        finally
                        {
                            _isUpdatingFromCode = false;
                        }
                    }
                }
                
                // Apply settings when properties change
                if ((e.PropertyName == nameof(IsSelected) || e.PropertyName == nameof(SliderValue)) && !IsUpdatingFromCode)
                {
                    _logService?.Log(LogLevel.Info, $"Property {e.PropertyName} changed for {Name}, applying setting");

                    // Check dependencies when enabling a setting
                    if (e.PropertyName == nameof(IsSelected))
                    {
                        if (IsSelected)
                        {
                            // Check if this setting can be enabled based on its dependencies
                            var allSettings = GetAllSettings();
                            if (allSettings != null && _dependencyManager != null)
                            {
                                if (!_dependencyManager.CanEnableSetting(Id, allSettings))
                                {
                                    _logService?.Log(LogLevel.Info, $"Setting {Name} has unsatisfied dependencies, attempting to enable them");
                                    
                                    // Get unsatisfied dependencies using the enhanced IDependencyManager
                                    var unsatisfiedDependencies = _dependencyManager.GetUnsatisfiedDependencies(Id, allSettings);
                                    
                                    if (unsatisfiedDependencies.Count > 0)
                                    {
                                        // Automatically enable the dependencies without asking
                                        bool enableDependencies = true;
                                        
                                        // Log what we're doing
                                        var dependencyNames = string.Join(", ", unsatisfiedDependencies.Select(d => $"'{d.Name}'"));
                                        _logService?.Log(LogLevel.Info, $"'{Name}' requires {dependencyNames} to be enabled. Automatically enabling dependencies.");
                                        
                                        if (enableDependencies)
                                        {
                                            // Enable all dependencies using the enhanced IDependencyManager
                                            bool success = _dependencyManager.EnableDependencies(unsatisfiedDependencies);
                                            _logService?.Log(success ? LogLevel.Info : LogLevel.Warning, 
                                                success ? "Successfully enabled all dependencies" : "Failed to enable some dependencies");
                                        }
                                        else
                                        {
                                            // User chose not to enable dependencies, so don't enable this setting
                                            IsUpdatingFromCode = true;
                                            try
                                            {
                                                IsSelected = false;
                                            }
                                            finally
                                            {
                                                IsUpdatingFromCode = false;
                                            }
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        // No dependencies found, show a generic message
                                        if (_dialogService != null)
                                        {
                                            _dialogService.ShowMessage(
                                                $"'{Name}' cannot be enabled because one of its dependencies is not satisfied.",
                                                "Setting Dependency");
                                        }
                                        
                                        // Prevent enabling this setting
                                        IsUpdatingFromCode = true;
                                        try
                                        {
                                            IsSelected = false;
                                        }
                                        finally
                                        {
                                            IsUpdatingFromCode = false;
                                        }
                                        return;
                                    }
                                }
                                
                                // Automatically enable any required settings
                                _dependencyManager.HandleSettingEnabled(Id, allSettings);
                            }
                        }
                        else
                        {
                            // Handle disabling a setting
                            var allSettings = GetAllSettings();
                            if (allSettings != null && _dependencyManager != null)
                            {
                                _dependencyManager.HandleSettingDisabled(Id, allSettings);
                            }
                        }
                    }

                    ApplySettingCommand.Execute(null);
                }
                else if ((e.PropertyName == nameof(IsSelected) || e.PropertyName == nameof(SliderValue)) && IsUpdatingFromCode)
                {
                    _logService?.Log(LogLevel.Info, $"Property {e.PropertyName} changed for {Name}, but not applying setting because IsUpdatingFromCode is true");
                }
            };

            // Initialize FullName
            UpdateFullName();
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
                    var privacyViewModel = _viewModelLocator.FindViewModel<Winhance.WPF.Features.Optimize.ViewModels.PrivacyOptimizationsViewModel>();
                    if (privacyViewModel != null && privacyViewModel.Settings != null && privacyViewModel.Settings.Count > 0)
                    {
                        _logService?.Log(LogLevel.Info, $"Found {privacyViewModel.Settings.Count} settings through view model locator");
                        
                        var settingsList = privacyViewModel.Settings.Cast<ISettingItem>().ToList();
                        
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
                
                // If that fails, try to find all settings directly
                var result = new List<ISettingItem>();
                var app = System.Windows.Application.Current;
                if (app == null) return null;

                // Try to find all settings view models
                foreach (System.Windows.Window window in app.Windows)
                {
                    if (window.DataContext != null)
                    {
                        // Look for view models that have a Settings property
                        var settingsProperties = window.DataContext.GetType().GetProperties()
                            .Where(p => p.Name == "Settings" || p.Name.EndsWith("Settings"));

                        foreach (var prop in settingsProperties)
                        {
                            var value = prop.GetValue(window.DataContext);
                            if (value is IEnumerable<ISettingItem> settings)
                            {
                                result.AddRange(settings);
                                _logService?.Log(LogLevel.Info, $"Found {settings.Count()} settings in {prop.Name}");
                            }
                        }

                        // Also look for view models that might contain other view models with settings
                        var viewModelProperties = window.DataContext.GetType().GetProperties()
                            .Where(p => p.Name.EndsWith("ViewModel") && p.Name != "DataContext");

                        foreach (var prop in viewModelProperties)
                        {
                            var viewModel = prop.GetValue(window.DataContext);
                            if (viewModel != null)
                            {
                                var nestedSettingsProps = viewModel.GetType().GetProperties()
                                    .Where(p => p.Name == "Settings" || p.Name.EndsWith("Settings"));

                                foreach (var nestedProp in nestedSettingsProps)
                                {
                                    var value = nestedProp.GetValue(viewModel);
                                    if (value is IEnumerable<ISettingItem> settings)
                                    {
                                        result.AddRange(settings);
                                        _logService?.Log(LogLevel.Info, $"Found {settings.Count()} settings in {prop.Name}.{nestedProp.Name}");
                                    }
                                }
                            }
                        }
                    }
                }

                // If we found settings, return them and add to our registry for future use
                if (result.Count > 0)
                {
                    _logService?.Log(LogLevel.Info, $"Found a total of {result.Count} settings through direct search");
                    
                    // Log the settings we found
                    foreach (var setting in result)
                    {
                        _logService?.Log(LogLevel.Info, $"Found setting: {setting.Id} - {setting.Name}");
                    }
                    
                    // Add these settings to our registry for future use
                    if (_settingsRegistry != null)
                    {
                        foreach (var setting in result)
                        {
                            _settingsRegistry.RegisterSetting(setting);
                        }
                    }
                    
                    return result;
                }
                
                // If all else fails, try to find settings through the base implementation
                var baseSettings = base.GetAllSettings();
                if (baseSettings != null && baseSettings.Count > 0)
                {
                    _logService?.Log(LogLevel.Info, $"Found {baseSettings.Count} settings through base implementation");
                    
                    // Add these settings to our registry for future use
                    if (_settingsRegistry != null)
                    {
                        foreach (var setting in baseSettings)
                        {
                            _settingsRegistry.RegisterSetting(setting);
                        }
                    }
                    
                    return baseSettings;
                }
                
                _logService?.Log(LogLevel.Warning, "Could not find any settings for dependency check");
                return null;
            }
            catch (Exception ex)
            {
                _logService?.Log(LogLevel.Error, $"Error getting all settings: {ex.Message}");
                return null;
            }
        }
    }
}
