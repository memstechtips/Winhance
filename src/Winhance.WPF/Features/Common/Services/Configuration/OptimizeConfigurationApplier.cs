using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Optimize.ViewModels;

namespace Winhance.WPF.Features.Common.Services.Configuration
{
    /// <summary>
    /// Service for applying configuration to the Optimize section.
    /// </summary>
    public class OptimizeConfigurationApplier : ISectionConfigurationApplier
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogService _logService;
        private readonly IViewModelRefresher _viewModelRefresher;
        private readonly IConfigurationPropertyUpdater _propertyUpdater;

        /// <summary>
        /// Gets the section name that this applier handles.
        /// </summary>
        public string SectionName => "Optimize";

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizeConfigurationApplier"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="viewModelRefresher">The view model refresher.</param>
        /// <param name="propertyUpdater">The property updater.</param>
        public OptimizeConfigurationApplier(
            IServiceProvider serviceProvider,
            ILogService logService,
            IViewModelRefresher viewModelRefresher,
            IConfigurationPropertyUpdater propertyUpdater
        )
        {
            _serviceProvider =
                serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _viewModelRefresher =
                viewModelRefresher ?? throw new ArgumentNullException(nameof(viewModelRefresher));
            _propertyUpdater =
                propertyUpdater ?? throw new ArgumentNullException(nameof(propertyUpdater));
        }

        /// <summary>
        /// Applies the configuration to the Optimize section.
        /// </summary>
        /// <param name="configFile">The configuration file to apply.</param>
        /// <returns>True if any items were updated, false otherwise.</returns>
        public async Task<bool> ApplyConfigAsync(ConfigurationFile configFile)
        {
            try
            {
                _logService.Log(LogLevel.Info, "Applying configuration to OptimizeViewModel");

                var viewModel = _serviceProvider.GetService<OptimizeViewModel>();
                if (viewModel == null)
                {
                    _logService.Log(LogLevel.Warning, "OptimizeViewModel not available");
                    return false;
                }

                // Set a timeout for the operation to prevent hanging
                using var cancellationTokenSource = new System.Threading.CancellationTokenSource(
                    TimeSpan.FromSeconds(45)
                );
                var cancellationToken = cancellationTokenSource.Token;

                // Add a log entry to track execution time
                var startTime = DateTime.Now;
                _logService.Log(
                    LogLevel.Info,
                    $"Starting OptimizeConfigurationApplier.ApplyConfigAsync at {startTime}"
                );

                // Ensure the view model is initialized
                if (!viewModel.IsInitialized)
                {
                    _logService.Log(
                        LogLevel.Info,
                        "OptimizeViewModel not initialized, initializing now"
                    );
                    try
                    {
                        var initializeTask = viewModel.InitializeCommand.ExecuteAsync(null);
                        await Task.WhenAny(initializeTask, Task.Delay(10000, cancellationToken)); // 10 second timeout

                        if (!initializeTask.IsCompleted)
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                "Initialization timed out, proceeding with partial initialization"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Error,
                            $"Error initializing OptimizeViewModel: {ex.Message}"
                        );
                        // Continue with the import even if initialization fails
                    }
                }

                int totalUpdatedCount = 0;

                // Apply the configuration directly to the view model's items
                try
                {
                    int mainItemsUpdatedCount = await _propertyUpdater.UpdateItemsAsync(
                        viewModel.Items,
                        configFile
                    );
                    totalUpdatedCount += mainItemsUpdatedCount;
                    _logService.Log(
                        LogLevel.Info,
                        $"Updated {mainItemsUpdatedCount} items in OptimizeViewModel.Items"
                    );
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error updating main items: {ex.Message}");
                }

                // Also apply to child view models - wrap each in try/catch to ensure one failure doesn't stop others

                // Gaming and Performance Settings
                if (viewModel.GamingandPerformanceOptimizationsViewModel?.Settings != null)
                {
                    try
                    {
                        int updatedCount = await _propertyUpdater.UpdateItemsAsync(
                            viewModel.GamingandPerformanceOptimizationsViewModel.Settings,
                            configFile
                        );
                        totalUpdatedCount += updatedCount;
                        _logService.Log(
                            LogLevel.Info,
                            $"Updated {updatedCount} items in GamingandPerformanceOptimizationsViewModel"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Error,
                            $"Error updating GamingandPerformanceOptimizationsViewModel: {ex.Message}"
                        );
                    }
                }

                // Privacy Settings
                if (viewModel.PrivacyOptimizationsViewModel?.Settings != null)
                {
                    try
                    {
                        int updatedCount = await _propertyUpdater.UpdateItemsAsync(
                            viewModel.PrivacyOptimizationsViewModel.Settings,
                            configFile
                        );
                        totalUpdatedCount += updatedCount;
                        _logService.Log(
                            LogLevel.Info,
                            $"Updated {updatedCount} items in PrivacyOptimizationsViewModel"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Error,
                            $"Error updating PrivacyOptimizationsViewModel: {ex.Message}"
                        );
                    }
                }

                // Update Settings
                if (viewModel.UpdateOptimizationsViewModel?.Settings != null)
                {
                    try
                    {
                        int updatedCount = await _propertyUpdater.UpdateItemsAsync(
                            viewModel.UpdateOptimizationsViewModel.Settings,
                            configFile
                        );
                        totalUpdatedCount += updatedCount;
                        _logService.Log(
                            LogLevel.Info,
                            $"Updated {updatedCount} items in UpdateOptimizationsViewModel"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Error,
                            $"Error updating UpdateOptimizationsViewModel: {ex.Message}"
                        );
                    }
                }

                // Power Settings - this is the most likely place for hangs
                if (viewModel.PowerSettingsViewModel?.Settings != null)
                {
                    try
                    {
                        // Use a separate timeout for power settings
                        using var powerSettingsCts = new System.Threading.CancellationTokenSource(
                            TimeSpan.FromSeconds(10)
                        );

                        var powerSettingsTask = ApplyPowerSettings(
                            viewModel.PowerSettingsViewModel,
                            configFile
                        );
                        await Task.WhenAny(
                            powerSettingsTask,
                            Task.Delay(10000, powerSettingsCts.Token)
                        );

                        if (powerSettingsTask.IsCompleted)
                        {
                            int updatedCount = await powerSettingsTask;
                            totalUpdatedCount += updatedCount;
                            _logService.Log(
                                LogLevel.Info,
                                $"Updated {updatedCount} items in PowerSettingsViewModel"
                            );
                        }
                        else
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                "Power settings update timed out, skipping"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Error,
                            $"Error updating PowerSettingsViewModel: {ex.Message}"
                        );
                    }
                }

