using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Customize.Interfaces;
// Explicitly using the domain service interfaces from their respective namespaces
using IStartMenuService = Winhance.Core.Features.Customize.Interfaces.IStartMenuService;
using ITaskbarService = Winhance.Core.Features.Customize.Interfaces.ITaskbarService;
using IWindowsThemeService = Winhance.Core.Features.Customize.Interfaces.IWindowsThemeService;
using INotificationService = Winhance.Core.Features.Optimize.Interfaces.INotificationService;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Registry implementation for mapping setting IDs to their appropriate domain services.
    /// Routes settings operations to the correct domain service based on setting ID patterns.
    /// </summary>
    public class DomainServiceRegistry : IDomainServiceRegistry
    {
        private readonly IWindowsThemeService _windowsThemeService;
        private readonly IStartMenuService _startMenuService;
        private readonly ITaskbarService _taskbarService;
        private readonly IExplorerOptimizationService _explorerOptimizationService;
        private readonly IExplorerCustomizationService _explorerCustomizationService;
        private readonly IGamingPerformanceService _gamingPerformanceService;
        private readonly IPrivacyService _privacyService;
        private readonly IUpdateService _updateService;
        private readonly IPowerService _powerService;
        private readonly INotificationService _notificationService;
        private readonly ISoundService _soundService;
        private readonly ISecurityService _securityService;
        private readonly ILogService _logService;

        public DomainServiceRegistry(
            IWindowsThemeService windowsThemeService,
            IStartMenuService startMenuService,
            ITaskbarService taskbarService,
            IExplorerOptimizationService explorerOptimizationService,
            IExplorerCustomizationService explorerCustomizationService,
            IGamingPerformanceService gamingPerformanceService,
            IPrivacyService privacyService,
            IUpdateService updateService,
            IPowerService powerService,
            INotificationService notificationService,
            ISoundService soundService,
            ISecurityService securityService,
            ILogService logService)
        {
            _windowsThemeService = windowsThemeService ?? throw new ArgumentNullException(nameof(windowsThemeService));
            _startMenuService = startMenuService ?? throw new ArgumentNullException(nameof(startMenuService));
            _taskbarService = taskbarService ?? throw new ArgumentNullException(nameof(taskbarService));
            _explorerOptimizationService = explorerOptimizationService ?? throw new ArgumentNullException(nameof(explorerOptimizationService));
            _explorerCustomizationService = explorerCustomizationService ?? throw new ArgumentNullException(nameof(explorerCustomizationService));
            _gamingPerformanceService = gamingPerformanceService ?? throw new ArgumentNullException(nameof(gamingPerformanceService));
            _privacyService = privacyService ?? throw new ArgumentNullException(nameof(privacyService));
            _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
            _powerService = powerService ?? throw new ArgumentNullException(nameof(powerService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Routing setting '{settingId}' to appropriate domain service");
                
                var service = GetDomainService(settingId);
                await service.ApplySettingAsync(settingId, enable, value);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying setting '{settingId}' through domain registry: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            try
            {
                var service = GetDomainService(settingId);
                return await service.IsSettingEnabledAsync(settingId);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking setting '{settingId}' through domain registry: {ex.Message}");
                return false;
            }
        }

        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            try
            {
                var service = GetDomainService(settingId);
                return await service.GetSettingValueAsync(settingId);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting setting value '{settingId}' through domain registry: {ex.Message}");
                return null;
            }
        }

        public async Task<Dictionary<string, bool>> GetMultipleSettingsStateAsync(IEnumerable<string> settingIds)
        {
            var results = new Dictionary<string, bool>();
            
            foreach (var settingId in settingIds)
            {
                try
                {
                    results[settingId] = await IsSettingEnabledAsync(settingId);
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error getting state for setting '{settingId}': {ex.Message}");
                    results[settingId] = false;
                }
            }
            
            return results;
        }

        public async Task<Dictionary<string, object?>> GetMultipleSettingsValuesAsync(IEnumerable<string> settingIds)
        {
            var results = new Dictionary<string, object?>();
            
            foreach (var settingId in settingIds)
            {
                try
                {
                    results[settingId] = await GetSettingValueAsync(settingId);
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error getting value for setting '{settingId}': {ex.Message}");
                    results[settingId] = null;
                }
            }
            
            return results;
        }

        public IDomainService GetDomainService(string settingId)
        {
            // Route based on setting ID patterns
            return settingId.ToLowerInvariant() switch
            {
                // Windows Theme domain
                var id when id.StartsWith("theme-") || id == "WindowsTheme" => _windowsThemeService,
                
                // Start Menu domain
                var id when id.StartsWith("start-") || id.StartsWith("startmenu-") => _startMenuService,
                
                // Taskbar domain
                var id when id.StartsWith("taskbar-") => _taskbarService,
                
                // Explorer Optimization domain
                var id when id.StartsWith("explorer-optimization-") => _explorerOptimizationService,
        
                // Explorer Customization domain
                var id when id.StartsWith("explorer-customization-") => _explorerCustomizationService,
        
                // Legacy Explorer domain (route to optimization by default)
                var id when id.StartsWith("explorer-") => _explorerOptimizationService,
                
                // Gaming & Performance domain
                var id when id.StartsWith("gaming-") || id.StartsWith("performance-") => _gamingPerformanceService,
                
                // Privacy domain
                var id when id.StartsWith("privacy-") => _privacyService,
                
                // Update domain
                var id when id.StartsWith("update-") => _updateService,
                
                // Power domain
                var id when id.StartsWith("power-") => _powerService,
                
                // Notification domain
                var id when id.StartsWith("notification-") => _notificationService,
                
                // Sound domain
                var id when id.StartsWith("sound-") => _soundService,
                
                // Security domain
                var id when id.StartsWith("security-") || id.StartsWith("uac-") => _securityService,
                
                // Default case - throw exception for unknown settings
                _ => throw new ArgumentException($"No domain service found for setting '{settingId}'. " +
                    "Setting ID should start with a recognized domain prefix (theme-, start-, taskbar-, explorer-, gaming-, privacy-, update-, power-, notification-, sound-, security-, uac-)")
            };
        }
    }
}
