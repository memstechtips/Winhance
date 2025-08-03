using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for Windows Security optimizations using clean architecture principles.
    /// </summary>
    public partial class WindowsSecurityOptimizationsViewModel : BaseSettingsViewModel
    {
        private readonly ISystemServices _systemServices;
        private readonly IUacSettingsService _uacSettingsService;

        /// <summary>
        /// Gets or sets the selected UAC level for backward compatibility.
        /// </summary>
        [ObservableProperty]
        private int _selectedUacLevel;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsSecurityOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="settingsService">The application settings service.</param>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="systemServices">The system services.</param>
        /// <param name="uacSettingsService">The UAC settings service.</param>
        public WindowsSecurityOptimizationsViewModel(
            IApplicationSettingsService settingsService,
            ITaskProgressService progressService,
            ILogService logService,
            ISystemServices systemServices,
            IUacSettingsService uacSettingsService)
            : base(settingsService, progressService, logService)
        {
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _uacSettingsService = uacSettingsService ?? throw new ArgumentNullException(nameof(uacSettingsService));
            
            CategoryName = "Windows Security Optimizations";
        }

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                _progressService.StartTask("Loading Windows security optimization settings...");
                
                // Clear existing collections
                Settings.Clear();
                
                // Load settings from the service
                var securitySettings = await _settingsService.GetWindowsSecurityOptimizationSettingsAsync();
                
                foreach (var setting in securitySettings)
                {
                    // Create UI item from the setting
                    var uiItem = new SettingUIItem
                    {
                        Id = setting.Id,
                        Name = setting.Name,
                        Description = setting.Description,
                        GroupName = setting.GroupName,
                        IsEnabled = setting.IsEnabled,
                        ControlType = setting.ControlType == ControlType.BinaryToggle ? ControlType.BinaryToggle : ControlType.ComboBox,
                        IsSelected = false,
                        IsVisible = true
                    };
                    
                    // Add options for ComboBox settings
                    if (setting.ControlType == ControlType.ComboBox)
                    {
                        // Add default options for ComboBox settings
                        uiItem.ComboBoxOptions.Add("Disabled");
                        uiItem.ComboBoxOptions.Add("Enabled");
                        
                        // Set selected value
                        uiItem.SelectedValue = "Disabled"; // Default to disabled
                    }
                    else if (setting.ControlType == ControlType.BinaryToggle)
                    {
                        // Set toggle state
                        uiItem.IsSelected = setting.IsEnabled;
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
                _logService.Log(LogLevel.Error, $"Error loading Windows security optimization settings: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Gets the application settings that this ViewModel manages.
        /// </summary>
        /// <returns>Collection of application settings for Windows security optimizations.</returns>
        protected override async Task<IEnumerable<ApplicationSetting>> GetApplicationSettingsAsync()
        {
            return await _settingsService.GetWindowsSecurityOptimizationSettingsAsync();
        }
    }
}
