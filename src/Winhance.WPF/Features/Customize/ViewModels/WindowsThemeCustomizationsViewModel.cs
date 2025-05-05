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
using Winhance.Core.Features.Customize.Models;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

using Winhance.WPF.Features.Common.Extensions;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// ViewModel for Windows Theme customizations.
    /// </summary>
    public partial class WindowsThemeCustomizationsViewModel : BaseSettingsViewModel<ApplicationSettingItem>
    {
        private readonly IDialogService _dialogService;
        private readonly IThemeService _themeService;
        private readonly WindowsThemeSettings _themeSettings;
        private readonly IRegistryService _registryService;

        // Flags to prevent triggering dark mode action during property change
        private bool _isHandlingDarkModeChange = false;
        private bool _isShowingDialog = false;
        
        // Store the original state before any changes
        private bool _originalDarkModeState;
        private string _originalTheme = string.Empty;
        
        // Store the requested state for the dialog
        private bool _requestedDarkModeState;
        private string _requestedTheme = string.Empty;

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
        /// Gets the category name.
        /// </summary>
        public string CategoryName => "Windows Theme";

        /// <summary>
        /// Gets the command to apply the theme.
        /// </summary>
        public ICommand ApplyThemeCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsThemeCustomizationsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="themeService">The theme service.</param>
        public WindowsThemeCustomizationsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService,
            IDialogService dialogService,
            IThemeService themeService)
            : base(progressService, registryService, logService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            
            // Initialize the theme settings model
            _themeSettings = new WindowsThemeSettings(themeService);
            
            // Load the current theme state
            LoadDarkModeState();
            
            // Initialize commands
            ApplyThemeCommand = new AsyncRelayCommand<bool>(ApplyThemeWithConfirmationAsync);

            // Subscribe to property changes to handle dark mode toggle and theme selection
            this.PropertyChanged += (s, e) =>
            {
                // Skip if we're already handling a change or showing a dialog
                if (_isHandlingDarkModeChange || _isShowingDialog)
                    return;
                    
                if (e.PropertyName == nameof(IsDarkModeEnabled))
                {
                    HandleDarkModePropertyChange();
                }
                else if (e.PropertyName == nameof(SelectedTheme))
                {
                    HandleThemeSelectionChange();
                }
            };
        }

        /// <summary>
        /// Handles changes to the IsDarkModeEnabled property.
        /// </summary>
        private void HandleDarkModePropertyChange()
        {
            // Store the original state before any changes
            _originalDarkModeState = _themeService.IsDarkModeEnabled();
            _originalTheme = _originalDarkModeState ? "Dark Mode" : "Light Mode";
            
            // Store the requested state
            _requestedDarkModeState = IsDarkModeEnabled;
            _requestedTheme = IsDarkModeEnabled ? "Dark Mode" : "Light Mode";
            
            // Log the state change
            _logService.Log(LogLevel.Info, $"Dark mode check completed. Is Dark Mode: {_originalDarkModeState}");
            
            // Update the selected theme to match the dark mode state
            _isHandlingDarkModeChange = true;
            try
            {
                SelectedTheme = IsDarkModeEnabled ? "Dark Mode" : "Light Mode";
                
                // Update the theme settings model
                _themeSettings.IsDarkMode = IsDarkModeEnabled;
                
                // Update the theme selector setting if it exists
                UpdateThemeSelectorSetting();
            }
            finally
            {
                _isHandlingDarkModeChange = false;
            }
            
            // Prompt for confirmation
            PromptForThemeChange();
        }

        /// <summary>
        /// Handles changes to the SelectedTheme property.
        /// </summary>
        private void HandleThemeSelectionChange()
        {
            // Store the original state before any changes
            _originalDarkModeState = _themeService.IsDarkModeEnabled();
            _originalTheme = _originalDarkModeState ? "Dark Mode" : "Light Mode";
            
            // Store the requested state
            _requestedTheme = SelectedTheme;
            _requestedDarkModeState = SelectedTheme == "Dark Mode";
            
            // Log the state change
            _logService.Log(LogLevel.Info, $"Theme mode changed to {SelectedTheme}");
            
            // Update the dark mode state based on the selected theme
            _isHandlingDarkModeChange = true;
            try
            {
                IsDarkModeEnabled = SelectedTheme == "Dark Mode";
                
                // Update the theme settings model
                _themeSettings.IsDarkMode = IsDarkModeEnabled;
                
                // Update the theme selector setting if it exists
                UpdateThemeSelectorSetting();
            }
            finally
            {
                _isHandlingDarkModeChange = false;
            }
            
            // Prompt for confirmation
            PromptForThemeChange();
        }

        /// <summary>
        /// Loads the dark mode state.
        /// </summary>
        private void LoadDarkModeState()
        {
            try
            {
                // Load the current theme settings
                _themeSettings.LoadCurrentSettings();
                
                // Update the view model properties
                _isHandlingDarkModeChange = true;
                try
                {
                    IsDarkModeEnabled = _themeSettings.IsDarkMode;
                    SelectedTheme = _themeSettings.ThemeName;
                }
                finally
                {
                    _isHandlingDarkModeChange = false;
                }

                // Store the original state
                _originalDarkModeState = IsDarkModeEnabled;
                _originalTheme = SelectedTheme;

                _logService.Log(LogLevel.Info, $"Loaded theme settings: {SelectedTheme}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading dark mode state: {ex.Message}");
                IsDarkModeEnabled = true; // Default to dark mode if there's an error
                SelectedTheme = "Dark Mode"; // Set selected theme to match
                
                // Store the original state
                _originalDarkModeState = IsDarkModeEnabled;
                _originalTheme = SelectedTheme;
            }
        }

        /// <summary>
        /// Prompts the user for theme change confirmation.
        /// </summary>
        private async void PromptForThemeChange()
        {
            // Prevent multiple dialogs from being shown at once
            if (_isShowingDialog)
            {
                _logService.Log(LogLevel.Info, "Dialog already showing, ignoring request");
                return;
            }
            
            _isShowingDialog = true;
            
            try
            {
                // Store the current UI state before showing the dialog
                bool currentDarkModeState = IsDarkModeEnabled;
                string currentTheme = SelectedTheme;
                
                // Revert to the original state before showing the dialog
                // This ensures that if the user cancels, we're already in the original state
                _isHandlingDarkModeChange = true;
                try
                {
                    IsDarkModeEnabled = _originalDarkModeState;
                    SelectedTheme = _originalTheme;
                    _themeSettings.IsDarkMode = _originalDarkModeState;
                }
                finally
                {
                    _isHandlingDarkModeChange = false;
                }
                
                // Prompt user about wallpaper change
                _logService.Log(LogLevel.Info, "Showing dialog for theme change confirmation");
                
                // Use the DialogService to show the confirmation dialog
                var result = await _dialogService.ShowYesNoCancelAsync(
                    $"Would you also like to change to the default {(currentDarkModeState ? "dark" : "light")} theme wallpaper?",
                    "Windows Theme Change"
                );

                switch (result)
                {
                    case true: // Yes - Change theme AND apply wallpaper
                        _logService.Log(LogLevel.Info, "User selected to apply theme with wallpaper");
                        _isHandlingDarkModeChange = true;
                        try
                        {
                            // Apply the requested theme
                            IsDarkModeEnabled = _requestedDarkModeState;
                            SelectedTheme = _requestedTheme;
                            ChangeWallpaper = true; // Store the user's preference
                            
                            // Update the theme settings model
                            _themeSettings.IsDarkMode = _requestedDarkModeState;
                            _themeSettings.ChangeWallpaper = true;
                            
                            // Apply the theme
                            await ApplyThemeAsync(true);
                            _logService.Log(LogLevel.Info, $"Theme changed to {(IsDarkModeEnabled ? "dark" : "light")} mode with wallpaper");
                        }
                        finally
                        {
                            _isHandlingDarkModeChange = false;
                        }
                        break;

                    case false: // No - Change theme but DON'T change wallpaper
                        _logService.Log(LogLevel.Info, "User selected to apply theme without wallpaper");
                        _isHandlingDarkModeChange = true;
                        try
                        {
                            // Apply the requested theme
                            IsDarkModeEnabled = _requestedDarkModeState;
                            SelectedTheme = _requestedTheme;
                            ChangeWallpaper = false; // Store the user's preference
                            
                            // Update the theme settings model
                            _themeSettings.IsDarkMode = _requestedDarkModeState;
                            _themeSettings.ChangeWallpaper = false;
                            
                            // Apply the theme
                            await ApplyThemeAsync(false);
                            _logService.Log(LogLevel.Info, $"Theme changed to {(IsDarkModeEnabled ? "dark" : "light")} mode without wallpaper");
                        }
                        finally
                        {
                            _isHandlingDarkModeChange = false;
                        }
                        break;

                    case null: // Cancel - Do NOTHING, just close dialog
                        _logService.Log(LogLevel.Info, "User canceled theme change - keeping original state");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error toggling dark mode: {ex.Message}");
                await _dialogService.ShowErrorAsync("Theme Error", $"Failed to switch themes: {ex.Message}");

                // Revert the toggle and selected theme on error
                _isHandlingDarkModeChange = true;
                try
                {
                    // Revert to the original state
                    IsDarkModeEnabled = _originalDarkModeState;
                    SelectedTheme = _originalTheme;
                    _themeSettings.IsDarkMode = _originalDarkModeState;
                    
                    _logService.Log(LogLevel.Info, "Reverted to original state after error");
                }
                finally
                {
                    _isHandlingDarkModeChange = false;
                }
            }
            finally
            {
                // Always reset the dialog flag
                _isShowingDialog = false;
                _logService.Log(LogLevel.Info, "Dialog handling completed");
            }
        }

        /// <summary>
        /// Applies the theme with confirmation dialog.
        /// </summary>
        /// <param name="changeWallpaper">Whether to change the wallpaper.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ApplyThemeWithConfirmationAsync(bool changeWallpaper)
        {
            // Store the current state
            bool currentDarkMode = IsDarkModeEnabled;
            
            // Prompt for confirmation
            var result = await _dialogService.ShowConfirmationAsync(
                "Apply Theme",
                $"Are you sure you want to apply the {(currentDarkMode ? "dark" : "light")} theme?"
            );
            
            if (result)
            {
                await ApplyThemeAsync(changeWallpaper);
            }
        }

        /// <summary>
        /// Applies the dark mode setting.
        /// </summary>
        /// <param name="changeWallpaper">Whether to change the wallpaper.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task<bool> ApplyThemeAsync(bool changeWallpaper)
        {
            try
            {
                // Update the theme settings model
                _themeSettings.IsDarkMode = IsDarkModeEnabled;
                _themeSettings.ChangeWallpaper = changeWallpaper;
                
                // Apply the theme
                bool success = await _themeSettings.ApplyThemeAsync();
                
                if (success)
                {
                    _logService.Log(LogLevel.Info, $"Windows Dark Mode {(IsDarkModeEnabled ? "enabled" : "disabled")}");
                }
                else
                {
                    _logService.Log(LogLevel.Error, "Failed to apply theme");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying dark mode: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Loads the settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                _logService.Log(LogLevel.Info, "Loading Windows theme settings");

                // Load dark mode state
                LoadDarkModeState();
                
                // Clear existing settings
                Settings.Clear();
                
                // Add a searchable item for the Theme Selector
                var themeItem = new ApplicationSettingItem(_registryService, null, _logService)
                {
                    Id = "ThemeSelector",
                    Name = "Windows Theme",
                    Description = "Select between light and dark theme for Windows",
                    GroupName = "Windows Theme",
                    IsVisible = true,
                    IsSelected = true, // Always mark as selected so it appears in the config list
                    ControlType = ControlType.ComboBox,
                    SelectedValue = SelectedTheme, // Store the selected theme directly
                    // Create a registry setting for the theme selector
                    RegistrySetting = new RegistrySetting
                    {
                        Category = "WindowsTheme",
                        Hive = Microsoft.Win32.RegistryHive.CurrentUser,
                        SubKey = WindowsThemeSettings.Registry.ThemesPersonalizeSubKey,
                        Name = WindowsThemeSettings.Registry.AppsUseLightThemeName,
                        RecommendedValue = SelectedTheme == "Dark Mode" ? 0 : 1,
                        DefaultValue = 1,
                        EnabledValue = SelectedTheme == "Dark Mode" ? 0 : 1,
                        DisabledValue = SelectedTheme == "Dark Mode" ? 1 : 0,
                        ValueType = Microsoft.Win32.RegistryValueKind.DWord,
                        Description = "Windows Theme Mode",
                        // Store the selected theme and wallpaper preference
                        CustomProperties = new Dictionary<string, object>
                        {
                            { "SelectedTheme", SelectedTheme },
                            { "ChangeWallpaper", ChangeWallpaper },
                            { "ThemeOptions", ThemeOptions } // Store available options
                        }
                    }
                };
                Settings.Add(themeItem);
                
                _logService.Log(LogLevel.Info, $"Added {Settings.Count} searchable items for Windows Theme settings");

                // Check the status of all settings
                await CheckSettingStatusesAsync();

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading Windows theme settings: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks the status of all settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task CheckSettingStatusesAsync()
        {
            try
            {
                IsLoading = true;
                _logService.Log(LogLevel.Info, "Checking status for Windows theme settings");

                // Refresh dark mode state
                LoadDarkModeState();

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking Windows theme settings status: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Gets the status message for a setting.
        /// </summary>
        /// <param name="setting">The setting.</param>
        /// <returns>The status message.</returns>
        private string GetStatusMessage(ApplicationSettingItem setting)
        {
            // Get status
            var status = setting.Status;
            string message = status switch
            {
                RegistrySettingStatus.Applied => "Setting is applied with recommended value",
                RegistrySettingStatus.NotApplied => "Setting is not applied or using default value",
                RegistrySettingStatus.Modified => "Setting has a custom value different from recommended",
                RegistrySettingStatus.Error => "Error checking setting status",
                _ => "Unknown status"
            };

            return message;
        }

        // ApplySelectedSettingsAsync and RestoreDefaultsAsync methods removed as part of the refactoring
        
        /// <summary>
        /// Updates the theme selector setting to match the current selected theme.
        /// </summary>
        private void UpdateThemeSelectorSetting()
        {
            // Find the theme selector setting
            var themeSelector = Settings.FirstOrDefault(s => s.Id == "ThemeSelector");
            if (themeSelector != null)
            {
                themeSelector.IsUpdatingFromCode = true;
                try
                {
                    // Update the SelectedValue directly with the current theme
                    themeSelector.SelectedValue = SelectedTheme;
                    _logService.Log(LogLevel.Info, $"Updated theme selector setting to {SelectedTheme}");
                    
                    // Store the wallpaper preference and selected theme in the setting's additional properties
                    // This will be used when saving/loading configurations
                    // Create a new RegistrySetting object with updated values
                    var customProperties = new Dictionary<string, object>();
                    customProperties["ChangeWallpaper"] = ChangeWallpaper;
                    customProperties["SelectedTheme"] = SelectedTheme;
                    customProperties["ThemeOptions"] = ThemeOptions; // Store available options
                    
                    // Create a new RegistrySetting with the updated values
                    var newRegistrySetting = new RegistrySetting
                    {
                        Category = "WindowsTheme",
                        Hive = Microsoft.Win32.RegistryHive.CurrentUser,
                        SubKey = WindowsThemeSettings.Registry.ThemesPersonalizeSubKey,
                        Name = WindowsThemeSettings.Registry.AppsUseLightThemeName,
                        RecommendedValue = SelectedTheme == "Dark Mode" ? 0 : 1,
                        DefaultValue = 1,
                        EnabledValue = SelectedTheme == "Dark Mode" ? 0 : 1,
                        DisabledValue = SelectedTheme == "Dark Mode" ? 1 : 0,
                        ValueType = Microsoft.Win32.RegistryValueKind.DWord,
                        Description = "Windows Theme Mode",
                        CustomProperties = customProperties
                    };
                    
                    // Assign the new RegistrySetting to the theme selector
                    themeSelector.RegistrySetting = newRegistrySetting;
                    
                    _logService.Log(LogLevel.Info, $"Stored theme preferences: SelectedTheme={SelectedTheme}, ChangeWallpaper={ChangeWallpaper}");
                }
                finally
                {
                    themeSelector.IsUpdatingFromCode = false;
                }
            }
        }
    }
}
