using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Optimize.ViewModels;

namespace Winhance.WPF.Features.Common.Services.Configuration
{
    /// <summary>
    /// Service for applying configuration to the Optimize section.
    /// </summary>
    public class OptimizeConfigurationApplier : Core.Features.Common.Interfaces.IOptimizeConfigurationApplier
    {
        private readonly ILogService _logService;
        private readonly IPropertyUpdater _propertyUpdater;

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizeConfigurationApplier"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        public OptimizeConfigurationApplier(ILogService logService, IPropertyUpdater propertyUpdater = null)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _propertyUpdater = propertyUpdater;
        }

        /// <summary>
        /// Applies configuration to the OptimizeViewModel.
        /// </summary>
        /// <param name="viewModelObj">The view model object to apply configuration to.</param>
        /// <param name="configFile">The configuration file containing settings to apply.</param>
        /// <returns>True if configuration was applied successfully, false otherwise.</returns>
        public async Task<bool> ApplyConfigAsync(
            object viewModelObj,
            ConfigurationFile configFile
        )
        {
            if (viewModelObj == null)
            {
                _logService.Log(LogLevel.Error, "Cannot apply config to null view model");
                return false;
            }

            if (configFile == null)
            {
                _logService.Log(LogLevel.Error, "Cannot apply null config file");
                return false;
            }

            // Check if this is an OptimizeViewModel
            if (viewModelObj is OptimizeViewModel)
            {
                _logService.Log(
                    LogLevel.Info,
                    "Detected OptimizeViewModel, skipping configuration application"
                );

                // Skip handling for OptimizeViewModel
                // as it uses a different architecture and doesn't have the same properties/methods
                _logService.Log(
                    LogLevel.Info,
                    "OptimizeViewModel uses a composition pattern which is incompatible with the legacy configuration system"
                );

                // Return early as we can't apply configuration to the composition-based ViewModel
                return false;
            }

            // This is not an OptimizeViewModel, so we don't handle it
            _logService.Log(
                LogLevel.Info,
                $"Not handling view model of type {viewModelObj.GetType().Name}"
            );
            return false;
        }

