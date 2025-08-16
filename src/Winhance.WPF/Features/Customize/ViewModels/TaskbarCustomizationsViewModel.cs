using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;

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
        public string ModuleId => "taskbar";
        public string DisplayName => "Taskbar";
        public int SettingsCount => Settings?.Count ?? 0;
        public string Category => "Customize";
        public string Description => "Customize Windows Taskbar settings";
        public int SortOrder => 2;

        /// <summary>
        /// Gets the command to load settings.
        /// </summary>
        public ICommand LoadSettingsCommand { get; private set; }

        // Header properties
        [ObservableProperty]
        private bool _isExpanded = true;

        public ICommand ToggleExpandCommand { get; private set; }

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
            ITaskProgressService progressService,
            ISystemServices systemServices
        )
        {
            _taskbarService =
                taskbarService ?? throw new ArgumentNullException(nameof(taskbarService));
            _uiCoordinator =
                uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _dialogService =
                dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _progressService =
                progressService ?? throw new ArgumentNullException(nameof(progressService));
            _systemServices =
                systemServices ?? throw new ArgumentNullException(nameof(systemServices));

            _uiCoordinator.CategoryName = "Taskbar";

            // Subscribe to coordinator's PropertyChanged events to relay them to the UI
            _uiCoordinator.PropertyChanged += (sender, e) => OnPropertyChanged(e.PropertyName);

            // Initialize commands
            CleanTaskbarCommand = new AsyncRelayCommand(ExecuteCleanTaskbarAsync);
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            ToggleExpandCommand = new RelayCommand(ToggleExpand);
        }

        /// <summary>
        /// Executes the clean taskbar operation using UI coordination.
        /// Delegates actual business logic to the domain service.
        /// </summary>
        private async Task ExecuteCleanTaskbarAsync()
        {
            try
            {
                // UI: Show confirmation dialog
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    "You are about to clean the Taskbar for the current user.\n\n"
                        + "This will remove all pinned apps except File Explorer and restore the default taskbar layout.\n\n"
                        + "Do you want to continue?",
                    "Taskbar Cleanup Confirmation"
                );

                if (!confirmed)
                    return;

                // UI: Start progress tracking
                _progressService.StartTask("Cleaning taskbar...");

                // DELEGATE: Call domain service for business logic
                await _taskbarService.ExecuteTaskbarCleanupAsync();

                // UI: Complete progress and show success
                _progressService.CompleteTask();
                await _dialogService.ShowInformationAsync(
                    "Taskbar has been cleaned successfully.",
                    "Taskbar Cleanup"
                );

                // UI: Refresh settings to reflect changes
                await LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                // UI: Handle error display and cleanup
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error cleaning taskbar: {ex.Message}");
                await _dialogService.ShowErrorAsync(
                    $"Failed to clean taskbar: {ex.Message}",
                    "Taskbar Cleanup Error"
                );
            }
        }

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public async Task LoadSettingsAsync()
        {
            _logService.Log(
                LogLevel.Info,
                "TaskbarCustomizationsViewModel: Starting LoadSettingsAsync"
            );

            try
            {
                _progressService.StartTask("Loading taskbar settings...");

                // Use the domain service which provides centralized ComboBox resolution
                await _uiCoordinator.LoadSettingsAsync(() => _taskbarService.GetSettingsAsync());

                _logService.Log(
                    LogLevel.Info,
                    $"TaskbarCustomizationsViewModel: UI Coordinator has {_uiCoordinator.Settings.Count} settings after load"
                );

                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading Taskbar settings: {ex.Message}");
                throw;
            }
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
        }

        /// <summary>
        /// Toggles the expand/collapse state of this feature section.
        /// </summary>
        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }
    }
}