                // Explorer Settings
                if (viewModel.ExplorerOptimizationsViewModel?.Settings != null)
                {
                    try
                    {
                        int updatedCount = await _propertyUpdater.UpdateItemsAsync(
                            viewModel.ExplorerOptimizationsViewModel.Settings,
                            configFile
                        );
                        totalUpdatedCount += updatedCount;
                        _logService.Log(
                            LogLevel.Info,
                            $"Updated {updatedCount} items in ExplorerOptimizationsViewModel"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Error,
                            $"Error updating ExplorerOptimizationsViewModel: {ex.Message}"
                        );
                    }
                }

                // Notification Settings
                if (viewModel.NotificationOptimizationsViewModel?.Settings != null)
                {
                    try
                    {
                        int updatedCount = await _propertyUpdater.UpdateItemsAsync(
                            viewModel.NotificationOptimizationsViewModel.Settings,
                            configFile
                        );
                        totalUpdatedCount += updatedCount;
                        _logService.Log(
                            LogLevel.Info,
                            $"Updated {updatedCount} items in NotificationOptimizationsViewModel"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Error,
                            $"Error updating NotificationOptimizationsViewModel: {ex.Message}"
                        );
                    }
                }

                // Sound Settings
                if (viewModel.SoundOptimizationsViewModel?.Settings != null)
                {
                    try
                    {
                        int updatedCount = await _propertyUpdater.UpdateItemsAsync(
                            viewModel.SoundOptimizationsViewModel.Settings,
                            configFile
                        );
                        totalUpdatedCount += updatedCount;
                        _logService.Log(
                            LogLevel.Info,
                            $"Updated {updatedCount} items in SoundOptimizationsViewModel"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Error,
                            $"Error updating SoundOptimizationsViewModel: {ex.Message}"
                        );
                    }
                }