        /// <summary>
        /// Applies power plan settings to the PowerOptimizationsViewModel.
        /// </summary>
        /// <param name="viewModel">The view model to apply settings to.</param>
        /// <param name="configFile">The configuration file containing settings.</param>
        /// <returns>Number of settings applied.</returns>
        private async Task<int> ApplyPowerSettings(dynamic viewModel, ConfigurationFile configFile)
        {
            int updatedCount = 0;
            var startTime = DateTime.Now;

            try
            {
                // First, check if there's a Power Plan item in the config file
                var powerPlanItem = configFile.Items?.FirstOrDefault(item =>
                    (
                        item.Name?.Contains("Power Plan") == true
                        || (
                            item.CustomProperties.TryGetValue("Id", out var id)
                            && id?.ToString() == "PowerPlanComboBox"
                        )
                    )
                );

                if (powerPlanItem != null)
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"Found Power Plan item in config file: {powerPlanItem.Name}"
                    );

                    int newPowerPlanValue = -1;
                    string selectedPowerPlan = null;

                    // First try to get the value from SelectedValue by mapping to index (preferred method)
                    if (!string.IsNullOrEmpty(powerPlanItem.SelectedValue))
                    {
                        selectedPowerPlan = powerPlanItem.SelectedValue;
                        _logService.Log(
                            LogLevel.Info,
                            $"Found SelectedValue in config: {selectedPowerPlan}"
                        );

                        var powerPlanLabels = viewModel.PowerPlanLabels;
                        for (int i = 0; i < powerPlanLabels.Count; i++)
                        {
                            if (
                                string.Equals(
                                    powerPlanLabels[i],
                                    selectedPowerPlan,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                newPowerPlanValue = i;
                                _logService.Log(
                                    LogLevel.Info,
                                    $"Mapped SelectedValue {selectedPowerPlan} to PowerPlanValue: {newPowerPlanValue}"
                                );
                                break;
                            }
                        }
                    }
                    // Then try to get the value from SliderValue in CustomProperties (fallback)
                    else if (
                        powerPlanItem.CustomProperties.TryGetValue(
                            "SliderValue",
                            out var sliderValue
                        )
                    )
                    {
                        newPowerPlanValue = Convert.ToInt32(sliderValue);
                        _logService.Log(
                            LogLevel.Info,
                            $"Found PowerPlanValue in CustomProperties.SliderValue: {newPowerPlanValue}"
                        );
                    }

                    // If we still don't have a valid power plan value, try to get it from PowerPlanOptions
                    if (
                        newPowerPlanValue < 0
                        && powerPlanItem.CustomProperties.TryGetValue(
                            "PowerPlanOptions",
                            out var powerPlanOptions
                        )
                    )
                    {
                        if (
                            powerPlanOptions is System.Collections.IList optionsList
                            && !string.IsNullOrEmpty(selectedPowerPlan)
                        )
                        {
                            for (int i = 0; i < optionsList.Count; i++)
                            {
                                if (
                                    string.Equals(
                                        optionsList[i]?.ToString(),
                                        selectedPowerPlan,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                                {
                                    newPowerPlanValue = i;
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"Mapped SelectedValue {selectedPowerPlan} to PowerPlanValue: {newPowerPlanValue} using PowerPlanOptions"
                                    );
                                    break;
                                }
                            }
                        }
                    }

                    if (newPowerPlanValue >= 0)
                    {
                        // Update the view model properties without triggering the actual power plan change
                        // Store the current value for comparison
                        int currentPowerPlanValue = viewModel.PowerPlanValue;

                        if (currentPowerPlanValue != newPowerPlanValue)
                        {
                            _logService.Log(
                                LogLevel.Info,
                                $"About to update PowerPlanValue from {currentPowerPlanValue} to {newPowerPlanValue}"
                            );

                            // Set IsApplyingPowerPlan to true before updating PowerPlanValue
                            viewModel.IsApplyingPowerPlan = true;
                            _logService.Log(
                                LogLevel.Info,
                                "Set IsApplyingPowerPlan to true to prevent auto-application"
                            );

                            // Add a small delay to ensure IsApplyingPowerPlan takes effect
                            await Task.Delay(50);

                            try
                            {
                                // Use reflection to directly set the backing field for PowerPlanValue
                                // This avoids triggering the property change notification that would call ApplyPowerPlanAsync
                                var field = viewModel
                                    .GetType()
                                    .GetField(
                                        "_powerPlanValue",
                                        System.Reflection.BindingFlags.NonPublic
                                            | System.Reflection.BindingFlags.Instance
                                    );

                                if (field != null)
                                {
                                    // Set the backing field directly
                                    field.SetValue(viewModel, newPowerPlanValue);
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"Directly updated _powerPlanValue field to {newPowerPlanValue}"
                                    );

                                    // Manually trigger property changed notification
                                    // Specify the parameter types to avoid ambiguous match error
                                    var onPropertyChangedMethod = viewModel
                                        .GetType()
                                        .GetMethod(
                                            "OnPropertyChanged",
                                            System.Reflection.BindingFlags.NonPublic
                                                | System.Reflection.BindingFlags.Instance,
                                            null,
                                            new[] { typeof(string) },
                                            null
                                        );

                                    if (onPropertyChangedMethod != null)
                                    {
                                        onPropertyChangedMethod.Invoke(
                                            viewModel,
                                            new object[] { "PowerPlanValue" }
                                        );
                                        _logService.Log(
                                            LogLevel.Info,
                                            "Manually triggered OnPropertyChanged for PowerPlanValue"
                                        );
                                    }
                                    else
                                    {
                                        // Fallback: Try with PropertyChangedEventArgs parameter
                                        var args =
                                            new System.ComponentModel.PropertyChangedEventArgs(
                                                "PowerPlanValue"
                                            );
                                        var altMethod = viewModel
                                            .GetType()
                                            .GetMethod(
                                                "OnPropertyChanged",
                                                System.Reflection.BindingFlags.NonPublic
                                                    | System.Reflection.BindingFlags.Instance,
                                                null,
                                                new[]
                                                {
                                                    typeof(System.ComponentModel.PropertyChangedEventArgs),
                                                },
                                                null
                                            );

                                        if (altMethod != null)
                                        {
                                            altMethod.Invoke(viewModel, new object[] { args });
                                            _logService.Log(
                                                LogLevel.Info,
                                                "Manually triggered OnPropertyChanged with PropertyChangedEventArgs"
                                            );
                                        }
                                        else
                                        {
                                            _logService.Log(
                                                LogLevel.Warning,
                                                "Could not find appropriate OnPropertyChanged method"
                                            );
                                        }
                                    }
                                }
                                else
                                {
                                    // Fallback to direct property setting if field not found
                                    _logService.Log(
                                        LogLevel.Warning,
                                        "Could not find _powerPlanValue field, using property setter instead"
                                    );
                                    viewModel.PowerPlanValue = newPowerPlanValue;
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"Updated PowerPlanValue property to {newPowerPlanValue}"
                                    );
                                }

                                // Add a small delay to ensure property change notifications are processed
                                await Task.Delay(100);
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(
                                    LogLevel.Error,
                                    $"Error updating power plan value: {ex.Message}"
                                );
                                throw; // Rethrow to be caught by outer catch
                            }

                            // Now actually apply the power plan
                            try
                            {
                                // Reset IsApplyingPowerPlan to false so we can apply the power plan
                                viewModel.IsApplyingPowerPlan = false;
                                _logService.Log(
                                    LogLevel.Info,
                                    "Reset IsApplyingPowerPlan to false"
                                );

                                // Call the method to apply the power plan
                                var applyPowerPlanMethod = viewModel
                                    .GetType()
                                    .GetMethod(
                                        "ApplyPowerPlanAsync",
                                        System.Reflection.BindingFlags.Public
                                            | System.Reflection.BindingFlags.NonPublic
                                            | System.Reflection.BindingFlags.Instance
                                    );

                                if (applyPowerPlanMethod != null)
                                {
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"Calling ApplyPowerPlanAsync to apply power plan: {selectedPowerPlan}"
                                    );
                                    await (Task)
                                        applyPowerPlanMethod.Invoke(
                                            viewModel,
                                            new object[] { newPowerPlanValue }
                                        );
                                    _logService.Log(
                                        LogLevel.Success,
                                        $"Successfully applied power plan: {selectedPowerPlan}"
                                    );
                                }
                                else
                                {
                                    // Try to find a method that takes no parameters
                                    applyPowerPlanMethod = viewModel
                                        .GetType()
                                        .GetMethod(
                                            "ApplyPowerPlanAsync",
                                            System.Reflection.BindingFlags.Public
                                                | System.Reflection.BindingFlags.NonPublic
                                                | System.Reflection.BindingFlags.Instance,
                                            null,
                                            Type.EmptyTypes,
                                            null
                                        );

                                    if (applyPowerPlanMethod != null)
                                    {
                                        _logService.Log(
                                            LogLevel.Info,
                                            $"Calling parameterless ApplyPowerPlanAsync to apply power plan: {selectedPowerPlan}"
                                        );
                                        await (Task)applyPowerPlanMethod.Invoke(viewModel, null);
                                        _logService.Log(
                                            LogLevel.Success,
                                            $"Successfully applied power plan: {selectedPowerPlan}"
                                        );
                                    }
                                    else
                                    {
                                        _logService.Log(
                                            LogLevel.Warning,
                                            "Could not find ApplyPowerPlanAsync method"
                                        );
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(
                                    LogLevel.Error,
                                    $"Error applying power plan: {ex.Message}"
                                );
                            }
                            finally
                            {
                                // Make sure IsApplyingPowerPlan is reset to false when done
                                if (viewModel.IsApplyingPowerPlan)
                                {
                                    viewModel.IsApplyingPowerPlan = false;
                                    _logService.Log(
                                        LogLevel.Info,
                                        "Reset IsApplyingPowerPlan to false in finally block"
                                    );
                                }
                            }

                            // Also update the SelectedIndex of the ComboBox if possible
                            try
                            {
                                // Find the ComboBox control in the PowerSettingsViewModel
                                Func<object, bool> powerPlanPredicate = s => {
                                    var item = s as dynamic;
                                    return item.Id == "PowerPlanComboBox" || (item.Name as string)?.Contains("Power Plan") == true;
                                };
                                var powerPlanComboBox = viewModel.Settings.FirstOrDefault(powerPlanPredicate);

                                if (powerPlanComboBox != null)
                                {
                                    var sliderValueProperty = powerPlanComboBox
                                        .GetType()
                                        .GetProperty("SliderValue");
                                    if (sliderValueProperty != null)
                                    {
                                        _logService.Log(
                                            LogLevel.Info,
                                            $"Setting SliderValue on PowerPlanComboBox to {newPowerPlanValue}"
                                        );
                                        sliderValueProperty.SetValue(
                                            powerPlanComboBox,
                                            newPowerPlanValue
                                        );
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                //Error updating PowerPlanComboBox
                            }

                            updatedCount++;
                        }
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "No Power Plan item found in config file");

                    // Try to find a power plan setting in the Settings collection
                    Func<object, bool> powerPlanSettingPredicate = s => {
                        var item = s as dynamic;
                        return item.Id == "PowerPlanComboBox" || (item.Name as string)?.Contains("Power Plan") == true;
                    };
                    var powerPlanSetting = viewModel.Settings?.FirstOrDefault(powerPlanSettingPredicate);

                    if (powerPlanSetting != null)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            $"Found Power Plan setting in Settings collection: {powerPlanSetting.Name}"
                        );

                        // Create a new ConfigurationItem for the power plan
                        var newPowerPlanItem = new ConfigurationItem
                        {
                            Name = powerPlanSetting.Name,
                            IsSelected = true,
                            InputType = SettingInputType.Selection,
                            CustomProperties = new Dictionary<string, object>
                            {
                                { "Id", "PowerPlanComboBox" },
                                { "GroupName", "Power Management" },
                                { "Description", "Select power plan for your system" },
                                { "SliderValue", viewModel.PowerPlanValue },
                            },
                        };

                        // Set the SelectedValue based on the current PowerPlanValue
                        if (
                            viewModel.PowerPlanValue >= 0
                            && viewModel.PowerPlanValue < viewModel.PowerPlanLabels.Count
                        )
                        {
                            newPowerPlanItem.SelectedValue = viewModel.PowerPlanLabels[
                                viewModel.PowerPlanValue
                            ];
                        }

                        // Add the item to the config file
                        if (configFile.Items == null)
                        {
                            configFile.Items = new List<ConfigurationItem>();
                        }

                        configFile.Items.Add(newPowerPlanItem);
                        _logService.Log(
                            LogLevel.Info,
                            $"Added Power Plan item to config file with SelectedValue: {newPowerPlanItem.SelectedValue}"
                        );
                    }
                }

                // Then apply to the Settings collection as usual
                int settingsUpdatedCount = await _propertyUpdater.UpdateItemsAsync(
                    viewModel.Settings,
                    configFile
                );
                updatedCount += settingsUpdatedCount;
            }
            catch (TaskCanceledException)
            {
                _logService.Log(LogLevel.Warning, "ApplyPowerSettings was canceled due to timeout");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying power settings: {ex.Message}");
            }

