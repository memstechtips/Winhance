using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.ViewModels
{
    /// <summary>
    /// Base class for settings view models.
    /// </summary>
    /// <typeparam name="T">The type of settings.</typeparam>
    public partial class BaseSettingsViewModel<T> : ObservableObject
        where T : ApplicationSettingItem
    {
        protected readonly ITaskProgressService _progressService;
        protected readonly IRegistryService _registryService;
        protected readonly ILogService _logService;

        /// <summary>
        /// Gets the collection of settings.
        /// </summary>
        public ObservableCollection<T> Settings { get; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether the settings are being loaded.
        /// </summary>
        [ObservableProperty]
        private bool _isLoading;

        /// <summary>
        /// Gets or sets a value indicating whether all settings are selected.
        /// </summary>
        [ObservableProperty]
        private bool _isSelected;

        /// <summary>
        /// Gets or sets a value indicating whether the view model has visible settings.
        /// </summary>
        [ObservableProperty]
        private bool _hasVisibleSettings = true;

        /// <summary>
        /// Gets or sets the category name for this settings view model.
        /// </summary>
        [ObservableProperty]
        private string _categoryName = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseSettingsViewModel{T}"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        protected BaseSettingsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService
        )
        {
            _progressService =
                progressService ?? throw new ArgumentNullException(nameof(progressService));
            _registryService =
                registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Loads the settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;

                // Clear existing settings
                Settings.Clear();

                // Load settings (to be implemented by derived classes)
                await Task.CompletedTask;

                // Refresh status for all settings after loading
                await RefreshAllSettingsStatusAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Refreshes the status of all settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected async Task RefreshAllSettingsStatusAsync()
        {
            foreach (var setting in Settings)
            {
                await setting.RefreshStatus();
            }
        }

        /// <summary>
        /// Checks the status of all settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task CheckSettingStatusesAsync()
        {
            _logService.Log(
                LogLevel.Info,
                $"Checking status of {Settings.Count} settings in {GetType().Name}"
            );

            foreach (var setting in Settings)
            {
                if (setting.IsGroupHeader)
                {
                    continue;
                }

                // Direct method call without reflection
                await setting.RefreshStatus();
            }

            // Update the overall IsSelected state
            UpdateIsSelectedState();
        }

        /// <summary>
        /// Updates the IsSelected state based on individual selections.
        /// </summary>
        protected void UpdateIsSelectedState()
        {
            var nonHeaderSettings = Settings.Where(s => !s.IsGroupHeader).ToList();
            if (nonHeaderSettings.Count == 0)
            {
                IsSelected = false;
                return;
            }

            IsSelected = nonHeaderSettings.All(s => s.IsSelected);
        }

        /// <summary>
        /// Executes the specified action asynchronously.
        /// </summary>
        /// <param name="actionName">The name of the action to execute.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task ExecuteActionAsync(string actionName)
        {
            _logService.Log(LogLevel.Info, $"Executing action: {actionName}");

            // Find the setting with the specified action
            foreach (var setting in Settings)
            {
                var action = setting.Actions.FirstOrDefault(a => a.Name == actionName);
                if (action != null)
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"Found action {actionName} in setting {setting.Name}"
                    );

                    await ExecuteActionAsync(action);
                    return;
                }
            }

            _logService.Log(LogLevel.Warning, $"Action {actionName} not found");
        }

        /// <summary>
        /// Executes the specified action asynchronously.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task ExecuteActionAsync(ApplicationAction action)
        {
            if (action == null)
            {
                _logService.Log(LogLevel.Warning, "Cannot execute null action");
                return;
            }

            _logService.Log(LogLevel.Info, $"Executing action: {action.Name}");

            // Execute the registry action if present
            if (action.RegistrySetting != null)
            {
                // Use the registry service's ApplySettingAsync method to properly handle Group Policy settings
                // For actions, we always apply the RecommendedValue (enable the action)
                var tempSetting = action.RegistrySetting with 
                {
                    EnabledValue = action.RegistrySetting.RecommendedValue,
                    DisabledValue = action.RegistrySetting.DefaultValue
                };
                
                await _registryService.ApplySettingAsync(tempSetting, true);
            }

            // Execute custom action if present
            if (action.CustomAction != null)
            {
                await action.CustomAction();
            }

            _logService.Log(LogLevel.Info, $"Action '{action.Name}' executed successfully");
        }

        /// <summary>
        /// Applies the setting asynchronously.
        /// </summary>
        /// <param name="setting">The setting to apply.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task ApplySettingAsync(T setting)
        {
            if (setting == null)
            {
                _logService.Log(LogLevel.Warning, "Cannot apply null setting");
                return;
            }

            _logService.Log(LogLevel.Info, $"Applying setting: {setting.Name}");

            // Apply the setting based on its properties
            if (setting.RegistrySetting != null)
            {
                // Use the registry service's ApplySettingAsync method to properly handle Group Policy settings
                await _registryService.ApplySettingAsync(setting.RegistrySetting, setting.IsSelected);
            }
            else if (
                setting.LinkedRegistrySettings != null
                && setting.LinkedRegistrySettings.Settings.Count > 0
            )
            {
                // Apply linked registry settings
                await _registryService.ApplyLinkedSettingsAsync(
                    setting.LinkedRegistrySettings,
                    setting.IsSelected
                );
            }

            _logService.Log(LogLevel.Info, $"Setting {setting.Name} applied successfully");

            // Add a small delay to ensure registry changes are processed
            await Task.Delay(50);
        }

        /// <summary>
        /// Gets the registry hive string.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <returns>The registry hive string.</returns>
        private string GetRegistryHiveString(RegistryHive hive)
        {
            return hive switch
            {
                RegistryHive.ClassesRoot => "HKCR",
                RegistryHive.CurrentUser => "HKCU",
                RegistryHive.LocalMachine => "HKLM",
                RegistryHive.Users => "HKU",
                RegistryHive.CurrentConfig => "HKCC",
                _ => throw new ArgumentOutOfRangeException(nameof(hive), hive, null),
            };
        }
    }
}
