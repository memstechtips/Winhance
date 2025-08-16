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
    /// ViewModel for Explorer customizations using clean architecture principles.
    /// </summary>
    public partial class ExplorerCustomizationsViewModel : ObservableObject, IFeatureViewModel
    {
        private readonly IExplorerCustomizationService _explorerService;
        private readonly ISettingsUICoordinator _uiCoordinator;
        private readonly ILogService _logService;
        private readonly IDialogService _dialogService;
        private readonly ITaskProgressService _progressService;
        private readonly ISystemServices _systemServices;

        // Delegating properties to UI coordinator
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
        public string ModuleId => "explorer-customization";
        public string DisplayName => "Explorer";
        public int SettingsCount => Settings?.Count ?? 0;
        public string Category => "Customize";
        public string Description => "Customize Windows Explorer settings";
        public int SortOrder => 4;

        /// <summary>
        /// Gets the command to load settings.
        /// </summary>
        public ICommand LoadSettingsCommand { get; private set; }

        // Header properties
        [ObservableProperty]
        private bool _isExpanded = true;

        public ICommand ToggleExpandCommand { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExplorerCustomizationsViewModel"/> class.
        /// </summary>
        /// <param name="explorerService">The explorer domain service.</param>
        /// <param name="uiCoordinator">The settings UI coordinator.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="progressService">The task progress service.</param>
        public ExplorerCustomizationsViewModel(
            IExplorerCustomizationService explorerService,
            ISettingsUICoordinator uiCoordinator,
            ILogService logService,
            IDialogService dialogService,
            ITaskProgressService progressService,
            ISystemServices systemServices
        )
        {
            _explorerService =
                explorerService ?? throw new ArgumentNullException(nameof(explorerService));
            _uiCoordinator =
                uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _dialogService =
                dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _progressService =
                progressService ?? throw new ArgumentNullException(nameof(progressService));
            _systemServices =
                systemServices ?? throw new ArgumentNullException(nameof(systemServices));

            _uiCoordinator.CategoryName = "Explorer";

            // Subscribe to coordinator's PropertyChanged events to relay them to the UI
            _uiCoordinator.PropertyChanged += (sender, e) => OnPropertyChanged(e.PropertyName);

            // Initialize commands
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            ToggleExpandCommand = new RelayCommand(ToggleExpand);
        }

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public async Task LoadSettingsAsync()
        {
            _logService.Log(
                LogLevel.Info,
                "ExplorerCustomizationsViewModel: Starting LoadSettingsAsync"
            );

            try
            {
                _progressService.StartTask("Loading explorer settings...");

                // Use UI coordinator to load settings - Domain service handles business logic
                await _uiCoordinator.LoadSettingsAsync(() => _explorerService.GetSettingsAsync());

                _logService.Log(
                    LogLevel.Info,
                    $"ExplorerCustomizationsViewModel: UI Coordinator has {_uiCoordinator.Settings.Count} settings after load"
                );

                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading explorer settings: {ex.Message}");
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
