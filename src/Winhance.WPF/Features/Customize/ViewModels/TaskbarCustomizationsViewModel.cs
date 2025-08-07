using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// ViewModel for Taskbar customizations using clean architecture principles.
    /// Uses composition pattern with ISettingsUICoordinator for UI state management.
    /// </summary>
    public partial class TaskbarCustomizationsViewModel : ObservableObject, IFeatureViewModel
    {
        private readonly ITaskbarService _taskbarService;
        private readonly ISettingsUICoordinator _uiCoordinator;
        private readonly ITaskProgressService _progressService;
        private readonly ILogService _logService;
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

        // Delegate properties to UI Coordinator
        public ObservableCollection<SettingUIItem> Settings => _uiCoordinator.Settings;
        public ObservableCollection<SettingGroup> SettingGroups => _uiCoordinator.SettingGroups;
        public bool IsLoading
        {
            get => _uiCoordinator.IsLoading;
            set => _uiCoordinator.IsLoading = value;
        }
        public string CategoryName
        {
            get => _uiCoordinator.CategoryName;
            set => _uiCoordinator.CategoryName = value;
        }
        public string SearchText
        {
            get => _uiCoordinator.SearchText;
            set => _uiCoordinator.SearchText = value;
        }
        public bool HasVisibleSettings => _uiCoordinator.HasVisibleSettings;

        // IFeatureViewModel implementation
        public string ModuleId => "TaskbarCustomization";
        public string DisplayName => "Taskbar";
        public int SettingsCount => Settings?.Count ?? 0;
        public string Category => "Customize";
        public string Description => "Customize Windows Taskbar settings";
        public int SortOrder => 2;
        public ICommand LoadSettingsCommand { get; private set; }

        /// <summary>
        /// Gets the command to clean the taskbar.
        /// </summary>
        public IAsyncRelayCommand CleanTaskbarCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskbarCustomizationsViewModel"/> class.
        /// </summary>
        /// <param name="taskbarService">The taskbar domain service.</param>
        /// <param name="settingsService">The application settings service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="systemServices">The system services.</param>
        /// <param name="progressService">The task progress service.</param>
        public TaskbarCustomizationsViewModel(
            ITaskbarService taskbarService,
            ISettingsUICoordinator uiCoordinator,
            ILogService logService,
            IDialogService dialogService,
            ISystemServices systemServices,
            ITaskProgressService progressService)
        {
            _taskbarService = taskbarService ?? throw new ArgumentNullException(nameof(taskbarService));
            _uiCoordinator = uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            _isWindows11 = _systemServices.IsWindows11();
            
            _uiCoordinator.CategoryName = "Taskbar";
            
            // Initialize commands
            CleanTaskbarCommand = new AsyncRelayCommand(ExecuteCleanTaskbarAsync);
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
        }

        /// <summary>
        /// Executes the clean taskbar operation.
        /// </summary>
        private async Task ExecuteCleanTaskbarAsync()
        {
            try
            {
                _progressService.StartTask("Cleaning taskbar...");
                
                // Use the taskbar service to clean the taskbar
                await _taskbarService.ExecuteTaskbarCleanupAsync();
                
                // Refresh settings after cleanup
                await LoadSettingsAsync();
                
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
        public async Task LoadSettingsAsync()
        {
            await _uiCoordinator.LoadSettingsAsync(() => _taskbarService.GetSettingsAsync());

            // Organize settings by control type for specialized UI display
            OrganizeSettingsByControlType();
        }
        
        /// <summary>
        /// Organizes settings by control type for specialized UI display.
        /// </summary>
        private void OrganizeSettingsByControlType()
        {
            ComboBoxSettings.Clear();
            ToggleSettings.Clear();

            foreach (var setting in Settings)
            {
                switch (setting.ControlType)
                {
                    case ControlType.ComboBox:
                        ComboBoxSettings.Add(setting);
                        break;
                    case ControlType.BinaryToggle:
                        ToggleSettings.Add(setting);
                        break;
                    // Add other control types as needed
                }
            }

            // Notify property changes
            OnPropertyChanged(nameof(HasComboBoxSettings));
        }

        /// <summary>
        /// Refreshes the settings for this feature asynchronously.
        /// </summary>
        public async Task RefreshSettingsAsync()
        {
            await LoadSettingsAsync();
        }

        /// <summary>
        /// Clears all settings and resets the feature state.
        /// </summary>
        public void ClearSettings()
        {
            _uiCoordinator.ClearSettings();
            ComboBoxSettings.Clear();
            ToggleSettings.Clear();
            OnPropertyChanged(nameof(HasComboBoxSettings));
        }
    }
}
