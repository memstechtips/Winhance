using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// Clean architecture ViewModel for Start Menu customizations.
    /// Uses composition pattern with ISettingsUICoordinator for UI state management.
    /// Follows SOLID principles and clean separation of concerns.
    /// </summary>
    public partial class StartMenuCustomizationsViewModel : ObservableObject, IFeatureViewModel
    {
        private readonly IStartMenuService _startMenuService;
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
        public string ModuleId => "start-menu";
        public string DisplayName => "Start Menu";
        public int SettingsCount => Settings?.Count ?? 0;
        public string Category => "Customize";
        public string Description => "Customize Windows Start Menu settings";
        public int SortOrder => 3;

        /// <summary>
        /// Gets the command to load settings.
        /// </summary>
        public ICommand LoadSettingsCommand { get; private set; }

        // Header properties
        [ObservableProperty]
        private bool _isExpanded = true;

        public ICommand ToggleExpandCommand { get; private set; }

        /// <summary>
        /// Command to clean the Start Menu.
        /// </summary>
        public ICommand CleanStartMenuCommand { get; }

        public StartMenuCustomizationsViewModel(
            IStartMenuService startMenuService,
            ISettingsUICoordinator uiCoordinator,
            ILogService logService,
            IDialogService dialogService,
            ITaskProgressService progressService,
            ISystemServices systemServices
        )
        {
            _startMenuService =
                startMenuService ?? throw new ArgumentNullException(nameof(startMenuService));
            _uiCoordinator =
                uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _dialogService =
                dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _progressService =
                progressService ?? throw new ArgumentNullException(nameof(progressService));
            _systemServices =
                systemServices ?? throw new ArgumentNullException(nameof(systemServices));

            _uiCoordinator.CategoryName = "Start Menu";

            // Subscribe to coordinator's PropertyChanged events to relay them to the UI
            _uiCoordinator.PropertyChanged += (sender, e) => OnPropertyChanged(e.PropertyName);

            // Initialize commands
            CleanStartMenuCommand = new AsyncRelayCommand(CleanStartMenuAsync);
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            ToggleExpandCommand = new RelayCommand(ToggleExpand);
        }

        /// <summary>
        /// Loads settings and organizes them by control type.
        /// </summary>
        public async Task LoadSettingsAsync()
        {
            _logService.Log(
                LogLevel.Info,
                "StartMenuCustomizationsViewModel: Starting LoadSettingsAsync"
            );

            try
            {
                _progressService.StartTask("Loading start menu settings...");

                // Use the domain service which provides centralized ComboBox resolution
                await _uiCoordinator.LoadSettingsAsync(() => _startMenuService.GetSettingsAsync());

                _logService.Log(
                    LogLevel.Info,
                    $"StartMenuCustomizationsViewModel: UI Coordinator has {_uiCoordinator.Settings.Count} settings after load"
                );

                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading Start Menu settings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Executes the clean Start Menu operation using UI coordination.
        /// Delegates actual business logic to the domain service.
        /// </summary>
        private async Task CleanStartMenuAsync()
        {
            try
            {
                // UI: Show confirmation dialog
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    "You are about to clean the Start Menu for all users on this computer.\n\n"
                        + "This will remove all pinned items and apply recommended settings to disable suggestions, "
                        + "recommendations, and tracking features.\n\n"
                        + "Do you want to continue?",
                    "Start Menu Cleaning Options"
                );

                if (!confirmed)
                    return;

                // UI: Start progress tracking
                _progressService.StartTask("Cleaning Start Menu...");

                // DELEGATE: Call domain service for business logic
                await _startMenuService.CleanStartMenuAsync();

                // UI: Complete progress and show success
                _progressService.CompleteTask();
                await _dialogService.ShowInformationAsync(
                    "Start Menu has been cleaned successfully.",
                    "Start Menu Cleanup"
                );

                // UI: Refresh settings to reflect changes
                await LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                // UI: Handle error display and cleanup
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error cleaning Start Menu: {ex.Message}");
                await _dialogService.ShowErrorAsync(
                    $"Failed to clean Start Menu: {ex.Message}",
                    "Start Menu Cleanup Error"
                );
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
