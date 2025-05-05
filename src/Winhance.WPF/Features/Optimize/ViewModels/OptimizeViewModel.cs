using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Features.Customize.Enums;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.Common.Messages;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for the OptimizeView that manages system optimization settings.
    /// </summary>
    public partial class OptimizeViewModel : SearchableViewModel<ApplicationSettingItem>
    {
        private readonly IRegistryService _registryService;
        private readonly IDialogService _dialogService;
        private readonly ILogService _logService;
        private readonly IConfigurationService _configurationService;
        private readonly IMessengerService _messengerService;
        private List<ApplicationSettingItem> _allItemsBackup = new List<ApplicationSettingItem>();
        private bool _updatingCheckboxes;
        private bool _isInitialSearchDone = false;
        
        /// <summary>
        /// Gets or sets a value indicating whether the view model is initialized.
        /// </summary>
        [ObservableProperty]
        private bool _isInitialized;
        
        /// <summary>
        /// Gets or sets a value indicating whether search has any results.
        /// </summary>
        [ObservableProperty]
        private bool _hasSearchResults = true;
        
        /// <summary>
        /// Gets or sets the status text.
        /// </summary>
        [ObservableProperty]
        private string _statusText = "Optimize Your Windows Settings and Performance";
        
        // Override the SearchText property to add explicit notification and direct control over the search flow
        private string _searchTextOverride = string.Empty;
        public override string SearchText
        {
            get => _searchTextOverride;
            set
            {
                if (_searchTextOverride != value)
                {
                    _searchTextOverride = value;
                    OnPropertyChanged(nameof(SearchText));
                    LogInfo($"OptimizeViewModel: SearchText changed to: '{value}'");
                    
                    // Explicitly update IsSearchActive and call ApplySearch
                    IsSearchActive = !string.IsNullOrWhiteSpace(value);
                    ApplySearch();
                    
                    // Update status text based on search results
                    if (IsSearchActive)
                    {
                        StatusText = $"Found {Items.Count} settings matching '{value}'";
                    }
                    else
                    {
                        StatusText = "Optimize Your Windows Settings and Performance";
                    }
                }
            }
        }

        /// <summary>
        /// Gets the messenger service.
        /// </summary>
        public IMessengerService MessengerService => _messengerService;

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizeViewModel"/> class.
        /// </summary>
        /// <param name="registryService">The registry service for interacting with the Windows Registry.</param>
        /// <param name="dialogService">The dialog service for showing dialogs.</param>
        /// <param name="logService">The logging service.</param>
        /// <param name="progressService">The progress service.</param>
        /// <param name="searchService">The search service.</param>
        /// <param name="configurationService">The configuration service.</param>
        /// <param name="gamingSettings">The gaming settings view model.</param>
        /// <param name="privacySettings">The privacy optimizations view model.</param>
        /// <param name="updateSettings">The update optimizations view model.</param>
        /// <param name="powerSettings">The power settings view model.</param>
        /// <param name="windowsSecuritySettings">The Windows security optimizations view model.</param>
        /// <param name="explorerSettings">The explorer optimizations view model.</param>
        /// <param name="notificationSettings">The notification optimizations view model.</param>
        /// <param name="soundSettings">The sound optimizations view model.</param>
        /// <param name="messengerService">The messenger service.</param>

        public OptimizeViewModel(
            IRegistryService registryService,
            IDialogService dialogService,
            ILogService logService,
            ITaskProgressService progressService,
            ISearchService searchService,
            IConfigurationService configurationService,
            GamingandPerformanceOptimizationsViewModel gamingSettings,
            PrivacyOptimizationsViewModel privacySettings,
            UpdateOptimizationsViewModel updateSettings,
            PowerOptimizationsViewModel powerSettings,
            WindowsSecurityOptimizationsViewModel windowsSecuritySettings,
            ExplorerOptimizationsViewModel explorerSettings,
            NotificationOptimizationsViewModel notificationSettings,
            SoundOptimizationsViewModel soundSettings,
            IMessengerService messengerService)
            : base(progressService, searchService, null)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));

            // Set specialized view models
            GamingandPerformanceOptimizationsViewModel = gamingSettings ?? throw new ArgumentNullException(nameof(gamingSettings));
            PrivacyOptimizationsViewModel = privacySettings ?? throw new ArgumentNullException(nameof(privacySettings));
            UpdateOptimizationsViewModel = updateSettings ?? throw new ArgumentNullException(nameof(updateSettings));
            PowerSettingsViewModel = powerSettings ?? throw new ArgumentNullException(nameof(powerSettings));
            WindowsSecuritySettingsViewModel = windowsSecuritySettings ?? throw new ArgumentNullException(nameof(windowsSecuritySettings));
            ExplorerOptimizationsViewModel = explorerSettings ?? throw new ArgumentNullException(nameof(explorerSettings));
            NotificationOptimizationsViewModel = notificationSettings ?? throw new ArgumentNullException(nameof(notificationSettings));
            SoundOptimizationsViewModel = soundSettings ?? throw new ArgumentNullException(nameof(soundSettings));

            // Store the configuration service
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

            // Create initialize command
            InitializeCommand = new AsyncRelayCommand(InitializeAsync);

            // SaveConfigCommand and ImportConfigCommand removed as part of unified configuration cleanup

            // We'll initialize when explicitly called, not automatically
            // This ensures the loading screen stays visible until initialization is complete

        }

        /// <summary>
        /// Gets the command to initialize the view model.
        /// </summary>
        public IAsyncRelayCommand InitializeCommand { get; }

        // SaveConfigCommand and ImportConfigCommand removed as part of unified configuration cleanup

        /// <summary>
        /// Gets the gaming settings view model.
        /// </summary>
        public GamingandPerformanceOptimizationsViewModel GamingandPerformanceOptimizationsViewModel { get; }

        /// <summary>
        /// Gets the privacy optimizations view model.
        /// </summary>
        public PrivacyOptimizationsViewModel PrivacyOptimizationsViewModel { get; }

        /// <summary>
        /// Gets the updates optimizations view model.
        /// </summary>
        public UpdateOptimizationsViewModel UpdateOptimizationsViewModel { get; }

        /// <summary>
        /// Gets the power settings view model.
        /// </summary>
        public PowerOptimizationsViewModel PowerSettingsViewModel { get; }

        /// <summary>
        /// Gets the Windows security optimizations view model.
        /// </summary>
        public WindowsSecurityOptimizationsViewModel WindowsSecuritySettingsViewModel { get; }

        /// <summary>
        /// Gets the explorer optimizations view model.
        /// </summary>
        public ExplorerOptimizationsViewModel ExplorerOptimizationsViewModel { get; }

        /// <summary>
        /// Gets the notification optimizations view model.
        /// </summary>
        public NotificationOptimizationsViewModel NotificationOptimizationsViewModel { get; }

        /// <summary>
        /// Gets the sound optimizations view model.
        /// </summary>
        public SoundOptimizationsViewModel SoundOptimizationsViewModel { get; }

        /// <summary>
        /// Toggles the selection state of all gaming settings.
        /// </summary>
        [RelayCommand]
        private void ToggleGaming()
        {
            try
            {
                _updatingCheckboxes = true;

                // Toggle the IsSelected property on the view model
                GamingandPerformanceOptimizationsViewModel.IsSelected = !GamingandPerformanceOptimizationsViewModel.IsSelected;

                // Update all settings in the view model
                foreach (var setting in GamingandPerformanceOptimizationsViewModel.Settings)
                {
                    setting.IsSelected = GamingandPerformanceOptimizationsViewModel.IsSelected;
                }
            }
            finally
            {
                _updatingCheckboxes = false;
            }
        }

        /// <summary>
        /// Toggles the selection state of all privacy settings.
        /// </summary>
        [RelayCommand]
        private void TogglePrivacy()
        {
            try
            {
                _updatingCheckboxes = true;

                // Toggle the IsSelected property on the view model
                PrivacyOptimizationsViewModel.IsSelected = !PrivacyOptimizationsViewModel.IsSelected;

                // Update all settings in the view model
                foreach (var setting in PrivacyOptimizationsViewModel.Settings)
                {
                    setting.IsSelected = PrivacyOptimizationsViewModel.IsSelected;
                }
            }
            finally
            {
                _updatingCheckboxes = false;
            }
        }

        /// <summary>
        /// Toggles the selection state of all update settings.
        /// </summary>
        [RelayCommand]
        private void ToggleUpdates()
        {
            try
            {
                _updatingCheckboxes = true;

                // Toggle the IsSelected property on the view model
                UpdateOptimizationsViewModel.IsSelected = !UpdateOptimizationsViewModel.IsSelected;

                // Update all settings in the view model
                foreach (var setting in UpdateOptimizationsViewModel.Settings)
                {
                    setting.IsSelected = UpdateOptimizationsViewModel.IsSelected;
                }
            }
            finally
            {
                _updatingCheckboxes = false;
            }
        }

        /// <summary>
        /// Toggles the selection state of all power settings.
        /// </summary>
        [RelayCommand]
        private void TogglePowerSettings()
        {
            try
            {
                _updatingCheckboxes = true;

                // Toggle the IsSelected property on the view model
                PowerSettingsViewModel.IsSelected = !PowerSettingsViewModel.IsSelected;

                // Update all settings in the view model
                foreach (var setting in PowerSettingsViewModel.Settings)
                {
                    setting.IsSelected = PowerSettingsViewModel.IsSelected;
                }
            }
            finally
            {
                _updatingCheckboxes = false;
            }
        }

        /// <summary>
        /// Toggles the selection state of Windows Security settings.
        /// </summary>
        [RelayCommand]
        private void ToggleWindowsSecurity()
        {
            try
            {
                _updatingCheckboxes = true;

                // Toggle the IsSelected property on the view model
                WindowsSecuritySettingsViewModel.IsSelected = !WindowsSecuritySettingsViewModel.IsSelected;

                // Windows Security only has the UAC notification level, no individual settings to update
                // This is primarily for UI consistency with other sections
            }
            finally
            {
                _updatingCheckboxes = false;
            }
        }

        /// <summary>
        /// Toggles the selection state of all explorer settings.
        /// </summary>
        [RelayCommand]
        private void ToggleExplorer()
        {
            try
            {
                _updatingCheckboxes = true;

                // Toggle the IsSelected property on the view model
                ExplorerOptimizationsViewModel.IsSelected = !ExplorerOptimizationsViewModel.IsSelected;

                // Update all settings in the view model
                foreach (var setting in ExplorerOptimizationsViewModel.Settings)
                {
                    setting.IsSelected = ExplorerOptimizationsViewModel.IsSelected;
                }
            }
            finally
            {
                _updatingCheckboxes = false;
            }
        }

        /// <summary>
        /// Toggles the selection state of all notification settings.
        /// </summary>
        [RelayCommand]
        private void ToggleNotifications()
        {
            try
            {
                _updatingCheckboxes = true;

                // Toggle the IsSelected property on the view model
                NotificationOptimizationsViewModel.IsSelected = !NotificationOptimizationsViewModel.IsSelected;

                // Update all settings in the view model
                foreach (var setting in NotificationOptimizationsViewModel.Settings)
                {
                    setting.IsSelected = NotificationOptimizationsViewModel.IsSelected;
                }
            }
            finally
            {
                _updatingCheckboxes = false;
            }
        }

        /// <summary>
        /// Toggles the selection state of all sound settings.
        /// </summary>
        [RelayCommand]
        private void ToggleSound()
        {
            try
            {
                _updatingCheckboxes = true;

                // Toggle the IsSelected property on the view model
                SoundOptimizationsViewModel.IsSelected = !SoundOptimizationsViewModel.IsSelected;

                // Update all settings in the view model
                foreach (var setting in SoundOptimizationsViewModel.Settings)
                {
                    setting.IsSelected = SoundOptimizationsViewModel.IsSelected;
                }
            }
            finally
            {
                _updatingCheckboxes = false;
            }
        }

        /// <summary>
        /// Loads items asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task LoadItemsAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Loading optimization settings...";

                // Clear the items collection
                Items.Clear();

                // Collect all settings from the various view models
                var allSettings = new List<ApplicationSettingItem>();
                
                // Ensure all child view models are initialized and have loaded their settings
                await EnsureChildViewModelsInitialized();

                // Add settings from each category - convert from OptimizationSettingViewModel to OptimizationSettingItem
                if (WindowsSecuritySettingsViewModel != null && WindowsSecuritySettingsViewModel.Settings != null)
                {
                    _logService.Log(LogLevel.Debug, $"Loading {WindowsSecuritySettingsViewModel.Settings.Count} settings from WindowsSecuritySettingsViewModel");
                    foreach (var setting in WindowsSecuritySettingsViewModel.Settings)
                    {
                        // Create a new OptimizationSettingItem with the properties from the setting
                        var item = new ApplicationSettingItem(_registryService, _dialogService, _logService)
                        {
                            Id = setting.Id,
                            Name = setting.Name,
                            Description = setting.Description,
                            IsSelected = setting.IsSelected,
                            GroupName = setting.GroupName,
                            IsVisible = setting.IsVisible
                        };
                        
                        // Copy properties directly without type casting
                        item.ControlType = setting.ControlType;
                        item.SliderValue = setting.SliderValue;
                        item.SliderSteps = setting.SliderSteps;
                        item.Status = setting.Status;
                        item.StatusMessage = setting.StatusMessage;
                        item.RegistrySetting = setting.RegistrySetting;
                        item.LinkedRegistrySettings = setting.LinkedRegistrySettings;
                        item.Dependencies = setting.Dependencies;
                        
                        // For UAC slider, add the slider labels
                        if (item.Id == "UACSlider" && item.ControlType == ControlType.ThreeStateSlider)
                        {
                            item.SliderLabels.Clear();
                            item.SliderLabels.Add("Low");
                            item.SliderLabels.Add("Moderate");
                            item.SliderLabels.Add("High");
                        }
                        
                        allSettings.Add(item);
                    }
                }

                if (GamingandPerformanceOptimizationsViewModel != null && GamingandPerformanceOptimizationsViewModel.Settings != null)
                {
                    foreach (var setting in GamingandPerformanceOptimizationsViewModel.Settings)
                    {
                        // Create a new OptimizationSettingItem with the properties from the setting
                        var item = new ApplicationSettingItem(_registryService, _dialogService, _logService)
                        {
                            Id = setting.Id,
                            Name = setting.Name,
                            Description = setting.Description,
                            IsSelected = setting.IsSelected,
                            GroupName = setting.GroupName,
                            IsVisible = setting.IsVisible
                        };
                        
                        // Copy properties directly without type casting
                        item.ControlType = setting.ControlType;
                        item.SliderValue = setting.SliderValue;
                        item.SliderSteps = setting.SliderSteps;
                        item.Status = setting.Status;
                        item.StatusMessage = setting.StatusMessage;
                        item.RegistrySetting = setting.RegistrySetting;
                        item.LinkedRegistrySettings = setting.LinkedRegistrySettings;
                        item.Dependencies = setting.Dependencies;
                        allSettings.Add(item);
                    }
                }

                if (PrivacyOptimizationsViewModel != null && PrivacyOptimizationsViewModel.Settings != null)
                {
                    foreach (var setting in PrivacyOptimizationsViewModel.Settings)
                    {
                        // Create a new OptimizationSettingItem with the properties from the setting
                        var item = new ApplicationSettingItem(_registryService, _dialogService, _logService)
                        {
                            Id = setting.Id,
                            Name = setting.Name,
                            Description = setting.Description,
                            IsSelected = setting.IsSelected,
                            GroupName = setting.GroupName,
                            IsVisible = setting.IsVisible
                        };
                        
                        // Copy properties directly without type casting
                        item.ControlType = setting.ControlType;
                        item.SliderValue = setting.SliderValue;
                        item.SliderSteps = setting.SliderSteps;
                        item.Status = setting.Status;
                        item.StatusMessage = setting.StatusMessage;
                        item.RegistrySetting = setting.RegistrySetting;
                        item.LinkedRegistrySettings = setting.LinkedRegistrySettings;
                        item.Dependencies = setting.Dependencies;
                        allSettings.Add(item);
                    }
                }

                if (UpdateOptimizationsViewModel != null && UpdateOptimizationsViewModel.Settings != null)
                {
                    foreach (var setting in UpdateOptimizationsViewModel.Settings)
                    {
                        // Create a new OptimizationSettingItem with the properties from the setting
                        var item = new ApplicationSettingItem(_registryService, _dialogService, _logService)
                        {
                            Id = setting.Id,
                            Name = setting.Name,
                            Description = setting.Description,
                            IsSelected = setting.IsSelected,
                            GroupName = setting.GroupName,
                            IsVisible = setting.IsVisible
                        };
                        
                        // Copy properties directly without type casting
                        item.ControlType = setting.ControlType;
                        item.SliderValue = setting.SliderValue;
                        item.SliderSteps = setting.SliderSteps;
                        item.Status = setting.Status;
                        item.StatusMessage = setting.StatusMessage;
                        item.RegistrySetting = setting.RegistrySetting;
                        item.LinkedRegistrySettings = setting.LinkedRegistrySettings;
                        item.Dependencies = setting.Dependencies;
                        allSettings.Add(item);
                    }
                }

                if (PowerSettingsViewModel != null && PowerSettingsViewModel.Settings != null)
                {
                    foreach (var setting in PowerSettingsViewModel.Settings)
                    {
                        // Create a new OptimizationSettingItem with the properties from the setting
                        var item = new ApplicationSettingItem(_registryService, _dialogService, _logService)
                        {
                            Id = setting.Id,
                            Name = setting.Name,
                            Description = setting.Description,
                            IsSelected = setting.IsSelected,
                            GroupName = setting.GroupName,
                            IsVisible = setting.IsVisible
                        };
                        
                        // Copy properties directly without type casting
                        item.ControlType = setting.ControlType;
                        item.SliderValue = setting.SliderValue;
                        item.SliderSteps = setting.SliderSteps;
                        item.Status = setting.Status;
                        item.StatusMessage = setting.StatusMessage;
                        item.RegistrySetting = setting.RegistrySetting;
                        item.LinkedRegistrySettings = setting.LinkedRegistrySettings;
                        item.Dependencies = setting.Dependencies;
                        
                        // For Power Plan ComboBox, add the ComboBox labels
                        if (item.Id == "PowerPlanComboBox" && item.ControlType == ControlType.ComboBox)
                        {
                            item.SliderLabels.Clear();
                            // Copy labels from PowerSettingsViewModel
                            if (PowerSettingsViewModel.PowerPlanLabels != null)
                            {
                                foreach (var label in PowerSettingsViewModel.PowerPlanLabels)
                                {
                                    item.SliderLabels.Add(label);
                                }
                                
                                // Set the SliderValue based on the current PowerPlanValue
                                if (PowerSettingsViewModel.PowerPlanValue >= 0 &&
                                    PowerSettingsViewModel.PowerPlanValue < PowerSettingsViewModel.PowerPlanLabels.Count)
                                {
                                    item.SliderValue = PowerSettingsViewModel.PowerPlanValue;
                                    _logService.Log(LogLevel.Info, $"Set SliderValue for Power Plan to {item.SliderValue} (label: {PowerSettingsViewModel.PowerPlanLabels[PowerSettingsViewModel.PowerPlanValue]})");
                                }
                            }
                            else
                            {
                                // Fallback labels if PowerPlanLabels is null
                                item.SliderLabels.Add("Balanced");
                                item.SliderLabels.Add("High Performance");
                                item.SliderLabels.Add("Ultimate Performance");
                                
                                // Set the SliderValue based on the current PowerPlanValue
                                if (PowerSettingsViewModel.PowerPlanValue >= 0 && PowerSettingsViewModel.PowerPlanValue < 3)
                                {
                                    item.SliderValue = PowerSettingsViewModel.PowerPlanValue;
                                    string[] defaultLabels = { "Balanced", "High Performance", "Ultimate Performance" };
                                    _logService.Log(LogLevel.Info, $"Set SliderValue for Power Plan to {item.SliderValue} (label: {defaultLabels[PowerSettingsViewModel.PowerPlanValue]})");
                                }
                            }
                        }
                        
                        allSettings.Add(item);
                    }
                }

                if (ExplorerOptimizationsViewModel != null && ExplorerOptimizationsViewModel.Settings != null)
                {
                    foreach (var setting in ExplorerOptimizationsViewModel.Settings)
                    {
                        // Create a new OptimizationSettingItem with the properties from the setting
                        var item = new ApplicationSettingItem(_registryService, _dialogService, _logService)
                        {
                            Id = setting.Id,
                            Name = setting.Name,
                            Description = setting.Description,
                            IsSelected = setting.IsSelected,
                            GroupName = setting.GroupName,
                            IsVisible = setting.IsVisible
                        };
                        
                        // Copy properties directly without type casting
                        item.ControlType = setting.ControlType;
                        item.SliderValue = setting.SliderValue;
                        item.SliderSteps = setting.SliderSteps;
                        item.Status = setting.Status;
                        item.StatusMessage = setting.StatusMessage;
                        item.RegistrySetting = setting.RegistrySetting;
                        item.LinkedRegistrySettings = setting.LinkedRegistrySettings;
                        item.Dependencies = setting.Dependencies;
                        allSettings.Add(item);
                    }
                }

                if (NotificationOptimizationsViewModel != null && NotificationOptimizationsViewModel.Settings != null)
                {
                    foreach (var setting in NotificationOptimizationsViewModel.Settings)
                    {
                        // Create a new OptimizationSettingItem with the properties from the setting
                        var item = new ApplicationSettingItem(_registryService, _dialogService, _logService)
                        {
                            Id = setting.Id,
                            Name = setting.Name,
                            Description = setting.Description,
                            IsSelected = setting.IsSelected,
                            GroupName = setting.GroupName,
                            IsVisible = setting.IsVisible
                        };
                        
                        // Copy properties directly without type casting
                        item.ControlType = setting.ControlType;
                        item.SliderValue = setting.SliderValue;
                        item.SliderSteps = setting.SliderSteps;
                        item.Status = setting.Status;
                        item.StatusMessage = setting.StatusMessage;
                        item.RegistrySetting = setting.RegistrySetting;
                        item.LinkedRegistrySettings = setting.LinkedRegistrySettings;
                        item.Dependencies = setting.Dependencies;
                        allSettings.Add(item);
                    }
                }

                if (SoundOptimizationsViewModel != null && SoundOptimizationsViewModel.Settings != null)
                {
                    foreach (var setting in SoundOptimizationsViewModel.Settings)
                    {
                        // Create a new OptimizationSettingItem with the properties from the setting
                        var item = new ApplicationSettingItem(_registryService, _dialogService, _logService)
                        {
                            Id = setting.Id,
                            Name = setting.Name,
                            Description = setting.Description,
                            IsSelected = setting.IsSelected,
                            GroupName = setting.GroupName,
                            IsVisible = setting.IsVisible
                        };
                        
                        // Copy properties directly without type casting
                        item.ControlType = setting.ControlType;
                        item.SliderValue = setting.SliderValue;
                        item.SliderSteps = setting.SliderSteps;
                        item.Status = setting.Status;
                        item.StatusMessage = setting.StatusMessage;
                        item.RegistrySetting = setting.RegistrySetting;
                        item.LinkedRegistrySettings = setting.LinkedRegistrySettings;
                        item.Dependencies = setting.Dependencies;
                        allSettings.Add(item);
                    }
                }

                // Add all settings to the Items collection
                foreach (var setting in allSettings)
                {
                    Items.Add(setting);
                }

                // Create a backup of all items for state recovery
                _allItemsBackup = new List<ApplicationSettingItem>(Items);

                // Only update StatusText if it's currently showing a loading message
                if (StatusText.Contains("Loading"))
                {
                    StatusText = "Optimize Your Windows Settings and Performance";
                }
                _logService.Log(LogLevel.Info, $"OptimizeViewModel.LoadItemsAsync completed with {Items.Count} items");
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading optimization settings: {ex.Message}";
                LogError($"Error loading optimization settings: {ex.Message}", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks the installation status of items asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task CheckInstallationStatusAsync()
        {
            // This method is not applicable for optimization settings
            // but we need to implement it to satisfy the interface
            await Task.CompletedTask;
        }

        
        /// <summary>
        /// Restores all items visibility to their original state.
        /// </summary>
        private void RestoreAllItemsVisibility()
        {
            // Make all settings visible in each category
            if (GamingandPerformanceOptimizationsViewModel?.Settings != null)
            {
                foreach (var setting in GamingandPerformanceOptimizationsViewModel.Settings)
                {
                    setting.IsVisible = true;
                }
                GamingandPerformanceOptimizationsViewModel.HasVisibleSettings = GamingandPerformanceOptimizationsViewModel.Settings.Count > 0;
            }
            
            if (PrivacyOptimizationsViewModel?.Settings != null)
            {
                foreach (var setting in PrivacyOptimizationsViewModel.Settings)
                {
                    setting.IsVisible = true;
                }
                PrivacyOptimizationsViewModel.HasVisibleSettings = PrivacyOptimizationsViewModel.Settings.Count > 0;
            }
            
            if (UpdateOptimizationsViewModel?.Settings != null)
            {
                foreach (var setting in UpdateOptimizationsViewModel.Settings)
                {
                    setting.IsVisible = true;
                }
                UpdateOptimizationsViewModel.HasVisibleSettings = UpdateOptimizationsViewModel.Settings.Count > 0;
            }
            
            if (PowerSettingsViewModel?.Settings != null)
            {
                foreach (var setting in PowerSettingsViewModel.Settings)
                {
                    setting.IsVisible = true;
                }
                PowerSettingsViewModel.HasVisibleSettings = PowerSettingsViewModel.Settings.Count > 0;
            }
            
            if (ExplorerOptimizationsViewModel?.Settings != null)
            {
                foreach (var setting in ExplorerOptimizationsViewModel.Settings)
                {
                    setting.IsVisible = true;
                }
                ExplorerOptimizationsViewModel.HasVisibleSettings = ExplorerOptimizationsViewModel.Settings.Count > 0;
            }
            
            if (NotificationOptimizationsViewModel?.Settings != null)
            {
                foreach (var setting in NotificationOptimizationsViewModel.Settings)
                {
                    setting.IsVisible = true;
                }
                NotificationOptimizationsViewModel.HasVisibleSettings = NotificationOptimizationsViewModel.Settings.Count > 0;
            }
            
            if (SoundOptimizationsViewModel?.Settings != null)
            {
                foreach (var setting in SoundOptimizationsViewModel.Settings)
                {
                    setting.IsVisible = true;
                }
                SoundOptimizationsViewModel.HasVisibleSettings = SoundOptimizationsViewModel.Settings.Count > 0;
            }
            
            if (WindowsSecuritySettingsViewModel?.Settings != null)
            {
                foreach (var setting in WindowsSecuritySettingsViewModel.Settings)
                {
                    setting.IsVisible = true;
                }
                WindowsSecuritySettingsViewModel.HasVisibleSettings = WindowsSecuritySettingsViewModel.Settings.Count > 0;
            }
            
            // Always ensure HasSearchResults is true when restoring visibility
            HasSearchResults = true;
            
            // Send a message to notify the view to reset section expansion states
            _messengerService.Send(new ResetExpansionStateMessage());
            
            LogInfo("OptimizeViewModel: RestoreAllItemsVisibility has reset all UI elements to visible state");
        }
        
        /// <summary>
        /// Applies the current search text to filter items.
        /// </summary>
        protected override void ApplySearch()
        {
            LogInfo($"OptimizeViewModel: ApplySearch called with SearchText: '{SearchText}'");
            
            // If we have no items yet, there's nothing to filter
            if (Items == null)
                return;
                
            // If this is our first time running a search and the backup isn't created yet, create it
            if (!_isInitialSearchDone && Items.Count > 0)
            {
                _allItemsBackup = new List<ApplicationSettingItem>(Items);
                _isInitialSearchDone = true;
                LogInfo($"OptimizeViewModel: Created backup of all items ({_allItemsBackup.Count} items)");
            }

            // If search is empty, restore all items visibility and the original items collection
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                LogInfo("OptimizeViewModel: Empty search, restoring all items visibility");
                
                // Restore visibility of UI elements first
                RestoreAllItemsVisibility();
                
                // Use the backup to restore all items
                if (_allItemsBackup.Count > 0)
                {
                    LogInfo($"OptimizeViewModel: Restoring {_allItemsBackup.Count} items from backup");
                    Items.Clear();
                    foreach (var item in _allItemsBackup)
                    {
                        Items.Add(item);
                    }
                }
                else if (Items.Count == 0)
                {
                    // If we don't have a backup and Items is empty (which could happen after a no-results search),
                    // try to reload items
                    LogInfo("OptimizeViewModel: No backup available and Items is empty, attempting to reload");
                    _ = LoadItemsAsync();
                }
                
                // Always set HasSearchResults to true when search is cleared
                HasSearchResults = true;
                
                // Update status text
                StatusText = "Optimize Your Windows Settings and Performance";
                LogInfo($"OptimizeViewModel: Restored items, count={Items.Count}");
                return;
            }

            // We're doing an active search, use the backup for filtering if available
            var itemsToFilter = _allItemsBackup.Count > 0
                ? new ObservableCollection<ApplicationSettingItem>(_allItemsBackup)
                : Items;
            
            // Normalize and clean the search text - convert to lowercase for consistent case-insensitive matching
            string normalizedSearchText = SearchText.Trim().ToLowerInvariant();
            LogInfo($"OptimizeViewModel: Normalized search text: '{normalizedSearchText}'");
            
            // Handle edge case: if search is empty after normalization, treat it as empty
            if (string.IsNullOrEmpty(normalizedSearchText))
            {
                // Same handling as empty search
                RestoreAllItemsVisibility();
                if (_allItemsBackup.Count > 0)
                {
                    Items.Clear();
                    foreach (var item in _allItemsBackup)
                    {
                        Items.Add(item);
                    }
                }
                HasSearchResults = true;
                StatusText = "Optimize Your Windows Settings and Performance";
                return;
            }
            
            // Add additional logging to help diagnose search issues
            LogInfo($"OptimizeViewModel: Starting search with term '{normalizedSearchText}'");
            
            // Filter items based on search text - ensure we pass normalized text
            // Don't convert to lowercase here - we'll handle case insensitivity in the MatchesSearch method
            var filteredItems = FilterItems(itemsToFilter).ToList();
            
            // Log each filtered item for debugging
            foreach (var item in filteredItems)
            {
                LogInfo($"OptimizeViewModel: Filtered item: '{item.Name}' (ID: {item.Id})");
            }
            
            // Log the number of filtered items
            int filteredCount = filteredItems.Count;
            LogInfo($"OptimizeViewModel: Found {filteredCount} matching items");
            
            // Log the filtered count for debugging
            LogInfo($"OptimizeViewModel: Initial filtered count: {filteredCount}");
            
            // We'll update HasSearchResults after counting the actual visible items
            // This is just an initial value
            bool initialHasResults = filteredCount > 0;
            LogInfo($"OptimizeViewModel: Initial HasSearchResults: {initialHasResults}");
            
            // Log the filtered items for debugging
            LogInfo($"OptimizeViewModel: Filtered items: {string.Join(", ", filteredItems.Select(item => item.Name))}");

            // Update each sub-view model's Settings collection with the normalized search text
            UpdateSubViewSettings(GamingandPerformanceOptimizationsViewModel, normalizedSearchText);
            UpdateSubViewSettings(PrivacyOptimizationsViewModel, normalizedSearchText);
            UpdateSubViewSettings(UpdateOptimizationsViewModel, normalizedSearchText);
            UpdateSubViewSettings(PowerSettingsViewModel, normalizedSearchText);
            UpdateSubViewSettings(ExplorerOptimizationsViewModel, normalizedSearchText);
            UpdateSubViewSettings(NotificationOptimizationsViewModel, normalizedSearchText);
            UpdateSubViewSettings(SoundOptimizationsViewModel, normalizedSearchText);
            UpdateSubViewSettings(WindowsSecuritySettingsViewModel, normalizedSearchText);

            // Count the actual visible items after filtering
            int visibleItemsCount = 0;
            
            // Count visible items in each category
            if (GamingandPerformanceOptimizationsViewModel?.Settings != null)
                visibleItemsCount += GamingandPerformanceOptimizationsViewModel.Settings.Count(s => s.IsVisible);
                
            if (PrivacyOptimizationsViewModel?.Settings != null)
                visibleItemsCount += PrivacyOptimizationsViewModel.Settings.Count(s => s.IsVisible);
                
            if (UpdateOptimizationsViewModel?.Settings != null)
                visibleItemsCount += UpdateOptimizationsViewModel.Settings.Count(s => s.IsVisible);
                
            if (PowerSettingsViewModel?.Settings != null)
                visibleItemsCount += PowerSettingsViewModel.Settings.Count(s => s.IsVisible);
                
            if (ExplorerOptimizationsViewModel?.Settings != null)
                visibleItemsCount += ExplorerOptimizationsViewModel.Settings.Count(s => s.IsVisible);
                
            if (NotificationOptimizationsViewModel?.Settings != null)
                visibleItemsCount += NotificationOptimizationsViewModel.Settings.Count(s => s.IsVisible);
                
            if (SoundOptimizationsViewModel?.Settings != null)
                visibleItemsCount += SoundOptimizationsViewModel.Settings.Count(s => s.IsVisible);
                
            if (WindowsSecuritySettingsViewModel?.Settings != null)
                visibleItemsCount += WindowsSecuritySettingsViewModel.Settings.Count(s => s.IsVisible);

            // Update the Items collection to match visible items
            Items.Clear();
            foreach (var setting in filteredItems)
            {
                if (setting.IsVisible)
                {
                    Items.Add(setting);
                }
            }
            
            // Now update HasSearchResults based on the ACTUAL visible items count
            // This is more accurate than using the filtered count
            HasSearchResults = visibleItemsCount > 0;
            LogInfo($"OptimizeViewModel: Final HasSearchResults: {HasSearchResults} (visibleItemsCount: {visibleItemsCount})");

            // Update status text with correct count
            if (IsSearchActive)
            {
                StatusText = $"Found {visibleItemsCount} settings matching '{SearchText}'";
                LogInfo($"OptimizeViewModel: StatusText updated to: '{StatusText}' with {visibleItemsCount} visible items");
            }
            else
            {
                StatusText = $"Showing all {Items.Count} optimization settings";
                LogInfo($"OptimizeViewModel: StatusText updated to: '{StatusText}'");
            }
        }

        /// <summary>
        /// Updates a sub-view model's Settings collection based on search text.
        /// </summary>
        /// <param name="viewModel">The sub-view model to update.</param>
        /// <param name="searchText">The normalized search text to match against.</param>
        private void UpdateSubViewSettings(object viewModel, string searchText)
        {
            if (viewModel == null)
                return;

            // Use dynamic to access properties without knowing the exact type
            dynamic dynamicViewModel = viewModel;
            
            if (dynamicViewModel.Settings == null)
                return;

            // If not searching, show all settings
            if (!IsSearchActive)
            {
                foreach (var setting in dynamicViewModel.Settings)
                {
                    setting.IsVisible = true;
                }
                
                // Update the view model's visibility
                dynamicViewModel.HasVisibleSettings = dynamicViewModel.Settings.Count > 0;
                
                LogInfo($"OptimizeViewModel: Set all settings visible in {dynamicViewModel.GetType().Name}");
                return;
            }

            // When searching, only show settings that match the search criteria
            bool hasVisibleSettings = false;
            int visibleCount = 0;
            
            foreach (var setting in dynamicViewModel.Settings)
            {
                // Use partial matching instead of exact name matching
                // This matches the same logic used in OptimizationSettingItem.MatchesSearch()
                bool matchesName = setting.Name != null &&
                                  setting.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchesDescription = setting.Description != null &&
                                         setting.Description.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchesGroupName = setting.GroupName != null &&
                                       setting.GroupName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                
                // Set visibility based on any match
                setting.IsVisible = matchesName || matchesDescription || matchesGroupName;
                
                // Log when we find a match for debugging
                if (setting.IsVisible)
                {
                    LogInfo($"OptimizeViewModel: Setting '{setting.Name}' is visible in {viewModel.GetType().Name}");
                }
                
                if (setting.IsVisible)
                {
                    hasVisibleSettings = true;
                    visibleCount++;
                }
            }
            
            // Update the view model's visibility
            dynamicViewModel.HasVisibleSettings = hasVisibleSettings;
            
            LogInfo($"OptimizeViewModel: {dynamicViewModel.GetType().Name} has {visibleCount} visible settings, HasVisibleSettings={hasVisibleSettings}");
        }

        // SaveConfig and ImportConfig methods removed as part of unified configuration cleanup

        /// <summary>
        /// Gets the name of the UAC level.
        /// </summary>
        /// <param name="level">The UAC level (0=Low, 1=Moderate, 2=High).</param>
        /// <returns>The name of the UAC level.</returns>
        private string GetUacLevelName(int level)
        {
            return level switch
            {
                0 => "Low",
                1 => "Moderate",
                2 => "High",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Called when the view model is navigated to.
        /// </summary>
        /// <param name="parameter">The navigation parameter.</param>
        public override async void OnNavigatedTo(object parameter)
        {
            LogInfo("OptimizeViewModel.OnNavigatedTo called");

            try
            {
                // If not already initialized, initialize now
                if (!IsInitialized)
                {
                    await InitializeAsync(CancellationToken.None);
                }
                else
                {
                    // Just refresh the settings status
                    await RefreshSettingsStatusAsync();
                }
            }
            catch (Exception ex)
            {
                LogError($"Error in OptimizeViewModel.OnNavigatedTo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Initializes the view model asynchronously.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (IsInitialized)
                return;

            try
            {
                IsLoading = true;
                LogInfo("Initializing OptimizeViewModel");

                // Create a progress reporter
                var progress = new Progress<TaskProgressDetail>(detail => {
                    // Report progress through the task progress service
                    int progress = detail.Progress.HasValue ? (int)detail.Progress.Value : 0;
                    ProgressService.UpdateProgress(progress, detail.StatusText);
                });

                // Initialize settings sequentially to provide better progress reporting
                ProgressService.UpdateProgress(0, "Loading Windows security settings...");
                await WindowsSecuritySettingsViewModel.LoadSettingsAsync();

                ProgressService.UpdateProgress(10, "Loading gaming settings...");
                await GamingandPerformanceOptimizationsViewModel.LoadSettingsAsync();

                ProgressService.UpdateProgress(25, "Loading privacy settings...");
                await PrivacyOptimizationsViewModel.LoadSettingsAsync();

                ProgressService.UpdateProgress(40, "Loading update settings...");
                await UpdateOptimizationsViewModel.LoadSettingsAsync();

                ProgressService.UpdateProgress(55, "Loading power settings...");
                await PowerSettingsViewModel.LoadSettingsAsync();

                ProgressService.UpdateProgress(70, "Loading explorer settings...");
                await ExplorerOptimizationsViewModel.LoadSettingsAsync();

                ProgressService.UpdateProgress(80, "Loading notification settings...");
                await NotificationOptimizationsViewModel.LoadSettingsAsync();

                ProgressService.UpdateProgress(90, "Loading sound settings...");
                await SoundOptimizationsViewModel.LoadSettingsAsync();

                ProgressService.UpdateProgress(100, "Initialization complete");

                // Mark as initialized
                IsInitialized = true;

                LogInfo("OptimizeViewModel initialized successfully");
            }
            catch (Exception ex)
            {
                LogError($"Error initializing OptimizeViewModel: {ex.Message}", ex);
                throw; // Rethrow to ensure the caller knows initialization failed
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        /// <summary>
        /// Ensures all child view models are initialized and have loaded their settings.
        /// </summary>
        private async Task EnsureChildViewModelsInitialized()
        {
            _logService.Log(LogLevel.Debug, "Ensuring all child view models are initialized");
            
            // Create a list of tasks to initialize all child view models
            var initializationTasks = new List<Task>();
            
            // Windows Security Settings
            if (WindowsSecuritySettingsViewModel != null && WindowsSecuritySettingsViewModel.Settings == null)
            {
                _logService.Log(LogLevel.Debug, "Loading WindowsSecuritySettingsViewModel settings");
                initializationTasks.Add(WindowsSecuritySettingsViewModel.LoadSettingsAsync());
            }
            
            // Gaming and Performance Settings
            if (GamingandPerformanceOptimizationsViewModel != null && GamingandPerformanceOptimizationsViewModel.Settings == null)
            {
                _logService.Log(LogLevel.Debug, "Loading GamingandPerformanceOptimizationsViewModel settings");
                initializationTasks.Add(GamingandPerformanceOptimizationsViewModel.LoadSettingsAsync());
            }
            
            // Privacy Settings
            if (PrivacyOptimizationsViewModel != null && PrivacyOptimizationsViewModel.Settings == null)
            {
                _logService.Log(LogLevel.Debug, "Loading PrivacyOptimizationsViewModel settings");
                initializationTasks.Add(PrivacyOptimizationsViewModel.LoadSettingsAsync());
            }
            
            // Update Settings
            if (UpdateOptimizationsViewModel != null && UpdateOptimizationsViewModel.Settings == null)
            {
                _logService.Log(LogLevel.Debug, "Loading UpdateOptimizationsViewModel settings");
                initializationTasks.Add(UpdateOptimizationsViewModel.LoadSettingsAsync());
            }
            
            // Power Settings
            if (PowerSettingsViewModel != null && PowerSettingsViewModel.Settings == null)
            {
                _logService.Log(LogLevel.Debug, "Loading PowerSettingsViewModel settings");
                initializationTasks.Add(PowerSettingsViewModel.LoadSettingsAsync());
            }
            
            // Explorer Settings
            if (ExplorerOptimizationsViewModel != null && ExplorerOptimizationsViewModel.Settings == null)
            {
                _logService.Log(LogLevel.Debug, "Loading ExplorerOptimizationsViewModel settings");
                initializationTasks.Add(ExplorerOptimizationsViewModel.LoadSettingsAsync());
            }
            
            // Notification Settings
            if (NotificationOptimizationsViewModel != null && NotificationOptimizationsViewModel.Settings == null)
            {
                _logService.Log(LogLevel.Debug, "Loading NotificationOptimizationsViewModel settings");
                initializationTasks.Add(NotificationOptimizationsViewModel.LoadSettingsAsync());
            }
            
            // Sound Settings
            if (SoundOptimizationsViewModel != null && SoundOptimizationsViewModel.Settings == null)
            {
                _logService.Log(LogLevel.Debug, "Loading SoundOptimizationsViewModel settings");
                initializationTasks.Add(SoundOptimizationsViewModel.LoadSettingsAsync());
            }
            
            // Wait for all initialization tasks to complete
            if (initializationTasks.Count > 0)
            {
                _logService.Log(LogLevel.Debug, $"Waiting for {initializationTasks.Count} child view models to initialize");
                await Task.WhenAll(initializationTasks);
                _logService.Log(LogLevel.Debug, "All child view models initialized");
            }
            else
            {
                _logService.Log(LogLevel.Debug, "All child view models were already initialized");
            }
        }

        /// <summary>
        /// Refreshes the status of all settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task RefreshSettingsStatusAsync()
        {
            try
            {
                IsLoading = true;
                LogInfo("Refreshing settings status");

                // Refresh all settings view models
                var refreshTasks = new List<Task>
                {
                    WindowsSecuritySettingsViewModel.CheckSettingStatusesAsync(),
                    GamingandPerformanceOptimizationsViewModel.CheckSettingStatusesAsync(),
                    PrivacyOptimizationsViewModel.CheckSettingStatusesAsync(),
                    UpdateOptimizationsViewModel.CheckSettingStatusesAsync(),
                    PowerSettingsViewModel.CheckSettingStatusesAsync(),
                    ExplorerOptimizationsViewModel.CheckSettingStatusesAsync(),
                    NotificationOptimizationsViewModel.CheckSettingStatusesAsync(),
                    SoundOptimizationsViewModel.CheckSettingStatusesAsync()
                };

                // Wait for all refresh tasks to complete
                await Task.WhenAll(refreshTasks);

                LogInfo("Settings status refreshed successfully");
            }
            catch (Exception ex)
            {
                LogError($"Error refreshing settings status: {ex.Message}", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Handles the checkbox state changed event for optimization settings.
        /// </summary>
        /// <param name="setting">The setting that was changed.</param>
        [RelayCommand]
        private void SettingChanged(Winhance.WPF.Features.Common.Models.ApplicationSettingItem setting)
        {
            if (_updatingCheckboxes)
                return;

            try
            {
                _updatingCheckboxes = true;

                if (setting.RegistrySetting?.Category == "PowerPlan")
                {
                    // Handle power plan changes separately
                    // This would call a method to change the power plan
                    LogInfo($"Power plan setting changed: {setting.Name} to {setting.IsSelected}");
                }
                else
                {
                    // Apply registry change
                    if (setting.RegistrySetting != null)
                    {
                        // Use EnabledValue/DisabledValue if available, otherwise fall back to RecommendedValue/DefaultValue
                        string valueToSet;
                        if (setting.IsSelected)
                        {
                            var enabledValue = setting.RegistrySetting.EnabledValue ?? setting.RegistrySetting.RecommendedValue;
                            valueToSet = enabledValue.ToString();
                        }
                        else
                        {
                            var disabledValue = setting.RegistrySetting.DisabledValue ?? setting.RegistrySetting.DefaultValue;
                            valueToSet = disabledValue?.ToString() ?? "";
                        }

                        var result = _registryService.SetValue(
                            setting.RegistrySetting.Hive + "\\" + setting.RegistrySetting.SubKey,
                            setting.RegistrySetting.Name,
                            valueToSet,
                            setting.RegistrySetting.ValueType);

                        if (result)
                        {
                            LogInfo($"Setting applied: {setting.Name}");

                            // Check if restart is required based on setting category or name
                            bool requiresRestart = setting.Name.Contains("restart", StringComparison.OrdinalIgnoreCase);

                            if (requiresRestart)
                            {
                                _dialogService.ShowMessage(
                                    "Some changes require a system restart to take effect.",
                                    "Restart Required");
                            }
                        }
                    }
                    else
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to apply setting: {setting.Name}");

                        // Revert the checkbox state
                        setting.IsSelected = !setting.IsSelected;

                        _dialogService.ShowMessage(
                            $"Failed to apply the setting: {setting.Name}. This may require administrator privileges.",
                            "Error");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error applying setting: {ex.Message}", ex);

                // Revert the checkbox state
                setting.IsSelected = !setting.IsSelected;

                _dialogService.ShowMessage(
                    $"An error occurred: {ex.Message}",
                    "Error");
            }
            finally
            {
                _updatingCheckboxes = false;
            }
        }

        // Note: ApplyOptimizationsCommand has been removed as settings are now applied immediately when toggled
        
        // The CreateOptimizationSettingItem method has been removed as it's no longer needed
    }
}
