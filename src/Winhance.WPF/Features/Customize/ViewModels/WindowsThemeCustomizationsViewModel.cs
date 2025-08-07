using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// ViewModel for Windows Theme customizations using clean architecture principles.
    /// Uses composition pattern with ISettingsUICoordinator and follows pure settings system approach.
    /// </summary>
    public partial class WindowsThemeCustomizationsViewModel : ObservableObject, IFeatureViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly IWindowsThemeService _windowsThemeService;
        private readonly ISystemServices _systemServices;
        private readonly ISettingsUICoordinator _uiCoordinator;
        private readonly ITaskProgressService _progressService;
        private readonly ILogService _logService;
        
        /// <summary>
        /// Gets the command to load settings.
        /// </summary>
        public ICommand LoadSettingsCommand { get; }

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
        public string ModuleId => "WindowsTheme";
        public string DisplayName => "Windows Theme";
        public int SettingsCount => Settings?.Count ?? 0;
        public string Category => "Customize";
        public string Description => "Customize Windows theme settings";
        public int SortOrder => 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsThemeCustomizationsViewModel"/> class.
        /// </summary>
        /// <param name="windowsThemeService">The windows theme domain service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="uiCoordinator">The settings UI coordinator.</param>
        /// <param name="systemServices">The system services.</param>
        /// <param name="progressService">The task progress service.</param>
        public WindowsThemeCustomizationsViewModel(
            IWindowsThemeService windowsThemeService,
            IDialogService dialogService,
            ISettingsUICoordinator uiCoordinator,
            ISystemServices systemServices,
            ITaskProgressService progressService,
            ILogService logService)
        {
            _windowsThemeService = windowsThemeService ?? throw new ArgumentNullException(nameof(windowsThemeService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _uiCoordinator = uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            // Initialize commands
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            
            // Set up UI coordinator
            _uiCoordinator.CategoryName = "Windows Theme";
            
            // Subscribe to UI coordinator property changes to forward notifications
            _uiCoordinator.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(ISettingsUICoordinator.HasVisibleSettings))
                {
                    OnPropertyChanged(nameof(HasVisibleSettings));
                }
            };
            
            // Auto-load settings when ViewModel is created
            _ = Task.Run(async () => await LoadSettingsAsync());
        }

        #region Commands and Methods

        /// <summary>
        /// Loads settings using the UI coordinator.
        /// </summary>
        public async Task LoadSettingsAsync()
        {
            _logService.Log(LogLevel.Info, "WindowsThemeCustomizationsViewModel: Starting LoadSettingsAsync");
            
            try
            {
                _progressService.StartTask("Loading theme settings...");
                
                var settings = await GetApplicationSettingsAsync();
                _logService.Log(LogLevel.Info, $"WindowsThemeCustomizationsViewModel: Got {settings.Count()} settings from service");
                
                foreach (var setting in settings)
                {
                    _logService.Log(LogLevel.Info, $"WindowsThemeCustomizationsViewModel: Setting - Id: {setting.Id}, Name: {setting.Name}, ControlType: {setting.ControlType}, IsEnabled: {setting.IsEnabled}");
                }
                
                await _uiCoordinator.LoadSettingsAsync(GetApplicationSettingsAsync);
                
                _logService.Log(LogLevel.Info, $"WindowsThemeCustomizationsViewModel: UI Coordinator has {_uiCoordinator.Settings.Count} settings after load");
                
                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading theme settings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets the application settings that this ViewModel manages.
        /// </summary>
        /// <returns>Collection of application settings for theme customizations.</returns>
        private async Task<IEnumerable<ApplicationSetting>> GetApplicationSettingsAsync()
        {
            // Use the domain service to get settings
            var settings = await _windowsThemeService.GetSettingsAsync();
            return settings;
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

        #endregion
    }
}
