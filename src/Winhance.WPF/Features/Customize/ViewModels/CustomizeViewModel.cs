using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Customize.Models;
using Winhance.WPF.Features.Optimize.ViewModels;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.Common.Messages;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// ViewModel for the Customize view.
    /// </summary>
    public partial class CustomizeViewModel : SearchableViewModel<CustomizationSettingItem>
    {
        // Store a backup of all items for state recovery
        private List<CustomizationSettingItem> _allItemsBackup = new List<CustomizationSettingItem>();
        private bool _isInitialSearchDone = false;

        // Tracks if search has any results
        [ObservableProperty]
        private bool _hasSearchResults = true;
        private readonly ISystemServices _windowsService;
        private readonly IDialogService _dialogService;
        private readonly IThemeManager _themeManager;
        private readonly IConfigurationService _configurationService;
        private readonly IMessengerService _messengerService;

        /// <summary>
        /// Gets the messenger service.
        /// </summary>
        public IMessengerService MessengerService => _messengerService;

        // Flag to prevent cascading checkbox updates
        private bool _updatingCheckboxes = false;
        /// <summary>
        /// Gets or sets a value indicating whether dark mode is enabled.
        /// </summary>
        [ObservableProperty]
        private bool _isDarkModeEnabled;

        /// <summary>
        /// Gets or sets a value indicating whether all settings are selected.
        /// </summary>
        [ObservableProperty]
        private bool _isSelectAllSelected;

        /// <summary>
        /// Gets or sets a value indicating whether settings are being loaded.
        /// </summary>
        [ObservableProperty]
        private bool _isLoading;

        /// <summary>
        /// Gets or sets the status text.
        /// </summary>
        [ObservableProperty]
        private string _statusText = "Customize Your Windows Appearance and Behaviour";
        
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
                    LogInfo($"CustomizeViewModel: SearchText changed to: '{value}'");
                    
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
                        StatusText = "Customize Your Windows Appearance and Behaviour";
                    }
                }
            }
        }
        
        // SaveConfigCommand and ImportConfigCommand removed as part of unified configuration cleanup

        /// <summary>
        /// Gets the collection of customization items.
        /// </summary>
        public ObservableCollection<CustomizationItem> CustomizationItems { get; } = new();

        /// <summary>
        /// Gets or sets the taskbar settings view model.
        /// </summary>
        [ObservableProperty]
        private TaskbarCustomizationsViewModel _taskbarSettings;

        /// <summary>
        /// Gets or sets the start menu settings view model.
        /// </summary>
        [ObservableProperty]
        private StartMenuCustomizationsViewModel _startMenuSettings;

        /// <summary>
        /// Gets or sets the explorer customizations view model.
        /// </summary>
        [ObservableProperty]
        private ExplorerCustomizationsViewModel _explorerSettings;

        /// <summary>
        /// Gets or sets the Windows theme customizations view model.
        /// </summary>
        [ObservableProperty]
        private WindowsThemeCustomizationsViewModel _windowsThemeSettings;

        /// <summary>
        /// Gets a value indicating whether the view model is initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Gets the initialize command.
        /// </summary>
        public AsyncRelayCommand InitializeCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomizeViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="windowsService">The Windows service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="themeManager">The theme manager.</param>
        /// <param name="searchService">The search service.</param>
        /// <param name="configurationService">The configuration service.</param>
        /// <param name="taskbarSettings">The taskbar customizations view model.</param>
        /// <param name="startMenuSettings">The start menu customizations view model.</param>
        /// <param name="explorerSettings">The explorer settings view model.</param>
        /// <param name="windowsThemeSettings">The Windows theme customizations view model.</param>
        /// <param name="messengerService">The messenger service.</param>
        public CustomizeViewModel(
            ITaskProgressService progressService,
            ISystemServices windowsService,
            ILogService logService,
            IDialogService dialogService,
            IThemeManager themeManager,
            ISearchService searchService,
            IConfigurationService configurationService,
            TaskbarCustomizationsViewModel taskbarSettings,
            StartMenuCustomizationsViewModel startMenuSettings,
            ExplorerCustomizationsViewModel explorerSettings,
            WindowsThemeCustomizationsViewModel windowsThemeSettings,
            IMessengerService messengerService)
            : base(progressService, searchService, null)
        {
            _windowsService = windowsService ?? throw new ArgumentNullException(nameof(windowsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));

            TaskbarSettings = taskbarSettings ?? throw new ArgumentNullException(nameof(taskbarSettings));
            StartMenuSettings = startMenuSettings ?? throw new ArgumentNullException(nameof(startMenuSettings));
            ExplorerSettings = explorerSettings ?? throw new ArgumentNullException(nameof(explorerSettings));
            WindowsThemeSettings = windowsThemeSettings ?? throw new ArgumentNullException(nameof(windowsThemeSettings));

            InitializeCustomizationItems();
            
            // Sync with WindowsThemeSettings
            IsDarkModeEnabled = WindowsThemeSettings.IsDarkModeEnabled;
            
            // Subscribe to WindowsThemeSettings property changes
            WindowsThemeSettings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WindowsThemeSettings.IsDarkModeEnabled))
                {
                    // Sync dark mode state with WindowsThemeSettings
                    IsDarkModeEnabled = WindowsThemeSettings.IsDarkModeEnabled;
                }
            };

            // Create initialize command
            InitializeCommand = new AsyncRelayCommand(InitializeAsync);
            
            // SaveConfigCommand and ImportConfigCommand removed as part of unified configuration cleanup
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomizeViewModel"/> class for design-time use.
        /// </summary>
        public CustomizeViewModel()
            : base(
                new Winhance.Infrastructure.Features.Common.Services.TaskProgressService(new Core.Features.Common.Services.LogService()),
                new Winhance.Infrastructure.Features.Common.Services.SearchService())
        {
            // Default constructor for design-time use
            CustomizationItems = new ObservableCollection<CustomizationItem>();
            InitializeCustomizationItems();
            IsDarkModeEnabled = true; // Default to dark mode for design-time
        }

        /// <summary>
        /// Called when the view model is navigated to.
        /// </summary>
        /// <param name="parameter">The navigation parameter.</param>
        public override void OnNavigatedTo(object parameter)
        {
            LogInfo("CustomizeViewModel.OnNavigatedTo called");

            // If not already initialized, initialize now
            if (!IsInitialized)
            {
                InitializeCommand.Execute(null);
            }
        }

        /// <summary>
        /// Initializes the view model asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (IsInitialized)
                return;

            try
            {
                IsLoading = true;
                LogInfo("Initializing CustomizeViewModel");

                // Report progress
                ProgressService.UpdateProgress(0, "Loading customization settings...");

                // Load settings for each category
                LogInfo("CustomizeViewModel.InitializeAsync: Loading Taskbar settings");
                ProgressService.UpdateProgress(10, "Loading taskbar settings...");
                await TaskbarSettings.LoadSettingsAsync();

                LogInfo("CustomizeViewModel.InitializeAsync: Loading Start Menu settings");
                ProgressService.UpdateProgress(30, "Loading Start Menu settings...");
                await StartMenuSettings.LoadSettingsAsync();

                LogInfo("CustomizeViewModel.InitializeAsync: Loading Explorer settings");
                ProgressService.UpdateProgress(50, "Loading Explorer settings...");
                await ExplorerSettings.LoadSettingsAsync();

                // Load Windows Theme settings if available
                if (WindowsThemeSettings != null)
                {
                    LogInfo("CustomizeViewModel.InitializeAsync: Loading Windows Theme settings");
                    ProgressService.UpdateProgress(70, "Loading Windows Theme settings...");
                    await WindowsThemeSettings.LoadSettingsAsync();
                }

                // Progress is now complete
                ProgressService.UpdateProgress(90, "Finalizing...");

                LogInfo("CustomizeViewModel.InitializeAsync: All settings loaded");
                ProgressService.UpdateProgress(100, "Initialization complete");

                // Set up property change handlers
                SetupPropertyChangeHandlers();

                // Update selection states
                UpdateSelectAllState();

                // Load all settings for search functionality
                await LoadItemsAsync();
                
                // Ensure Windows Theme settings are properly loaded for search
                if (WindowsThemeSettings != null && WindowsThemeSettings.Settings.Count == 0)
                {
                    LogInfo("CustomizeViewModel: Reloading Windows Theme settings for search");
                    await WindowsThemeSettings.LoadSettingsAsync();
                }
                
                // Mark as initialized
                IsInitialized = true;

                LogInfo("CustomizeViewModel initialized successfully");
            }
            catch (Exception ex)
            {
                LogError($"Error initializing customization settings: {ex.Message}");
                throw; // Rethrow to ensure the caller knows initialization failed
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Initializes the customization items.
        /// </summary>
        private void InitializeCustomizationItems()
        {
            CustomizationItems.Clear();

            // Taskbar customization
            var taskbarItem = new CustomizationItem
            {
                Name = "Taskbar",
                Description = "Customize taskbar appearance and behavior",
                IsSelected = false
            };
            CustomizationItems.Add(taskbarItem);

            // Start Menu customization
            var startMenuItem = new CustomizationItem
            {
                Name = "Start Menu",
                Description = "Modify Start Menu layout and settings",
                IsSelected = false
            };
            CustomizationItems.Add(startMenuItem);

            // Explorer customization
            var explorerItem = new CustomizationItem
            {
                Name = "Explorer",
                Description = "Adjust File Explorer settings and appearance",
                IsSelected = false
            };
            CustomizationItems.Add(explorerItem);

            // Windows Theme customization
            var windowsThemeItem = new CustomizationItem
            {
                Name = "Windows Theme",
                Description = "Customize Windows appearance themes",
                IsSelected = false
            };
            CustomizationItems.Add(windowsThemeItem);
        }

        /// <summary>
        /// Sets up property change handlers.
        /// </summary>
        private void SetupPropertyChangeHandlers()
        {
            // Set up property change handlers for all settings
            foreach (var item in CustomizationItems)
            {
                // Add property changed handler for the category itself
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(CustomizationItem.IsSelected) && !_updatingCheckboxes)
                    {
                        _updatingCheckboxes = true;
                        try
                        {
                            // Update all settings in this category
                            UpdateCategorySettings(item);

                            // Update the global state
                            UpdateSelectAllState();
                        }
                        finally
                        {
                            _updatingCheckboxes = false;
                        }
                    }
                };
            }
        }

        /// <summary>
        /// Updates the settings for a category.
        /// </summary>
        /// <param name="category">The category.</param>
        private void UpdateCategorySettings(CustomizationItem category)
        {
            if (category == null) return;

            // Get the IsSelected property from ISettingItem interface
            PropertyInfo isSelectedProp = typeof(ISettingItem).GetProperty("IsSelected");
            if (isSelectedProp == null) return;

            switch (category.Name)
            {
                case "Taskbar":
                    foreach (var setting in TaskbarSettings.Settings)
                    {
                        isSelectedProp.SetValue(setting, category.IsSelected);
                    }
                    break;
                case "Start Menu":
                    foreach (var setting in StartMenuSettings.Settings)
                    {
                        isSelectedProp.SetValue(setting, category.IsSelected);
                    }
                    break;
                case "Explorer":
                    foreach (var setting in ExplorerSettings.Settings)
                    {
                        isSelectedProp.SetValue(setting, category.IsSelected);
                    }
                    break;
                case "Windows Theme":
                    if (WindowsThemeSettings != null && WindowsThemeSettings.Settings.Count > 0)
                    {
                        foreach (var setting in WindowsThemeSettings.Settings)
                        {
                            isSelectedProp.SetValue(setting, category.IsSelected);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Updates the category selection state.
        /// </summary>
        /// <param name="categoryName">The category name.</param>
        private void UpdateCategorySelectionState(string categoryName)
        {
            // Skip if we're already in an update operation
            if (_updatingCheckboxes)
                return;

            _updatingCheckboxes = true;

            try
            {
                var category = CustomizationItems.FirstOrDefault(c => c.Name == categoryName);
                if (category == null) return;

                bool allSelected = false;

                // Get the IsSelected property from ISettingItem interface
                PropertyInfo isSelectedProp = typeof(ISettingItem).GetProperty("IsSelected");
                if (isSelectedProp == null) return;

                switch (categoryName)
                {
                    case "Taskbar":
                        allSelected = TaskbarSettings.Settings.Count > 0 &&
                                     TaskbarSettings.Settings.All(s => (bool)isSelectedProp.GetValue(s));
                        break;
                    case "Start Menu":
                        allSelected = StartMenuSettings.Settings.Count > 0 &&
                                     StartMenuSettings.Settings.All(s => (bool)isSelectedProp.GetValue(s));
                        break;
                    case "Explorer":
                        allSelected = ExplorerSettings.Settings.Count > 0 &&
                                     ExplorerSettings.Settings.All(s => (bool)isSelectedProp.GetValue(s));
                        break;
                    case "Windows Theme":
                        allSelected = WindowsThemeSettings != null && 
                                     WindowsThemeSettings.Settings.Count > 0 &&
                                     WindowsThemeSettings.Settings.All(s => (bool)isSelectedProp.GetValue(s));
                        break;
                    // Removed Notifications and Sound cases
                }

                category.IsSelected = allSelected;

                // Update global state
                UpdateSelectAllState();
            }
            finally
            {
                _updatingCheckboxes = false;
            }
        }

        /// <summary>
        /// Updates the select all state.
        /// </summary>
        private void UpdateSelectAllState()
        {
            // Skip if we're already in an update operation
            if (_updatingCheckboxes)
                return;

            _updatingCheckboxes = true;

            try
            {
                // Skip if no customization items
                if (CustomizationItems.Count == 0)
                    return;

                // Update global "Select All" checkbox based on all categories
                IsSelectAllSelected = CustomizationItems.All(c => c.IsSelected);
            }
            finally
            {
                _updatingCheckboxes = false;
            }
        }


        /// <summary>
        /// Refreshes the Windows GUI.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task RefreshWindowsGUI()
        {
            try
            {
                // Use the Windows service to refresh the GUI without restarting explorer
                await _windowsService.RefreshWindowsGUI(false);

                LogInfo("Windows GUI refresh completed");
            }
            catch (Exception ex)
            {
                LogError($"Error refreshing Windows GUI: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggles the select all state.
        /// </summary>
        [RelayCommand]
        private void ToggleSelectAll()
        {
            bool newState = !IsSelectAllSelected;
            IsSelectAllSelected = newState;

            _updatingCheckboxes = true;

            try
            {
                foreach (var item in CustomizationItems)
                {
                    item.IsSelected = newState;
                    UpdateCategorySettings(item);
                }
            }
            finally
            {
                _updatingCheckboxes = false;
            }
        }
        // Note: ApplyCustomizationsCommand has been removed as settings are now applied immediately when toggled
        // Note: RestoreDefaultsCommand has been removed as settings are now applied immediately when toggled

        /// <summary>
        /// Executes a customization action.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        [RelayCommand]
        private async void ExecuteAction(CustomizationAction? action)
        {
            if (action == null) return;

            // Show confirmation dialog if needed
            if (!string.IsNullOrEmpty(action.ConfirmationMessage))
            {
                var result = await _dialogService.ShowConfirmationAsync(
                    "Confirm Action",
                    action.ConfirmationMessage);

                if (!result) return; // User canceled
            }

            try
            {
                // Perform backup if supported and requested
                if (action.SupportsBackup)
                {
                    var backupResult = await _dialogService.ShowConfirmationAsync(
                        "Backup Current State",
                        $"Would you like to backup your current {action.Name} state before proceeding?");

                    if (backupResult && action.BackupAction != null)
                    {
                        await action.BackupAction();
                    }
                }

                // Execute the registry action if present
                if (action.RegistrySetting != null)
                {
                    string hiveString = action.RegistrySetting.Hive.ToString();
                    if (hiveString == "LocalMachine") hiveString = "HKLM";
                    else if (hiveString == "CurrentUser") hiveString = "HKCU";
                    else if (hiveString == "ClassesRoot") hiveString = "HKCR";
                    else if (hiveString == "Users") hiveString = "HKU";
                    else if (hiveString == "CurrentConfig") hiveString = "HKCC";

                    string fullPath = $"{hiveString}\\{action.RegistrySetting.SubKey}";
                    // Use EnabledValue if available, otherwise fall back to RecommendedValue for backward compatibility
                    object valueToSet = action.RegistrySetting.EnabledValue ?? action.RegistrySetting.RecommendedValue;
                    _windowsService.RegistryService.SetValue(
                        fullPath,
                        action.RegistrySetting.Name,
                        valueToSet,
                        action.RegistrySetting.ValueType);
                }

                // Execute custom action if present
                if (action.CustomAction != null)
                {
                    await action.CustomAction();
                }

                // Refresh UI if needed
                bool needsGuiRefresh = action.GroupName == "Taskbar" || action.GroupName == "Start Menu";
                if (needsGuiRefresh)
                {
                    await RefreshWindowsGUI();
                }

                // Show success message
                await _dialogService.ShowInformationAsync("Success", $"{action.Name} completed successfully!");

                LogInfo($"Action '{action.Name}' executed successfully");
            }
            catch (Exception ex)
            {
                LogError($"Error executing action '{action.Name}': {ex.Message}");
                await _dialogService.ShowErrorAsync("Error",
                    $"An error occurred while executing {action.Name}. {ex.Message}");
            }
        }

        /// <summary>
        /// Toggles a category.
        /// </summary>
        /// <param name="category">The category to toggle.</param>
        [RelayCommand]
        private void ToggleCategory(CustomizationItem category)
        {
            if (category == null) return;

            // Skip if we're already in an update operation
            if (_updatingCheckboxes)
                return;

            _updatingCheckboxes = true;

            try
            {
                bool newState = !category.IsSelected;
                category.IsSelected = newState;

                // Apply the state to all settings in this category
                UpdateCategorySettings(category);

                // Check if all categories are selected
                UpdateSelectAllState();
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
                StatusText = "Loading customization settings...";

                // Clear the items collection
                Items.Clear();

                // Collect all settings from the various view models
                var allSettings = new List<CustomizationSettingItem>();

                // Add settings from each category
                if (TaskbarSettings != null && TaskbarSettings.Settings != null)
                {
                    foreach (var setting in TaskbarSettings.Settings)
                    {
                        if (setting is CustomizationSettingItem customizationSetting)
                        {
                            allSettings.Add(customizationSetting);
                        }
                    }
                }

                if (StartMenuSettings != null && StartMenuSettings.Settings != null)
                {
                    foreach (var setting in StartMenuSettings.Settings)
                    {
                        if (setting is CustomizationSettingItem customizationSetting)
                        {
                            allSettings.Add(customizationSetting);
                        }
                    }
                }

                if (ExplorerSettings != null && ExplorerSettings.Settings != null)
                {
                    foreach (var setting in ExplorerSettings.Settings)
                    {
                        if (setting is CustomizationSettingItem customizationSetting)
                        {
                            allSettings.Add(customizationSetting);
                        }
                    }
                }

                if (WindowsThemeSettings != null && WindowsThemeSettings.Settings != null)
                {
                    foreach (var setting in WindowsThemeSettings.Settings)
                    {
                        if (setting is CustomizationSettingItem customizationSetting)
                        {
                            allSettings.Add(customizationSetting);
                        }
                    }
                }

                // Add all settings to the Items collection
                foreach (var setting in allSettings)
                {
                    Items.Add(setting);
                }

                // Create a backup of all items for state recovery
                _allItemsBackup = new List<CustomizationSettingItem>(Items);

                // Only update StatusText if it's currently showing a loading message
                if (StatusText.Contains("Loading"))
                {
                    StatusText = "Customize Your Windows Appearance and Behaviour";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading customization settings: {ex.Message}";
                LogError($"Error loading customization settings: {ex.Message}", ex);
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
            // This method is not applicable for customization settings
            // but we need to implement it to satisfy the interface
            await Task.CompletedTask;
        }

        /// <summary>
        /// Restores all items visibility to their original state.
        /// </summary>
        private void RestoreAllItemsVisibility()
        {
            // Make all settings visible in each category
            if (TaskbarSettings?.Settings != null)
            {
                foreach (var setting in TaskbarSettings.Settings)
                {
                    setting.IsVisible = true;
                }
                TaskbarSettings.HasVisibleSettings = TaskbarSettings.Settings.Count > 0;
                LogInfo($"CustomizeViewModel: Restored visibility for {TaskbarSettings.Settings.Count} Taskbar settings");
            }
            
            if (StartMenuSettings?.Settings != null)
            {
                foreach (var setting in StartMenuSettings.Settings)
                {
                    setting.IsVisible = true;
                }
                StartMenuSettings.HasVisibleSettings = StartMenuSettings.Settings.Count > 0;
                LogInfo($"CustomizeViewModel: Restored visibility for {StartMenuSettings.Settings.Count} Start Menu settings");
            }
            
            if (ExplorerSettings?.Settings != null)
            {
                foreach (var setting in ExplorerSettings.Settings)
                {
                    setting.IsVisible = true;
                }
                ExplorerSettings.HasVisibleSettings = ExplorerSettings.Settings.Count > 0;
                LogInfo($"CustomizeViewModel: Restored visibility for {ExplorerSettings.Settings.Count} Explorer settings");
            }
            
            if (WindowsThemeSettings?.Settings != null)
            {
                foreach (var setting in WindowsThemeSettings.Settings)
                {
                    setting.IsVisible = true;
                }
                WindowsThemeSettings.HasVisibleSettings = WindowsThemeSettings.Settings.Count > 0;
                LogInfo($"CustomizeViewModel: Restored visibility for {WindowsThemeSettings.Settings.Count} Windows Theme settings, HasVisibleSettings={WindowsThemeSettings.HasVisibleSettings}");
            }
            
            // Make all customization items visible
            foreach (var item in CustomizationItems)
            {
                item.IsVisible = true;
                LogInfo($"CustomizeViewModel: Restored visibility for CustomizationItem '{item.Name}'");
            }
            
            // Always ensure HasSearchResults is true when restoring visibility
            HasSearchResults = true;
            
            // Send a message to notify the view to reset section expansion states
            _messengerService.Send(new ResetExpansionStateMessage());
            
            LogInfo("CustomizeViewModel: RestoreAllItemsVisibility has reset all UI elements to visible state");
        }
        
        /// <summary>
        /// Applies the current search text to filter items.
        /// </summary>
        protected override void ApplySearch()
        {
            LogInfo($"CustomizeViewModel: ApplySearch called with SearchText: '{SearchText}'");
            
            // If we have no items yet, there's nothing to filter
            if (Items == null)
                return;
                
            // If this is our first time running a search and the backup isn't created yet, create it
            if (!_isInitialSearchDone && Items.Count > 0)
            {
                _allItemsBackup = new List<CustomizationSettingItem>(Items);
                _isInitialSearchDone = true;
                LogInfo($"CustomizeViewModel: Created backup of all items ({_allItemsBackup.Count} items)");
            }

            // If search is empty, restore all items visibility and the original items collection
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                LogInfo("CustomizeViewModel: Empty search, restoring all items visibility");
                
                // Restore visibility of UI elements first
                RestoreAllItemsVisibility();
                
                // Use the backup to restore all items
                if (_allItemsBackup.Count > 0)
                {
                    LogInfo($"CustomizeViewModel: Restoring {_allItemsBackup.Count} items from backup");
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
                    LogInfo("CustomizeViewModel: No backup available and Items is empty, attempting to reload");
                    _ = LoadItemsAsync();
                }
                
                // Always set HasSearchResults to true when search is cleared - critical to fix the bug
                HasSearchResults = true;
                
                // Make sure all CustomizationItems are visible
                foreach (var item in CustomizationItems)
                {
                    item.IsVisible = true;
                    LogInfo($"CustomizeViewModel: Setting CustomizationItem '{item.Name}' visibility to true");
                }
                
                // Ensure all sub-view models have HasVisibleSettings set to true
                if (TaskbarSettings != null)
                {
                    TaskbarSettings.HasVisibleSettings = TaskbarSettings.Settings.Count > 0;
                    LogInfo($"CustomizeViewModel: Setting TaskbarSettings.HasVisibleSettings to {TaskbarSettings.HasVisibleSettings}");
                }
                
                if (StartMenuSettings != null)
                {
                    StartMenuSettings.HasVisibleSettings = StartMenuSettings.Settings.Count > 0;
                    LogInfo($"CustomizeViewModel: Setting StartMenuSettings.HasVisibleSettings to {StartMenuSettings.HasVisibleSettings}");
                }
                
                if (ExplorerSettings != null)
                {
                    ExplorerSettings.HasVisibleSettings = ExplorerSettings.Settings.Count > 0;
                    LogInfo($"CustomizeViewModel: Setting ExplorerSettings.HasVisibleSettings to {ExplorerSettings.HasVisibleSettings}");
                }
                
                if (WindowsThemeSettings != null)
                {
                    WindowsThemeSettings.HasVisibleSettings = WindowsThemeSettings.Settings.Count > 0;
                    LogInfo($"CustomizeViewModel: Setting WindowsThemeSettings.HasVisibleSettings to {WindowsThemeSettings.HasVisibleSettings}");
                }
                
                // Update status text
                StatusText = "Customize Your Windows Appearance and Behaviour";
                LogInfo($"CustomizeViewModel: Restored items, count={Items.Count}");
                return;
            }

            // We're doing an active search, use the backup for filtering if available
            var itemsToFilter = _allItemsBackup.Count > 0 
                ? new ObservableCollection<CustomizationSettingItem>(_allItemsBackup) 
                : Items;
            
            // Normalize and clean the search text
            string normalizedSearchText = SearchText.Trim().ToLowerInvariant();
            LogInfo($"CustomizeViewModel: Normalized search text: '{normalizedSearchText}'");
            
            // Handle edge case: if search is all whitespace after trimming, treat it as empty
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
                StatusText = "Customize Your Windows Appearance and Behaviour";
                return;
            }
            
            // Filter items based on search text - ensure we pass normalized text
            var filteredItems = FilterItems(itemsToFilter)
                .Where(item => 
                    item.Name?.IndexOf(normalizedSearchText, StringComparison.OrdinalIgnoreCase) >= 0 || 
                    item.Description?.IndexOf(normalizedSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            
            // Log the number of filtered items
            int filteredCount = filteredItems.Count;
            LogInfo($"CustomizeViewModel: Found {filteredCount} matching items");
            
            // Update HasSearchResults based on filtered count
            HasSearchResults = filteredCount > 0;
            LogInfo($"CustomizeViewModel: Setting HasSearchResults to {HasSearchResults} based on filtered count {filteredCount}");
            
            // Create a HashSet of filtered item IDs for efficient lookup
            var filteredItemIds = new HashSet<string>(filteredItems.Select(item => item.Id));

            // Update each sub-view model's Settings collection
            UpdateSubViewSettings(TaskbarSettings, filteredItemIds);
            UpdateSubViewSettings(StartMenuSettings, filteredItemIds);
            UpdateSubViewSettings(ExplorerSettings, filteredItemIds);
            UpdateSubViewSettings(WindowsThemeSettings, filteredItemIds);

            // Count the actual visible items after filtering
            int visibleItemsCount = 0;
            
            // Count visible items in each category
            if (TaskbarSettings?.Settings != null)
                visibleItemsCount += TaskbarSettings.Settings.Count(s => s is CustomizationSettingItem csItem && csItem.IsVisible);
                
            if (StartMenuSettings?.Settings != null)
                visibleItemsCount += StartMenuSettings.Settings.Count(s => s is CustomizationSettingItem csItem && csItem.IsVisible);
                
            if (ExplorerSettings?.Settings != null)
                visibleItemsCount += ExplorerSettings.Settings.Count(s => s is CustomizationSettingItem csItem && csItem.IsVisible);
                
            if (WindowsThemeSettings?.Settings != null)
                visibleItemsCount += WindowsThemeSettings.Settings.Count(s => s is CustomizationSettingItem csItem && csItem.IsVisible);

            // Update the Items collection to match visible items
            Items.Clear();
            foreach (var setting in filteredItems)
            {
                if (setting.IsVisible)
                {
                    Items.Add(setting);
                }
            }

            // Update status text with correct count
            if (IsSearchActive)
            {
                StatusText = $"Found {visibleItemsCount} settings matching '{SearchText}'";
                LogInfo($"CustomizeViewModel: StatusText updated to: '{StatusText}' with {visibleItemsCount} visible items");
            }
            else
            {
                StatusText = $"Showing all {Items.Count} customization settings";
                LogInfo($"CustomizeViewModel: StatusText updated to: '{StatusText}'");
            }
        }

        /// <summary>
        /// Updates a sub-view model's Settings collection based on filtered items.
        /// </summary>
        /// <param name="viewModel">The sub-view model to update.</param>
        /// <param name="filteredItemIds">HashSet of IDs of items that match the search criteria.</param>
        private void UpdateSubViewSettings(BaseCustomizationsViewModel viewModel, HashSet<string> filteredItemIds)
        {
            if (viewModel == null || viewModel.Settings == null)
                return;

            // If not searching, show all settings
            if (!IsSearchActive)
            {
                foreach (var setting in viewModel.Settings)
                {
                    setting.IsVisible = true;
                }
                
                // Update the view model's visibility
                viewModel.HasVisibleSettings = viewModel.Settings.Count > 0;
                
                // Update the corresponding CustomizationItem
                var item = CustomizationItems.FirstOrDefault(i => i.Name == viewModel.CategoryName);
                if (item != null)
                {
                    item.IsVisible = true;
                }
                
                LogInfo($"CustomizeViewModel: Set all settings visible in {viewModel.CategoryName}");
                return;
            }

            // When searching, only show settings that match the search criteria
            bool hasVisibleSettings = false;
            int visibleCount = 0;
            
            foreach (var setting in viewModel.Settings)
            {
                // Check if this setting is in the filtered items
                setting.IsVisible = filteredItemIds.Contains(setting.Id);
                
                if (setting.IsVisible)
                {
                    hasVisibleSettings = true;
                    visibleCount++;
                }
            }
            
            // Update the view model's visibility
            viewModel.HasVisibleSettings = hasVisibleSettings;
            
            // Update the corresponding CustomizationItem - this is critical for proper UI behavior
            var categoryItem = CustomizationItems.FirstOrDefault(i => i.Name == viewModel.CategoryName);
            if (categoryItem != null)
            {
                categoryItem.IsVisible = hasVisibleSettings;
                LogInfo($"CustomizeViewModel: Setting CustomizationItem '{categoryItem.Name}' visibility to {hasVisibleSettings}");
            }
            
            LogInfo($"CustomizeViewModel: {viewModel.CategoryName} has {visibleCount} visible settings, HasVisibleSettings={hasVisibleSettings}");
        }

        // SaveConfig and ImportConfig methods removed as part of unified configuration cleanup
    }
}
