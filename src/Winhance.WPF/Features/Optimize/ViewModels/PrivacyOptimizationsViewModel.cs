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
    /// ViewModel for Privacy optimizations using clean architecture principles.
    /// Directly manages settings using domain services without UI coordinator.
    /// </summary>
    public partial class PrivacyOptimizationsViewModel : ObservableObject, IFeatureViewModel
    {
        private readonly IDomainServiceRouter _domainServiceRouter;
        private readonly ISettingApplicationService _settingApplicationService;
        private readonly IEventBus _eventBus;
        private readonly ILogService _logService;
        private readonly ITaskProgressService _progressService;
        private readonly ISettingsLoadingService _settingsLoadingService;

        [ObservableProperty]
        private ObservableCollection<SettingItemViewModel> _settings = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public string CategoryName => "Privacy Optimizations";
        public bool HasVisibleSettings => Settings.Any(s => s.IsVisible);

        // IFeatureViewModel implementation
        public string ModuleId => FeatureIds.Privacy;
        public string DisplayName => "Privacy";
        public int SettingsCount => Settings?.Count ?? 0;
        public string Category => "Optimize";
        public string Description => "Optimize Windows privacy settings";
        public int SortOrder => 2;
        public ICommand LoadSettingsCommand { get; private set; }

        [ObservableProperty]
        private bool _isExpanded = true;

        public ICommand ToggleExpandCommand { get; }

        public PrivacyOptimizationsViewModel(
            IDomainServiceRouter DomainServiceRouter,
            ISettingApplicationService settingApplicationService,
            IEventBus eventBus,
            ITaskProgressService progressService,
            ILogService logService,
            ISettingsLoadingService settingsLoadingService
        )
        {
            _domainServiceRouter =
                DomainServiceRouter
                ?? throw new ArgumentNullException(nameof(DomainServiceRouter));
            _settingApplicationService =
                settingApplicationService
                ?? throw new ArgumentNullException(nameof(settingApplicationService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _progressService =
                progressService ?? throw new ArgumentNullException(nameof(progressService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _settingsLoadingService =
                settingsLoadingService
                ?? throw new ArgumentNullException(nameof(settingsLoadingService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

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
                            "Loading theme settings..."
                        )
                    ).Cast<SettingItemViewModel>()
                );

                _logService.Log(
                    LogLevel.Info,
                    $"PrivacyOptimizationsViewModel: Successfully loaded {Settings.Count} settings"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading privacy settings: {ex.Message}");
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
