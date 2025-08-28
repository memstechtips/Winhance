using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Features;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for Windows Security optimizations using clean architecture principles.
    /// Directly manages settings using domain services without UI coordinator.
    /// </summary>
    public partial class WindowsSecurityOptimizationsViewModel : ObservableObject, IFeatureViewModel
    {
        private readonly IDomainServiceRouter _domainServiceRouter;
        private readonly ISystemServices _systemServices;
        private readonly IUacSettingsService _uacSettingsService;
        private readonly ISettingApplicationService _settingApplicationService;
        private readonly IEventBus _eventBus;
        private readonly ILogService _logService;
        private readonly ITaskProgressService _progressService;
        private readonly ISettingsLoadingService _settingsLoadingService;

        /// <summary>
        /// Gets or sets the selected UAC level for backward compatibility.
        /// </summary>
        [ObservableProperty]
        private int _selectedUacLevel;

        [ObservableProperty]
        private ObservableCollection<SettingItemViewModel> _settings = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public string CategoryName => "Windows Security";
        public bool HasVisibleSettings => Settings.Any(s => s.IsVisible);

        // IFeatureViewModel implementation
        public string ModuleId => FeatureIds.Security;
        public string DisplayName => "Windows Security";
        public int SettingsCount => Settings?.Count ?? 0;
        public string Category => "Optimize";
        public string Description => "Optimize Windows security settings";
        public int SortOrder => 5;
        public ICommand LoadSettingsCommand { get; }
        
        [ObservableProperty]
        private bool _isExpanded = true;
        
        public ICommand ToggleExpandCommand { get; }

        public WindowsSecurityOptimizationsViewModel(
            IDomainServiceRouter DomainServiceRouter,
            ISystemServices systemServices,
            IUacSettingsService uacSettingsService,
            ISettingApplicationService settingApplicationService,
            IEventBus eventBus,
            ITaskProgressService progressService,
            ILogService logService,
            ISettingsLoadingService settingsLoadingService)
        {
            _domainServiceRouter = DomainServiceRouter ?? throw new ArgumentNullException(nameof(DomainServiceRouter));
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _uacSettingsService = uacSettingsService ?? throw new ArgumentNullException(nameof(uacSettingsService));
            _settingApplicationService = settingApplicationService ?? throw new ArgumentNullException(nameof(settingApplicationService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _settingsLoadingService = settingsLoadingService ?? throw new ArgumentNullException(nameof(settingsLoadingService));
            
            // Initialize commands
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            ToggleExpandCommand = new RelayCommand(ToggleExpand);
        }

        /// <summary>
        /// Loads settings and initializes the UI state using the centralized loading service.
        /// </summary>
        public async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;

                Settings = new ObservableCollection<SettingItemViewModel>(
                    (
                        await _settingsLoadingService.LoadConfiguredSettingsAsync(
                            _domainServiceRouter.GetDomainService(ModuleId),
                            ModuleId,
                            "Loading security settings..."
                        )
                    ).Cast<SettingItemViewModel>()
                );

                _logService.Log(
                    LogLevel.Info,
                    $"WindowsSecurityOptimizationsViewModel: Successfully loaded {Settings.Count} settings"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading security settings: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Refreshes the settings for this feature asynchronously.
        /// </summary>
        public async Task RefreshSettingsAsync()
        {
            foreach (var setting in Settings)
            {
                await setting.RefreshStateAsync();
            }
        }

        /// <summary>
        /// Clears all settings and resets the feature state.
        /// </summary>
        public void ClearSettings()
        {
            Settings.Clear();
        }

        /// <summary>
        /// Updates visibility of settings based on search text.
        /// </summary>
        partial void OnSearchTextChanged(string value)
        {
            foreach (var setting in Settings)
            {
                setting.UpdateVisibility(value);
            }
            OnPropertyChanged(nameof(HasVisibleSettings));
        }
        
        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }
    }
}
