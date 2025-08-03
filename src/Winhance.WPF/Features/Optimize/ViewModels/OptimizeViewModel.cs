using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for the OptimizeView that manages system optimization settings using clean architecture.
    /// </summary>
    public partial class OptimizeViewModel : BaseSettingsViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly IConfigurationService _configurationService;
        private readonly IMessengerService _messengerService;
        private readonly ISearchService _searchService;

        // Child ViewModels
        private readonly GamingandPerformanceOptimizationsViewModel _gamingViewModel;
        private readonly PrivacyOptimizationsViewModel _privacyViewModel;
        private readonly UpdateOptimizationsViewModel _updateViewModel;
        private readonly PowerOptimizationsViewModel _powerViewModel;
        private readonly WindowsSecurityOptimizationsViewModel _securityViewModel;
        private readonly ExplorerOptimizationsViewModel _explorerViewModel;
        private readonly NotificationOptimizationsViewModel _notificationViewModel;
        private readonly SoundOptimizationsViewModel _soundViewModel;

        // Store a backup of all items for state recovery
        private List<SettingUIItem> _allItemsBackup = new List<SettingUIItem>();
        private bool _isInitialSearchDone = false;

        /// <summary>
        /// Gets or sets a value indicating whether search has any results.
        /// </summary>
        [ObservableProperty]
        private bool _hasSearchResults = true;

        /// <summary>
        /// Gets or sets the status text.
        /// </summary>
        [ObservableProperty]
        private string _statusText = "Optimize Your Windows System Performance and Privacy";

        /// <summary>
        /// Gets the collection of optimization items.
        /// </summary>
        public ObservableCollection<ApplicationSettingGroup> OptimizationItems { get; } = new();

        /// <summary>
        /// Gets the messenger service.
        /// </summary>
        public IMessengerService MessengerService => _messengerService;

        /// <summary>
        /// Gets the gaming and performance view model.
        /// </summary>
        public GamingandPerformanceOptimizationsViewModel GamingandPerformanceOptimizationsViewModel => _gamingViewModel;

        /// <summary>
        /// Gets the privacy view model.
        /// </summary>
        public PrivacyOptimizationsViewModel PrivacyOptimizationsViewModel => _privacyViewModel;

        /// <summary>
        /// Gets the update view model.
        /// </summary>
        public UpdateOptimizationsViewModel UpdateOptimizationsViewModel => _updateViewModel;

        /// <summary>
        /// Gets the power view model.
        /// </summary>
        public PowerOptimizationsViewModel PowerSettingsViewModel => _powerViewModel;

        /// <summary>
        /// Gets the security view model.
        /// </summary>
        public WindowsSecurityOptimizationsViewModel WindowsSecuritySettingsViewModel => _securityViewModel;

        /// <summary>
        /// Gets the explorer view model.
        /// </summary>
        public ExplorerOptimizationsViewModel ExplorerOptimizationsViewModel => _explorerViewModel;

        /// <summary>
        /// Gets the notification view model.
        /// </summary>
        public NotificationOptimizationsViewModel NotificationOptimizationsViewModel => _notificationViewModel;

        /// <summary>
        /// Gets the sound view model.
        /// </summary>
        public SoundOptimizationsViewModel SoundOptimizationsViewModel => _soundViewModel;

        /// <summary>
        /// Gets a value indicating whether the view model is initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Gets the Items collection for backward compatibility.
        /// </summary>
        public ObservableCollection<SettingUIItem> Items => Settings;

        /// <summary>
        /// Gets the initialize command.
        /// </summary>
        public AsyncRelayCommand InitializeCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizeViewModel"/> class.
        /// </summary>
        /// <param name="settingsService">The application settings service.</param>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="searchService">The search service.</param>
        /// <param name="configurationService">The configuration service.</param>
        /// <param name="gamingViewModel">The gaming and performance view model.</param>
        /// <param name="privacyViewModel">The privacy view model.</param>
        /// <param name="updateViewModel">The update view model.</param>
        /// <param name="powerViewModel">The power view model.</param>
        /// <param name="securityViewModel">The security view model.</param>
        /// <param name="explorerViewModel">The explorer view model.</param>
        /// <param name="notificationViewModel">The notification view model.</param>
        /// <param name="soundViewModel">The sound view model.</param>
        /// <param name="messengerService">The messenger service.</param>
        public OptimizeViewModel(
            IApplicationSettingsService settingsService,
            ITaskProgressService progressService,
            ILogService logService,
            IDialogService dialogService,
            ISearchService searchService,
            IConfigurationService configurationService,
            GamingandPerformanceOptimizationsViewModel gamingViewModel,
            PrivacyOptimizationsViewModel privacyViewModel,
            UpdateOptimizationsViewModel updateViewModel,
            PowerOptimizationsViewModel powerViewModel,
            WindowsSecurityOptimizationsViewModel securityViewModel,
            ExplorerOptimizationsViewModel explorerViewModel,
            NotificationOptimizationsViewModel notificationViewModel,
            SoundOptimizationsViewModel soundViewModel,
            IMessengerService messengerService)
            : base(settingsService, progressService, logService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));

            _gamingViewModel = gamingViewModel ?? throw new ArgumentNullException(nameof(gamingViewModel));
            _privacyViewModel = privacyViewModel ?? throw new ArgumentNullException(nameof(privacyViewModel));
            _updateViewModel = updateViewModel ?? throw new ArgumentNullException(nameof(updateViewModel));
            _powerViewModel = powerViewModel ?? throw new ArgumentNullException(nameof(powerViewModel));
            _securityViewModel = securityViewModel ?? throw new ArgumentNullException(nameof(securityViewModel));
            _explorerViewModel = explorerViewModel ?? throw new ArgumentNullException(nameof(explorerViewModel));
            _notificationViewModel = notificationViewModel ?? throw new ArgumentNullException(nameof(notificationViewModel));
            _soundViewModel = soundViewModel ?? throw new ArgumentNullException(nameof(soundViewModel));

            CategoryName = "Optimize";
            
            InitializeOptimizationItems();
            
            // Create initialize command
            InitializeCommand = new AsyncRelayCommand(InitializeAsync);
        }

        /// <summary>
        /// Initializes the optimization items.
        /// </summary>
        private void InitializeOptimizationItems()
        {
            OptimizationItems.Clear();

            // Add optimization categories
            OptimizationItems.Add(new ApplicationSettingGroup
            {
                Name = "Gaming and Performance",
                Description = "Optimize Windows for gaming and system performance",
                IsSelected = false,
                IsVisible = true
            });

            OptimizationItems.Add(new ApplicationSettingGroup
            {
                Name = "Privacy",
                Description = "Enhance your privacy by disabling telemetry and data collection",
                IsSelected = false,
                IsVisible = true
            });

            OptimizationItems.Add(new ApplicationSettingGroup
            {
                Name = "Updates",
                Description = "Control Windows Update behavior and settings",
                IsSelected = false,
                IsVisible = true
            });

            OptimizationItems.Add(new ApplicationSettingGroup
            {
                Name = "Power",
                Description = "Optimize power management and battery settings",
                IsSelected = false,
                IsVisible = true
            });

            OptimizationItems.Add(new ApplicationSettingGroup
            {
                Name = "Security",
                Description = "Configure Windows security and UAC settings",
                IsSelected = false,
                IsVisible = true
            });

            OptimizationItems.Add(new ApplicationSettingGroup
            {
                Name = "Explorer",
                Description = "Optimize Windows Explorer performance and behavior",
                IsSelected = false,
                IsVisible = true
            });

            OptimizationItems.Add(new ApplicationSettingGroup
            {
                Name = "Notifications",
                Description = "Control Windows notification settings and behavior",
                IsSelected = false,
                IsVisible = true
            });

            OptimizationItems.Add(new ApplicationSettingGroup
            {
                Name = "Sound",
                Description = "Optimize audio settings and sound enhancements",
                IsSelected = false,
                IsVisible = true
            });
        }

        /// <summary>
        /// Initializes the view model asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task InitializeAsync()
        {
            if (IsInitialized)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusText = "Loading optimization settings...";

                // Initialize all child view models
                await Task.WhenAll(
                    _gamingViewModel.LoadSettingsAsync(),
                    _privacyViewModel.LoadSettingsAsync(),
                    _updateViewModel.LoadSettingsAsync(),
                    _powerViewModel.LoadSettingsAsync(),
                    _securityViewModel.LoadSettingsAsync(),
                    _explorerViewModel.LoadSettingsAsync(),
                    _notificationViewModel.LoadSettingsAsync(),
                    _soundViewModel.LoadSettingsAsync()
                );

                // Load items
                await LoadItemsAsync();

                // Mark as initialized
                IsInitialized = true;
                StatusText = "Optimization settings loaded successfully";
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error initializing OptimizeViewModel: {ex.Message}");
                StatusText = "Error loading optimization settings";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Loads items asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task LoadItemsAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Loading optimization settings...";

                // Clear existing items
                Settings.Clear();
                _allItemsBackup.Clear();

                // Load settings from all child view models
                await _gamingViewModel.LoadSettingsAsync();
                await _privacyViewModel.LoadSettingsAsync();
                await _updateViewModel.LoadSettingsAsync();
                await _powerViewModel.LoadSettingsAsync();
                await _securityViewModel.LoadSettingsAsync();
                await _explorerViewModel.LoadSettingsAsync();
                await _notificationViewModel.LoadSettingsAsync();
                await _soundViewModel.LoadSettingsAsync();

                // Add settings from all child view models
                foreach (var setting in _gamingViewModel.Settings)
                {
                    Settings.Add(setting);
                    _allItemsBackup.Add(setting);
                }

                foreach (var setting in _privacyViewModel.Settings)
                {
                    Settings.Add(setting);
                    _allItemsBackup.Add(setting);
                }

                foreach (var setting in _updateViewModel.Settings)
                {
                    Settings.Add(setting);
                    _allItemsBackup.Add(setting);
                }

                foreach (var setting in _powerViewModel.Settings)
                {
                    Settings.Add(setting);
                    _allItemsBackup.Add(setting);
                }

                foreach (var setting in _securityViewModel.Settings)
                {
                    Settings.Add(setting);
                    _allItemsBackup.Add(setting);
                }

                foreach (var setting in _explorerViewModel.Settings)
                {
                    Settings.Add(setting);
                    _allItemsBackup.Add(setting);
                }

                foreach (var setting in _notificationViewModel.Settings)
                {
                    Settings.Add(setting);
                    _allItemsBackup.Add(setting);
                }

                foreach (var setting in _soundViewModel.Settings)
                {
                    Settings.Add(setting);
                    _allItemsBackup.Add(setting);
                }

                StatusText = $"Loaded {Settings.Count} optimization settings";
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading items: {ex.Message}");
                StatusText = "Error loading optimization settings";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public override async Task LoadSettingsAsync()
        {
            await LoadItemsAsync();
        }

        /// <summary>
        /// Gets the application settings that this ViewModel manages.
        /// </summary>
        /// <returns>Collection of application settings for all optimizations.</returns>
        protected override async Task<IEnumerable<ApplicationSetting>> GetApplicationSettingsAsync()
        {
            // Return all settings from all categories
            var allSettings = new List<ApplicationSetting>();
            
            var gamingSettings = await _settingsService.GetGamingAndPerformanceSettingsAsync();
            var privacySettings = await _settingsService.GetPrivacyOptimizationSettingsAsync();
            var updateSettings = await _settingsService.GetUpdateOptimizationSettingsAsync();
            var powerSettings = await _settingsService.GetPowerOptimizationSettingsAsync();
            var securitySettings = await _settingsService.GetWindowsSecurityOptimizationSettingsAsync();
            var explorerSettings = await _settingsService.GetExplorerOptimizationSettingsAsync();
            var notificationSettings = await _settingsService.GetNotificationOptimizationSettingsAsync();
            var soundSettings = await _settingsService.GetSoundOptimizationSettingsAsync();
            
            allSettings.AddRange(gamingSettings);
            allSettings.AddRange(privacySettings);
            allSettings.AddRange(updateSettings);
            allSettings.AddRange(powerSettings);
            allSettings.AddRange(securitySettings);
            allSettings.AddRange(explorerSettings);
            allSettings.AddRange(notificationSettings);
            allSettings.AddRange(soundSettings);
            
            return allSettings;
        }
    }
}
