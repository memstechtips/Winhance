using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// ViewModel for Explorer customizations using clean architecture principles.
    /// </summary>
    public partial class ExplorerCustomizationsViewModel : BaseSettingsViewModel
    {
        private readonly IDialogService _dialogService;

        /// <summary>
        /// Gets the command to execute an action.
        /// </summary>
        public IAsyncRelayCommand<string> ExecuteActionCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExplorerCustomizationsViewModel"/> class.
        /// </summary>
        /// <param name="settingsService">The application settings service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="progressService">The task progress service.</param>
        public ExplorerCustomizationsViewModel(
            IApplicationSettingsService settingsService,
            ILogService logService,
            IDialogService dialogService,
            ITaskProgressService progressService)
            : base(settingsService, progressService, logService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            
            CategoryName = "Explorer";
            
            // Initialize commands
            ExecuteActionCommand = new AsyncRelayCommand<string>(ExecuteActionAsync);
        }

        /// <summary>
        /// Executes a named action.
        /// </summary>
        /// <param name="actionId">The ID of the action to execute.</param>
        private async Task ExecuteActionAsync(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
                return;

            try
            {
                _progressService.StartTask($"Executing action: {actionId}...");
                
                // Use the settings service to execute the action
                await _settingsService.ExecuteExplorerActionAsync(actionId);
                
                // Refresh settings after action
                await RefreshAllSettingsAsync();
                
                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error executing action {actionId}: {ex.Message}");
                await _dialogService.ShowErrorAsync($"Failed to execute action: {ex.Message}", "Action Error");
            }
        }

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                _progressService.StartTask("Loading explorer settings...");
                
                // Clear existing collections
                Settings.Clear();
                
                // Load settings from the service
                var explorerSettings = await _settingsService.GetExplorerSettingsAsync();
                
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
                        CurrentValue = null
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
                        // Set toggle state based on current setting state
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
                _logService.Log(LogLevel.Error, $"Error loading explorer settings: {ex.Message}");
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
        /// <returns>Collection of application settings for explorer customizations.</returns>
        protected override async Task<IEnumerable<ApplicationSetting>> GetApplicationSettingsAsync()
        {
            return await _settingsService.GetExplorerSettingsAsync();
        }
    }
}
