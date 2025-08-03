using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.Resources.Theme;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// Clean Architecture implementation of the ViewModel for the Customize view.
    /// Aggregates all customization settings from different categories.
    /// </summary>
    public partial class CustomizeViewModel : BaseSettingsViewModel
    {
        #region Private Fields

        private readonly IDialogService _dialogService;
        private readonly IThemeManager _themeManager;
        private readonly IMessengerService _messengerService;

        #endregion

        #region Individual ViewModels

        /// <summary>
        /// Gets the Windows Theme customizations ViewModel.
        /// </summary>
        public WindowsThemeCustomizationsViewModel WindowsThemeSettings { get; }

        /// <summary>
        /// Gets the Start Menu customizations ViewModel.
        /// </summary>
        public StartMenuCustomizationsViewModel StartMenuSettings { get; }

        /// <summary>
        /// Gets the Taskbar customizations ViewModel.
        /// </summary>
        public TaskbarCustomizationsViewModel TaskbarSettings { get; }

        /// <summary>
        /// Gets the Explorer customizations ViewModel.
        /// </summary>
        public ExplorerCustomizationsViewModel ExplorerSettings { get; }

        #endregion

        #region Observable Properties

        /// <summary>
        /// Whether search has any results.
        /// </summary>
        [ObservableProperty]
        private bool _hasSearchResults = true;

        /// <summary>
        /// Status text for the view.
        /// </summary>
        [ObservableProperty]
        private string _statusText = string.Empty;

        /// <summary>
        /// Whether the view model is initialized.
        /// </summary>
        [ObservableProperty]
        private bool _isInitialized;

        #endregion

        #region Constructor

        public CustomizeViewModel(
            IApplicationSettingsService settingsService,
            ITaskProgressService progressService,
            ILogService logService,
            IDialogService dialogService,
            IThemeManager themeManager,
            IMessengerService messengerService,
            WindowsThemeCustomizationsViewModel windowsThemeSettings,
            StartMenuCustomizationsViewModel startMenuSettings,
            TaskbarCustomizationsViewModel taskbarSettings,
            ExplorerCustomizationsViewModel explorerSettings)
            : base(settingsService, progressService, logService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));

            // Initialize individual ViewModels
            WindowsThemeSettings = windowsThemeSettings ?? throw new ArgumentNullException(nameof(windowsThemeSettings));
            StartMenuSettings = startMenuSettings ?? throw new ArgumentNullException(nameof(startMenuSettings));
            TaskbarSettings = taskbarSettings ?? throw new ArgumentNullException(nameof(taskbarSettings));
            ExplorerSettings = explorerSettings ?? throw new ArgumentNullException(nameof(explorerSettings));

            CategoryName = "Customization";

            // Subscribe to search text changes
            PropertyChanged += OnPropertyChanged;

            // Initialize the command
            InitializeCommand = new AsyncRelayCommand(InitializeAsync);
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the messenger service for communication with other ViewModels.
        /// </summary>
        public IMessengerService MessengerService => _messengerService;

        /// <summary>
        /// Gets the initialize command for backward compatibility.
        /// </summary>
        public AsyncRelayCommand InitializeCommand { get; }

        #endregion

        #region Overrides

        /// <summary>
        /// Gets all customization settings from different categories.
        /// </summary>
        /// <returns>Collection of all customization settings.</returns>
        protected override async Task<IEnumerable<ApplicationSetting>> GetApplicationSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading all customization settings...");

                // Load settings from all customization categories
                var startMenuTask = _settingsService.GetStartMenuSettingsAsync();
                var taskbarTask = _settingsService.GetTaskbarSettingsAsync();
                var explorerTask = _settingsService.GetExplorerSettingsAsync();
                var themeTask = _settingsService.GetThemeSettingsAsync();

                await Task.WhenAll(startMenuTask, taskbarTask, explorerTask, themeTask);

                var allSettings = new List<ApplicationSetting>();
                allSettings.AddRange(await startMenuTask);
                allSettings.AddRange(await taskbarTask);
                allSettings.AddRange(await explorerTask);
                allSettings.AddRange(await themeTask);

                _logService.Log(LogLevel.Info, $"Loaded {allSettings.Count} customization settings");
                return allSettings;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading customization settings: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the view model and loads all settings.
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                if (IsInitialized) return;

                _logService.Log(LogLevel.Info, "Initializing CustomizeViewModel...");
                
                // Initialize individual ViewModels
                await WindowsThemeSettings.LoadSettingsAsync();
                await StartMenuSettings.LoadSettingsAsync();
                await TaskbarSettings.LoadSettingsAsync();
                await ExplorerSettings.LoadSettingsAsync();
                
                // Load aggregated settings for search/filtering
                await LoadSettingsAsync();
                
                IsInitialized = true;
                UpdateStatusText();
                
                _logService.Log(LogLevel.Info, "CustomizeViewModel initialized successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error initializing CustomizeViewModel: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles property changes, particularly for search functionality.
        /// </summary>
        private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SearchText))
            {
                ApplySearch();
            }
        }

        /// <summary>
        /// Applies search filtering to the settings.
        /// </summary>
        private void ApplySearch()
        {
            if (!IsInitialized || Settings == null)
            {
                return;
            }

            try
            {
                // Filter settings based on search text
                var hasSearchText = !string.IsNullOrWhiteSpace(SearchText);
                var visibleCount = 0;

                foreach (var setting in Settings)
                {
                    var isVisible = !hasSearchText || setting.MatchesSearch(SearchText);
                    setting.IsVisible = isVisible;
                    
                    if (isVisible)
                    {
                        visibleCount++;
                    }
                }

                // Update group visibility
                foreach (var group in SettingGroups)
                {
                    group.UpdateVisibility();
                }

                // Update search results status
                HasSearchResults = visibleCount > 0;
                UpdateStatusText();

                _logService.Log(LogLevel.Debug, $"Search applied: {visibleCount} visible settings");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying search: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the status text based on current state.
        /// </summary>
        private void UpdateStatusText()
        {
            try
            {
                if (!IsInitialized)
                {
                    StatusText = "Loading...";
                    return;
                }

                var visibleCount = Settings?.Count(s => s.IsVisible) ?? 0;
                var totalCount = Settings?.Count ?? 0;

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    StatusText = $"Found {visibleCount} settings matching '{SearchText}'";
                }
                else
                {
                    StatusText = $"Showing all {totalCount} customization settings";
                }

                _logService.Log(LogLevel.Debug, $"Status text updated: {StatusText}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error updating status text: {ex.Message}");
                StatusText = "Error updating status";
            }
        }

        #endregion


    }
}
