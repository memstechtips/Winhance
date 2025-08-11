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
    /// ViewModel for Windows Security optimizations using clean architecture principles.
    /// </summary>
    public partial class WindowsSecurityOptimizationsViewModel : ObservableObject, IFeatureViewModel
    {
        private readonly ISecurityService _securityService;
        private readonly ISystemServices _systemServices;
        private readonly IUacSettingsService _uacSettingsService;
        private readonly ISettingsUICoordinator _uiCoordinator;
        private readonly ILogService _logService;
        private readonly ITaskProgressService _progressService;

        /// <summary>
        /// Gets or sets the selected UAC level for backward compatibility.
        /// </summary>
        [ObservableProperty]
        private int _selectedUacLevel;



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
        public string ModuleId => "WindowsSecurity";
        public string DisplayName => "Windows Security";
        public int SettingsCount => Settings?.Count ?? 0;
        public string Category => "Optimize";
        public string Description => "Optimize Windows security settings";
        public int SortOrder => 5;
        public ICommand LoadSettingsCommand { get; }
        
        [ObservableProperty]
        private bool _isExpanded = true;
        
        public ICommand ToggleExpandCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsSecurityOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="securityService">The security domain service.</param>
        /// <param name="uiCoordinator">The settings UI coordinator.</param>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="logService">The log service.</param>
        public WindowsSecurityOptimizationsViewModel(
            ISecurityService securityService,
            ISettingsUICoordinator uiCoordinator,
            ITaskProgressService progressService,
            ILogService logService)
        {
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
            _uiCoordinator = uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            
            _uiCoordinator.CategoryName = "Windows Security Optimizations";
            
            // Subscribe to coordinator property changes to forward them to the UI
            _uiCoordinator.PropertyChanged += (sender, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(ISettingsUICoordinator.HasVisibleSettings):
                        OnPropertyChanged(nameof(HasVisibleSettings));
                        break;
                    case nameof(ISettingsUICoordinator.Settings):
                        OnPropertyChanged(nameof(Settings));
                        OnPropertyChanged(nameof(SettingsCount));
                        break;
                    case nameof(ISettingsUICoordinator.SettingGroups):
                        OnPropertyChanged(nameof(SettingGroups));
                        break;
                    case nameof(ISettingsUICoordinator.IsLoading):
                        OnPropertyChanged(nameof(IsLoading));
                        break;
                }
            };
            
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
                _progressService.StartTask("Loading Windows security optimization settings...");
                
                
                // Use UI coordinator to load settings - Application Service handles business logic
                await _uiCoordinator.LoadSettingsAsync(() => _securityService.GetSettingsAsync());
                
                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading Windows security optimization settings: {ex.Message}");
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
