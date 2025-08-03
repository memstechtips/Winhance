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
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// ViewModel for Taskbar customizations using clean architecture principles.
    /// </summary>
    public partial class TaskbarCustomizationsViewModel : BaseSettingsViewModel
    {
        private readonly ISystemServices _systemServices;
        private readonly IDialogService _dialogService;
        private bool _isWindows11;

        /// <summary>
        /// Gets the collection of ComboBox settings.
        /// </summary>
        public ObservableCollection<SettingUIItem> ComboBoxSettings { get; } = new();

        /// <summary>
        /// Gets the collection of Toggle settings.
        /// </summary>
        public ObservableCollection<SettingUIItem> ToggleSettings { get; } = new();

        /// <summary>
        /// Gets a value indicating whether there are ComboBox settings.
        /// </summary>
        public bool HasComboBoxSettings => ComboBoxSettings.Count > 0;

        /// <summary>
        /// Gets the command to clean the taskbar.
        /// </summary>
        public IAsyncRelayCommand CleanTaskbarCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskbarCustomizationsViewModel"/> class.
        /// </summary>
        /// <param name="settingsService">The application settings service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="systemServices">The system services.</param>
        /// <param name="progressService">The task progress service.</param>
        public TaskbarCustomizationsViewModel(
            IApplicationSettingsService settingsService,
            ILogService logService,
            IDialogService dialogService,
            ISystemServices systemServices,
            ITaskProgressService progressService)
            : base(settingsService, progressService, logService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _isWindows11 = _systemServices.IsWindows11();
            
            CategoryName = "Taskbar";
            
            // Initialize the CleanTaskbarCommand
            CleanTaskbarCommand = new AsyncRelayCommand(ExecuteCleanTaskbarAsync);
        }

        /// <summary>
        /// Executes the clean taskbar operation.
        /// </summary>
        private async Task ExecuteCleanTaskbarAsync()
        {
            try
            {
                _progressService.StartTask("Cleaning taskbar...");
                
                // Use the settings service to clean the taskbar
                await _settingsService.ExecuteTaskbarCleanupAsync();
                
                // Refresh settings after cleanup
                await RefreshAllSettingsAsync();
                
                _progressService.CompleteTask();
                await _dialogService.ShowInformationAsync("Taskbar has been cleaned successfully.", "Taskbar Cleanup");
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error cleaning taskbar: {ex.Message}");
                await _dialogService.ShowErrorAsync($"Failed to clean taskbar: {ex.Message}", "Taskbar Cleanup Error");
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
                _progressService.StartTask("Loading taskbar settings...");
                
                // Clear existing collections
                Settings.Clear();
                ComboBoxSettings.Clear();
                ToggleSettings.Clear();
                
                // Load settings from the service
                var taskbarSettings = await _settingsService.GetTaskbarSettingsAsync();
                
                foreach (var setting in taskbarSettings)
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
                        IsVisible = true
                    };
                    
                    // Add options for ComboBox settings
                    if (setting.ControlType == ControlType.ComboBox)
                    {
                        // For ComboBox settings, we would typically get options from registry settings
                        // For now, add some default options
                        uiItem.ComboBoxOptions.Add("Default");
                        uiItem.ComboBoxOptions.Add("Custom");
                        
                        // Set selected value
                        uiItem.SelectedValue = "Default";
                    }
                    else if (setting.ControlType == ControlType.BinaryToggle)
                    {
                        // Set toggle state
                        uiItem.IsSelected = setting.IsEnabled;
                    }
                    
                    // Add to appropriate collections
                    Settings.Add(uiItem);
                    
                    if (setting.ControlType == ControlType.ComboBox)
                    {
                        ComboBoxSettings.Add(uiItem);
                    }
                    else if (setting.ControlType == ControlType.BinaryToggle)
                    {
                        ToggleSettings.Add(uiItem);
                    }
                }
                
                // Refresh status of all settings
                await RefreshAllSettingsAsync();
                
                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading taskbar settings: {ex.Message}");
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
        /// <returns>Collection of application settings for taskbar customizations.</returns>
        protected override async Task<IEnumerable<ApplicationSetting>> GetApplicationSettingsAsync()
        {
            return await _settingsService.GetTaskbarSettingsAsync();
        }
    }
}
