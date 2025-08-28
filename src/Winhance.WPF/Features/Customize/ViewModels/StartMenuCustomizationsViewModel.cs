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
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// Clean architecture ViewModel for Start Menu customizations.
    /// Directly manages settings using domain services without UI coordinator.
    /// </summary>
    public partial class StartMenuCustomizationsViewModel : ObservableObject, IFeatureViewModel
    {
        private readonly IDomainServiceRouter _domainServiceRouter;
        private readonly ISettingApplicationService _settingApplicationService;
        private readonly IEventBus _eventBus;
        private readonly ITaskProgressService _progressService;
        private readonly ILogService _logService;
        private readonly ISystemServices _systemServices;
        private readonly IDialogService _dialogService;
        private readonly ISettingsLoadingService _settingsLoadingService;

        [ObservableProperty]
        private ObservableCollection<SettingItemViewModel> _settings = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public string CategoryName => "Start Menu";
        public bool HasVisibleSettings => Settings.Any(s => s.IsVisible);

        // IFeatureViewModel implementation
        public string ModuleId => FeatureIds.StartMenu;
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

        public StartMenuCustomizationsViewModel(
            IDomainServiceRouter DomainServiceRouter,
            ISettingApplicationService settingApplicationService,
            IEventBus eventBus,
            ILogService logService,
            IDialogService dialogService,
            ITaskProgressService progressService,
            ISystemServices systemServices,
            ISettingsLoadingService settingsLoadingService
        )
        {
            _domainServiceRouter =
                DomainServiceRouter ?? throw new ArgumentNullException(nameof(DomainServiceRouter));
            _settingApplicationService =
                settingApplicationService ?? throw new ArgumentNullException(nameof(settingApplicationService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _dialogService =
                dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _progressService =
                progressService ?? throw new ArgumentNullException(nameof(progressService));
            _systemServices =
                systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _settingsLoadingService =
                settingsLoadingService ?? throw new ArgumentNullException(nameof(settingsLoadingService));

            // Initialize commands
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            ToggleExpandCommand = new RelayCommand(ToggleExpand);
        }

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
                    $"StartMenuCustomizationsViewModel: Successfully loaded {Settings.Count} settings"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading Start Menu settings: {ex.Message}");
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

        public void ClearSettings()
        {
            Settings.Clear();
        }

        partial void OnSearchTextChanged(string value)
        {
            foreach (var setting in Settings)
            {
                setting.UpdateVisibility(value);
            }
            OnPropertyChanged(nameof(HasVisibleSettings));
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
