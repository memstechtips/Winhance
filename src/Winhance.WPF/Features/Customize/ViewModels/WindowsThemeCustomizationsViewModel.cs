using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// ViewModel for Windows Theme customizations using clean architecture principles.
    /// </summary>
    public partial class WindowsThemeCustomizationsViewModel : BaseSettingsViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly IThemeService _themeService;
        private readonly ISystemServices _systemServices;
        
        // Flags to prevent triggering dark mode action during property change
        private bool _isHandlingDarkModeChange = false;
        
        /// <summary>
        /// Gets or sets a value indicating whether dark mode is enabled.
        /// </summary>
        [ObservableProperty]
        private bool _isDarkModeEnabled;
        
        /// <summary>
        /// Gets or sets a value indicating whether to change the wallpaper when changing the theme.
        /// </summary>
        [ObservableProperty]
        private bool _changeWallpaper;

        /// <summary>
        /// Gets a value indicating whether theme options are available.
        /// </summary>
        public bool HasThemeOptions => true;

        /// <summary>
        /// Gets the available theme options.
        /// </summary>
        public List<string> ThemeOptions => new List<string> { "Light Mode", "Dark Mode" };

        /// <summary>
        /// Gets or sets the selected theme.
        /// </summary>
        public string SelectedTheme
        {
            get => IsDarkModeEnabled ? "Dark Mode" : "Light Mode";
            set
            {
                if (value == "Dark Mode" && !IsDarkModeEnabled)
                {
                    IsDarkModeEnabled = true;
                }
                else if (value == "Light Mode" && IsDarkModeEnabled)
                {
                    IsDarkModeEnabled = false;
                }
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the command to apply the theme.
        /// </summary>
        public ICommand ApplyThemeCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsThemeCustomizationsViewModel"/> class.
        /// </summary>
        /// <param name="settingsService">The application settings service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="themeService">The theme service.</param>
        /// <param name="systemServices">The system services.</param>
        /// <param name="progressService">The task progress service.</param>
        public WindowsThemeCustomizationsViewModel(
            IApplicationSettingsService settingsService,
            ILogService logService,
            IDialogService dialogService,
            IThemeService themeService,
            ISystemServices systemServices,
            ITaskProgressService progressService)
            : base(settingsService, progressService, logService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            
            CategoryName = "Windows Theme";
            
            // Initialize commands
            ApplyThemeCommand = new AsyncRelayCommand<bool>(ApplyThemeWithConfirmationAsync);
            
            // Subscribe to property changes
            this.PropertyChanged += (s, e) => 
            {
                if (e.PropertyName == nameof(IsDarkModeEnabled) && !_isHandlingDarkModeChange)
                {
                    HandleDarkModePropertyChange();
                }
            };
        }

        /// <summary>
        /// Handles changes to the IsDarkModeEnabled property.
        /// </summary>
        private void HandleDarkModePropertyChange()
        {
            try
            {
                _isHandlingDarkModeChange = true;
                
                // Update the theme selector setting in the UI
                var themeSetting = Settings.FirstOrDefault(s => s.Id == "ThemeSelector");
                if (themeSetting != null)
                {
                    themeSetting.IsSelected = IsDarkModeEnabled;
                    themeSetting.SelectedValue = SelectedTheme;
                }
                
                _logService.Log(LogLevel.Info, $"Theme mode changed to {SelectedTheme}");
            }
            finally
            {
                _isHandlingDarkModeChange = false;
            }
        }

        /// <summary>
        /// Applies the theme with confirmation dialog.
        /// </summary>
        /// <param name="changeWallpaper">Whether to change the wallpaper.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ApplyThemeWithConfirmationAsync(bool changeWallpaper)
        {
            try
            {
                var result = await _dialogService.ShowConfirmationAsync(
                    "Apply Theme",
                    $"Are you sure you want to apply {SelectedTheme}?{(changeWallpaper ? " This will also change your wallpaper." : "")}");
                
                if (result)
                {
                    await ApplyThemeAsync(changeWallpaper);
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error showing theme confirmation dialog: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies the dark mode setting.
        /// </summary>
        /// <param name="changeWallpaper">Whether to change the wallpaper.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ApplyThemeAsync(bool changeWallpaper)
        {
            try
            {
                _progressService.StartTask($"Applying {SelectedTheme}...");
                
                // Use the theme service to apply the theme
                await _themeService.ApplyThemeAsync(IsDarkModeEnabled, changeWallpaper);
                
                // Refresh settings after theme change
                await RefreshAllSettingsAsync();
                
                _progressService.CompleteTask();
                await _dialogService.ShowInformationAsync("Theme Applied", $"{SelectedTheme} has been applied successfully.");
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error applying theme: {ex.Message}");
                await _dialogService.ShowErrorAsync("Theme Error", $"Failed to apply {SelectedTheme}: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                _progressService.StartTask("Loading theme settings...");
                
                // Clear existing collections
                Settings.Clear();
                
                // Load settings from the service
                var themeSettings = await _settingsService.GetWindowsThemeSettingsAsync();
                
                foreach (var setting in themeSettings)
                {
                    // Create UI item from the setting
                    var uiItem = new SettingUIItem
                    {
                        Id = setting.Id,
                        Name = setting.Name,
                        Description = setting.Description,
                        GroupName = setting.GroupName,
                        IsEnabled = setting.IsEnabled,
                        ControlType = setting.ControlType,
                        IsSelected = false,
                        IsVisible = true
                    };
                    
                    // Add options for ComboBox settings
                    if (setting.ControlType == ControlType.ComboBox)
                    {
                        // Add default theme options
                        uiItem.ComboBoxOptions.Add("Light");
                        uiItem.ComboBoxOptions.Add("Dark");
                        uiItem.ComboBoxOptions.Add("Auto");
                        
                        // Set selected value to Light as default
                        uiItem.SelectedValue = "Light";
                    }
                    else if (setting.ControlType == ControlType.BinaryToggle)
                    {
                        // Set toggle state
                        uiItem.IsSelected = setting.IsEnabled;
                    }
                    
                    // Add to settings collection
                    Settings.Add(uiItem);
                }
                
                // Get the current theme state from the service
                var themeState = await _settingsService.GetCurrentThemeStateAsync();
                
                // Update UI properties without triggering property change events
                _isHandlingDarkModeChange = true;
                _isDarkModeEnabled = themeState.Contains("Dark", StringComparison.OrdinalIgnoreCase);
                _changeWallpaper = false; // Default value
                _isHandlingDarkModeChange = false;
                
                // Notify UI of property changes
                OnPropertyChanged(nameof(IsDarkModeEnabled));
                OnPropertyChanged(nameof(ChangeWallpaper));
                OnPropertyChanged(nameof(SelectedTheme));
                
                // Refresh status of all settings
                await RefreshAllSettingsAsync();
                
                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading theme settings: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Gets the application settings that this ViewModel manages.
        /// </summary>
        /// <returns>Collection of application settings for theme customizations.</returns>
        protected override async Task<IEnumerable<ApplicationSetting>> GetApplicationSettingsAsync()
        {
            return await _settingsService.GetWindowsThemeSettingsAsync();
        }
    }
}
