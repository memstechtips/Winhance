using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service responsible for initializing the ApplicationSettingsService with all settings definitions.
    /// This service loads settings from static model classes and registers them with the ApplicationSettingsService cache.
    /// </summary>
    public class SettingsInitializationService : ISettingsInitializationService
    {
        private readonly IApplicationSettingsService _applicationSettingsService;
        private readonly ILogService _logService;
        private bool _isInitialized = false;

        public SettingsInitializationService(
            IApplicationSettingsService applicationSettingsService,
            ILogService logService)
        {
            _applicationSettingsService = applicationSettingsService ?? throw new ArgumentNullException(nameof(applicationSettingsService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Initializes all settings by loading them from model classes and registering them with the ApplicationSettingsService.
        /// </summary>
        public async Task InitializeAllSettingsAsync()
        {
            if (_isInitialized)
            {
                _logService.Log(LogLevel.Debug, "Settings already initialized, skipping...");
                return;
            }

            try
            {
                _logService.Log(LogLevel.Info, "Starting settings initialization...");

                // Initialize Customization Settings
                await InitializeCustomizationSettingsAsync();

                // Initialize Optimization Settings  
                await InitializeOptimizationSettingsAsync();

                _isInitialized = true;
                _logService.Log(LogLevel.Info, "Settings initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error during settings initialization: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Initializes all customization settings from static model classes.
        /// </summary>
        private async Task InitializeCustomizationSettingsAsync()
        {
            _logService.Log(LogLevel.Info, "Initializing customization settings...");

            // Load Start Menu customizations
            var startMenuGroup = StartMenuCustomizations.GetStartMenuCustomizations();
            _applicationSettingsService.RegisterSettings(startMenuGroup.Settings);
            _logService.Log(LogLevel.Info, $"Registered {startMenuGroup.Settings.Count} Start Menu settings");

            // Load Taskbar customizations
            var taskbarGroup = TaskbarCustomizations.GetTaskbarCustomizations();
            _applicationSettingsService.RegisterSettings(taskbarGroup.Settings);
            _logService.Log(LogLevel.Info, $"Registered {taskbarGroup.Settings.Count} Taskbar settings");

            // Load Explorer customizations
            var explorerGroup = ExplorerCustomizations.GetExplorerCustomizations();
            _applicationSettingsService.RegisterSettings(explorerGroup.Settings);
            _logService.Log(LogLevel.Info, $"Registered {explorerGroup.Settings.Count} Explorer settings");

            await Task.CompletedTask; // For consistency with async pattern
        }

        /// <summary>
        /// Initializes all optimization settings from static model classes.
        /// </summary>
        private async Task InitializeOptimizationSettingsAsync()
        {
            _logService.Log(LogLevel.Info, "Initializing optimization settings...");

            // Load Gaming and Performance optimizations
            var gamingGroup = GamingandPerformanceOptimizations.GetGamingandPerformanceOptimizations();
            _applicationSettingsService.RegisterSettings(gamingGroup.Settings);
            _logService.Log(LogLevel.Info, $"Registered {gamingGroup.Settings.Count} Gaming and Performance settings");

            // Load Explorer optimizations
            var explorerOptGroup = ExplorerOptimizations.GetExplorerOptimizations();
            _applicationSettingsService.RegisterSettings(explorerOptGroup.Settings);
            _logService.Log(LogLevel.Info, $"Registered {explorerOptGroup.Settings.Count} Explorer optimization settings");

            // Load Notification optimizations
            var notificationGroup = NotificationOptimizations.GetNotificationOptimizations();
            _applicationSettingsService.RegisterSettings(notificationGroup.Settings);
            _logService.Log(LogLevel.Info, $"Registered {notificationGroup.Settings.Count} Notification settings");

            // Load Privacy optimizations
            var privacyGroup = PrivacyOptimizations.GetPrivacyOptimizations();
            _applicationSettingsService.RegisterSettings(privacyGroup.Settings);
            _logService.Log(LogLevel.Info, $"Registered {privacyGroup.Settings.Count} Privacy settings");

            // Load Sound optimizations
            var soundGroup = SoundOptimizations.GetSoundOptimizations();
            _applicationSettingsService.RegisterSettings(soundGroup.Settings);
            _logService.Log(LogLevel.Info, $"Registered {soundGroup.Settings.Count} Sound settings");

            // Load Update optimizations
            var updateGroup = UpdateOptimizations.GetUpdateOptimizations();
            _applicationSettingsService.RegisterSettings(updateGroup.Settings);
            _logService.Log(LogLevel.Info, $"Registered {updateGroup.Settings.Count} Update settings");

            // NOTE: Power optimizations are now loaded dynamically from advanced power settings
            // in ApplicationSettingsService.GetPowerOptimizationSettingsAsync() to avoid duplicates
            // var powerGroup = PowerOptimizations.GetPowerOptimizations();
            // _applicationSettingsService.RegisterSettings(powerGroup.Settings);
            // _logService.Log(LogLevel.Info, $"Registered {powerGroup.Settings.Count} Power settings");

            await Task.CompletedTask; // For consistency with async pattern
        }

        /// <summary>
        /// Gets whether the settings have been initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized;
    }
}
