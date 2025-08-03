using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Customize.Enums;
using Winhance.Core.Features.Optimize.Models;


namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Generic service implementation for handling application settings business logic.
    /// Works with any type of ApplicationSetting (CustomizationSetting, OptimizationSetting, etc.).
    /// Centralizes all business logic that was previously scattered in ApplicationSettingItem.
    /// </summary>
    public class ApplicationSettingsService : IApplicationSettingsService
    {
        private readonly IRegistryService _registryService;
        private readonly ICommandService _commandService;
        private readonly IDomainDependencyService _domainDependencyService;
        private readonly ILogService _logService;
        private readonly ISystemSettingsDiscoveryService _discoveryService;
        private readonly ISystemServices _systemServices;

        // Cache for settings to avoid repeated lookups
        private readonly Dictionary<string, ApplicationSetting> _settingsCache = new();

        public event EventHandler<SettingStatusChangedEventArgs>? SettingStatusChanged;

        public ApplicationSettingsService(
            IRegistryService registryService,
            ICommandService commandService,
            IDomainDependencyService domainDependencyService,
            ILogService logService,
            ISystemSettingsDiscoveryService discoveryService,
            ISystemServices systemServices)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _domainDependencyService = domainDependencyService ?? throw new ArgumentNullException(nameof(domainDependencyService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            
            // Initialize settings cache on construction
            _ = InitializeSettingsCacheAsync();
        }
        
        /// <summary>
        /// Initializes the settings cache by loading all settings from optimization models.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task InitializeSettingsCacheAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Initializing ApplicationSettingsService cache...");
                
                // Load all optimization settings (excluding power settings which are now handled dynamically)
                var gamingSettings = GamingandPerformanceOptimizations.GetGamingandPerformanceOptimizations();
                var explorerOptSettings = ExplorerOptimizations.GetExplorerOptimizations();
                var privacySettings = PrivacyOptimizations.GetPrivacyOptimizations();
                var updateSettings = UpdateOptimizations.GetUpdateOptimizations();
                // NOTE: Power settings are now loaded dynamically from advanced power settings in GetPowerOptimizationSettingsAsync()
                var notificationSettings = NotificationOptimizations.GetNotificationOptimizations();
                var soundSettings = SoundOptimizations.GetSoundOptimizations();
                
                // Register all optimization settings (excluding power settings)
                RegisterSettings(gamingSettings.Settings);
                RegisterSettings(explorerOptSettings.Settings);
                RegisterSettings(privacySettings.Settings);
                RegisterSettings(updateSettings.Settings);
                // RegisterSettings(powerSettings.Settings); // REMOVED: Power settings now come from advanced conversion
                RegisterSettings(notificationSettings.Settings);
                RegisterSettings(soundSettings.Settings);
                
                // Load all customization settings
                var startMenuCustomizations = StartMenuCustomizations.GetStartMenuCustomizations();
                var taskbarCustomizations = TaskbarCustomizations.GetTaskbarCustomizations();
                var explorerCustomizations = ExplorerCustomizations.GetExplorerCustomizations();
                var windowsThemeCustomizations = WindowsThemeSettings.GetWindowsThemeCustomizations();
                
                // Register all customization settings
                RegisterSettings(startMenuCustomizations.Settings);
                RegisterSettings(taskbarCustomizations.Settings);
                RegisterSettings(explorerCustomizations.Settings);
                RegisterSettings(windowsThemeCustomizations.Settings);
                
                _logService.Log(LogLevel.Info, $"ApplicationSettingsService cache initialized with {_settingsCache.Count} settings");
                
                // Log the power settings specifically
                var powerSettingsCount = _settingsCache.Values.Count(s => s is OptimizationSetting opt && opt.Category == OptimizationCategory.Power);
                _logService.Log(LogLevel.Info, $"Loaded {powerSettingsCount} power optimization settings");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error initializing ApplicationSettingsService cache: {ex.Message}");
            }
        }

        public void RegisterSetting(ApplicationSetting setting)
        {
            _settingsCache[setting.Id] = setting;
        }

        public void RegisterSettings(IEnumerable<ApplicationSetting> settings)
        {
            foreach (var setting in settings)
            {
                RegisterSetting(setting);
            }
        }

        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            try
            {
                _logService.Log(LogLevel.Debug, $"Checking if setting '{settingId}' is enabled");

                if (!_settingsCache.TryGetValue(settingId, out var setting))
                {
                    _logService.Log(LogLevel.Warning, $"Setting '{settingId}' not found in cache");
                    return false;
                }

                // Check registry settings
                if (setting.RegistrySettings?.Count > 0)
                {
                    if (setting.RegistrySettings.Count == 1)
                    {
                        return await IsRegistrySettingEnabledAsync(setting.RegistrySettings[0]);
                    }
                    else
                    {
                        // Handle linked settings
                        var linkedSettings = setting.CreateLinkedRegistrySettings();
                        return await IsLinkedSettingsEnabledAsync(linkedSettings);
                    }
                }

                // Check command settings
                if (setting.CommandSettings?.Count > 0)
                {
                    return await _commandService.IsCommandSettingEnabledAsync(setting.CommandSettings[0]);
                }

                _logService.Log(LogLevel.Warning, $"Setting '{settingId}' has no registry or command settings");
                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking if setting '{settingId}' is enabled: {ex.Message}");
                return false;
            }
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying setting '{settingId}': enable={enable}, value={value}");

                if (!_settingsCache.TryGetValue(settingId, out var setting))
                {
                    _logService.Log(LogLevel.Error, $"Setting '{settingId}' not found in cache");
                    throw new ArgumentException($"Setting '{settingId}' not found", nameof(settingId));
                }

                // Check dependencies before applying (only for enable operations)
                if (enable)
                {
                    var currentState = await GetMultipleSettingsStateAsync(_settingsCache.Keys);
                    if (!_domainDependencyService.CanEnableSetting(settingId, _settingsCache.Values, currentState))
                    {
                        _logService.Log(LogLevel.Warning, $"Cannot enable setting '{settingId}' due to unsatisfied dependencies");
                        
                        // Get dependency resolution plan
                        var resolutionPlan = _domainDependencyService.GetDependencyResolutionPlan(settingId, _settingsCache.Values, currentState);
                        
                        _logService.Log(LogLevel.Info, $"Dependency resolution plan for '{settingId}': {string.Join(", ", resolutionPlan.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                        
                        // For now, throw an exception with helpful information
                        var requiredDeps = _domainDependencyService.GetRequiredDependencies(settingId, _settingsCache.Values);
                        var conflictingDeps = _domainDependencyService.GetConflictingDependencies(settingId, _settingsCache.Values);
                        
                        var errorMessage = $"Cannot enable setting '{settingId}' due to dependencies.";
                        if (requiredDeps.Any())
                        {
                            errorMessage += $" Required settings: {string.Join(", ", requiredDeps)}.";
                        }
                        if (conflictingDeps.Any())
                        {
                            errorMessage += $" Conflicting settings: {string.Join(", ", conflictingDeps)}.";
                        }
                        
                        throw new InvalidOperationException(errorMessage);
                    }
                }

                // Apply registry settings
                if (setting.RegistrySettings?.Count > 0)
                {
                    await ApplyRegistrySettingsAsync(setting, enable, value);
                }

                // Apply command settings
                if (setting.CommandSettings?.Count > 0)
                {
                    var (success, message) = await _commandService.ApplyCommandSettingsAsync(setting.CommandSettings, enable);
                    if (!success)
                    {
                        throw new InvalidOperationException($"Failed to apply command settings for '{settingId}': {message}");
                    }
                }

                // Note: Post-application dependency handling is managed at the UI layer
                // The domain service focuses on pre-validation of dependencies

                // Notify status change
                var currentValue = await GetSettingValueAsync(settingId);
                SettingStatusChanged?.Invoke(this, new SettingStatusChangedEventArgs(settingId, enable, currentValue));

                _logService.Log(LogLevel.Info, $"Successfully applied setting '{settingId}'");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying setting '{settingId}': {ex.Message}");
                throw;
            }
        }

        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            try
            {
                if (!_settingsCache.TryGetValue(settingId, out var setting))
                {
                    _logService.Log(LogLevel.Warning, $"Setting '{settingId}' not found in cache");
                    return null;
                }

                // Get value from registry settings
                if (setting.RegistrySettings?.Count > 0)
                {
                    var registrySetting = setting.RegistrySettings[0];
                    var keyPath = $"{registrySetting.Hive}\\{registrySetting.SubKey}";
                    return _registryService.GetValue(keyPath, registrySetting.Name);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting value for setting '{settingId}': {ex.Message}");
                return null;
            }
        }

        public async Task ApplyMultipleSettingsAsync(Dictionary<string, (bool enable, object? value)> settings)
        {
            var tasks = settings.Select(kvp => ApplySettingAsync(kvp.Key, kvp.Value.enable, kvp.Value.value));
            await Task.WhenAll(tasks);
        }

        public async Task<Dictionary<string, bool>> GetMultipleSettingsStateAsync(IEnumerable<string> settingIds)
        {
            var tasks = settingIds.Select(async id => new { Id = id, IsEnabled = await IsSettingEnabledAsync(id) });
            var results = await Task.WhenAll(tasks);
            return results.ToDictionary(r => r.Id, r => r.IsEnabled);
        }

        public async Task<Dictionary<string, object?>> GetMultipleSettingsValuesAsync(IEnumerable<string> settingIds)
        {
            var tasks = settingIds.Select(async id => new { Id = id, Value = await GetSettingValueAsync(id) });
            var results = await Task.WhenAll(tasks);
            return results.ToDictionary(r => r.Id, r => r.Value);
        }

        public async Task RestoreSettingToDefaultAsync(string settingId)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Restoring setting '{settingId}' to default");

                if (!_settingsCache.TryGetValue(settingId, out var setting))
                {
                    throw new ArgumentException($"Setting '{settingId}' not found", nameof(settingId));
                }

                // Restore registry settings to default
                if (setting.RegistrySettings?.Count > 0)
                {
                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        var keyPath = $"{registrySetting.Hive}\\{registrySetting.SubKey}";
                        
                        if (registrySetting.DefaultValue == null)
                        {
                            // Delete the value to restore default
                            _registryService.DeleteValue(keyPath, registrySetting.Name);
                        }
                        else
                        {
                            // Set to default value
                            _registryService.SetValue(keyPath, registrySetting.Name, registrySetting.DefaultValue, registrySetting.ValueType);
                        }
                    }
                }

                _logService.Log(LogLevel.Info, $"Successfully restored setting '{settingId}' to default");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error restoring setting '{settingId}' to default: {ex.Message}");
                throw;
            }
        }

        public async Task RestoreMultipleSettingsToDefaultAsync(IEnumerable<string> settingIds)
        {
            var tasks = settingIds.Select(RestoreSettingToDefaultAsync);
            await Task.WhenAll(tasks);
        }

        public async Task RefreshSettingStatusAsync(string settingId)
        {
            try
            {
                var isEnabled = await IsSettingEnabledAsync(settingId);
                var currentValue = await GetSettingValueAsync(settingId);
                
                SettingStatusChanged?.Invoke(this, new SettingStatusChangedEventArgs(settingId, isEnabled, currentValue));
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error refreshing status for setting '{settingId}': {ex.Message}");
                throw;
            }
        }

        #region Private Helper Methods

        private async Task<bool> IsRegistrySettingEnabledAsync(RegistrySetting registrySetting)
        {
            var keyPath = $"{registrySetting.Hive}\\{registrySetting.SubKey}";
            var currentValue = _registryService.GetValue(keyPath, registrySetting.Name);

            if (currentValue == null)
            {
                // Handle absence logic
                return registrySetting.AbsenceMeansEnabled;
            }

            // Compare with enabled value
            if (registrySetting.EnabledValue != null)
            {
                return currentValue.Equals(registrySetting.EnabledValue);
            }

            // Fallback to recommended value
            return currentValue.Equals(registrySetting.RecommendedValue);
        }

        private async Task<bool> IsLinkedSettingsEnabledAsync(LinkedRegistrySettings linkedSettings)
        {
            var results = new List<bool>();

            foreach (var setting in linkedSettings.Settings)
            {
                results.Add(await IsRegistrySettingEnabledAsync(setting));
            }

            return linkedSettings.Logic switch
            {
                LinkedSettingsLogic.All => results.All(r => r),
                LinkedSettingsLogic.Any => results.Any(r => r),
                LinkedSettingsLogic.Primary => results.Where((r, i) => linkedSettings.Settings[i].IsPrimary).FirstOrDefault(),
                _ => results.Any(r => r)
            };
        }

        private async Task ApplyRegistrySettingsAsync(ApplicationSetting setting, bool enable, object? value)
        {
            if (setting.RegistrySettings.Count == 1)
            {
                var registrySetting = setting.RegistrySettings[0];
                
                // Handle special cases like News and Interests (only for CustomizationSettings)
                if (setting is CustomizationSetting customizationSetting && customizationSetting.Id == "news-and-interests")
                {
                    var linkedSettings = setting.CreateLinkedRegistrySettings();
                    await TaskbarCustomizations.ApplyNewsAndInterestsSettingsAsync(
                        _registryService, _logService, linkedSettings, enable);
                    return;
                }

                // Handle ComboBox values
                if (setting.ControlType == ControlType.ComboBox && value != null)
                {
                    var comboValue = GetComboBoxRegistryValue(registrySetting, value);
                    var keyPath = $"{registrySetting.Hive}\\{registrySetting.SubKey}";
                    _registryService.SetValue(keyPath, registrySetting.Name, comboValue, registrySetting.ValueType);
                }
                else
                {
                    await _registryService.ApplySettingAsync(registrySetting, enable);
                }
            }
            else if (setting.RegistrySettings.Count > 1)
            {
                var linkedSettings = setting.CreateLinkedRegistrySettings();
                await _registryService.ApplyLinkedSettingsAsync(linkedSettings, enable);
            }
        }

        private object GetComboBoxRegistryValue(RegistrySetting registrySetting, object selectedValue)
        {
            if (registrySetting.CustomProperties?.TryGetValue("ComboBoxOptions", out var optionsObj) == true &&
                selectedValue?.ToString() is string selectedString)
            {
                // Handle both Dictionary<string, int> and Dictionary<string, object> formats
                if (optionsObj is Dictionary<string, int> intOptions &&
                    intOptions.TryGetValue(selectedString, out var intValue))
                {
                    return intValue;
                }
                else if (optionsObj is Dictionary<string, object> objectOptions &&
                    objectOptions.TryGetValue(selectedString, out var objectValue))
                {
                    return objectValue ?? registrySetting.DefaultValue ?? 0;
                }
            }

            // Fallback to default value
            return registrySetting.DefaultValue ?? 0;
        }

        #endregion

        #region Category-Specific Methods

        public async Task<IEnumerable<ApplicationSetting>> GetGamingAndPerformanceSettingsAsync()
        {
            // Return cached settings filtered by category
            var settings = _settingsCache.Values
                .Where(s => s is OptimizationSetting opt && opt.Category == OptimizationCategory.GamingandPerformance)
                .ToList();

            _logService.Log(LogLevel.Debug, $"Found {settings.Count} Gaming and Performance settings in cache");
            return settings;
        }

        public async Task<IEnumerable<ApplicationSetting>> GetStartMenuSettingsAsync()
        {
            var settings = _settingsCache.Values
                .Where(s => s is CustomizationSetting cust && cust.Category == CustomizationCategory.StartMenu)
                .ToList();

            // Filter based on build number requirements
            var filteredSettings = FilterSettingsByBuildNumber(settings);

            _logService.Log(LogLevel.Debug, $"Found {settings.Count} Start Menu settings in cache, {filteredSettings.Count()} after build filtering");
            return filteredSettings;
        }

        /// <summary>
        /// Filters settings based on MinimumBuildNumber and MaximumBuildNumber requirements.
        /// </summary>
        /// <param name="settings">The settings to filter.</param>
        /// <returns>Filtered settings that meet build number requirements.</returns>
        private IEnumerable<ApplicationSetting> FilterSettingsByBuildNumber(IEnumerable<ApplicationSetting> settings)
        {
            try
            {
                var currentBuildNumber = _systemServices.GetWindowsBuildNumber();
                _logService.Log(LogLevel.Debug, $"Current Windows build number: {currentBuildNumber}");

                var filteredSettings = new List<ApplicationSetting>();

                foreach (var setting in settings)
                {
                    if (setting is CustomizationSetting customSetting)
                    {
                        // Check minimum build number requirement
                        if (customSetting.MinimumBuildNumber.HasValue && currentBuildNumber < customSetting.MinimumBuildNumber.Value)
                        {
                            _logService.Log(LogLevel.Debug, $"Filtering out setting '{setting.Name}' - requires minimum build {customSetting.MinimumBuildNumber.Value}, current: {currentBuildNumber}");
                            continue;
                        }

                        // Check maximum build number requirement
                        if (customSetting.MaximumBuildNumber.HasValue && currentBuildNumber > customSetting.MaximumBuildNumber.Value)
                        {
                            _logService.Log(LogLevel.Debug, $"Filtering out setting '{setting.Name}' - maximum build {customSetting.MaximumBuildNumber.Value}, current: {currentBuildNumber}");
                            continue;
                        }

                        // Check Windows 11 requirement
                        if (customSetting.IsWindows11Only && !_systemServices.IsWindows11())
                        {
                            _logService.Log(LogLevel.Debug, $"Filtering out setting '{setting.Name}' - requires Windows 11");
                            continue;
                        }
                    }

                    // Setting passes all filters
                    filteredSettings.Add(setting);
                }

                return filteredSettings;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error filtering settings by build number: {ex.Message}");
                // Return original settings if filtering fails
                return settings;
            }
        }

        public async Task<IEnumerable<ApplicationSetting>> GetTaskbarSettingsAsync()
        {
            var settings = _settingsCache.Values
                .Where(s => s is CustomizationSetting cust && cust.Category == CustomizationCategory.Taskbar)
                .ToList();

            // Filter based on build number requirements
            var filteredSettings = FilterSettingsByBuildNumber(settings);

            _logService.Log(LogLevel.Debug, $"Found {settings.Count} Taskbar settings in cache, {filteredSettings.Count()} after build filtering");
            return filteredSettings;
        }

        public async Task<IEnumerable<ApplicationSetting>> GetExplorerSettingsAsync()
        {
            var settings = _settingsCache.Values
                .Where(s => s is CustomizationSetting cust && cust.Category == CustomizationCategory.Explorer)
                .ToList();

            // Filter based on build number requirements
            var filteredSettings = FilterSettingsByBuildNumber(settings);

            _logService.Log(LogLevel.Debug, $"Found {settings.Count} Explorer settings in cache, {filteredSettings.Count()} after build filtering");
            return filteredSettings;
        }

        public async Task<IEnumerable<ApplicationSetting>> GetThemeSettingsAsync()
        {
            var settings = _settingsCache.Values
                .Where(s => s is CustomizationSetting cust && cust.Category == CustomizationCategory.WindowsTheme)
                .ToList();

            // Filter based on build number requirements
            var filteredSettings = FilterSettingsByBuildNumber(settings);

            _logService.Log(LogLevel.Debug, $"Found {settings.Count} Theme settings in cache, {filteredSettings.Count()} after build filtering");
            return filteredSettings;
        }

        public async Task<IEnumerable<ApplicationSetting>> GetExplorerOptimizationSettingsAsync()
        {
            var settings = _settingsCache.Values
                .Where(s => s is OptimizationSetting opt && opt.Category == OptimizationCategory.Explorer)
                .ToList();

            _logService.Log(LogLevel.Debug, $"Found {settings.Count} Explorer optimization settings in cache");
            return settings;
        }

        public async Task<IEnumerable<ApplicationSetting>> GetPrivacyOptimizationSettingsAsync()
        {
            var settings = _settingsCache.Values
                .Where(s => s is OptimizationSetting opt && opt.Category == OptimizationCategory.Privacy)
                .ToList();

            _logService.Log(LogLevel.Debug, $"Found {settings.Count} Privacy optimization settings in cache");
            return settings;
        }

        public async Task<IEnumerable<ApplicationSetting>> GetUpdateOptimizationSettingsAsync()
        {
            var settings = _settingsCache.Values
                .Where(s => s is OptimizationSetting opt && opt.Category == OptimizationCategory.Updates)
                .ToList();

            _logService.Log(LogLevel.Debug, $"Found {settings.Count} Update optimization settings in cache");
            return settings;
        }

        public async Task<IEnumerable<ApplicationSetting>> GetPowerOptimizationSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading power optimization settings from advanced power settings");
                
                // Use advanced power settings as the single source of truth
                var advancedGroups = await GetAdvancedPowerSettingGroupsAsync();
                var settings = new List<ApplicationSetting>();
                
                foreach (var groupObj in advancedGroups)
                {
                    if (groupObj is AdvancedPowerSettingGroup group)
                    {
                        foreach (var advancedSetting in group.Settings)
                        {
                            var optimizationSetting = ConvertAdvancedToOptimizationSetting(advancedSetting, group.DisplayName);
                            if (optimizationSetting != null)
                            {
                                settings.Add(optimizationSetting);
                            }
                        }
                    }
                }
                
                _logService.Log(LogLevel.Info, $"Converted {settings.Count} advanced power settings to optimization settings");
                
                // Register the converted settings in the cache to avoid "not found in cache" warnings
                foreach (var setting in settings)
                {
                    _settingsCache[setting.Id] = setting;
                }
                
                _logService.Log(LogLevel.Info, $"Registered {settings.Count} advanced power settings in cache");
                return settings;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading power optimization settings: {ex.Message}");
                
                // Fallback to cached basic settings if advanced conversion fails
                var fallbackSettings = _settingsCache.Values
                    .Where(s => s is OptimizationSetting opt && opt.Category == OptimizationCategory.Power)
                    .ToList();
                    
                _logService.Log(LogLevel.Warning, $"Using fallback: {fallbackSettings.Count} basic power settings from cache");
                return fallbackSettings;
            }
        }

        public async Task<IEnumerable<ApplicationSetting>> GetWindowsSecurityOptimizationSettingsAsync()
        {
            var settings = _settingsCache.Values
                .Where(s => s.GroupName?.Contains("Security", StringComparison.OrdinalIgnoreCase) == true ||
                           s.GroupName?.Contains("Windows Security", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            _logService.Log(LogLevel.Debug, $"Found {settings.Count} Windows Security optimization settings in cache");
            return settings;
        }

        public async Task<IEnumerable<ApplicationSetting>> GetNotificationOptimizationSettingsAsync()
        {
            var settings = _settingsCache.Values
                .Where(s => s is OptimizationSetting opt && opt.Category == OptimizationCategory.Notifications)
                .ToList();

            _logService.Log(LogLevel.Debug, $"Found {settings.Count} Notification optimization settings in cache");
            return settings;
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSoundOptimizationSettingsAsync()
        {
            var settings = _settingsCache.Values
                .Where(s => s is OptimizationSetting opt && opt.Category == OptimizationCategory.Sound)
                .ToList();

            _logService.Log(LogLevel.Debug, $"Found {settings.Count} Sound optimization settings in cache");
            return settings;
        }

        #endregion

        #region Action Methods

        /// <summary>
        /// Executes taskbar cleanup operation.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ExecuteTaskbarCleanupAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Starting taskbar cleanup operation");
                
                // TODO: Implement actual taskbar cleanup logic
                // This could involve:
                // - Removing pinned taskbar items
                // - Resetting taskbar settings to defaults
                // - Clearing taskbar cache
                
                await Task.Delay(100); // Placeholder for actual work
                
                _logService.Log(LogLevel.Info, "Taskbar cleanup completed successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error during taskbar cleanup: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Executes explorer action asynchronously.
        /// </summary>
        /// <param name="actionId">The action identifier.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ExecuteExplorerActionAsync(string actionId)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Executing explorer action: {actionId}");
                
                // TODO: Implement actual explorer action logic
                // This could involve:
                // - Restarting explorer.exe
                // - Clearing explorer cache
                // - Resetting explorer settings
                // - Executing specific explorer commands
                
                await Task.Delay(100); // Placeholder for actual work
                
                _logService.Log(LogLevel.Info, $"Explorer action '{actionId}' completed successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error executing explorer action '{actionId}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets all Windows theme customization settings.
        /// </summary>
        /// <returns>Collection of Windows theme settings.</returns>
        public async Task<IEnumerable<ApplicationSetting>> GetWindowsThemeSettingsAsync()
        {
            var settings = _settingsCache.Values
                .Where(s => s is CustomizationSetting cust && cust.Category == CustomizationCategory.WindowsTheme)
                .ToList();

            _logService.Log(LogLevel.Debug, $"Found {settings.Count} Windows Theme settings in cache");
            return settings;
        }

        /// <summary>
        /// Gets the current theme state from the system.
        /// </summary>
        /// <returns>The current theme state.</returns>
        public async Task<string> GetCurrentThemeStateAsync()
        {
            try
            {
                // TODO: Implement actual theme detection logic
                // This could involve checking registry values for:
                // - Dark/Light mode
                // - High contrast mode
                // - Color scheme
                
                await Task.Delay(10); // Placeholder for actual work
                
                return "Light"; // Default placeholder
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting current theme state: {ex.Message}");
                return "Unknown";
            }
        }

        /// <summary>
        /// Gets all advanced power setting groups with their settings.
        /// </summary>
        /// <returns>Collection of advanced power setting groups.</returns>
        public async Task<IEnumerable<object>> GetAdvancedPowerSettingGroupsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading advanced power setting groups");
                
                // Get all subgroups from PowerOptimizations
                var allSubgroups = PowerOptimizations.GetAllSubgroups();
                var advancedGroups = new List<AdvancedPowerSettingGroup>();
                
                // Get current active power plan for loading values
                var activePlan = await GetActivePowerPlanAsync() as PowerPlan;
                if (activePlan == null)
                {
                    _logService.Log(LogLevel.Warning, "No active power plan found for advanced settings");
                    return advancedGroups;
                }
                
                foreach (var subgroup in allSubgroups)
                {
                    var group = new AdvancedPowerSettingGroup
                    {
                        Subgroup = subgroup,
                        IsExpanded = true
                    };
                    
                    // Add settings to the group
                    foreach (var settingDef in subgroup.Settings)
                    {
                        var setting = new AdvancedPowerSetting
                        {
                            Definition = settingDef
                        };
                        
                        // Set default values
                        setting.AcValue = 0;
                        setting.DcValue = 0;
                        
                        group.Settings.Add(setting);
                    }
                    
                    advancedGroups.Add(group);
                }
                
                _logService.Log(LogLevel.Info, $"Loaded {advancedGroups.Count} advanced power setting groups");
                return advancedGroups;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading advanced power setting groups: {ex.Message}");
                return new List<AdvancedPowerSettingGroup>();
            }
        }
        
        /// <summary>
        /// Gets all available power plans.
        /// </summary>
        /// <returns>Collection of available power plans.</returns>
        public async Task<IEnumerable<object>> GetAvailablePowerPlansAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading available power plans");
                
                // Fallback to static power plans
                var fallbackPlans = PowerPlans.GetAllPowerPlans();
                _logService.Log(LogLevel.Info, $"Using fallback power plans: {fallbackPlans.Count}");
                return fallbackPlans;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading available power plans: {ex.Message}");
                return PowerPlans.GetAllPowerPlans();
            }
        }
        
        /// <summary>
        /// Gets the currently active power plan.
        /// </summary>
        /// <returns>The active power plan, or null if none found.</returns>
        public async Task<object?> GetActivePowerPlanAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Getting active power plan");
                
                // Fallback to balanced plan
                _logService.Log(LogLevel.Info, "Using Balanced as default active power plan");
                return PowerPlans.Balanced;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting active power plan: {ex.Message}");
                return PowerPlans.Balanced;
            }
        }
        
        /// <summary>
        /// Applies an advanced power setting value.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <param name="acValue">The AC power value.</param>
        /// <param name="dcValue">The DC power value.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ApplyAdvancedPowerSettingAsync(string powerPlanGuid, string subgroupGuid, string settingGuid, int acValue, int dcValue)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying advanced power setting: Plan={powerPlanGuid}, Subgroup={subgroupGuid}, Setting={settingGuid}, AC={acValue}, DC={dcValue}");
                
                // Placeholder for actual implementation
                await Task.Delay(10);
                
                _logService.Log(LogLevel.Info, "Advanced power setting applied (placeholder)");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying advanced power setting: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Checks system capabilities for power management (battery, lid detection).
        /// </summary>
        /// <returns>Dictionary with capability information.</returns>
        public async Task<Dictionary<string, bool>> CheckPowerSystemCapabilitiesAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Checking power system capabilities");
                
                var capabilities = new Dictionary<string, bool>
                {
                    ["HasBattery"] = false,
                    ["HasLid"] = false
                };
                
                // For now, assume desktop systems don't have battery/lid
                // This can be enhanced later with proper system detection
                await Task.Delay(10);
                
                _logService.Log(LogLevel.Info, $"System capabilities: Battery={capabilities["HasBattery"]}, Lid={capabilities["HasLid"]}");
                return capabilities;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking power system capabilities: {ex.Message}");
                return new Dictionary<string, bool>
                {
                    ["HasBattery"] = false,
                    ["HasLid"] = false
                };
            }
        }

        /// <summary>
        /// Converts an AdvancedPowerSetting to an OptimizationSetting for UI consistency.
        /// </summary>
        /// <param name="advancedSetting">The advanced power setting to convert.</param>
        /// <param name="groupName">The group name for the setting.</param>
        /// <returns>The converted OptimizationSetting, or null if conversion fails.</returns>
        private OptimizationSetting? ConvertAdvancedToOptimizationSetting(AdvancedPowerSetting advancedSetting, string groupName)
        {
            try
            {
                if (advancedSetting?.Definition == null)
                {
                    _logService.Log(LogLevel.Warning, "Cannot convert null advanced setting or definition");
                    return null;
                }

                var definition = advancedSetting.Definition;
                var controlType = DetermineControlType(definition);
                
                var optimizationSetting = new OptimizationSetting
                {
                    Id = $"power-advanced-{definition.Alias}",
                    Name = definition.DisplayName,
                    Description = definition.Description,
                    GroupName = groupName,
                    Category = OptimizationCategory.Power,
                    ControlType = controlType,
                    IsEnabled = false, // Will be determined by current power plan values
                    CommandSettings = CreatePowerCommandSettings(definition),
                    CustomProperties = new Dictionary<string, object>()
                };
                
                // Add custom properties based on setting type
                var customProps = optimizationSetting.CustomProperties;
                
                // For ComboBox controls, add options based on possible values
                if (controlType == ControlType.ComboBox && definition.PossibleValues?.Count > 0)
                {
                    // Store ComboBox options in custom properties for later use
                    customProps["ComboBoxOptions"] = definition.PossibleValues.ToDictionary(
                        pv => pv.FriendlyName, 
                        pv => (object)pv.Index
                    );
                    customProps["DefaultValue"] = definition.PossibleValues.FirstOrDefault()?.Index ?? 0;
                }
                else if (definition.SettingType == PowerSettingType.Numeric)
                {
                    // Store numeric range information
                    customProps["MinValue"] = definition.MinValue;
                    customProps["MaxValue"] = definition.MaxValue;
                    customProps["Units"] = definition.Units ?? string.Empty;
                }
                
                // Always store reference to advanced power setting
                customProps["AdvancedPowerSetting"] = advancedSetting;

                _logService.Log(LogLevel.Debug, $"Converted advanced power setting '{definition.DisplayName}' to optimization setting with control type {controlType}");
                return optimizationSetting;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error converting advanced power setting to optimization setting: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Determines the appropriate control type for a power setting definition.
        /// </summary>
        /// <param name="definition">The power setting definition.</param>
        /// <returns>The appropriate control type.</returns>
        private ControlType DetermineControlType(PowerSettingDefinition definition)
        {
            return definition.SettingType switch
            {
                PowerSettingType.Enum when definition.PossibleValues?.Count > 0 => ControlType.ComboBox,
                PowerSettingType.Numeric when definition.UseTimeIntervals => ControlType.ComboBox, // Time intervals as ComboBox
                PowerSettingType.Numeric => ControlType.NumericUpDown,
                PowerSettingType.Boolean => ControlType.BinaryToggle,
                _ => ControlType.ComboBox // Default to ComboBox for advanced settings
            };
        }

        /// <summary>
        /// Creates command settings for power configuration.
        /// </summary>
        /// <param name="definition">The power setting definition.</param>
        /// <returns>List of command settings for power management.</returns>
        private List<CommandSetting> CreatePowerCommandSettings(PowerSettingDefinition definition)
        {
            var commands = new List<CommandSetting>();

            // Handle custom commands (like hibernation)
            if (definition.CustomCommand && !string.IsNullOrEmpty(definition.CustomCommandTemplate))
            {
                commands.Add(new CommandSetting
                {
                    Id = $"power-{definition.Alias}-custom",
                    Category = "Power",
                    EnabledCommand = $"powercfg {definition.CustomCommandTemplate.Replace("{0}", "on")}",
                    DisabledCommand = $"powercfg {definition.CustomCommandTemplate.Replace("{0}", "off")}",
                    Description = $"Toggle {definition.DisplayName}"
                });
            }
            else
            {
                // Standard powercfg commands for AC and DC power
                commands.Add(new CommandSetting
                {
                    Id = $"power-{definition.Alias}-ac",
                    Category = "Power",
                    EnabledCommand = $"powercfg /setacvalueindex SCHEME_CURRENT {definition.SubgroupGuid} {definition.Guid} 1",
                    DisabledCommand = $"powercfg /setacvalueindex SCHEME_CURRENT {definition.SubgroupGuid} {definition.Guid} 0",
                    Description = $"Set {definition.DisplayName} on AC power"
                });

                commands.Add(new CommandSetting
                {
                    Id = $"power-{definition.Alias}-dc",
                    Category = "Power",
                    EnabledCommand = $"powercfg /setdcvalueindex SCHEME_CURRENT {definition.SubgroupGuid} {definition.Guid} 1",
                    DisabledCommand = $"powercfg /setdcvalueindex SCHEME_CURRENT {definition.SubgroupGuid} {definition.Guid} 0",
                    Description = $"Set {definition.DisplayName} on battery power"
                });

                commands.Add(new CommandSetting
                {
                    Id = $"power-{definition.Alias}-apply",
                    Category = "Power",
                    EnabledCommand = "powercfg /setactive SCHEME_CURRENT",
                    DisabledCommand = "powercfg /setactive SCHEME_CURRENT",
                    Description = "Apply power plan changes"
                });
            }

            return commands;
        }

        #endregion
    }
}
