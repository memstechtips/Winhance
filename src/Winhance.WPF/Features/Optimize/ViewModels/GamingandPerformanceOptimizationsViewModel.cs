using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for Gaming and Performance optimizations using clean architecture principles.
    /// Uses composition pattern with ISettingsUICoordinator.
    /// </summary>
    public partial class GamingandPerformanceOptimizationsViewModel : ObservableObject, IFeatureViewModel
    {
        private readonly IGamingPerformanceService _gamingPerformanceService;
        private readonly ISettingsUICoordinator _uiCoordinator;
        private readonly ITaskProgressService _progressService;
        private readonly ILogService _logService;

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
        public string ModuleId => "GamingPerformance";
        public string DisplayName => "Gaming and Performance";
        public int SettingsCount => Settings?.Count ?? 0;
        public string Category => "Optimize";
        public string Description => "Optimize Windows for gaming and performance";
        public int SortOrder => 1;
        public ICommand LoadSettingsCommand { get; }
        
        // Header properties
        public string HeaderIcon => "&#xE338;"; // Gaming controller icon
        
        [ObservableProperty]
        private bool _isExpanded = true;
        
        public ICommand ToggleExpandCommand { get; }
        /// <summary>
        /// Initializes a new instance of the <see cref="GamingandPerformanceOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="gamingPerformanceService">The gaming performance domain service.</param>
        /// <param name="uiCoordinator">The settings UI coordinator.</param>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="logService">The log service.</param>
        public GamingandPerformanceOptimizationsViewModel(
            IGamingPerformanceService gamingPerformanceService,
            ISettingsUICoordinator uiCoordinator,
            ITaskProgressService progressService,
            ILogService logService)
        {
            _gamingPerformanceService = gamingPerformanceService ?? throw new ArgumentNullException(nameof(gamingPerformanceService));
            _uiCoordinator = uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            _uiCoordinator.CategoryName = "Gaming and Performance Settings";
            
            // Subscribe to coordinator's PropertyChanged events to relay them to the UI
            _uiCoordinator.PropertyChanged += (sender, e) => OnPropertyChanged(e.PropertyName);
            
            // Initialize commands
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            ToggleExpandCommand = new RelayCommand(ToggleExpand);
            
            _logService.Log(LogLevel.Info, "GamingandPerformanceOptimizationsViewModel constructor completed");
        }

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public async Task LoadSettingsAsync()
        {
            try
            {
                _progressService.StartTask("Loading gaming and performance settings...");
                
                // Load settings using UI coordinator (this will handle all the UI mapping)
                await _uiCoordinator.LoadSettingsAsync(() => _gamingPerformanceService.GetSettingsAsync());
                
                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading gaming and performance settings: {ex.Message}");
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
        /// Toggles the expanded state of the section.
        /// </summary>
        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }
    }
}
