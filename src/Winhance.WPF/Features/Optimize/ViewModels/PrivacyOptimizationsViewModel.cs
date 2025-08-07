using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for Privacy optimizations using clean architecture principles.
    /// </summary>
    public partial class PrivacyOptimizationsViewModel : ObservableObject, IFeatureViewModel
    {
        private readonly IPrivacyService _privacyService;
        private readonly ISettingsUICoordinator _uiCoordinator;
        private readonly ILogService _logService;
        private readonly ITaskProgressService _progressService;



        // Delegating properties to UI coordinator
        public ObservableCollection<SettingUIItem> Settings => _uiCoordinator.Settings;
        public ObservableCollection<SettingGroup> SettingGroups => _uiCoordinator.SettingGroups;
        public bool IsLoading => _uiCoordinator.IsLoading;
        public string CategoryName => _uiCoordinator.CategoryName;
        public string SearchText
        {
            get => _uiCoordinator.SearchText;
            set => _uiCoordinator.SearchText = value;
        }
        public bool HasVisibleSettings => _uiCoordinator.HasVisibleSettings;

        // IFeatureViewModel implementation
        public string ModuleId => "Privacy";
        public string DisplayName => "Privacy";
        public int SettingsCount => Settings?.Count ?? 0;
        public string Category => "Optimize";
        public string Description => "Optimize Windows privacy settings";
        public int SortOrder => 2;
        public ICommand LoadSettingsCommand { get; private set; }
        
        [ObservableProperty]
        private bool _isExpanded = true;
        
        public ICommand ToggleExpandCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrivacyOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="privacyService">The privacy domain service.</param>
        /// <param name="uiCoordinator">The settings UI coordinator.</param>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="logService">The log service.</param>
        public PrivacyOptimizationsViewModel(
            IPrivacyService privacyService,
            ISettingsUICoordinator uiCoordinator,
            ITaskProgressService progressService,
            ILogService logService)
        {
            _privacyService = privacyService ?? throw new ArgumentNullException(nameof(privacyService));
            _uiCoordinator = uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            
            _uiCoordinator.CategoryName = "Privacy Optimizations";
            
            // Initialize commands
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            ToggleExpandCommand = new RelayCommand(ToggleExpand);
        }

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public async Task LoadSettingsAsync()
        {
            try
            {
                _progressService.StartTask("Loading privacy optimization settings...");
                
                // Use UI coordinator to load settings with both setting change and value change handlers
                await _uiCoordinator.LoadSettingsAsync(
                    () => _privacyService.GetSettingsAsync(),
                    async (settingId, isEnabled) => await _privacyService.ApplySettingAsync(settingId, isEnabled),
                    async (settingId, value) => await _privacyService.ApplySettingAsync(settingId, true, value)
                );
                
                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading privacy optimization settings: {ex.Message}");
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
        
        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }
    }
}