                // Windows Security Settings
                _logService.Log(LogLevel.Info, "Starting to process Windows Security Settings");
                var securityStartTime = DateTime.Now;

                if (viewModel.WindowsSecuritySettingsViewModel?.Settings != null)
                {
                    try
                    {
                        // Use a separate timeout for security settings
                        using var securitySettingsCts =
                            new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));

                        var securitySettingsTask = ApplySecuritySettings(
                            viewModel.WindowsSecuritySettingsViewModel,
                            configFile
                        );
                        await Task.WhenAny(
                            securitySettingsTask,
                            Task.Delay(10000, securitySettingsCts.Token)
                        );

                        if (securitySettingsTask.IsCompleted)
                        {
                            int updatedCount = await securitySettingsTask;
                            totalUpdatedCount += updatedCount;
                            _logService.Log(
                                LogLevel.Info,
                                $"Updated {updatedCount} items in WindowsSecuritySettingsViewModel"
                            );
                        }
                        else
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                "Security settings update timed out, skipping"
                            );
                            securitySettingsCts.Cancel();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Error,
                            $"Error updating WindowsSecuritySettingsViewModel: {ex.Message}"
                        );
                    }

                    var securityEndTime = DateTime.Now;
                    var securityExecutionTime = securityEndTime - securityStartTime;
                    _logService.Log(
                        LogLevel.Info,
                        $"Completed Windows Security Settings processing in {securityExecutionTime.TotalSeconds:F2} seconds"
                    );
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Updated a total of {totalUpdatedCount} items in OptimizeViewModel and its child view models"
                );

                // Refresh the UI
                try
                {
                    await _viewModelRefresher.RefreshViewModelAsync(viewModel);
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error refreshing view model: {ex.Message}");
                }

                // Skip calling ApplyOptimizations during import to avoid system changes
                // We'll just update the UI to reflect the imported values

                // Reload the main Items collection to reflect the changes in child view models
                try
                {
                    await viewModel.LoadItemsAsync();
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error reloading items: {ex.Message}");
                }

                // Calculate and log execution time
                var endTime = DateTime.Now;
                var executionTime = endTime - startTime;
                _logService.Log(
                    LogLevel.Info,
                    $"Completed OptimizeConfigurationApplier.ApplyConfigAsync in {executionTime.TotalSeconds:F2} seconds"
                );

                return totalUpdatedCount > 0;
            }
            catch (TaskCanceledException)
            {
                _logService.Log(
                    LogLevel.Warning,
                    "OptimizeConfigurationApplier.ApplyConfigAsync was canceled due to timeout"
                );
                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying Optimize configuration: {ex.Message}"
                );
                return false;
            }
        }

        private async Task<int> ApplyPowerSettings(
            PowerOptimizationsViewModel viewModel,
            ConfigurationFile configFile
        )
        {
            int updatedCount = 0;

            // Use a cancellation token with a timeout to prevent hanging
            using var cancellationTokenSource = new CancellationTokenSource(
                TimeSpan.FromSeconds(15)
            );
            var cancellationToken = cancellationTokenSource.Token;

            // Track execution time
            var startTime = DateTime.Now;
            _logService.Log(LogLevel.Info, $"Starting ApplyPowerSettings at {startTime}");

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
                                var powerPlanComboBox = viewModel.Settings.FirstOrDefault(s =>
                                    s.Id == "PowerPlanComboBox"
                                    || s.Name?.Contains("Power Plan") == true
                                );

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
                    var powerPlanSetting = viewModel.Settings?.FirstOrDefault(s =>
                        s.Id == "PowerPlanComboBox" || s.Name?.Contains("Power Plan") == true
                    );

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
                            ControlType = ControlType.ComboBox,
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

        private async Task<int> ApplySecuritySettings(
            WindowsSecurityOptimizationsViewModel viewModel,
            ConfigurationFile configFile
        )
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
                                    viewModel.SelectedUacLevel =
                                        (Winhance.Core.Models.Enums.UacLevel)newUacLevel;
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
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying security settings: {ex.Message}");
            }

            // Calculate and log execution time
            var endTime = DateTime.Now;
            var executionTime = endTime - startTime;
            _logService.Log(
                LogLevel.Info,
                $"Completed ApplySecuritySettings in {executionTime.TotalSeconds:F2} seconds"
            );

            return updatedCount;
        }
    }
}
