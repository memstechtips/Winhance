using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Enums;
using Winhance.Infrastructure.Features.Common.Registry;
using Winhance.WPF.Features.Common.Extensions;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// ViewModel for Explorer customizations.
    /// </summary>
    public partial class ExplorerCustomizationsViewModel
        : BaseSettingsViewModel<ApplicationSettingItem>
    {
        private readonly IDialogService _dialogService;

        /// <summary>
        /// Gets the command to execute an action.
        /// </summary>
        [RelayCommand]
        public async Task ExecuteAction(ApplicationAction? action)
        {
            if (action == null)
                return;

            try
            {
                // Execute the registry action if present
                if (action.RegistrySetting != null)
                {
                    string hiveString = RegistryExtensions.GetRegistryHiveString(
                        action.RegistrySetting.Hive
                    );
                    string fullPath = $"{hiveString}\\{action.RegistrySetting.SubKey}";
                    _registryService.SetValue(
                        fullPath,
                        action.RegistrySetting.Name,
                        action.RegistrySetting.RecommendedValue,
                        action.RegistrySetting.ValueType
                    );
                }

                // Execute the command action if present
                if (action.CommandAction != null)
                {
                    // Execute the command
                    // This would typically be handled by a command execution service
                }

                // Refresh the status after applying the action
                await CheckSettingStatusesAsync();
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error executing action: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExplorerCustomizationsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        public ExplorerCustomizationsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService,
            IDialogService dialogService
        )
            : base(progressService, registryService, logService)
        {
            _dialogService =
                dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        /// <summary>
        /// Loads the Explorer customizations.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;

                // Clear existing settings
                Settings.Clear();

                // Load Explorer customizations from ExplorerCustomizations
                var explorerCustomizations =
                    Core.Features.Customize.Models.ExplorerCustomizations.GetExplorerCustomizations();
                if (explorerCustomizations?.Settings != null)
                {
                    // Add settings sorted alphabetically by name
                    foreach (var setting in explorerCustomizations.Settings.OrderBy(s => s.Name))
                    {
                        // Create ApplicationSettingItem directly
                        var settingItem = new ApplicationSettingItem(
                            _registryService,
                            _dialogService,
                            _logService
                        )
                        {
                            Id = setting.Id,
                            Name = setting.Name,
                            Description = setting.Description,
                            ControlType = setting.ControlType,
                        };

                        // Add any actions
                        var actionsProperty = setting.GetType().GetProperty("Actions");
                        if (
                            actionsProperty != null
                            && actionsProperty.GetValue(setting) is IEnumerable<object> actions
                            && actions.Any()
                        )
                        {
                            // We need to handle this differently since the Actions property doesn't exist in ApplicationSetting
                            // This is a temporary workaround until we refactor the code properly
                        }

                        // Set up the registry settings
                        if (setting.RegistrySettings.Count == 1)
                        {
                            // Single registry setting
                            settingItem.RegistrySetting = setting.RegistrySettings[0];
                            _logService.Log(
                                LogLevel.Info,
                                $"Setting up single registry setting for {setting.Name}: {setting.RegistrySettings[0].Hive}\\{setting.RegistrySettings[0].SubKey}\\{setting.RegistrySettings[0].Name}"
                            );
                        }
                        else if (setting.RegistrySettings.Count > 1)
                        {
                            // Linked registry settings
                            settingItem.LinkedRegistrySettings =
                                setting.CreateLinkedRegistrySettings();
                            _logService.Log(
                                LogLevel.Info,
                                $"Setting up linked registry settings for {setting.Name} with {setting.RegistrySettings.Count} entries and logic {setting.LinkedSettingsLogic}"
                            );
                        }
                        else
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                $"No registry settings found for {setting.Name}"
                            );
                        }

                        Settings.Add(settingItem);
                    }

                    // Set up property change handlers for settings
                    foreach (var setting in Settings)
                    {
                        setting.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(ApplicationSettingItem.IsSelected))
                            {
                                UpdateIsSelectedState();
                            }
                        };
                    }
                }

                // Check setting statuses
                await CheckSettingStatusesAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks the status of all Explorer customizations.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task CheckSettingStatusesAsync()
        {
            try
            {
                foreach (var setting in Settings)
                {
                    // Get status
                    if (
                        setting.LinkedRegistrySettings != null
                        && setting.LinkedRegistrySettings.Settings.Count > 0
                    )
                    {
                        // For linked registry settings, use GetLinkedSettingsStatusAsync
                        var linkedStatus = await _registryService.GetLinkedSettingsStatusAsync(
                            setting.LinkedRegistrySettings
                        );
                        setting.Status = linkedStatus;

                        // Update IsSelected based on status - this is crucial for the initial toggle state
                        setting.IsUpdatingFromCode = true;
                        setting.IsSelected = linkedStatus == RegistrySettingStatus.Applied;
                        setting.IsUpdatingFromCode = false;
                    }
                    else
                    {
                        // For single registry setting
                        var status = await _registryService.GetSettingStatusAsync(
                            setting.RegistrySetting
                        );
                        setting.Status = status;
                    }

                    // Get current value
                    var currentValue = await _registryService.GetCurrentValueAsync(
                        setting.RegistrySetting
                    );
                    setting.CurrentValue = currentValue;

                    // Set IsRegistryValueNull property based on current value for single registry setting
                    if (
                        setting.LinkedRegistrySettings == null
                        || setting.LinkedRegistrySettings.Settings.Count == 0
                    )
                    {
                        // Special handling for specific Explorer items that should not show warning icons
                        if (
                            setting.Name == "3D Objects"
                            || setting.Name == "Gallery in Navigation Pane"
                            || setting.Name == "Home in Navigation Pane"
                        )
                        {
                            // Don't show warning icon for these specific items
                            setting.IsRegistryValueNull = false;
                        }
                        else
                        {
                            setting.IsRegistryValueNull = currentValue == null;
                        }
                    }

                    // Update LinkedRegistrySettingsWithValues for tooltip display
                    var linkedRegistrySettingsWithValues =
                        new ObservableCollection<LinkedRegistrySettingWithValue>();

                    // Get the LinkedRegistrySettings property
                    var linkedRegistrySettings = setting.LinkedRegistrySettings;

                    if (linkedRegistrySettings != null && linkedRegistrySettings.Settings.Count > 0)
                    {
                        // For linked settings, get fresh values from registry
                        bool anyNull = false;
                        foreach (var regSetting in linkedRegistrySettings.Settings)
                        {
                            string hiveString = RegistryExtensions.GetRegistryHiveString(
                                regSetting.Hive
                            );
                            var regCurrentValue = _registryService.GetValue(
                                $"{hiveString}\\{regSetting.SubKey}",
                                regSetting.Name
                            );

                            if (regCurrentValue == null)
                            {
                                anyNull = true;
                            }

                            linkedRegistrySettingsWithValues.Add(
                                new LinkedRegistrySettingWithValue(regSetting, regCurrentValue)
                            );
                        }

                        // For linked settings, set IsRegistryValueNull if any value is null
                        // Special handling for specific Explorer items that should not show warning icons
                        if (
                            setting.Name == "3D Objects"
                            || setting.Name == "Gallery in Navigation Pane"
                            || setting.Name == "Home in Navigation Pane"
                        )
                        {
                            // Don't show warning icon for these specific items
                            setting.IsRegistryValueNull = false;
                        }
                        else
                        {
                            setting.IsRegistryValueNull = anyNull;
                        }
                    }
                    else if (setting.RegistrySetting != null)
                    {
                        // For single setting
                        linkedRegistrySettingsWithValues.Add(
                            new LinkedRegistrySettingWithValue(
                                setting.RegistrySetting,
                                currentValue
                            )
                        );
                    }

                    setting.LinkedRegistrySettingsWithValues = linkedRegistrySettingsWithValues;

                    // Set status message
                    string statusMessage = GetStatusMessage(setting);
                    setting.StatusMessage = statusMessage;

                    // Set the IsUpdatingFromCode flag to prevent automatic application
                    setting.IsUpdatingFromCode = true;

                    try
                    {
                        // Update IsSelected based on status
                        bool shouldBeSelected = setting.Status == RegistrySettingStatus.Applied;

                        // Set the checkbox state to match the registry state
                        _logService.Log(
                            LogLevel.Info,
                            $"Setting {setting.Name} status is {setting.Status}, setting IsSelected to {shouldBeSelected}"
                        );
                        setting.IsSelected = shouldBeSelected;
                    }
                    finally
                    {
                        // Reset the flag
                        setting.IsUpdatingFromCode = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error checking Explorer customization statuses: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Gets the status message for a setting.
        /// </summary>
        /// <param name="setting">The setting.</param>
        /// <returns>The status message.</returns>
        private string GetStatusMessage(ApplicationSettingItem setting)
        {
            var status = setting.Status;
            string message = status switch
            {
                RegistrySettingStatus.Applied => "Setting is applied with recommended value",
                RegistrySettingStatus.NotApplied => "Setting is not applied or using default value",
                RegistrySettingStatus.Modified =>
                    "Setting has a custom value different from recommended",
                RegistrySettingStatus.Error => "Error checking setting status",
                _ => "Unknown status",
            };

            // Add current value if available
            var currentValue = setting.CurrentValue;
            if (currentValue != null)
            {
                message += $"\nCurrent value: {currentValue}";
            }

            // Add recommended value if available
            var registrySetting = setting.RegistrySetting;
            if (registrySetting?.RecommendedValue != null)
            {
                message += $"\nRecommended value: {registrySetting.RecommendedValue}";
            }

            // Add default value if available
            if (registrySetting?.DefaultValue != null)
            {
                message += $"\nDefault value: {registrySetting.DefaultValue}";
            }

            return message;
        }

        // ApplySelectedSettingsAsync and RestoreDefaultsAsync methods removed as part of the refactoring
    }
}
