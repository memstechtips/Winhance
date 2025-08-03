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
using Winhance.Core.Features.Customize.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// Clean architecture ViewModel for Start Menu customizations.
    /// Uses IApplicationSettingsService for business logic and SettingUIItem for UI state.
    /// Replaces the complex 963-line StartMenuCustomizationsViewModel with clean separation of concerns.
    /// </summary>
    public partial class StartMenuCustomizationsViewModel : BaseSettingsViewModel
    {
        #region Private Fields

        private readonly ISystemServices _systemServices;
        private readonly IDialogService _dialogService;
        private readonly IScheduledTaskService _scheduledTaskService;
        private bool _isWindows11;

        #endregion

        #region Observable Properties

        /// <summary>
        /// Collection of ComboBox settings for specialized display.
        /// </summary>
        public ObservableCollection<SettingUIItem> ComboBoxSettings { get; } = new();

        /// <summary>
        /// Collection of Toggle settings for specialized display.
        /// </summary>
        public ObservableCollection<SettingUIItem> ToggleSettings { get; } = new();

        /// <summary>
        /// Whether there are ComboBox settings available.
        /// </summary>
        public bool HasComboBoxSettings => ComboBoxSettings.Count > 0;

        /// <summary>
        /// Whether there are Toggle settings available.
        /// </summary>
        public bool HasToggleSettings => ToggleSettings.Count > 0;

        #endregion

        #region Commands

        /// <summary>
        /// Command to clean the Start Menu.
        /// </summary>
        public ICommand CleanStartMenuCommand { get; }

        /// <summary>
        /// Command to apply recommended Start Menu settings.
        /// </summary>
        public ICommand ApplyRecommendedSettingsCommand { get; }

        /// <summary>
        /// Command to execute a specific action.
        /// </summary>
        public ICommand ExecuteActionCommand { get; }

        #endregion

        #region Constructor

        public StartMenuCustomizationsViewModel(
            IApplicationSettingsService settingsService,
            ITaskProgressService progressService,
            ILogService logService,
            ISystemServices systemServices,
            IDialogService dialogService,
            IScheduledTaskService scheduledTaskService)
            : base(settingsService, progressService, logService)
        {
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _scheduledTaskService = scheduledTaskService ?? throw new ArgumentNullException(nameof(scheduledTaskService));

            _isWindows11 = _systemServices.IsWindows11();
            CategoryName = "Start Menu";

            // Initialize commands
            CleanStartMenuCommand = new AsyncRelayCommand(CleanStartMenuAsync);
            ApplyRecommendedSettingsCommand = new AsyncRelayCommand(ApplyRecommendedSettingsAsync);
            ExecuteActionCommand = new AsyncRelayCommand<string>(ExecuteActionAsync);
        }

        #endregion

        #region Protected Override Methods

        /// <summary>
        /// Gets the Start Menu customization settings.
        /// </summary>
        protected override async Task<IEnumerable<ApplicationSetting>> GetApplicationSettingsAsync()
        {
            await Task.CompletedTask; // Placeholder for any async initialization

            var startMenuCustomizations = StartMenuCustomizations.GetStartMenuCustomizations();
            
            // Filter settings based on Windows version and build
            var filteredSettings = startMenuCustomizations.Settings
                .Where(IsSettingSupportedOnCurrentSystem)
                .ToList();

            _logService.Log(LogLevel.Info, $"Loaded {filteredSettings.Count} Start Menu settings for current system");

            return filteredSettings;
        }

        /// <summary>
        /// Loads settings and organizes them by control type.
        /// </summary>
        public override async Task LoadSettingsAsync()
        {
            await base.LoadSettingsAsync();

            // Organize settings by control type for specialized UI display
            OrganizeSettingsByControlType();

            // Update dynamic tooltips for Windows 11
            if (_isWindows11)
            {
                UpdateDynamicTooltips();
            }
        }

        #endregion

        #region Command Handlers

        /// <summary>
        /// Cleans the Start Menu by removing all pinned items and applying recommended settings.
        /// </summary>
        private async Task CleanStartMenuAsync()
        {
            try
            {
                // Show confirmation dialog
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    "You are about to clean the Start Menu for all users on this computer.\n\n" +
                    "This will remove all pinned items and apply recommended settings to disable suggestions, " +
                    "recommendations, and tracking features.\n\n" +
                    "Do you want to continue?",
                    "Start Menu Cleaning Options");

                if (!confirmed) return;

                _progressService.StartTask("Cleaning Start Menu...");
                _logService.Log(LogLevel.Info, "Starting Start Menu cleaning process");

                // Clean the Start Menu using the static method
                await Task.Run(() => StartMenuCustomizations.CleanStartMenu(_isWindows11, _systemServices, _logService, _scheduledTaskService));

                // Apply recommended settings
                await ApplyRecommendedSettingsAsync();

                _progressService.CompleteTask();
                _logService.Log(LogLevel.Info, "Start Menu cleaned successfully");

                // Refresh all settings to reflect changes
                await RefreshAllSettingsAsync();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error cleaning Start Menu: {ex.Message}");
                await _dialogService.ShowErrorAsync($"Failed to clean Start Menu: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Applies comprehensive recommended Start Menu settings based on Windows version.
        /// </summary>
        private async Task ApplyRecommendedSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying recommended Start Menu settings for Windows {(_isWindows11 ? "11" : "10")}");

                var recommendedSettingIds = GetRecommendedSettingIds();
                var settingsToApply = recommendedSettingIds.ToDictionary(id => id, _ => (true, (object?)null));

                await _settingsService.ApplyMultipleSettingsAsync(settingsToApply);
                
                _logService.Log(LogLevel.Info, $"Applied {recommendedSettingIds.Count} recommended settings");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying recommended settings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Executes a specific action by name.
        /// </summary>
        private async Task ExecuteActionAsync(string? actionName)
        {
            if (string.IsNullOrEmpty(actionName)) return;

            try
            {
                _logService.Log(LogLevel.Info, $"Executing action: {actionName}");

                // Find the action in the Start Menu customizations
                var startMenuCustomizations = StartMenuCustomizations.GetStartMenuCustomizations();
                var action = startMenuCustomizations.Settings?.FirstOrDefault(a => a.Name == actionName);

                if (action == null)
                {
                    _logService.Log(LogLevel.Warning, $"Action '{actionName}' not found");
                    return;
                }

                // Execute the action using the service
                if (action.RegistrySettings?.Count > 0)
                {
                    // For actions, we apply the recommended value from the first registry setting
                    var registrySetting = action.RegistrySettings[0];
                    await _settingsService.ApplySettingAsync($"action_{actionName}", true, registrySetting.RecommendedValue);
                }

                // For now, we don't have custom actions in the new architecture
                // TODO: Implement custom action handling if needed

                _logService.Log(LogLevel.Info, $"Action '{actionName}' executed successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error executing action '{actionName}': {ex.Message}");
                await _dialogService.ShowErrorAsync($"Failed to execute action '{actionName}': {ex.Message}", "Error");
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Determines if a setting is supported on the current system.
        /// </summary>
        private bool IsSettingSupportedOnCurrentSystem(CustomizationSetting setting)
        {
            // Check Windows version requirements
            if (setting.IsWindows11Only && !_isWindows11) return false;
            if (setting.IsWindows10Only && _isWindows11) return false;

            // Check build number requirements - using Windows version instead
            var windowsVersion = _systemServices.GetWindowsVersion();
            // For now, assume we can proceed without specific build number checks
            var currentBuild = 0; // TODO: Parse build number from version string if needed

            // Check supported build ranges (takes precedence)
            if (setting.SupportedBuildRanges?.Any() == true)
            {
                return setting.SupportedBuildRanges.Any(range => 
                    currentBuild >= range.MinBuild && currentBuild <= range.MaxBuild);
            }

            // Check minimum build requirement
            if (setting.MinimumBuildNumber.HasValue && currentBuild < setting.MinimumBuildNumber.Value)
                return false;

            // Check maximum build requirement
            if (setting.MaximumBuildNumber.HasValue && currentBuild > setting.MaximumBuildNumber.Value)
                return false;

            return true;
        }

        /// <summary>
        /// Organizes settings by control type for specialized UI display.
        /// </summary>
        private void OrganizeSettingsByControlType()
        {
            ComboBoxSettings.Clear();
            ToggleSettings.Clear();

            foreach (var setting in Settings)
            {
                switch (setting.ControlType)
                {
                    case ControlType.ComboBox:
                        ComboBoxSettings.Add(setting);
                        break;
                    case ControlType.BinaryToggle:
                        ToggleSettings.Add(setting);
                        break;
                    // Add other control types as needed
                }
            }

            // Notify property changes
            OnPropertyChanged(nameof(HasComboBoxSettings));
            OnPropertyChanged(nameof(HasToggleSettings));
        }

        /// <summary>
        /// Gets the list of recommended setting IDs based on Windows version.
        /// </summary>
        private List<string> GetRecommendedSettingIds()
        {
            if (_isWindows11)
            {
                return new List<string>
                {
                    "show-recently-added-apps",
                    "start-track-progs",
                    "show-recommended-files",
                    "start-menu-recommendations",
                    "recommended-section"
                };
            }
            else
            {
                return new List<string>
                {
                    "show-recently-added-apps",
                    "start-track-progs",
                    "show-most-used-apps",
                    "show-suggestions"
                };
            }
        }

        /// <summary>
        /// Updates the descriptions of settings that are affected by the "Recommended Section" ComboBox on Windows 11.
        /// </summary>
        private void UpdateDynamicTooltips()
        {
            if (!_isWindows11) return;

            var recommendedSectionSetting = Settings.FirstOrDefault(s => s.Id == "recommended-section");
            if (recommendedSectionSetting == null) return;

            bool isRecommendedSectionHidden = recommendedSectionSetting.SelectedValue?.ToString() == "Hide";

            var affectedSettingIds = new List<string>
            {
                "show-recently-added-apps",
                "start-track-progs",
                "show-recommended-files",
                "start-menu-recommendations"
            };

            foreach (var settingId in affectedSettingIds)
            {
                var setting = Settings.FirstOrDefault(s => s.Id == settingId);
                if (setting != null)
                {
                    // Get original description from core model
                    var startMenuCustomizations = StartMenuCustomizations.GetStartMenuCustomizations();
                    var originalSetting = startMenuCustomizations.Settings.FirstOrDefault(s => s.Id == settingId);
                    
                    if (originalSetting != null)
                    {
                        string baseDescription = originalSetting.Description;
                        
                        if (isRecommendedSectionHidden)
                        {
                            setting.Description = baseDescription + 
                                "\n\nNote: This setting won't have any visual effect while the 'Recommended Section' is set to 'Hide'.";
                        }
                        else
                        {
                            setting.Description = baseDescription;
                        }
                    }
                }
            }
        }

        #endregion
    }
}
