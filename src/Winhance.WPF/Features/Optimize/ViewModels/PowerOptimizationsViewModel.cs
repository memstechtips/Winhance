using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Features.Optimize.Services;
using Winhance.Infrastructure.Features.Optimize.Services;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for Power optimizations using clean architecture principles.
    /// </summary>
    public partial class PowerOptimizationsViewModel : BaseSettingsViewModel
    {
        private readonly IPowerPlanService _powerPlanService;
        private readonly IPowerSettingService _powerSettingService;
        private readonly IPowerPlanManagerService _powerPlanManagerService;

        /// <summary>
        /// Gets or sets a value indicating whether the system has a battery.
        /// </summary>
        [ObservableProperty]
        private bool _hasBattery;

        /// <summary>
        /// Gets or sets a value indicating whether the system has a lid.
        /// </summary>
        [ObservableProperty]
        private bool _hasLid;

        /// <summary>
        /// Gets or sets the power plan value.
        /// </summary>
        [ObservableProperty]
        private int _powerPlanValue;

        /// <summary>
        /// Gets or sets a value indicating whether a power plan is being applied.
        /// </summary>
        [ObservableProperty]
        private bool _isApplyingPowerPlan;

        /// <summary>
        /// Gets or sets the advanced power setting groups.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasAdvancedPowerSettings))]
        private ObservableCollection<AdvancedPowerSettingGroup> _advancedPowerSettingGroups = new();

        /// <summary>
        /// Gets a value indicating whether there are any advanced power settings available.
        /// </summary>
        public bool HasAdvancedPowerSettings => AdvancedPowerSettingGroups.Count > 0;

        /// <summary>
        /// Gets or sets a value indicating whether advanced power settings are being loaded.
        /// </summary>
        [ObservableProperty]
        private bool _isLoadingAdvancedSettings;

        /// <summary>
        /// Gets or sets the selected power plan.
        /// </summary>
        [ObservableProperty]
        private PowerPlan _selectedPowerPlan;

        /// <summary>
        /// Gets or sets the available power plans.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<PowerPlan> _powerPlans = new();

        /// <summary>
        /// Gets the power plan labels for backward compatibility.
        /// </summary>
        public List<string> PowerPlanLabels { get; private set; } = new List<string>
        {
            "High Performance",
            "Balanced",
            "Power Saver",
            "Ultimate Performance"
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="settingsService">The application settings service.</param>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="powerPlanService">The power plan service.</param>
        /// <param name="powerSettingService">The power setting service.</param>
        /// <param name="powerPlanManagerService">The power plan manager service.</param>
        public PowerOptimizationsViewModel(
            IApplicationSettingsService settingsService,
            ITaskProgressService progressService,
            ILogService logService,
            IPowerPlanService powerPlanService,
            IPowerSettingService powerSettingService,
            IPowerPlanManagerService powerPlanManagerService)
            : base(settingsService, progressService, logService)
        {
            _powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            _powerSettingService = powerSettingService ?? throw new ArgumentNullException(nameof(powerSettingService));
            _powerPlanManagerService = powerPlanManagerService ?? throw new ArgumentNullException(nameof(powerPlanManagerService));
            
            CategoryName = "Power Optimizations";
        }

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                _progressService.StartTask("Loading power optimization settings...");
                
                // Clear existing collections
                Settings.Clear();
                
                // Load available power plans
                var powerPlans = await _settingsService.GetAvailablePowerPlansAsync();
                PowerPlans.Clear();
                foreach (var plan in powerPlans.Cast<PowerPlan>())
                {
                    PowerPlans.Add(plan);
                }
                
                // Load advanced power settings (for UI display)
                var advancedGroups = await _settingsService.GetAdvancedPowerSettingGroupsAsync();
                AdvancedPowerSettingGroups.Clear();
                foreach (var group in advancedGroups.Cast<AdvancedPowerSettingGroup>())
                {
                    AdvancedPowerSettingGroups.Add(group);
                }
                
                // Get active power plan
                var activePlan = await _settingsService.GetActivePowerPlanAsync() as PowerPlan;
                if (activePlan != null)
                {
                    SelectedPowerPlan = activePlan;
                }
                
                // Check system capabilities
                var capabilities = await _settingsService.CheckPowerSystemCapabilitiesAsync();
                HasBattery = capabilities.GetValueOrDefault("HasBattery", false);
                HasLid = capabilities.GetValueOrDefault("HasLid", false);
                
                // Load power optimization settings from the service (now includes converted advanced settings)
                var powerSettings = await _settingsService.GetPowerOptimizationSettingsAsync();
                
                foreach (var setting in powerSettings)
                {
                    // Cast to OptimizationSetting to access CustomProperties
                    var optimizationSetting = setting as OptimizationSetting;
                    
                    // Create UI item from the setting
                    var uiItem = new SettingUIItem
                    {
                        Id = setting.Id,
                        Name = setting.Name,
                        Description = setting.Description,
                        GroupName = setting.GroupName,
                        IsEnabled = setting.IsEnabled,
                        ControlType = setting.ControlType,
                        IsSelected = false,
                        IsVisible = true
                    };
                    
                    // Add options for ComboBox settings
                    if (setting.ControlType == ControlType.ComboBox)
                    {
                        // Check if we have advanced power setting options
                        if (optimizationSetting?.CustomProperties?.ContainsKey("ComboBoxOptions") == true &&
                            optimizationSetting.CustomProperties["ComboBoxOptions"] is Dictionary<string, object> options)
                        {
                            // Use the proper advanced power setting options
                            foreach (var option in options.Keys)
                            {
                                uiItem.ComboBoxOptions.Add(option);
                            }
                            
                            // Set default selected value
                            var defaultValue = optimizationSetting.CustomProperties.GetValueOrDefault("DefaultValue", 0);
                            var defaultOption = options.FirstOrDefault(kvp => kvp.Value.Equals(defaultValue)).Key;
                            uiItem.SelectedValue = defaultOption ?? options.Keys.FirstOrDefault() ?? "";
                        }
                        else
                        {
                            // Fallback to basic options
                            uiItem.ComboBoxOptions.Add("Disabled");
                            uiItem.ComboBoxOptions.Add("Enabled");
                            uiItem.SelectedValue = "Disabled";
                        }
                    }
                    else if (setting.ControlType == ControlType.BinaryToggle)
                    {
                        // Set toggle state
                        uiItem.IsSelected = setting.IsEnabled;
                    }
                    else if (setting.ControlType == ControlType.NumericUpDown)
                    {
                        // Handle numeric settings
                        if (optimizationSetting?.CustomProperties != null)
                        {
                            var minValue = optimizationSetting.CustomProperties.GetValueOrDefault("MinValue", 0);
                            var maxValue = optimizationSetting.CustomProperties.GetValueOrDefault("MaxValue", 100);
                            var units = optimizationSetting.CustomProperties.GetValueOrDefault("Units", "")?.ToString() ?? "";
                            
                            // Store numeric range info for UI (SettingUIItem doesn't have CustomProperties, so we'll use a different approach)
                            // For now, we can store this info in the description or handle it differently
                            uiItem.Description += $" (Range: {minValue}-{maxValue} {units})".Trim();
                        }
                    }
                    
                    // Add to settings collection
                    Settings.Add(uiItem);
                }
                
                // Organize settings into groups if needed
                if (Settings.Count > 0)
                {
                    var groups = Settings.GroupBy(s => s.GroupName).ToList();
                    foreach (var group in groups)
                    {
                        if (!string.IsNullOrEmpty(group.Key))
                        {
                            var settingGroup = new SettingGroup(group.Key);
                            foreach (var setting in group)
                            {
                                settingGroup.AddSetting(setting);
                            }
                            SettingGroups.Add(settingGroup);
                        }
                    }
                }
                
                // Refresh status of all settings
                await RefreshAllSettingsAsync();
                
                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading power optimization settings: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks the system capabilities for power management.
        /// </summary>
        private async Task CheckSystemCapabilitiesAsync()
        {
            try
            {
                // For now, assume desktop systems don't have battery/lid
                // This can be enhanced later with proper system detection
                HasBattery = false;
                HasLid = false;
                
                _logService.Log(LogLevel.Info, $"System capabilities: Battery={HasBattery}, Lid={HasLid}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking system capabilities: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the application settings that this ViewModel manages.
        /// </summary>
        /// <returns>Collection of application settings for power optimizations.</returns>
        protected override async Task<IEnumerable<ApplicationSetting>> GetApplicationSettingsAsync()
        {
            return await _settingsService.GetPowerOptimizationSettingsAsync();
        }
    }
}