            // Calculate and log execution time
            var endTime = DateTime.Now;
            var executionTime = endTime - startTime;
            _logService.Log(
                LogLevel.Info,
                $"Completed ApplyPowerSettings in {executionTime.TotalSeconds:F2} seconds"
            );

            return updatedCount;
        }

        /// <summary>
        /// Applies security settings to the WindowsSecurityOptimizationsViewModel.
        /// </summary>
        /// <param name="viewModel">The view model to apply settings to.</param>
        /// <param name="configFile">The configuration file containing settings.</param>
        /// <returns>Number of settings applied.</returns>
        private async Task<int> ApplySecuritySettings(WindowsSecurityOptimizationsViewModel viewModel, ConfigurationFile configFile)
        {
            int updatedCount = 0;

            // Use a cancellation token with a timeout to prevent hanging
            using var cancellationTokenSource = new CancellationTokenSource(
                TimeSpan.FromSeconds(15)
            );
            var cancellationToken = cancellationTokenSource.Token;

            // Track execution time
            var startTime = DateTime.Now;
            _logService.Log(LogLevel.Info, $"Starting ApplySecuritySettings at {startTime}");

            try
            {
                // First, check if there's a UAC Slider item in the config file
                var uacSliderItem = configFile.Items?.FirstOrDefault(item =>
                    (
                        item.Name?.Contains("User Account Control") == true
                        || (
                            item.CustomProperties.TryGetValue("Id", out var id)
                            && id?.ToString() == "UACSlider"
                        )
                    )
                );

                if (
                    uacSliderItem != null
                    && uacSliderItem.CustomProperties.TryGetValue(
                        "SliderValue",
                        out var sliderValue
                    )
                )
                {
                    // Check if the SelectedUacLevel property exists
                    var selectedUacLevelProperty = viewModel
                        .GetType()
                        .GetProperty(
                            "SelectedUacLevel",
                            System.Reflection.BindingFlags.Public
                                | System.Reflection.BindingFlags.Instance
                        );

                    if (selectedUacLevelProperty != null)
                    {
                        // Convert the slider value to the corresponding UacLevel enum value
                        int newUacLevelValue = Convert.ToInt32(sliderValue);
                        var currentUacLevel = selectedUacLevelProperty.GetValue(viewModel);

                        // Get the UacLevel enum type
                        var uacLevelType = selectedUacLevelProperty.PropertyType;

                        // Convert the integer to the corresponding UacLevel enum value
                        var newUacLevel = (Winhance.Core.Models.Enums.UacLevel)
                            Enum.ToObject(uacLevelType, newUacLevelValue);
                        var currentUacLevelTyped =
                            currentUacLevel != null
                                ? (Winhance.Core.Models.Enums.UacLevel)currentUacLevel
                                : Winhance.Core.Models.Enums.UacLevel.NotifyChangesOnly;

                        if (currentUacLevelTyped != newUacLevel)
                        {
                            // Define isApplyingProperty at the beginning of the block for proper scope
                            var isApplyingProperty = viewModel
                                .GetType()
                                .GetProperty(
                                    "IsApplyingUacLevel",
                                    System.Reflection.BindingFlags.Public
                                        | System.Reflection.BindingFlags.NonPublic
                                        | System.Reflection.BindingFlags.Instance
                                );

                            if (isApplyingProperty != null)
                            {
                                _logService.Log(
                                    LogLevel.Info,
                                    "Found IsApplyingUacLevel property, setting to true"
                                );
                                isApplyingProperty.SetValue(viewModel, true);

                                // Add a small delay to ensure the property takes effect
                                await Task.Delay(50, cancellationToken);
                            }

                            try
                            {
                                // Use reflection to set the field directly to avoid triggering HandleUACLevelChange
                                var field = viewModel
                                    .GetType()
                                    .GetField(
                                        "_selectedUacLevel",
                                        System.Reflection.BindingFlags.NonPublic
                                            | System.Reflection.BindingFlags.Instance
                                    );

                                if (field != null)
                                {
                                    field.SetValue(viewModel, newUacLevel);
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"Directly updated _selectedUacLevel field to {newUacLevel}"
                                    );

                                    // Trigger property changed notification without calling HandleUACLevelChange
                                    // Specify the parameter types to avoid ambiguous match error
                                    var onPropertyChangedMethod = viewModel
                                        .GetType()
                                        .GetMethod(
                                            "OnPropertyChanged",
                                            System.Reflection.BindingFlags.NonPublic
                                                | System.Reflection.BindingFlags.Instance,
                                            null,
                                            new[] { typeof(string) },
                                            null
                                        );

                                    if (onPropertyChangedMethod != null)
                                    {
                                        onPropertyChangedMethod.Invoke(
                                            viewModel,
                                            new object[] { "SelectedUacLevel" }
                                        );
                                        _logService.Log(
                                            LogLevel.Info,
                                            "Manually triggered OnPropertyChanged for SelectedUacLevel"
                                        );
                                    }
                                    else
                                    {
                                        // Fallback: Try with PropertyChangedEventArgs parameter
                                        var args =
                                            new System.ComponentModel.PropertyChangedEventArgs(
                                                "SelectedUacLevel"
                                            );
                                        var altMethod = viewModel
                                            .GetType()
                                            .GetMethod(
                                                "OnPropertyChanged",
                                                System.Reflection.BindingFlags.NonPublic
                                                    | System.Reflection.BindingFlags.Instance,
                                                null,
                                                new[]
                                                {
                                                    typeof(System.ComponentModel.PropertyChangedEventArgs),
                                                },
                                                null
                                            );

                                        if (altMethod != null)
                                        {
                                            altMethod.Invoke(viewModel, new object[] { args });
                                            _logService.Log(
                                                LogLevel.Info,
                                                "Manually triggered OnPropertyChanged with PropertyChangedEventArgs"
                                            );
                                        }
                                        else
                                        {
                                            _logService.Log(
                                                LogLevel.Warning,
                                                "Could not find appropriate OnPropertyChanged method"
                                            );
                                        }
                                    }

                                    // Add a small delay to ensure property change notifications are processed
                                    await Task.Delay(100, cancellationToken);
                                }
                                else
                                {
                                    // Fallback to direct property setting if field not found
                                    _logService.Log(
                                        LogLevel.Warning,
                                        "Could not find _selectedUacLevel field, using property setter instead"
                                    );
                                    viewModel.SelectedUacLevel = (int)newUacLevel;
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"Updated SelectedUacLevel property to {newUacLevel}"
                                    );

                                    // Add a small delay to ensure property change notifications are processed
                                    await Task.Delay(100, cancellationToken);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(
                                    LogLevel.Error,
                                    $"Error updating UAC level value: {ex.Message}"
                                );
                                throw; // Rethrow to be caught by outer catch
                            }

                            // Now actually apply the UAC level
                            try
                            {
                                // Reset IsApplyingUacLevel if it exists
                                if (isApplyingProperty != null)
                                {
                                    isApplyingProperty.SetValue(viewModel, false);
                                    _logService.Log(
                                        LogLevel.Info,
                                        "Reset IsApplyingUacLevel to false"
                                    );
                                }

                                // Call the method to apply the UAC level
                                var handleUacLevelChangeMethod = viewModel
                                    .GetType()
                                    .GetMethod(
                                        "HandleUACLevelChange",
                                        System.Reflection.BindingFlags.Public
                                            | System.Reflection.BindingFlags.NonPublic
                                            | System.Reflection.BindingFlags.Instance
                                    );

                                if (handleUacLevelChangeMethod != null)
                                {
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"Calling HandleUACLevelChange to apply UAC level: {newUacLevel}"
                                    );
                                    // HandleUACLevelChange doesn't take any parameters
                                    handleUacLevelChangeMethod.Invoke(viewModel, null);
                                    _logService.Log(
                                        LogLevel.Success,
                                        $"Successfully applied UAC level: {newUacLevel}"
                                    );
                                }
                                else
                                {
                                    // Try to find a method that takes no parameters
                                    handleUacLevelChangeMethod = viewModel
                                        .GetType()
                                        .GetMethod(
                                            "HandleUACLevelChange",
                                            System.Reflection.BindingFlags.Public
                                                | System.Reflection.BindingFlags.NonPublic
                                                | System.Reflection.BindingFlags.Instance,
                                            null,
                                            Type.EmptyTypes,
                                            null
                                        );

                                    if (handleUacLevelChangeMethod != null)
                                    {
                                        _logService.Log(
                                            LogLevel.Info,
                                            $"Calling parameterless HandleUACLevelChange to apply UAC level: {newUacLevel}"
                                        );
                                        handleUacLevelChangeMethod.Invoke(viewModel, null);
                                        _logService.Log(
                                            LogLevel.Success,
                                            $"Successfully applied UAC level: {newUacLevel}"
                                        );
                                    }
                                    else
                                    {
                                        _logService.Log(
                                            LogLevel.Warning,
                                            "Could not find HandleUACLevelChange method"
                                        );
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(
                                    LogLevel.Error,
                                    $"Error applying UAC level: {ex.Message}"
                                );
                            }
                            finally
                            {
                                // Make sure IsApplyingUacLevel is reset to false when done
                                if (
                                    isApplyingProperty != null
                                    && isApplyingProperty.GetValue(viewModel) is true
                                )
                                {
                                    isApplyingProperty.SetValue(viewModel, false);
                                    _logService.Log(
                                        LogLevel.Info,
                                        "Reset IsApplyingUacLevel to false in finally block"
                                    );
                                }
                            } // End of the if (currentUacLevel != newUacLevel) block
                        }

                        updatedCount++;
                    }
                }

                // Then apply to the Settings collection as usual
                int settingsUpdatedCount = await _propertyUpdater.UpdateItemsAsync(
                    viewModel.Settings,
                    configFile
                );
                updatedCount += settingsUpdatedCount;
            }
            catch (TaskCanceledException)
            {
                _logService.Log(
                    LogLevel.Warning,
                    "ApplySecuritySettings was canceled due to timeout"
                );
                return 0;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying security settings: {ex.Message}");
                return 0;
            }
            return 0;
        }
    }
}
