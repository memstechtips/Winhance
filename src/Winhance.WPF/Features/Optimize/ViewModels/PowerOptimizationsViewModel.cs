using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Features.Optimize.Services;
using Winhance.Infrastructure.Features.Common.Registry;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for power optimizations.
    /// </summary>
    public partial class PowerOptimizationsViewModel : BaseSettingsViewModel<ApplicationSettingItem>
    {
        private readonly IPowerPlanService _powerPlanService;
        private readonly IPowerSettingService? _powerSettingService;
        private readonly IPowerPlanManagerService? _powerPlanManagerService;
        private readonly IViewModelLocator? _viewModelLocator;
        private readonly ISettingsRegistry? _settingsRegistry;
        private readonly IBatteryService? _batteryService;
        private List<PowerPlan> _availablePowerPlans = new();
        private string _activePowerPlanGuid = string.Empty;
        private bool _showAdvancedSettings;

        [ObservableProperty]
        private bool _hasBattery;

        [ObservableProperty]
        private bool _hasLid;

        [ObservableProperty]
        private int _powerPlanValue;

        [ObservableProperty]
        private bool _isApplyingPowerPlan;

        [ObservableProperty]
        private string _statusText = "Power settings";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasAdvancedPowerSettings))]
        private ObservableCollection<AdvancedPowerSettingGroup> _advancedPowerSettingGroups = new();

        /// <summary>
        /// Gets a value indicating whether there are any advanced power settings available.
        /// </summary>
        public bool HasAdvancedPowerSettings => AdvancedPowerSettingGroups.Count > 0;

        [ObservableProperty]
        private bool _showAdvancedPowerSettings;

        /// <summary>
        /// Called when ShowAdvancedPowerSettings property changes
        /// </summary>
        partial void OnShowAdvancedPowerSettingsChanged(bool value)
        {
            // If showing advanced settings, ensure they're loaded
            if (value && _powerSettingService != null && _powerPlanManagerService != null)
            {
                _logService.Log(
                    LogLevel.Info,
                    "ShowAdvancedPowerSettings changed to true - ensuring settings are loaded"
                );
                _ = LoadAdvancedPowerSettingsAsync();
            }
        }

        [ObservableProperty]
        private bool _isLoadingAdvancedSettings;

        [ObservableProperty]
        private PowerPlan _selectedPowerPlan;

        /// <summary>
        /// Called when SelectedPowerPlan property changes
        /// </summary>
        partial void OnSelectedPowerPlanChanged(PowerPlan value)
        {
            // If advanced settings are visible and we have a new power plan, reload the settings
            if (value != null && ShowAdvancedPowerSettings && _powerSettingService != null)
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Selected power plan changed to {value.Name} - reloading advanced settings"
                );
                _ = LoadAdvancedPowerSettingsAsync();
            }
        }

        [ObservableProperty]
        private ObservableCollection<PowerPlan> _powerPlans = new();

        [ObservableProperty]
        private ObservableCollection<string> _powerPlanLabels = new()
        {
            "Balanced",
            "High Performance",
            "Ultimate Performance",
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="powerPlanService">The power plan service.</param>
        /// <param name="powerSettingService">The power setting service.</param>
        /// <param name="powerPlanManagerService">The power plan manager service.</param>
        /// <param name="viewModelLocator">The view model locator.</param>
        /// <param name="settingsRegistry">The settings registry.</param>
        /// <param name="batteryService">The battery service.</param>
        public PowerOptimizationsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService,
            IPowerPlanService powerPlanService,
            IPowerSettingService? powerSettingService = null,
            IPowerPlanManagerService? powerPlanManagerService = null,
            IViewModelLocator? viewModelLocator = null,
            ISettingsRegistry? settingsRegistry = null,
            IBatteryService? batteryService = null
        )
            : base(progressService, registryService, logService)
        {
            _powerPlanService =
                powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            _powerSettingService = powerSettingService;
            _powerPlanManagerService = powerPlanManagerService;
            _viewModelLocator = viewModelLocator;
            _settingsRegistry = settingsRegistry;
            _batteryService = batteryService;

            // Initialize battery detection
            _ = InitializeBatteryDetectionAsync();
        }

        /// <summary>
        /// Cleans up event subscriptions for all settings.
        /// </summary>
        private void CleanupSettingsEvents()
        {
            foreach (var setting in Settings)
            {
                // Check if this is a PowerCfg setting
                if (
                    setting.CustomProperties != null
                    && setting.CustomProperties.ContainsKey("PowerCfgSettings")
                )
                {
                    // Unsubscribe from property changes
                    setting.PropertyChanged -= HandleSettingPropertyChanged;
                }
            }
        }

        /// <summary>
        /// Loads the power settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                _logService.Log(LogLevel.Info, "Loading power settings");

                // Clean up event subscriptions before clearing settings
                CleanupSettingsEvents();

                // Load advanced power settings if the service is available
                if (_powerSettingService != null && _powerPlanManagerService != null)
                {
                    await LoadAdvancedPowerSettingsAsync();
                }

                // Initialize Power settings from PowerOptimizations.GetPowerOptimizations()
                Settings.Clear();

                // The Power Plan ComboBox is already defined in the XAML, so we don't need to add it to the Settings collection
                _logService.Log(LogLevel.Info, "Power Plan ComboBox is defined in XAML");

                // Load available power plans including custom ones
                await LoadAvailablePowerPlansAsync();

                // Get power optimizations from the new method
                var powerOptimizations = PowerOptimizations.GetPowerOptimizations();

                // Add items for each optimization setting
                foreach (var setting in powerOptimizations.Settings)
                {
                    // Create a view model for each setting
                    var settingItem = new ApplicationSettingItem(
                        _registryService,
                        null,
                        _logService
                    )
                    {
                        Id = setting.Id,
                        Name = setting.Name,
                        Description = setting.Description,
                        IsUpdatingFromCode = true, // Set this to true to allow RefreshStatus to set the correct state
                        GroupName = setting.GroupName,
                        Dependencies = setting.Dependencies,
                        ControlType = setting.ControlType,
                    };

                    // Check if this is a PowerCfg setting
                    if (
                        setting.CustomProperties != null
                        && setting.CustomProperties.ContainsKey("PowerCfgSettings")
                    )
                    {
                        // Copy the PowerCfg settings to the view model
                        settingItem.CustomProperties = new Dictionary<string, object>(
                            setting.CustomProperties
                        );
                        _logService.Log(
                            LogLevel.Info,
                            $"Added PowerCfg setting: {setting.Name} with {((List<PowerCfgSetting>)setting.CustomProperties["PowerCfgSettings"]).Count} commands"
                        );

                        // Subscribe to property changes to handle PowerCfg settings
                        settingItem.PropertyChanged += HandleSettingPropertyChanged;
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
                        settingItem.LinkedRegistrySettings = setting.CreateLinkedRegistrySettings();
                        _logService.Log(
                            LogLevel.Info,
                            $"Setting up linked registry settings for {setting.Name} with {setting.RegistrySettings.Count} entries and logic {setting.LinkedSettingsLogic}"
                        );

                        // Log details about each registry entry for debugging
                        foreach (var regSetting in setting.RegistrySettings)
                        {
                            _logService.Log(
                                LogLevel.Info,
                                $"Linked registry entry: {regSetting.Hive}\\{regSetting.SubKey}\\{regSetting.Name}, IsPrimary={regSetting.IsPrimary}"
                            );
                        }
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"No registry settings found for {setting.Name}"
                        );
                    }

                    // Register the setting in the settings registry if available
                    if (_settingsRegistry != null && !string.IsNullOrEmpty(settingItem.Id))
                    {
                        _settingsRegistry.RegisterSetting(settingItem);
                        _logService.Log(
                            LogLevel.Info,
                            $"Registered setting {settingItem.Id} in settings registry during creation"
                        );
                    }

                    Settings.Add(settingItem);
                }

                // Refresh status for all settings to populate LinkedRegistrySettingsWithValues
                foreach (var setting in Settings)
                {
                    await setting.RefreshStatus();
                }

                // Set up power plan ComboBox
                await LoadCurrentPowerPlanAsync();
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading power settings: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Loads all available power plans including custom ones.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task LoadAvailablePowerPlansAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading available power plans");

                // Get all available power plans from the service
                var allPlans = await _powerPlanService.GetAvailablePowerPlansAsync();
                _logService.Log(LogLevel.Info, $"Found {allPlans.Count} power plans");

                // Clear existing collections
                _availablePowerPlans.Clear();
                PowerPlanLabels.Clear();

                // Create a new ordered list to ensure consistent indexing
                List<PowerPlan> orderedPlans = new List<PowerPlan>();

                // First add standard plans in a specific order if they exist
                AddPlanIfAvailable(allPlans, orderedPlans, "Balanced");
                AddPlanIfAvailable(allPlans, orderedPlans, "High Performance");
                AddPlanIfAvailable(allPlans, orderedPlans, "Ultimate Performance");

                // Then add any remaining plans that weren't already added
                foreach (var plan in allPlans)
                {
                    if (!orderedPlans.Any(p => p.Guid == plan.Guid))
                    {
                        orderedPlans.Add(plan);
                        _logService.Log(
                            LogLevel.Info,
                            $"Added custom power plan: {plan.Name} ({plan.Guid})"
                        );
                    }
                }

                // If no plans were found (unlikely), add the default ones
                if (orderedPlans.Count == 0)
                {
                    _logService.Log(LogLevel.Warning, "No power plans found, adding default plans");
                    orderedPlans = PowerOptimizations.PowerPlans.GetAllPowerPlans();
                }

                // Update our collections with the ordered plans
                _availablePowerPlans = orderedPlans;

                // Ensure PowerPlanLabels has the same order as _availablePowerPlans
                foreach (var plan in _availablePowerPlans)
                {
                    PowerPlanLabels.Add(plan.Name);
                    _logService.Log(
                        LogLevel.Info,
                        $"Added to UI dropdown: {plan.Name} ({plan.Guid})"
                    );
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Final power plan count: {_availablePowerPlans.Count} plans, {PowerPlanLabels.Count} labels"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error loading available power plans: {ex.Message}"
                );

                // Fallback to default plans
                PowerPlanLabels.Clear();
                _availablePowerPlans = PowerOptimizations.PowerPlans.GetAllPowerPlans();

                foreach (var plan in _availablePowerPlans)
                {
                    PowerPlanLabels.Add(plan.Name);
                }
            }
        }

        /// <summary>
        /// Adds a power plan to the ordered list if it exists in the available plans.
        /// </summary>
        /// <param name="allPlans">All available power plans.</param>
        /// <param name="orderedPlans">The ordered list to add the plan to.</param>
        /// <param name="planName">The name of the power plan to add.</param>
        private void AddPlanIfAvailable(
            List<PowerPlan> allPlans,
            List<PowerPlan> orderedPlans,
            string planName
        )
        {
            var plan = allPlans.FirstOrDefault(p =>
                p.Name.Contains(planName, StringComparison.OrdinalIgnoreCase)
            );
            if (plan != null)
            {
                orderedPlans.Add(plan);
                _logService.Log(
                    LogLevel.Info,
                    $"Added standard power plan to ordered list: {plan.Name} ({plan.Guid})"
                );
            }
        }

        /// <summary>
        /// Loads the current power plan and sets the ComboBox value accordingly.
        /// </summary>
        private async Task LoadCurrentPowerPlanAsync()
        {
            // Use a cancellation token with a timeout to prevent hanging
            using var cancellationTokenSource = new CancellationTokenSource(
                TimeSpan.FromSeconds(10)
            );
            var cancellationToken = cancellationTokenSource.Token;

            try
            {
                _logService.Log(LogLevel.Info, "Starting to load current power plan");

                // Get the current active power plan GUID using the service with timeout
                var getPlanTask = _powerPlanService.GetActivePowerPlanGuidAsync();
                await Task.WhenAny(getPlanTask, Task.Delay(5000, cancellationToken));

                string activePlanGuid;
                if (getPlanTask.IsCompleted)
                {
                    activePlanGuid = await getPlanTask;
                    _logService.Log(LogLevel.Info, $"Active power plan GUID: {activePlanGuid}");
                }
                else
                {
                    _logService.Log(
                        LogLevel.Warning,
                        "GetActivePowerPlanGuidAsync timed out, defaulting to Balanced"
                    );
                    activePlanGuid = PowerOptimizations.PowerPlans.Balanced.Guid;
                    cancellationTokenSource.Cancel();
                }

                // Log all available power plans for debugging
                _logService.Log(LogLevel.Info, "Available power plans:");
                for (int i = 0; i < _availablePowerPlans.Count; i++)
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"  [{i}] {_availablePowerPlans[i].Name} ({_availablePowerPlans[i].Guid})"
                    );
                }

                // Find the index of the active power plan in our list by exact GUID match
                int planIndex = _availablePowerPlans.FindIndex(p =>
                    string.Equals(p.Guid, activePlanGuid, StringComparison.OrdinalIgnoreCase)
                );
                _logService.Log(LogLevel.Info, $"Initial plan index by GUID: {planIndex}");

                // If not found, check if it's the Ultimate Performance plan with a different GUID
                if (planIndex == -1)
                {
                    // Get the Ultimate Performance GUID from the service
                    string ultimatePerformanceGuid;
                    var field =
                        typeof(Winhance.Infrastructure.Features.Optimize.Services.PowerPlanService).GetField(
                            "ULTIMATE_PERFORMANCE_PLAN_GUID",
                            System.Reflection.BindingFlags.Public
                                | System.Reflection.BindingFlags.Static
                        );

                    if (field != null)
                    {
                        ultimatePerformanceGuid = (string)field.GetValue(null);
                        _logService.Log(
                            LogLevel.Info,
                            $"Using Ultimate Performance GUID from service: {ultimatePerformanceGuid}"
                        );

                        if (
                            string.Equals(
                                activePlanGuid,
                                ultimatePerformanceGuid,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            // Find the Ultimate Performance plan by name
                            planIndex = _availablePowerPlans.FindIndex(p =>
                                p.Name.Contains(
                                    "Ultimate Performance",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            );
                            _logService.Log(
                                LogLevel.Info,
                                $"Found Ultimate Performance plan at index: {planIndex}"
                            );
                        }
                    }
                }

                // If still not found, try to find a plan with the same name
                if (planIndex == -1)
                {
                    // Get the name of the active plan from Windows
                    var allPlans = await _powerPlanService.GetAvailablePowerPlansAsync();
                    var activePlan = allPlans.FirstOrDefault(p =>
                        string.Equals(p.Guid, activePlanGuid, StringComparison.OrdinalIgnoreCase)
                    );

                    if (activePlan != null)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            $"Active plan from Windows: {activePlan.Name} ({activePlan.Guid})"
                        );

                        // Find by name in our list
                        planIndex = _availablePowerPlans.FindIndex(p =>
                            string.Equals(
                                p.Name,
                                activePlan.Name,
                                StringComparison.OrdinalIgnoreCase
                            )
                        );

                        if (planIndex != -1)
                        {
                            _logService.Log(
                                LogLevel.Info,
                                $"Found power plan by name at index: {planIndex}"
                            );

                            // Update the GUID in our list to match the actual GUID from Windows
                            // This ensures future operations use the correct GUID
                            _availablePowerPlans[planIndex].Guid = activePlan.Guid;
                            _logService.Log(
                                LogLevel.Info,
                                $"Updated GUID for {activePlan.Name} to {activePlan.Guid}"
                            );
                        }
                    }
                }

                // If still not found, default to Balanced or first available
                if (planIndex == -1)
                {
                    // Try to find Balanced plan
                    planIndex = _availablePowerPlans.FindIndex(p =>
                        p.Name.Contains("Balanced", StringComparison.OrdinalIgnoreCase)
                    );

                    if (planIndex != -1)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            $"Defaulting to Balanced power plan at index: {planIndex}"
                        );
                    }
                    // If still not found, use the first plan
                    else if (_availablePowerPlans.Count > 0)
                    {
                        planIndex = 0;
                        _logService.Log(
                            LogLevel.Info,
                            $"Defaulting to first available power plan: {_availablePowerPlans[0].Name}"
                        );
                    }
                }

                // Set the power plan value
                if (planIndex >= 0 && planIndex < _availablePowerPlans.Count)
                {
                    bool wasApplying = IsApplyingPowerPlan;
                    IsApplyingPowerPlan = true;
                    PowerPlanValue = planIndex;
                    IsApplyingPowerPlan = wasApplying;
                    _logService.Log(
                        LogLevel.Info,
                        $"Set PowerPlanValue to {planIndex} for plan: {_availablePowerPlans[planIndex].Name}"
                    );
                }
                else
                {
                    _logService.Log(
                        LogLevel.Warning,
                        "Could not find active power plan in available plans list"
                    );
                }
            }
            catch (TaskCanceledException)
            {
                _logService.Log(LogLevel.Warning, "Power plan loading was canceled due to timeout");
                bool wasApplying = IsApplyingPowerPlan;
                IsApplyingPowerPlan = true;
                PowerPlanValue = 0; // Default to first plan on timeout
                IsApplyingPowerPlan = wasApplying;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading current power plan: {ex.Message}");
                bool wasApplying = IsApplyingPowerPlan;
                IsApplyingPowerPlan = true;
                PowerPlanValue = 0; // Default to Balanced on error
                IsApplyingPowerPlan = wasApplying;
            }
        }

        /// <summary>
        /// Called when the PowerPlanValue property changes.
        /// </summary>
        /// <param name="value">The new value.</param>
        partial void OnPowerPlanValueChanged(int value)
        {
            // Only apply the power plan when not in the middle of applying a power plan
            // This prevents recursive calls and allows for importing without applying
            if (!IsApplyingPowerPlan)
            {
                _logService.Log(
                    LogLevel.Info,
                    $"PowerPlanValue changed to {value}, applying power plan"
                );
                try
                {
                    // Use ConfigureAwait(false) to avoid deadlocks
                    ApplyPowerPlanAsync(value).ConfigureAwait(false);
                    _logService.Log(
                        LogLevel.Info,
                        $"Successfully initiated power plan change to {value}"
                    );
                }
                catch (Exception ex)
                {
                    _logService.Log(
                        LogLevel.Error,
                        $"Error initiating power plan change: {ex.Message}"
                    );
                }
            }
            else
            {
                _logService.Log(
                    LogLevel.Info,
                    $"PowerPlanValue changed to {value}, but not applying because IsApplyingPowerPlan is true"
                );
            }
        }

        /// <summary>
        /// Applies the selected power plan.
        /// </summary>
        /// <param name="planIndex">The index of the power plan in the available power plans list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [RelayCommand]
        private async Task ApplyPowerPlanAsync(int planIndex)
        {
            // Double-check to prevent recursive calls
            if (IsApplyingPowerPlan)
            {
                _logService.Log(
                    LogLevel.Warning,
                    $"ApplyPowerPlanAsync called while IsApplyingPowerPlan is true, ignoring"
                );
                return;
            }

            // Use a cancellation token with a timeout to prevent hanging
            using var cancellationTokenSource = new CancellationTokenSource(
                TimeSpan.FromSeconds(15)
            );
            var cancellationToken = cancellationTokenSource.Token;

            try
            {
                IsApplyingPowerPlan = true;

                // Log all available power plans for debugging
                _logService.Log(LogLevel.Info, "Available power plans at time of selection:");
                for (int i = 0; i < _availablePowerPlans.Count; i++)
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"  [{i}] {_availablePowerPlans[i].Name} ({_availablePowerPlans[i].Guid})"
                    );
                }
                _logService.Log(LogLevel.Info, $"Selected index: {planIndex}");

                // Validate the index is within range
                if (planIndex < 0 || planIndex >= _availablePowerPlans.Count)
                {
                    _logService.Log(
                        LogLevel.Warning,
                        $"Invalid power plan index: {planIndex}, defaulting to Balanced"
                    );

                    // Try to find Balanced plan
                    var balancedPlan = _availablePowerPlans.FirstOrDefault(p =>
                        p.Name.Contains("Balanced", StringComparison.OrdinalIgnoreCase)
                    );
                    if (balancedPlan != null)
                    {
                        planIndex = _availablePowerPlans.IndexOf(balancedPlan);
                        _logService.Log(
                            LogLevel.Info,
                            $"Using Balanced power plan at index {planIndex}"
                        );
                    }
                    else if (_availablePowerPlans.Count > 0)
                    {
                        // Use the first available plan
                        planIndex = 0;
                        _logService.Log(
                            LogLevel.Info,
                            $"Using first available power plan at index {planIndex}"
                        );
                    }
                    else
                    {
                        _logService.Log(LogLevel.Error, "No power plans available");
                        StatusText = "Error: No power plans available";
                        return;
                    }
                }

                // Get the selected power plan from our list
                var selectedPlan = _availablePowerPlans[planIndex];
                string planGuid = selectedPlan.Guid;

                StatusText = $"Applying {selectedPlan.Name} power plan...";
                _logService.Log(
                    LogLevel.Info,
                    $"Applying power plan: {selectedPlan.Name} with GUID: {planGuid}"
                );

                // Special handling for Ultimate Performance plan
                if (
                    selectedPlan.Name.Contains(
                        "Ultimate Performance",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    // Get the GUID from the service directly
                    // This ensures we're using the most up-to-date GUID that might have been created dynamically
                    var field =
                        typeof(Winhance.Infrastructure.Features.Optimize.Services.PowerPlanService).GetField(
                            "ULTIMATE_PERFORMANCE_PLAN_GUID",
                            System.Reflection.BindingFlags.Public
                                | System.Reflection.BindingFlags.Static
                        );

                    if (field != null)
                    {
                        var serviceGuid = (string)field.GetValue(null);
                        if (!string.IsNullOrEmpty(serviceGuid))
                        {
                            planGuid = serviceGuid;
                            _logService.Log(
                                LogLevel.Info,
                                $"Using Ultimate Performance GUID from service: {planGuid}"
                            );
                        }
                    }
                }

                // For custom power plans, verify the GUID exists in Windows
                if (
                    !selectedPlan.Name.Contains("Balanced", StringComparison.OrdinalIgnoreCase)
                    && !selectedPlan.Name.Contains(
                        "High Performance",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && !selectedPlan.Name.Contains(
                        "Ultimate Performance",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"Custom power plan detected: {selectedPlan.Name}"
                    );

                    // Get all available plans from Windows to verify the GUID
                    var allWindowsPlans = await _powerPlanService.GetAvailablePowerPlansAsync();
                    var matchingPlan = allWindowsPlans.FirstOrDefault(p =>
                        string.Equals(p.Name, selectedPlan.Name, StringComparison.OrdinalIgnoreCase)
                    );

                    if (matchingPlan != null)
                    {
                        // Use the GUID from Windows instead of our cached one
                        planGuid = matchingPlan.Guid;
                        _logService.Log(
                            LogLevel.Info,
                            $"Using verified GUID from Windows for {selectedPlan.Name}: {planGuid}"
                        );

                        // Update our cached GUID
                        selectedPlan.Guid = planGuid;
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"Custom power plan {selectedPlan.Name} not found in Windows power plans"
                        );
                    }
                }

                // Use the service to set the power plan with timeout
                var setPlanTask = _powerPlanService.SetPowerPlanAsync(planGuid);
                var timeoutTask = Task.Delay(10000, cancellationToken); // 10 second timeout

                await Task.WhenAny(setPlanTask, timeoutTask);

                if (setPlanTask.IsCompleted)
                {
                    bool success = await setPlanTask;

                    if (success)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            $"Power plan set to {selectedPlan.Name} ({planGuid})"
                        );
                        StatusText = $"{selectedPlan.Name} power plan applied";

                        // Verify the plan was actually set by checking the active plan GUID
                        var verifyTask = _powerPlanService.GetActivePowerPlanGuidAsync();
                        await Task.WhenAny(verifyTask, Task.Delay(3000, cancellationToken));

                        if (verifyTask.IsCompleted)
                        {
                            var activeGuid = await verifyTask;
                            _logService.Log(
                                LogLevel.Info,
                                $"Verified active power plan GUID: {activeGuid}"
                            );

                            if (
                                !string.Equals(
                                    activeGuid,
                                    planGuid,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                _logService.Log(
                                    LogLevel.Warning,
                                    $"Active power plan GUID ({activeGuid}) doesn't match requested GUID ({planGuid})"
                                );
                            }
                        }

                        // Refresh the current power plan to ensure the UI is updated correctly
                        var loadTask = LoadCurrentPowerPlanAsync();
                        await Task.WhenAny(loadTask, Task.Delay(5000, cancellationToken));

                        if (!loadTask.IsCompleted)
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                "LoadCurrentPowerPlanAsync timed out, continuing anyway"
                            );
                        }

                        // Refresh all PowerCfg settings to reflect the new power plan
                        _logService.Log(
                            LogLevel.Info,
                            "Refreshing PowerCfg settings after power plan change"
                        );
                        var checkTask = CheckSettingStatusesAsync();
                        await Task.WhenAny(checkTask, Task.Delay(5000, cancellationToken));

                        if (!checkTask.IsCompleted)
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                "CheckSettingStatusesAsync timed out, continuing anyway"
                            );
                        }
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"Failed to set power plan to {selectedPlan.Name} ({planGuid})"
                        );
                        StatusText = $"Failed to apply {selectedPlan.Name} power plan";
                    }
                }
                else
                {
                    _logService.Log(
                        LogLevel.Warning,
                        $"Setting power plan timed out after 10 seconds"
                    );
                    StatusText = $"Timeout while applying {selectedPlan.Name} power plan";

                    // Cancel the operation
                    cancellationTokenSource.Cancel();
                }
            }
            catch (TaskCanceledException)
            {
                _logService.Log(
                    LogLevel.Warning,
                    "Power plan application was canceled due to timeout"
                );
                StatusText = "Power plan application timed out";
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying power plan: {ex.Message}");
                StatusText = $"Error applying power plan: {ex.Message}";
            }
            finally
            {
                // Make sure we always reset IsApplyingPowerPlan to false when done
                IsApplyingPowerPlan = false;
                _logService.Log(
                    LogLevel.Info,
                    "Reset IsApplyingPowerPlan to false in ApplyPowerPlanAsync"
                );
            }
        }

        /// <summary>
        /// Checks the status of all power settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task CheckSettingStatusesAsync()
        {
            try
            {
                IsLoading = true;
                _logService.Log(
                    LogLevel.Info,
                    $"Checking status for {Settings.Count} power settings"
                );

                foreach (var setting in Settings)
                {
                    try
                    {
                        // Check if this is a command-based setting with PowerCfg settings
                        bool isCommandBasedSetting =
                            setting.CustomProperties != null
                            && setting.CustomProperties.ContainsKey("PowerCfgSettings")
                            && setting.CustomProperties["PowerCfgSettings"]
                                is List<PowerCfgSetting>;

                        if (isCommandBasedSetting)
                        {
                            _logService.Log(
                                LogLevel.Info,
                                $"Checking command-based setting status for: {setting.Name}"
                            );

                            // Get PowerCfg settings from CustomProperties
                            var powerCfgSettings =
                                setting.CustomProperties["PowerCfgSettings"]
                                as List<PowerCfgSetting>;

                            try
                            {
                                // Special handling for Desktop Slideshow setting
                                bool isDesktopSlideshowSetting = setting.Name.Contains(
                                    "Desktop Slideshow",
                                    StringComparison.OrdinalIgnoreCase
                                );

                                // Check if all PowerCfg settings are applied
                                bool allApplied =
                                    await _powerPlanService.AreAllPowerCfgSettingsAppliedAsync(
                                        powerCfgSettings
                                    );

                                // Update setting status based on PowerCfg settings
                                setting.Status = allApplied
                                    ? RegistrySettingStatus.Applied
                                    : RegistrySettingStatus.NotApplied;

                                // Set a more descriptive current value
                                if (isDesktopSlideshowSetting)
                                {
                                    // For Desktop Slideshow, the value is counter-intuitive
                                    // When enabled (IsSelected=true), the slideshow is "Available" (value=0)
                                    // When disabled (IsSelected=false), the slideshow is "Paused" (value=1)
                                    setting.CurrentValue = allApplied ? "Available" : "Paused";
                                }
                                else
                                {
                                    setting.CurrentValue = allApplied ? "Enabled" : "Disabled";
                                }

                                // Set status message with more detailed information
                                setting.StatusMessage = allApplied
                                    ? "This setting is currently applied."
                                    : "This setting is not currently applied.";

                                _logService.Log(
                                    LogLevel.Info,
                                    $"Command-based setting {setting.Name} status: {setting.Status}, isApplied={allApplied}, currentValue={setting.CurrentValue}"
                                );

                                // Update IsSelected to match the detected state
                                setting.IsUpdatingFromCode = true;
                                try
                                {
                                    setting.IsSelected = allApplied;
                                }
                                finally
                                {
                                    setting.IsUpdatingFromCode = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(
                                    LogLevel.Error,
                                    $"Error checking PowerCfg setting status for {setting.Name}: {ex.Message}"
                                );

                                // On error, assume the setting is in its default state
                                setting.Status = RegistrySettingStatus.NotApplied;
                                setting.CurrentValue = "Unknown";
                                setting.StatusMessage =
                                    "Could not determine the current status of this setting.";
                            }
                        }
                        else if (setting.RegistrySetting != null)
                        {
                            _logService.Log(
                                LogLevel.Info,
                                $"Checking registry-based setting status for: {setting.Name}"
                            );

                            // Get the status
                            var status = await _registryService.GetSettingStatusAsync(
                                setting.RegistrySetting
                            );
                            _logService.Log(LogLevel.Info, $"Status for {setting.Name}: {status}");
                            setting.Status = status;

                            // Get the current value
                            var currentValue = await _registryService.GetCurrentValueAsync(
                                setting.RegistrySetting
                            );
                            _logService.Log(
                                LogLevel.Info,
                                $"Current value for {setting.Name}: {currentValue ?? "null"}"
                            );
                            setting.CurrentValue = currentValue;

                            setting.LinkedRegistrySettingsWithValues.Clear();
                            setting.LinkedRegistrySettingsWithValues.Add(
                                new Winhance.WPF.Features.Common.Models.LinkedRegistrySettingWithValue(
                                    setting.RegistrySetting,
                                    currentValue
                                )
                            );

                            // Set status message
                            setting.StatusMessage = GetStatusMessage(setting);

                            // Update IsSelected based on status
                            bool shouldBeSelected = status == RegistrySettingStatus.Applied;

                            // Set the checkbox state to match the registry state
                            _logService.Log(
                                LogLevel.Info,
                                $"Setting {setting.Name} status is {status}, setting IsSelected to {shouldBeSelected}"
                            );
                            setting.IsUpdatingFromCode = true;
                            try
                            {
                                setting.IsSelected = shouldBeSelected;
                            }
                            finally
                            {
                                setting.IsUpdatingFromCode = false;
                            }
                        }
                        else if (
                            setting.LinkedRegistrySettings != null
                            && setting.LinkedRegistrySettings.Settings.Count > 0
                        )
                        {
                            _logService.Log(
                                LogLevel.Info,
                                $"Checking linked registry settings status for: {setting.Name} with {setting.LinkedRegistrySettings.Settings.Count} registry entries"
                            );

                            // Log details about each registry entry for debugging
                            foreach (var regSetting in setting.LinkedRegistrySettings.Settings)
                            {
                                string hiveString = RegistryExtensions.GetRegistryHiveString(
                                    regSetting.Hive
                                );
                                string fullPath = $"{hiveString}\\{regSetting.SubKey}";
                                _logService.Log(
                                    LogLevel.Info,
                                    $"Registry entry: {fullPath}\\{regSetting.Name}, EnabledValue={regSetting.EnabledValue}, DisabledValue={regSetting.DisabledValue}"
                                );

                                // Check if the key exists
                                bool keyExists = _registryService.KeyExists(fullPath);
                                _logService.Log(LogLevel.Info, $"Key exists: {keyExists}");

                                if (keyExists)
                                {
                                    // Check if the value exists and get its current value
                                    var currentValue = await _registryService.GetCurrentValueAsync(
                                        regSetting
                                    );
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"Current value: {currentValue ?? "null"}"
                                    );
                                }
                            }

                            // Get the combined status of all linked settings
                            var status = await _registryService.GetLinkedSettingsStatusAsync(
                                setting.LinkedRegistrySettings
                            );
                            _logService.Log(
                                LogLevel.Info,
                                $"Combined status for {setting.Name}: {status}"
                            );
                            setting.Status = status;

                            // For current value display, use the first setting's value
                            if (setting.LinkedRegistrySettings.Settings.Count > 0)
                            {
                                var firstSetting = setting.LinkedRegistrySettings.Settings[0];
                                var currentValue = await _registryService.GetCurrentValueAsync(
                                    firstSetting
                                );
                                _logService.Log(
                                    LogLevel.Info,
                                    $"Current value for {setting.Name} (first entry): {currentValue ?? "null"}"
                                );
                                setting.CurrentValue = currentValue;

                                // Check for null registry values
                                bool anyNull = false;

                                // Populate the LinkedRegistrySettingsWithValues collection for tooltip display
                                setting.LinkedRegistrySettingsWithValues.Clear();
                                foreach (var regSetting in setting.LinkedRegistrySettings.Settings)
                                {
                                    var regCurrentValue =
                                        await _registryService.GetCurrentValueAsync(regSetting);
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"Current value for linked setting {regSetting.Name}: {regCurrentValue ?? "null"}"
                                    );

                                    if (regCurrentValue == null)
                                    {
                                        anyNull = true;
                                    }

                                    setting.LinkedRegistrySettingsWithValues.Add(
                                        new Winhance.WPF.Features.Common.Models.LinkedRegistrySettingWithValue(
                                            regSetting,
                                            regCurrentValue
                                        )
                                    );
                                }

                                // Set IsRegistryValueNull for linked settings
                                setting.IsRegistryValueNull = anyNull;
                            }

                            // Set status message
                            setting.StatusMessage = GetStatusMessage(setting);

                            // Update IsSelected based on status
                            bool shouldBeSelected = status == RegistrySettingStatus.Applied;

                            // Set the checkbox state to match the registry state
                            _logService.Log(
                                LogLevel.Info,
                                $"Setting {setting.Name} status is {status}, setting IsSelected to {shouldBeSelected}"
                            );
                            setting.IsUpdatingFromCode = true;
                            try
                            {
                                setting.IsSelected = shouldBeSelected;
                            }
                            finally
                            {
                                setting.IsUpdatingFromCode = false;
                            }
                        }
                        else
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                $"No registry setting or command-based setting found for {setting.Name}"
                            );
                            setting.Status = RegistrySettingStatus.Unknown;
                            setting.StatusMessage = "Setting information is missing";
                        }

                        // If this is a grouped setting, update child settings too
                        if (setting.IsGroupedSetting && setting.ChildSettings.Count > 0)
                        {
                            _logService.Log(
                                LogLevel.Info,
                                $"Updating {setting.ChildSettings.Count} child settings for {setting.Name}"
                            );
                            foreach (var childSetting in setting.ChildSettings)
                            {
                                // Check if this is a command-based child setting
                                bool isCommandBasedChildSetting =
                                    childSetting.CustomProperties != null
                                    && childSetting.CustomProperties.ContainsKey("PowerCfgSettings")
                                    && childSetting.CustomProperties["PowerCfgSettings"]
                                        is List<PowerCfgSetting>;

                                if (isCommandBasedChildSetting)
                                {
                                    try
                                    {
                                        var powerCfgSettings =
                                            childSetting.CustomProperties["PowerCfgSettings"]
                                            as List<PowerCfgSetting>;

                                        // Special handling for Desktop Slideshow setting
                                        bool isDesktopSlideshowSetting = childSetting.Name.Contains(
                                            "Desktop Slideshow",
                                            StringComparison.OrdinalIgnoreCase
                                        );

                                        bool allApplied =
                                            await _powerPlanService.AreAllPowerCfgSettingsAppliedAsync(
                                                powerCfgSettings
                                            );

                                        childSetting.Status = allApplied
                                            ? RegistrySettingStatus.Applied
                                            : RegistrySettingStatus.NotApplied;

                                        // Set a more descriptive current value
                                        if (isDesktopSlideshowSetting)
                                        {
                                            childSetting.CurrentValue = allApplied
                                                ? "Available"
                                                : "Paused";
                                        }
                                        else
                                        {
                                            childSetting.CurrentValue = allApplied
                                                ? "Enabled"
                                                : "Disabled";
                                        }

                                        childSetting.StatusMessage = allApplied
                                            ? "This setting is currently applied."
                                            : "This setting is not currently applied.";

                                        // Update IsSelected based on status
                                        childSetting.IsUpdatingFromCode = true;
                                        try
                                        {
                                            childSetting.IsSelected = allApplied;
                                        }
                                        finally
                                        {
                                            childSetting.IsUpdatingFromCode = false;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logService.Log(
                                            LogLevel.Error,
                                            $"Error checking PowerCfg setting status for child setting {childSetting.Name}: {ex.Message}"
                                        );

                                        // On error, assume the setting is in its default state
                                        childSetting.Status = RegistrySettingStatus.NotApplied;
                                        childSetting.CurrentValue = "Unknown";
                                        childSetting.StatusMessage =
                                            "Could not determine the current status of this setting.";
                                    }
                                }
                                else if (childSetting.RegistrySetting != null)
                                {
                                    var status = await _registryService.GetSettingStatusAsync(
                                        childSetting.RegistrySetting
                                    );
                                    childSetting.Status = status;

                                    var currentValue = await _registryService.GetCurrentValueAsync(
                                        childSetting.RegistrySetting
                                    );
                                    childSetting.CurrentValue = currentValue;

                                    childSetting.StatusMessage = GetStatusMessage(childSetting);

                                    // Update IsSelected based on status
                                    bool shouldBeSelected = status == RegistrySettingStatus.Applied;

                                    // Set the checkbox state to match the registry state
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"Child setting {childSetting.Name} status is {status}, setting IsSelected to {shouldBeSelected}"
                                    );
                                    childSetting.IsUpdatingFromCode = true;
                                    try
                                    {
                                        childSetting.IsSelected = shouldBeSelected;
                                    }
                                    finally
                                    {
                                        childSetting.IsUpdatingFromCode = false;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Error,
                            $"Error updating status for setting {setting.Name}: {ex.Message}"
                        );
                        setting.Status = RegistrySettingStatus.Error;
                        setting.StatusMessage = $"Error: {ex.Message}";
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error checking power setting statuses: {ex.Message}"
                );
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Updates the IsSelected state based on individual selections.
        /// </summary>
        private void UpdateIsSelectedState()
        {
            if (Settings.Count == 0)
                return;

            var selectedCount = Settings.Count(setting => setting.IsSelected);
            IsSelected = selectedCount == Settings.Count;
        }

        /// <summary>
        /// Applies a registry setting with the specified value.
        /// </summary>
        /// <param name="registrySetting">The registry setting to apply.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ApplyRegistrySetting(RegistrySetting registrySetting, object value)
        {
            // Use the registry service's ApplySettingAsync method to properly handle Group Policy settings
            if (value == null)
            {
                // For null values, create a temporary setting that will delete the value
                var tempSetting = registrySetting with 
                {
                    EnabledValue = null,
                    DisabledValue = null
                };
                
                await _registryService.ApplySettingAsync(tempSetting, false);
            }
            else
            {
                // For non-null values, create a temporary setting with the specified value
                var tempSetting = registrySetting with 
                {
                    EnabledValue = value,
                    DisabledValue = value
                };
                
                await _registryService.ApplySettingAsync(tempSetting, true);
            }
        }

        /// <summary>
        /// Handles property changes for settings, specifically to detect IsSelected changes for PowerCfg settings.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private async void HandleSettingPropertyChanged(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs e
        )
        {
            // Only handle IsSelected property changes
            if (e.PropertyName != nameof(ApplicationSettingItem.IsSelected))
            {
                return;
            }

            // Get the setting that was changed
            var setting = sender as ApplicationSettingItem;
            if (setting == null)
            {
                return;
            }

            // Skip if the setting is being updated from code
            if (setting.IsUpdatingFromCode)
            {
                return;
            }

            // Check if this is a PowerCfg setting
            if (
                setting.CustomProperties != null
                && setting.CustomProperties.ContainsKey("PowerCfgSettings")
                && setting.CustomProperties["PowerCfgSettings"]
                    is List<PowerCfgSetting> powerCfgSettings
            )
            {
                _logService.Log(
                    LogLevel.Info,
                    $"PowerCfg setting {setting.Name} toggled to {setting.IsSelected}"
                );

                try
                {
                    // Apply the PowerCfg settings
                    await ApplyPowerCfgSettingsAsync(setting, powerCfgSettings, setting.IsSelected);
                }
                catch (Exception ex)
                {
                    _logService.Log(
                        LogLevel.Error,
                        $"Error applying PowerCfg settings for {setting.Name}: {ex.Message}"
                    );
                }
            }
        }

        /// <summary>
        /// Applies PowerCfg settings based on the toggle state.
        /// </summary>
        /// <param name="setting">The setting being toggled.</param>
        /// <param name="powerCfgSettings">The PowerCfg settings to apply.</param>
        /// <param name="enable">Whether to enable or disable the settings.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ApplyPowerCfgSettingsAsync(
            ApplicationSettingItem setting,
            List<PowerCfgSetting> powerCfgSettings,
            bool enable
        )
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Applying PowerCfg settings for {setting.Name}, enable={enable}"
                );

                // Get the active power plan GUID
                string activePlanGuid = await _powerPlanService.GetActivePowerPlanGuidAsync();
                _logService.Log(LogLevel.Info, $"Active power plan GUID: {activePlanGuid}");

                // Create a list to hold the PowerCfg settings to apply
                List<PowerCfgSetting> settingsToApply = new List<PowerCfgSetting>();

                // For each PowerCfg setting, create a copy with the appropriate value based on the enable flag
                foreach (var powerCfgSetting in powerCfgSettings)
                {
                    // Create a copy of the PowerCfg setting
                    var settingToApply = new PowerCfgSetting
                    {
                        Description = powerCfgSetting.Description,
                        EnabledValue = powerCfgSetting.EnabledValue,
                        DisabledValue = powerCfgSetting.DisabledValue,
                    };

                    // Extract the base command without the value
                    string baseCommand = powerCfgSetting.Command;
                    int lastSpaceIndex = baseCommand.LastIndexOf(' ');
                    if (lastSpaceIndex > 0)
                    {
                        baseCommand = baseCommand.Substring(0, lastSpaceIndex + 1);
                    }

                    // Determine which value to use based on the enable flag
                    string valueToApply = enable
                        ? powerCfgSetting.EnabledValue
                        : powerCfgSetting.DisabledValue;

                    // Build the full command with the appropriate value
                    settingToApply.Command = baseCommand + valueToApply;

                    // Replace the placeholder with the active power plan GUID
                    settingToApply.Command = settingToApply.Command.Replace(
                        "{active_guid}",
                        activePlanGuid
                    );

                    // Add the setting to the list
                    settingsToApply.Add(settingToApply);

                    _logService.Log(
                        LogLevel.Info,
                        $"Prepared PowerCfg command: {settingToApply.Command}"
                    );
                }

                // Apply all the PowerCfg settings
                bool success = await _powerPlanService.ApplyPowerCfgSettingsAsync(settingsToApply);

                if (success)
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"Successfully applied PowerCfg settings for {setting.Name}"
                    );

                    // Update the setting status
                    setting.Status = RegistrySettingStatus.Applied;
                    setting.CurrentValue = enable ? "Enabled" : "Disabled";
                    setting.StatusMessage = "This setting has been applied.";
                }
                else
                {
                    _logService.Log(
                        LogLevel.Warning,
                        $"Failed to apply some PowerCfg settings for {setting.Name}"
                    );

                    // Update the setting status
                    setting.Status = RegistrySettingStatus.Error;
                    setting.CurrentValue = "Error";
                    setting.StatusMessage = "There was an error applying this setting.";

                    // Revert the toggle state
                    setting.IsUpdatingFromCode = true;
                    try
                    {
                        setting.IsSelected = !enable;
                    }
                    finally
                    {
                        setting.IsUpdatingFromCode = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying PowerCfg settings for {setting.Name}: {ex.Message}"
                );

                // Update the setting status
                setting.Status = RegistrySettingStatus.Error;
                setting.CurrentValue = "Error";
                setting.StatusMessage = $"Error: {ex.Message}";

                // Revert the toggle state
                setting.IsUpdatingFromCode = true;
                try
                {
                    setting.IsSelected = !enable;
                }
                finally
                {
                    setting.IsUpdatingFromCode = false;
                }

                // Re-throw the exception
                throw;
            }
        }

        /// <summary>
        /// Gets a user-friendly status message for a setting.
        /// </summary>
        /// <param name="setting">The setting to get the status message for.</param>
        /// <returns>A user-friendly status message.</returns>
        private string GetStatusMessage(ApplicationSettingItem setting)
        {
            return setting.Status switch
            {
                RegistrySettingStatus.Applied =>
                    "This setting is already applied with the recommended value.",

                RegistrySettingStatus.NotApplied => setting.CurrentValue == null
                    ? "This setting is not applied (registry value does not exist)."
                    : "This setting is using the default value.",

                RegistrySettingStatus.Modified =>
                    "This setting has a custom value that differs from the recommended value.",

                RegistrySettingStatus.Error =>
                    "An error occurred while checking this setting's status.",

                _ => "The status of this setting is unknown.",
            };
        }

        /// <summary>
        /// Converts a tuple to a RegistrySetting object.
        /// </summary>
        /// <param name="key">The key for the setting.</param>
        /// <param name="settingTuple">The tuple containing the setting information.</param>
        /// <returns>A RegistrySetting object.</returns>
        private RegistrySetting ConvertToRegistrySetting(
            string key,
            (
                string Path,
                string Name,
                object Value,
                Microsoft.Win32.RegistryValueKind ValueKind
            ) settingTuple
        )
        {
            // Determine the registry hive from the path
            RegistryHive hive = RegistryHive.LocalMachine; // Default to HKLM
            if (
                settingTuple.Path.StartsWith("HKEY_CURRENT_USER")
                || settingTuple.Path.StartsWith("HKCU")
                || settingTuple.Path.StartsWith("Software\\")
            )
            {
                hive = RegistryHive.CurrentUser;
            }
            else if (
                settingTuple.Path.StartsWith("HKEY_LOCAL_MACHINE")
                || settingTuple.Path.StartsWith("HKLM")
            )
            {
                hive = RegistryHive.LocalMachine;
            }
            else if (
                settingTuple.Path.StartsWith("HKEY_CLASSES_ROOT")
                || settingTuple.Path.StartsWith("HKCR")
            )
            {
                hive = RegistryHive.ClassesRoot;
            }
            else if (
                settingTuple.Path.StartsWith("HKEY_USERS") || settingTuple.Path.StartsWith("HKU")
            )
            {
                hive = RegistryHive.Users;
            }
            else if (
                settingTuple.Path.StartsWith("HKEY_CURRENT_CONFIG")
                || settingTuple.Path.StartsWith("HKCC")
            )
            {
                hive = RegistryHive.CurrentConfig;
            }

            // Clean up the path to remove any hive prefix
            string subKey = settingTuple.Path;
            if (subKey.StartsWith("HKEY_CURRENT_USER\\") || subKey.StartsWith("HKCU\\"))
            {
                subKey = subKey.Replace("HKEY_CURRENT_USER\\", "").Replace("HKCU\\", "");
            }
            else if (subKey.StartsWith("HKEY_LOCAL_MACHINE\\") || subKey.StartsWith("HKLM\\"))
            {
                subKey = subKey.Replace("HKEY_LOCAL_MACHINE\\", "").Replace("HKLM\\", "");
            }
            else if (subKey.StartsWith("HKEY_CLASSES_ROOT\\") || subKey.StartsWith("HKCR\\"))
            {
                subKey = subKey.Replace("HKEY_CLASSES_ROOT\\", "").Replace("HKCR\\", "");
            }
            else if (subKey.StartsWith("HKEY_USERS\\") || subKey.StartsWith("HKU\\"))
            {
                subKey = subKey.Replace("HKEY_USERS\\", "").Replace("HKU\\", "");
            }
            else if (subKey.StartsWith("HKEY_CURRENT_CONFIG\\") || subKey.StartsWith("HKCC\\"))
            {
                subKey = subKey.Replace("HKEY_CURRENT_CONFIG\\", "").Replace("HKCC\\", "");
            }

            // Create and return the RegistrySetting object
            var registrySetting = new RegistrySetting
            {
                Category = "Power",
                Hive = hive,
                SubKey = subKey,
                Name = settingTuple.Name,
                EnabledValue = settingTuple.Value, // Recommended value becomes EnabledValue
                DisabledValue = settingTuple.Value,
                ValueType = settingTuple.ValueKind,
                // Keep these for backward compatibility
                RecommendedValue = settingTuple.Value,
                DefaultValue = settingTuple.Value,
                Description = $"Power setting: {key}",
            };

            return registrySetting;
        }

        /// <summary>
        /// Loads advanced power settings for the current power plan.
        /// </summary>
        private async Task LoadAdvancedPowerSettingsAsync()
        {
            if (_powerSettingService == null || _powerPlanManagerService == null)
            {
                _logService.Log(
                    LogLevel.Warning,
                    "Cannot load advanced power settings: required services are not available"
                );
                return;
            }

            try
            {
                IsLoadingAdvancedSettings = true;
                _logService.Log(LogLevel.Info, "Loading advanced power settings");

                // Load available power plans if not already loaded
                if (PowerPlans.Count == 0)
                {
                    var powerPlans = await _powerPlanManagerService.GetPowerPlansAsync();
                    var activePlan = await _powerPlanManagerService.GetActivePowerPlanAsync();

                    // Update observable collection for UI binding
                    PowerPlans = new ObservableCollection<PowerPlan>(powerPlans);
                    SelectedPowerPlan =
                        PowerPlans.FirstOrDefault(p => p.Guid == activePlan?.Guid)
                        ?? PowerPlans.FirstOrDefault();
                }

                if (SelectedPowerPlan == null)
                {
                    _logService.Log(LogLevel.Warning, "No power plans found");
                    return;
                }

                // Load all advanced power setting groups
                // Clear existing groups
                AdvancedPowerSettingGroups.Clear();

                // Get all subgroups from the service
                var allSubgroups = _powerSettingService.GetAllSubgroups();

                // Filter out duplicate subgroups by GUID and DisplayName to ensure uniqueness
                // First group by GUID to get unique GUIDs
                var uniqueGuidGroups = allSubgroups
                    .GroupBy(sg => sg.Guid)
                    .Select(g => g.First())
                    .ToList();

                // Then further filter by DisplayName to ensure no duplicate names in the UI
                var subgroups = uniqueGuidGroups
                    .GroupBy(sg => sg.DisplayName)
                    .Select(g => g.First())
                    .ToList();

                _logService.Log(
                    LogLevel.Info,
                    $"Found {allSubgroups.Count} total subgroups, filtered to {subgroups.Count} unique subgroups"
                );

                // Add groups to observable collection
                foreach (var subgroup in subgroups)
                {
                    // Skip empty subgroups
                    if (subgroup.Settings == null || subgroup.Settings.Count == 0)
                    {
                        continue;
                    }
                    
                    // Check if the subgroup exists on the system
                    bool subgroupExists = false;
                    try
                    {
                        // Use the active power plan to check if the subgroup exists
                        subgroupExists = await _powerSettingService.DoesSettingExistAsync(
                            SelectedPowerPlan.Guid,
                            subgroup.Guid
                        );
                        
                        if (!subgroupExists)
                        {
                            _logService.Log(
                                LogLevel.Info,
                                $"Skipping non-existent subgroup: {subgroup.DisplayName} with GUID: {subgroup.Guid}"
                            );
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Error,
                            $"Error checking if subgroup exists: {subgroup.DisplayName}, {ex.Message}"
                        );
                        // Continue with the subgroup even if we couldn't verify it exists
                        // This is safer than potentially hiding valid settings
                    }

                    var group = new AdvancedPowerSettingGroup
                    {
                        Subgroup = subgroup,
                        IsExpanded = true, // Start expanded to make settings visible
                    };

                    _logService.Log(
                        LogLevel.Info,
                        $"Processing subgroup: {subgroup.DisplayName} with {subgroup.Settings.Count} settings"
                    );

                    // Add settings to the group
                    foreach (var settingDef in subgroup.Settings)
                    {
                        // Skip settings without proper definition
                        if (
                            string.IsNullOrEmpty(settingDef.Guid)
                            || string.IsNullOrEmpty(settingDef.DisplayName)
                        )
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                "Skipping invalid setting definition"
                            );
                            continue;
                        }

                        // Skip lid-related settings on devices without lids
                        // Lid close action GUID: 5ca83367-6e45-459f-a27b-476b1d01c936
                        if (
                            settingDef.Guid.Equals(
                                "5ca83367-6e45-459f-a27b-476b1d01c936",
                                StringComparison.OrdinalIgnoreCase
                            ) && !HasLid
                        )
                        {
                            _logService.Log(
                                LogLevel.Info,
                                $"Skipping lid-related setting '{settingDef.DisplayName}' on device without lid"
                            );
                            continue;
                        }

                        // Skip battery-related settings on devices without batteries
                        // Battery settings GUIDs
                        string[] batterySettingGuids = new string[]
                        {
                            "637ea02f-bbcb-4015-8e2c-a1c7b9c0b546", // Critical battery action
                            "8183ba9a-e910-48da-8769-14ae6dc1170a", // Low battery level
                            "9a66d8d7-4ff7-4ef9-b5a2-5a326ca2a469", // Critical battery level
                            "bcded951-187b-4d05-bccc-f7e51960c258", // Low battery notification
                        };

                        if (
                            batterySettingGuids.Contains(
                                settingDef.Guid,
                                StringComparer.OrdinalIgnoreCase
                            ) && !HasBattery
                        )
                        {
                            _logService.Log(
                                LogLevel.Info,
                                $"Skipping battery-related setting '{settingDef.DisplayName}' on device without battery"
                            );
                            continue;
                        }
                        
                        // Check if the setting exists on the system
                        try
                        {
                            bool settingExists = await _powerSettingService.DoesSettingExistAsync(
                                SelectedPowerPlan.Guid,
                                subgroup.Guid,
                                settingDef.Guid
                            );
                            
                            if (!settingExists)
                            {
                                _logService.Log(
                                    LogLevel.Info,
                                    $"Skipping non-existent setting: '{settingDef.DisplayName}' with GUID: {settingDef.Guid}"
                                );
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logService.Log(
                                LogLevel.Error,
                                $"Error checking if setting exists: '{settingDef.DisplayName}', {ex.Message}"
                            );
                            // Continue with the setting even if we couldn't verify it exists
                            // This is safer than potentially hiding valid settings
                        }

                        var setting = new AdvancedPowerSetting
                        {
                            Definition = settingDef,
                            IsUpdatingFromCode = true,
                        };

                        // Load current values
                        try
                        {
                            var (acValue, dcValue) =
                                await _powerSettingService.GetSettingValueAsync(
                                    SelectedPowerPlan.Guid,
                                    subgroup.Guid,
                                    settingDef.Guid
                                );

                            setting.AcValue = acValue;
                            setting.DcValue = dcValue;

                            _logService.Log(
                                LogLevel.Info,
                                $"Loaded setting: {settingDef.DisplayName}, AC={acValue}, DC={dcValue}"
                            );
                        }
                        catch (Exception ex)
                        {
                            // If we can't get the current value, use defaults
                            setting.AcValue =
                                settingDef.SettingType == PowerSettingType.Numeric
                                    ? settingDef.MinValue
                                    : 0;
                            setting.DcValue =
                                settingDef.SettingType == PowerSettingType.Numeric
                                    ? settingDef.MinValue
                                    : 0;
                            _logService.Log(
                                LogLevel.Error,
                                $"Error loading setting {settingDef.DisplayName}: {ex.Message}"
                            );
                        }

                        setting.IsUpdatingFromCode = false;
                        setting.PropertyChanged += AdvancedSetting_PropertyChanged;
                        group.Settings.Add(setting);
                    }

                    if (group.Settings.Count > 0)
                    {
                        // Check if a group with this display name already exists in the collection
                        // This is an extra safeguard against duplicates
                        if (
                            !AdvancedPowerSettingGroups.Any(g => g.DisplayName == group.DisplayName)
                        )
                        {
                            AdvancedPowerSettingGroups.Add(group);
                            _logService.Log(
                                LogLevel.Info,
                                $"Added group {group.DisplayName} with {group.Settings.Count} settings"
                            );
                        }
                        else
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                $"Skipped adding duplicate group: {group.DisplayName}"
                            );
                        }
                    }
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Loaded {AdvancedPowerSettingGroups.Count} advanced power setting groups with a total of {AdvancedPowerSettingGroups.Sum(g => g.Settings.Count)} settings"
                );

                // Force UI update
                OnPropertyChanged(nameof(HasAdvancedPowerSettings));
                OnPropertyChanged(nameof(AdvancedPowerSettingGroups));
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error loading advanced power settings: {ex.Message}"
                );
            }
            finally
            {
                IsLoadingAdvancedSettings = false;
            }
        }

        /// <summary>
        /// Handles property changed events for advanced settings.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private async void AdvancedSetting_PropertyChanged(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs e
        )
        {
            if (sender is not AdvancedPowerSetting setting)
                return;

            // Check if AC or DC value changed
            if (
                e.PropertyName == nameof(AdvancedPowerSetting.AcValue)
                || e.PropertyName == nameof(AdvancedPowerSetting.DcValue)
            )
            {
                // Apply the setting change
                await ApplyAdvancedSettingAsync(setting);
            }
        }

        /// <summary>
        /// Applies a single advanced power setting.
        /// </summary>
        /// <param name="setting">The setting to apply.</param>
        private async Task ApplyAdvancedSettingAsync(AdvancedPowerSetting setting)
        {
            if (_powerSettingService == null || SelectedPowerPlan == null)
                return;

            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Applying advanced power setting: {setting.DisplayName}"
                );

                // Create the apply value object
                var applyValue = new PowerSettingApplyValue
                {
                    PowerPlanGuid = SelectedPowerPlan.Guid,
                    SubgroupGuid = setting.SubgroupGuid,
                    SettingGuid = setting.SettingGuid,
                    AcValue = setting.AcValue,
                    DcValue = setting.DcValue,
                };

                // Apply the setting to the current power plan
                await _powerSettingService.ApplySettingValueAsync(applyValue);

                _logService.Log(
                    LogLevel.Info,
                    $"Successfully applied advanced power setting: {setting.DisplayName}"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying advanced power setting {setting.DisplayName}: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Applies all advanced power settings.
        /// </summary>
        [RelayCommand]
        private async Task ApplyAllAdvancedSettingsAsync()
        {
            if (_powerSettingService == null || SelectedPowerPlan == null)
                return;

            try
            {
                _logService.Log(LogLevel.Info, "Applying all advanced power settings");

                foreach (var group in AdvancedPowerSettingGroups)
                {
                    foreach (var setting in group.Settings)
                    {
                        await ApplyAdvancedSettingAsync(setting);
                    }
                }

                _logService.Log(LogLevel.Info, "Successfully applied all advanced power settings");
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying all advanced power settings: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Updates the selected power plan and loads its settings.
        /// </summary>
        [RelayCommand]
        private async Task UpdateSelectedPowerPlanAsync()
        {
            if (_powerPlanManagerService == null || SelectedPowerPlan == null)
                return;

            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Setting active power plan to: {SelectedPowerPlan.Name}"
                );

                // Set the selected plan as active
                await _powerPlanManagerService.SetActivePowerPlanAsync(SelectedPowerPlan.Guid);

                // Reload advanced settings for the new power plan
                await LoadAdvancedPowerSettingsAsync();

                _logService.Log(
                    LogLevel.Info,
                    $"Successfully set active power plan to: {SelectedPowerPlan.Name}"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error setting active power plan: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a friendly name for a power setting.
        /// </summary>
        /// <param name="key">The power setting key.</param>
        /// <returns>A user-friendly name for the power setting.</returns>
        private string GetFriendlyNameForPowerSetting(string key)
        {
            return key switch
            {
                "HibernateEnabled" => "Disable Hibernate",
                "HibernateEnabledDefault" => "Disable Hibernate by Default",
                "VideoQuality" => "High Video Quality on Battery",
                "LockOption" => "Hide Lock Option",
                "FastBoot" => "Disable Fast Boot",
                "CpuUnpark" => "CPU Core Unparking",
                "PowerThrottling" => "Disable Power Throttling",
                _ => key,
            };
        }

        /// <summary>
        /// Initializes hardware detection to determine if the system has a battery and/or lid.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task InitializeBatteryDetectionAsync()
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    "Initializing hardware detection for battery and lid"
                );

                // Default to false to hide battery settings unless we confirm a battery exists
                HasBattery = false;
                HasLid = false;

                // If battery service is not available, log and hide battery settings
                if (_batteryService == null)
                {
                    _logService.Log(
                        LogLevel.Warning,
                        "Battery service not available, hiding battery and lid settings"
                    );
                    return;
                }

                // Check if the system has a battery
                _logService.Log(LogLevel.Info, "Calling BatteryService.HasBatteryAsync()");
                HasBattery = await _batteryService.HasBatteryAsync();
                _logService.Log(
                    LogLevel.Info,
                    $"Battery detection completed. HasBattery: {HasBattery}"
                );

                // Check if the system has a lid (is a laptop)
                _logService.Log(LogLevel.Info, "Calling BatteryService.HasLidAsync()");
                HasLid = await _batteryService.HasLidAsync();
                _logService.Log(LogLevel.Info, $"Lid detection completed. HasLid: {HasLid}");

                // Force UI updates
                OnPropertyChanged(nameof(HasBattery));
                OnPropertyChanged(nameof(HasLid));
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error during hardware detection: {ex.Message}");

                // Default to hiding battery settings but showing lid settings in case of error
                // It's better to show lid settings unnecessarily than to hide them when needed
                HasBattery = false;
                HasLid = true;

                OnPropertyChanged(nameof(HasBattery));
                OnPropertyChanged(nameof(HasLid));
            }
        }
    }
}
