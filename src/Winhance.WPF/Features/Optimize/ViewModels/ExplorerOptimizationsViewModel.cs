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
    /// ViewModel for Explorer optimizations using clean architecture principles.
    /// </summary>
    public partial class ExplorerOptimizationsViewModel : BaseSettingsViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExplorerOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="settingsService">The application settings service.</param>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="logService">The log service.</param>
        public ExplorerOptimizationsViewModel(
            IApplicationSettingsService settingsService,
            ITaskProgressService progressService,
            ILogService logService)
            : base(settingsService, progressService, logService)
        {
            CategoryName = "Explorer Optimizations";
        }

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                _progressService.StartTask("Loading explorer optimization settings...");
                
                // Clear existing collections
                Settings.Clear();
                
                // Load settings from the service
                var explorerSettings = await _settingsService.GetExplorerOptimizationSettingsAsync();
                
                foreach (var setting in explorerSettings)
                {
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
                        IsVisible = true,
                        StatusMessage = "Ready"
                    };
                    
                    // For ComboBox settings, we'll need to populate options from registry settings
                    if (setting.ControlType == ControlType.ComboBox)
                    {
                        // TODO: Populate ComboBox options from registry settings or configuration
                        // For now, just set a default selected value
                        uiItem.SelectedValue = "Default";
                    }
                    else if (setting.ControlType == ControlType.BinaryToggle)
                    {
                        // Set toggle state based on IsEnabled
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
                _logService.Log(LogLevel.Error, $"Error loading explorer optimization settings: {ex.Message}");
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
        /// <returns>Collection of application settings for explorer optimizations.</returns>
        protected override async Task<IEnumerable<ApplicationSetting>> GetApplicationSettingsAsync()
        {
            return await _settingsService.GetExplorerOptimizationSettingsAsync();
        }
    }
}
