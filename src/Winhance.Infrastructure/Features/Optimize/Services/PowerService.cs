using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    /// <summary>
    /// Unified service implementation for managing power management optimization settings.
    /// Handles power plans, advanced power settings, battery detection, and power-related optimizations.
    /// Consolidates functionality from PowerPlanService, PowerSettingService, and PowerPlanManagerService.
    /// </summary>
    public class PowerService : IPowerService, IPowerSettingService
    {
        private readonly SystemSettingOrchestrator _orchestrator;
        private readonly ILogService _logService;
        private readonly IComboBoxValueResolver _comboBoxResolver;
        private readonly ICommandService _commandService;

        /// <summary>
        /// Gets the domain name for Power optimizations.
        /// </summary>
        public string DomainName => "Power";

        private readonly IBatteryService _batteryService;
        private readonly IPowerShellExecutionService _powerShellService;
        private string? _cachedActivePowerPlanGuid;

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerService"/> class.
        /// Uses composition with SystemSettingOrchestrator and dedicated services.
        /// </summary>
        public PowerService(
            SystemSettingOrchestrator orchestrator,
            ILogService logService,
            IComboBoxValueResolver comboBoxResolver,
            ICommandService commandService,
            IBatteryService batteryService,
            IPowerShellExecutionService powerShellService
        )
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _comboBoxResolver =
                comboBoxResolver ?? throw new ArgumentNullException(nameof(comboBoxResolver));
            _commandService =
                commandService ?? throw new ArgumentNullException(nameof(commandService));
            _batteryService =
                batteryService ?? throw new ArgumentNullException(nameof(batteryService));
            _powerShellService =
                powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
        }

        /// <summary>
        /// Gets all Power optimization settings with their current system state, filtered by system capabilities.
        /// </summary>
        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Power optimization settings");

                // Use filtered subgroups that only show available settings for this system
                var filteredSubgroups = await GetFilteredSubgroupsAsync();
                var filteredSettings = new List<OptimizationSetting>();

                // Convert filtered subgroups to optimization settings
                foreach (var subgroup in filteredSubgroups)
                {
                    foreach (var setting in subgroup.Settings)
                    {
                        var optimizationSetting = PowerOptimizations.ConvertToOptimizationSetting(
                            setting
                        );
                        if (optimizationSetting != null)
                        {
                            filteredSettings.Add(optimizationSetting);
                        }
                    }
                }

                return await _orchestrator.GetSettingsWithSystemStateAsync(
                    filteredSettings,
                    DomainName
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error loading Power optimization settings: {ex.Message}"
                );
                return Enumerable.Empty<ApplicationSetting>();
            }
        }

        /// <summary>
        /// Applies a setting.
        /// </summary>
        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            var settings = await GetRawSettingsAsync();
            await _orchestrator.ApplySettingAsync(settingId, enable, value, settings, DomainName);
        }

        /// <summary>
        /// Checks if a setting is enabled.
        /// </summary>
        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _orchestrator.GetSettingStatusAsync(settingId, settings);
        }

        /// <summary>
        /// Gets the current value of a setting.
        /// </summary>
        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _orchestrator.GetSettingValueAsync(settingId, settings);
        }

        /// <summary>
        /// Helper method to get raw settings without system state.
        /// </summary>
        private async Task<IEnumerable<ApplicationSetting>> GetRawSettingsAsync()
        {
            // Use filtered subgroups that only show available settings for this system
            var filteredSubgroups = await GetFilteredSubgroupsAsync();
            var filteredSettings = new List<OptimizationSetting>();

            // Convert filtered subgroups to optimization settings
            foreach (var subgroup in filteredSubgroups)
            {
                foreach (var setting in subgroup.Settings)
                {
                    var optimizationSetting = PowerOptimizations.ConvertToOptimizationSetting(
                        setting
                    );
                    if (optimizationSetting != null)
                    {
                        filteredSettings.Add(optimizationSetting);
                    }
                }
            }

            return await GetPowerSettingsWithSystemStateAsync(
                filteredSettings.AsEnumerable<ApplicationSetting>()
            );
        }

        public async Task<PowerPlan?> GetActivePowerPlanAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Getting active power plan");

                // Use command service to get active power plan
                var result = await _commandService.ExecuteCommandAsync("powercfg /getactivescheme");
                if (result.Success && !string.IsNullOrEmpty(result.Output))
                {
                    // Extract GUID and name from output like "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)"
                    var guidMatch = System.Text.RegularExpressions.Regex.Match(
                        result.Output,
                        @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})"
                    );
                    var nameMatch = System.Text.RegularExpressions.Regex.Match(
                        result.Output,
                        @"\((.+?)\)"
                    );

                    if (guidMatch.Success)
                    {
                        string name = nameMatch.Success
                            ? nameMatch.Groups[1].Value.Trim()
                            : "Unknown";

                        return new PowerPlan
                        {
                            Guid = guidMatch.Value, // Keep GUID clean
                            Name = $"{name} [Active]", // Add [Active] suffix to display name
                            IsActive = true,
                        };
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting active power plan: {ex.Message}");
                return null;
            }
        }

        public async Task<Dictionary<string, bool>> CheckPowerSystemCapabilitiesAsync()
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    "Checking power system capabilities using battery service"
                );

                var capabilities = new Dictionary<string, bool>();

                // Use IBatteryService for accurate battery detection
                capabilities["HasBattery"] = await _batteryService.HasBatteryAsync();

                // Use IBatteryService for lid detection
                capabilities["HasLidDetection"] = await _batteryService.HasLidAsync();

                // Check for hibernate support
                var hibernateCommand = "powercfg /availablesleepstates";
                try
                {
                    var result = await _commandService.ExecuteCommandAsync(hibernateCommand);
                    capabilities["SupportsHibernate"] =
                        result.Success && result.Output?.Contains("Hibernate") == true;
                }
                catch
                {
                    capabilities["SupportsHibernate"] = false;
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Power capabilities: Battery={capabilities["HasBattery"]}, "
                        + $"Lid={capabilities["HasLidDetection"]}, "
                        + $"Hibernate={capabilities["SupportsHibernate"]}"
                );

                return capabilities;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error checking power system capabilities: {ex.Message}"
                );
                return new Dictionary<string, bool>();
            }
        }

        public async Task ApplyAdvancedPowerSettingAsync(
            string powerPlanGuid,
            string subgroupGuid,
            string settingGuid,
            int acValue,
            int dcValue
        )
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"[PowerService] Applying advanced power setting: {settingGuid} (AC: {acValue}, DC: {dcValue}) to plan {powerPlanGuid}"
                );

                var settingValue = new PowerSettingApplyValue
                {
                    PowerPlanGuid = powerPlanGuid,
                    SubgroupGuid = subgroupGuid,
                    SettingGuid = settingGuid,
                    AcValue = acValue,
                    DcValue = dcValue,
                    ApplyAcValue = true,
                    ApplyDcValue = true,
                };

                _logService.Log(
                    LogLevel.Debug,
                    $"[PowerService] Created PowerSettingApplyValue: Plan={settingValue.PowerPlanGuid}, Subgroup={settingValue.SubgroupGuid}, Setting={settingValue.SettingGuid}"
                );

                bool success = await ApplySettingValueAsync(settingValue);

                if (success)
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"[PowerService] Successfully applied advanced power setting: {settingGuid}"
                    );

                    // Verify the setting was actually applied by reading it back
                    try
                    {
                        var (currentAc, currentDc) = await GetSettingValueAsync(
                            powerPlanGuid,
                            subgroupGuid,
                            settingGuid
                        );
                        _logService.Log(
                            LogLevel.Info,
                            $"[PowerService] Verification: Setting {settingGuid} now has values AC={currentAc}, DC={currentDc}"
                        );
                    }
                    catch (Exception verifyEx)
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"[PowerService] Could not verify setting application: {verifyEx.Message}"
                        );
                    }
                }
                else
                {
                    _logService.Log(
                        LogLevel.Warning,
                        $"[PowerService] Failed to apply advanced power setting: {settingGuid}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"[PowerService] Error applying advanced power setting {settingGuid}: {ex.Message}"
                );
                _logService.Log(LogLevel.Error, $"[PowerService] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<IEnumerable<object>> GetAvailablePowerPlansAsync()
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    "Getting available power plans with dynamic discovery"
                );

                var result = await _commandService.ExecuteCommandAsync("powercfg /list");
                var powerPlans = new List<object>();

                if (result.Success && !string.IsNullOrEmpty(result.Output))
                {
                    // Get active power plan first to mark it correctly
                    var activePlan = await GetActivePowerPlanAsync();
                    string activePlanGuid = activePlan?.Guid ?? "";

                    var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("Power Scheme GUID:"))
                        {
                            var guidMatch = System.Text.RegularExpressions.Regex.Match(
                                line,
                                @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})"
                            );
                            var nameMatch = System.Text.RegularExpressions.Regex.Match(
                                line,
                                @"\((.+?)\)"
                            );

                            if (guidMatch.Success && nameMatch.Success)
                            {
                                string guid = guidMatch.Value;
                                string name = nameMatch.Groups[1].Value.Trim();
                                bool isActive =
                                    guid.Equals(activePlanGuid, StringComparison.OrdinalIgnoreCase)
                                    || line.Contains("*");

                                // Add [Active] suffix to the active power plan name for UI display
                                string displayName = isActive ? $"{name} [Active]" : name;

                                powerPlans.Add(
                                    new PowerPlan
                                    {
                                        Guid = guid,
                                        Name = displayName, // Use display name with [Active] suffix
                                        IsActive = isActive,
                                    }
                                );
                            }
                        }
                    }
                }

                // Sort power plans: Active first, then alphabetically
                var sortedPlans = powerPlans
                    .Cast<PowerPlan>()
                    .OrderByDescending(p => p.IsActive)
                    .ThenBy(p => p.Name.Replace(" [Active]", ""))
                    .Cast<object>()
                    .ToList();

                return sortedPlans;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error getting available power plans: {ex.Message}"
                );
                return Enumerable.Empty<object>();
            }
        }

        public async Task<
            IEnumerable<AdvancedPowerSettingGroup>
        > GetAdvancedPowerSettingGroupsAsync()
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    "Getting advanced power setting groups with current system values"
                );

                // Get active power plan first
                var activePlan = await GetActivePowerPlanAsync();
                if (activePlan == null)
                {
                    _logService.Log(
                        LogLevel.Warning,
                        "No active power plan found for advanced settings"
                    );
                    return Enumerable.Empty<AdvancedPowerSettingGroup>();
                }

                // Get filtered subgroups from catalog with system capability filtering
                var filteredSubgroups = await GetFilteredSubgroupsAsync();
                var advancedGroups = new List<AdvancedPowerSettingGroup>();

                foreach (var subgroup in filteredSubgroups)
                {
                    var systemValues = new Dictionary<string, (int ac, int dc)>();

                    // Get current system values for all settings in this subgroup
                    foreach (var settingDef in subgroup.Settings)
                    {
                        try
                        {
                            var (acValue, dcValue) = await GetSettingValueAsync(
                                activePlan.Guid,
                                subgroup.Guid,
                                settingDef.Guid
                            );

                            systemValues[settingDef.Guid] = (acValue, dcValue);

                            _logService.Log(
                                LogLevel.Debug,
                                $"Loaded system values for '{settingDef.DisplayName}' - AC: {acValue}, DC: {dcValue}"
                            );
                        }
                        catch (Exception ex)
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                $"Failed to load system value for setting '{settingDef.DisplayName}': {ex.Message}"
                            );
                            // Use default values if system query fails
                            systemValues[settingDef.Guid] = (0, 0);
                        }
                    }

                    // Create advanced power setting group with proper Definition assignment
                    var advancedGroup = PowerOptimizations.CreateAdvancedPowerSettingGroup(
                        subgroup,
                        systemValues
                    );
                    advancedGroups.Add(advancedGroup);

                    _logService.Log(
                        LogLevel.Info,
                        $"Created advanced group '{subgroup.DisplayName}' with {advancedGroup.Settings.Count} settings"
                    );
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Successfully created {advancedGroups.Count} advanced power setting groups"
                );

                return advancedGroups;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error getting advanced power setting groups: {ex.Message}"
                );
                return Enumerable.Empty<AdvancedPowerSettingGroup>();
            }
        }

        #region IPowerSettingService Implementation

        /// <summary>
        /// Gets all available power settings.
        /// </summary>
        public List<PowerSettingDefinition> GetAllSettings()
        {
            return PowerOptimizations.GetAllSettings();
        }

        /// <summary>
        /// Gets all power setting subgroups filtered by system capabilities.
        /// </summary>
        public List<PowerSettingSubgroup> GetAllSubgroups()
        {
            return PowerOptimizations.GetAllSubgroups();
        }

        /// <summary>
        /// Gets power setting subgroups filtered by system capabilities (async version).
        /// Only shows Battery and Power Buttons sections if the hardware supports them.
        /// </summary>
        public async Task<List<PowerSettingSubgroup>> GetFilteredSubgroupsAsync()
        {
            var allSubgroups = PowerOptimizations.GetAllSubgroups();
            var capabilities = await CheckPowerSystemCapabilitiesAsync();

            var filteredSubgroups = new List<PowerSettingSubgroup>();

            foreach (var subgroup in allSubgroups)
            {
                // Filter out hardware-specific subgroups based on system capabilities
                bool shouldInclude = subgroup.Alias.ToLowerInvariant() switch
                {
                    "battery" => capabilities.GetValueOrDefault("HasBattery", false),
                    "buttons" => capabilities.GetValueOrDefault("HasLidDetection", false)
                        || capabilities.GetValueOrDefault("HasBattery", false), // Show if has lid OR battery (power buttons)
                    _ => true, // Include all other subgroups
                };

                if (shouldInclude)
                {
                    // Further filter settings within subgroups that might not be available
                    var filteredSettings = await FilterSettingsAsync(
                        subgroup.Settings,
                        capabilities
                    );

                    if (filteredSettings.Count > 0)
                    {
                        var filteredSubgroup = new PowerSettingSubgroup
                        {
                            Guid = subgroup.Guid,
                            Alias = subgroup.Alias,
                            DisplayName = subgroup.DisplayName,
                            Settings = filteredSettings,
                        };
                        filteredSubgroups.Add(filteredSubgroup);
                    }
                }
            }

            return filteredSubgroups;
        }

        /// <summary>
        /// Filters individual settings based on system capabilities and availability.
        /// Uses efficient batch checking to avoid UI freezing while ensuring only valid settings are shown.
        /// </summary>
        private async Task<List<PowerSettingDefinition>> FilterSettingsAsync(
            List<PowerSettingDefinition> settings,
            Dictionary<string, bool> capabilities
        )
        {
            var filteredSettings = new List<PowerSettingDefinition>();

            // Get active power plan for availability checking
            var activePlan = await GetActivePowerPlanAsync();
            if (activePlan == null)
            {
                _logService.Log(
                    LogLevel.Warning,
                    "No active power plan found for filtering settings"
                );
                return settings; // Return all if we can't check
            }

            _logService.Log(
                LogLevel.Info,
                $"Filtering {settings.Count} power settings for system compatibility"
            );

            foreach (var setting in settings)
            {
                try
                {
                    // First, check hardware capability requirements for specific settings
                    bool hardwareSupported = IsSettingHardwareSupported(setting, capabilities);

                    if (!hardwareSupported)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            $"Excluding hardware-unsupported setting: {setting.DisplayName} [{setting.Alias}]"
                        );
                        continue;
                    }

                    // Then check if the setting actually exists on this system
                    bool settingExists = await DoesSettingExistAsync(
                        activePlan.Guid,
                        setting.SubgroupGuid,
                        setting.Guid
                    );

                    if (settingExists)
                    {
                        filteredSettings.Add(setting);
                        _logService.Log(
                            LogLevel.Debug,
                            $"Including power setting: {setting.DisplayName} [{setting.Guid}]"
                        );
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Info,
                            $"Excluding non-existent power setting: {setting.DisplayName} [{setting.Guid}]"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logService.Log(
                        LogLevel.Warning,
                        $"Error checking power setting '{setting.DisplayName}': {ex.Message} - including anyway"
                    );
                    // If we can't check, include the setting anyway to be safe
                    filteredSettings.Add(setting);
                }
            }

            _logService.Log(
                LogLevel.Info,
                $"Filtered to {filteredSettings.Count} valid power settings"
            );
            return filteredSettings;
        }

        /// <summary>
        /// Determines if a power setting is supported by the current hardware configuration.
        /// Follows SRP by isolating hardware capability logic.
        /// </summary>
        private bool IsSettingHardwareSupported(
            PowerSettingDefinition setting,
            Dictionary<string, bool> capabilities
        )
        {
            // Hardware-specific setting filtering based on aliases and descriptions
            return setting.Alias.ToLowerInvariant() switch
            {
                // Lid-specific settings - only show on systems with lids
                "lidaction" => capabilities.GetValueOrDefault("HasLidDetection", false),

                // Battery-specific settings - only show on systems with batteries
                _
                    when setting.Description?.Contains(
                        "battery",
                        StringComparison.OrdinalIgnoreCase
                    ) == true => capabilities.GetValueOrDefault("HasBattery", false),

                // Laptop lid references in description - only show on systems with lids
                _
                    when setting.Description?.Contains("lid", StringComparison.OrdinalIgnoreCase)
                        == true => capabilities.GetValueOrDefault("HasLidDetection", false),

                // Laptop-specific language in description - only show on systems with lids or batteries
                _
                    when setting.Description?.Contains("laptop", StringComparison.OrdinalIgnoreCase)
                        == true => capabilities.GetValueOrDefault("HasLidDetection", false)
                    || capabilities.GetValueOrDefault("HasBattery", false),

                // Mobile device specific settings - only show on systems with batteries
                _
                    when setting.Description?.Contains(
                        "portable",
                        StringComparison.OrdinalIgnoreCase
                    ) == true => capabilities.GetValueOrDefault("HasBattery", false),

                // All other settings are supported by default
                _ => true,
            };
        }

        /// <summary>
        /// Gets a power setting by its GUID.
        /// </summary>
        public PowerSettingDefinition? GetSettingByGuid(string guid)
        {
            return PowerOptimizations.GetSettingByGuid(guid);
        }

        /// <summary>
        /// Gets a power setting by its alias.
        /// </summary>
        public PowerSettingDefinition? GetSettingByAlias(string alias)
        {
            return PowerOptimizations.GetSettingByAlias(alias);
        }

        /// <summary>
        /// Gets the current value of a power setting for a power plan.
        /// </summary>
        public async Task<(int acValue, int dcValue)> GetSettingValueAsync(
            string powerPlanGuid,
            string subgroupGuid,
            string settingGuid
        )
        {
            // Check if this is a custom command setting
            var settingDefinition = PowerOptimizations.GetSettingByGuid(settingGuid);
            if (settingDefinition != null && settingDefinition.CustomCommand)
            {
                // For custom commands like hibernation, we need to determine the current state
                if (settingDefinition.Alias == "hibernation")
                {
                    // Run powercfg /a to check available sleep states
                    var result = await _commandService.ExecuteCommandAsync("powercfg /a");

                    // If hibernation is available, the output will contain "Hibernation"
                    bool hibernationEnabled =
                        result.Output.Contains("Hibernation", StringComparison.OrdinalIgnoreCase)
                        && !result.Output.Contains(
                            "Hibernation is not available",
                            StringComparison.OrdinalIgnoreCase
                        );

                    // Return 1 for enabled, 0 for disabled (for both AC and DC since this is a system-wide setting)
                    return (hibernationEnabled ? 1 : 0, hibernationEnabled ? 1 : 0);
                }

                // For other custom commands, default to 0
                return (0, 0);
            }

            // Standard power setting
            int acValue = await GetAcValueAsync(powerPlanGuid, subgroupGuid, settingGuid);
            int dcValue = await GetDcValueAsync(powerPlanGuid, subgroupGuid, settingGuid);
            return (acValue, dcValue);
        }

        /// <summary>
        /// Gets the current AC value of a power setting for a power plan.
        /// </summary>
        public async Task<int> GetAcValueAsync(
            string powerPlanGuid,
            string subgroupGuid,
            string settingGuid
        )
        {
            string command = $"powercfg /query {powerPlanGuid} {subgroupGuid} {settingGuid}";
            var result = await _commandService.ExecuteCommandAsync(command);

            return ParsePowerSettingValue(result.Output, "Current AC Power Setting Index:");
        }

        /// <summary>
        /// Gets the current DC value of a power setting for a power plan.
        /// </summary>
        public async Task<int> GetDcValueAsync(
            string powerPlanGuid,
            string subgroupGuid,
            string settingGuid
        )
        {
            string command = $"powercfg /query {powerPlanGuid} {subgroupGuid} {settingGuid}";
            var result = await _commandService.ExecuteCommandAsync(command);

            return ParsePowerSettingValue(result.Output, "Current DC Power Setting Index:");
        }

        /// <summary>
        /// Sets the AC value of a power setting for a power plan.
        /// </summary>
        public async Task<bool> SetAcValueAsync(
            string powerPlanGuid,
            string subgroupGuid,
            string settingGuid,
            int value
        )
        {
            string command =
                $"powercfg /setacvalueindex {powerPlanGuid} {subgroupGuid} {settingGuid} {value}";
            var result = await _commandService.ExecuteCommandAsync(command);

            // Apply changes to the active power plan
            await _commandService.ExecuteCommandAsync($"powercfg /setactive {powerPlanGuid}");

            return result.Success && !result.Output.Contains("Error");
        }

        /// <summary>
        /// Sets the DC value of a power setting for a power plan.
        /// </summary>
        public async Task<bool> SetDcValueAsync(
            string powerPlanGuid,
            string subgroupGuid,
            string settingGuid,
            int value
        )
        {
            string command =
                $"powercfg /setdcvalueindex {powerPlanGuid} {subgroupGuid} {settingGuid} {value}";
            var result = await _commandService.ExecuteCommandAsync(command);

            // Apply changes to the active power plan
            var activateResult = await _commandService.ExecuteCommandAsync(
                $"powercfg /setactive {powerPlanGuid}"
            );

            return result.Success && !result.Output.Contains("Error");
        }

        /// <summary>
        /// Applies a power setting value to a power plan.
        /// </summary>
        public async Task<bool> ApplySettingValueAsync(PowerSettingApplyValue settingValue)
        {
            // Get the setting definition to check if it's a custom command
            var settingDefinition = PowerOptimizations.GetSettingByGuid(settingValue.SettingGuid);

            // If this is a custom command setting, handle it differently
            if (settingDefinition != null && settingDefinition.CustomCommand)
            {
                // For custom commands, we typically only need to use one value (AC or DC)
                // We'll prioritize AC value if both are set
                int valueToUse = settingValue.ApplyAcValue
                    ? settingValue.AcValue
                    : settingValue.DcValue;

                // Check if we have a mapping for this value
                if (
                    settingDefinition.CustomCommandValueMap.TryGetValue(
                        valueToUse,
                        out string commandArg
                    )
                )
                {
                    // Format the command using the template and the mapped argument
                    string command = string.Format(
                        settingDefinition.CustomCommandTemplate,
                        commandArg
                    );

                    // Execute the custom command
                    var result = await _commandService.ExecuteCommandAsync(command);
                    return result.Success && !result.Output.Contains("Error");
                }

                return false;
            }

            // Standard power setting handling
            bool success = true;

            if (settingValue.ApplyAcValue)
            {
                success &= await SetAcValueAsync(
                    settingValue.PowerPlanGuid,
                    settingValue.SubgroupGuid,
                    settingValue.SettingGuid,
                    settingValue.AcValue
                );
            }

            if (settingValue.ApplyDcValue)
            {
                success &= await SetDcValueAsync(
                    settingValue.PowerPlanGuid,
                    settingValue.SubgroupGuid,
                    settingValue.SettingGuid,
                    settingValue.DcValue
                );
            }

            return success;
        }

        /// <summary>
        /// Checks if a power setting exists on the system.
        /// </summary>
        public async Task<bool> DoesSettingExistAsync(
            string powerPlanGuid,
            string subgroupGuid,
            string? settingGuid = null
        )
        {
            try
            {
                // Check if this is a custom command setting
                if (!string.IsNullOrEmpty(settingGuid))
                {
                    var settingDefinition = PowerOptimizations.GetSettingByGuid(settingGuid);
                    if (settingDefinition != null && settingDefinition.CustomCommand)
                    {
                        // For custom commands like hibernation, we consider them to exist if they're defined in our catalog
                        return true;
                    }
                }

                // Standard powercfg query approach for regular settings
                string command;

                if (string.IsNullOrEmpty(settingGuid))
                {
                    // Only check if the subgroup exists
                    command = $"powercfg /query {powerPlanGuid} {subgroupGuid}";
                }
                else
                {
                    // Check if both subgroup and setting exist
                    command = $"powercfg /query {powerPlanGuid} {subgroupGuid} {settingGuid}";
                }

                var result = await _commandService.ExecuteCommandAsync(command);

                // If the command was successful and the output doesn't contain error messages,
                // the setting exists
                return result.Success
                    && !result.Output.Contains("Error", StringComparison.OrdinalIgnoreCase)
                    && !result.Output.Contains("not valid", StringComparison.OrdinalIgnoreCase)
                    && !result.Output.Contains(
                        "does not exist",
                        StringComparison.OrdinalIgnoreCase
                    );
            }
            catch
            {
                // If an exception occurs, the setting doesn't exist or can't be accessed
                return false;
            }
        }

        /// <summary>
        /// Parses a power setting value from the powercfg output.
        /// </summary>
        private int ParsePowerSettingValue(string output, string marker)
        {
            if (string.IsNullOrEmpty(output))
                return 0;

            int index = output.IndexOf(marker);
            if (index == -1)
                return 0;

            string valueString = output.Substring(index + marker.Length).Trim();
            int endIndex = valueString.IndexOf('\r');
            if (endIndex != -1)
                valueString = valueString.Substring(0, endIndex).Trim();

            if (int.TryParse(valueString, out int value))
                return value;

            // Try to parse as hex (0x format)
            if (
                valueString.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(
                    valueString.Substring(2),
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out int hexValue
                )
            )
                return hexValue;

            return 0;
        }

        #endregion

        #region Power-Specific System State Detection

        /// <summary>
        /// Power-specific system state detection using powercfg commands.
        /// Power settings require special handling since they use powercfg instead of registry.
        /// Used internally by GetRawSettingsAsync to provide system state for power settings.
        /// </summary>
        private async Task<IEnumerable<ApplicationSetting>> GetPowerSettingsWithSystemStateAsync(
            IEnumerable<ApplicationSetting> originalSettings
        )
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    "Loading Power settings with system state via powercfg"
                );

                var activePlan = await GetActivePowerPlanAsync();
                if (activePlan == null)
                {
                    _logService.Log(LogLevel.Warning, "No active power plan found, using defaults");
                    return originalSettings;
                }

                var updatedSettings = new List<ApplicationSetting>();

                foreach (var setting in originalSettings)
                {
                    try
                    {
                        // Special handling for power plan selection
                        if (setting.Id == "active-power-plan")
                        {
                            // Map active power plan GUID to ComboBox index
                            var currentPlanIndex = activePlan.Guid switch
                            {
                                "381b4222-f694-41f0-9685-ff5bb260df2e" => 0, // Balanced
                                "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" => 1, // High Performance
                                "e9a42b02-d5df-448d-aa00-03f14749eb61" => 2, // Ultimate Performance (Standard)
                                "97bd6c8d-57c8-4c99-af6e-ff26af36fd24" => 2, // Ultimate Performance (Alternative GUID)
                                "99999999-9999-9999-9999-999999999999" => 2, // Ultimate Performance (Custom/System created)
                                _ => 0, // Default to Balanced if unknown
                            };

                            var updatedSetting = setting with
                            {
                                CurrentValue = currentPlanIndex,
                                IsInitiallyEnabled = true, // Power plan is always "enabled"
                                IsEnabled = true,
                                CustomProperties = new Dictionary<string, object>(
                                    setting.CustomProperties ?? new Dictionary<string, object>()
                                )
                                {
                                    ["ActivePowerPlan"] = activePlan.Name,
                                    ["ActivePowerPlanGuid"] = activePlan.Guid,
                                    ["CurrentPlanIndex"] = currentPlanIndex,
                                },
                            };

                            updatedSettings.Add(updatedSetting);
                            continue;
                        }

                        // Try to find the power setting definition by ID (assuming setting ID matches power setting GUID)
                        var powerSetting = GetSettingByGuid(setting.Id);

                        // DEBUGGING: Log setting lookup results
                        _logService.Log(
                            LogLevel.Info,
                            $"[PowerService] Looking up setting ID: {setting.Id}, Name: {setting.Name}, Found: {powerSetting != null}"
                        );
                        if (powerSetting != null)
                        {
                            _logService.Log(
                                LogLevel.Info,
                                $"[PowerService] Found setting: {powerSetting.DisplayName}, Units: {powerSetting.Units}, MaxValue: {powerSetting.MaxValue}"
                            );
                        }
                        else if (
                            setting.Name?.Contains("hard disk", StringComparison.OrdinalIgnoreCase)
                            == true
                        )
                        {
                            // DEBUGGING: For hard disk setting, show available GUIDs
                            var allSettings = GetAllSettings();
                            var diskSettings = allSettings.Where(s =>
                                s.DisplayName.Contains("disk", StringComparison.OrdinalIgnoreCase)
                            );
                            _logService.Log(
                                LogLevel.Error,
                                $"[PowerService] Hard disk setting not found! Available disk-related GUIDs:"
                            );
                            foreach (var diskSetting in diskSettings)
                            {
                                _logService.Log(
                                    LogLevel.Error,
                                    $"  - {diskSetting.Guid}: {diskSetting.DisplayName}"
                                );
                            }
                        }

                        if (powerSetting != null)
                        {
                            // Get current AC/DC values for this power setting
                            var (acValue, dcValue) = await GetSettingValueAsync(
                                activePlan.Guid,
                                powerSetting.SubgroupGuid,
                                powerSetting.Guid
                            );

                            // Convert system value (seconds) to UI display units if needed
                            var displayValue = ConvertFromSystemUnits(powerSetting, acValue);

                            // DEBUGGING: Log the value conversion process
                            _logService.Log(
                                LogLevel.Info,
                                $"[PowerService] Setting '{powerSetting.DisplayName}' (ID: {setting.Id}) - Raw AC: {acValue}, Converted Display: {displayValue}, Units: {powerSetting.Units}"
                            );

                            // Create updated setting with current system values
                            // CRITICAL FIX: Ensure MaxValue/MinValue are properly set for NumericUpDown controls
                            // to prevent the default Maximum=100 from capping values during binding initialization
                            var updatedSetting = setting with
                            {
                                CurrentValue = displayValue, // Use converted value for UI display
                                IsInitiallyEnabled = acValue != 0, // Non-zero typically means enabled
                                IsEnabled = acValue != 0,
                                // Store both values in custom properties for advanced scenarios
                                CustomProperties = new Dictionary<string, object>(
                                    setting.CustomProperties ?? new Dictionary<string, object>()
                                )
                                {
                                    ["CurrentAcValue"] = acValue,
                                    ["CurrentDcValue"] = dcValue,
                                    ["ActivePowerPlan"] = activePlan.Name,
                                    ["ActivePowerPlanGuid"] = activePlan.Guid,
                                    // CRITICAL: Ensure NumericUpDown constraints are available for binding
                                    ["MaxValue"] = powerSetting.MaxValue,
                                    ["MinValue"] = powerSetting.MinValue,
                                    ["Units"] = powerSetting.Units,
                                },
                            };

                            updatedSettings.Add(updatedSetting);
                        }
                        else
                        {
                            // Setting not found in power catalog, use as-is
                            _logService.Log(
                                LogLevel.Warning,
                                $"Power setting '{setting.Id}' (Name: {setting.Name}) not found in catalog, using defaults. This may cause incorrect behavior!"
                            );

                            // DEBUGGING: Special check for hard disk setting
                            if (
                                setting.Name?.Contains(
                                    "hard disk",
                                    StringComparison.OrdinalIgnoreCase
                                ) == true
                            )
                            {
                                _logService.Log(
                                    LogLevel.Error,
                                    $"CRITICAL: Hard disk setting not found in catalog! ID: {setting.Id}"
                                );
                            }

                            updatedSettings.Add(setting);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"Error loading system state for power setting '{setting.Id}': {ex.Message}"
                        );
                        // Use original setting if we can't get current state
                        updatedSettings.Add(setting);
                    }
                }

                return updatedSettings;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error loading Power settings with system state: {ex.Message}"
                );
                return originalSettings; // Fallback to original settings
            }
        }

        #endregion

        #region Power Plan Management (consolidated from removed services)

        /// <summary>
        /// Gets all available power plans.
        /// </summary>
        public async Task<List<PowerPlan>> GetPowerPlansAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Getting available power plans using PowerShell");
                var result = await _powerShellService.ExecuteScriptAsync("powercfg /list");
                return ParsePowerPlans(result);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting power plans: {ex.Message}");
                return new List<PowerPlan>();
            }
        }

        /// <summary>
        /// Gets the active power plan GUID.
        /// </summary>
        public async Task<string> GetActivePowerPlanGuidAsync()
        {
            if (!string.IsNullOrEmpty(_cachedActivePowerPlanGuid))
            {
                return _cachedActivePowerPlanGuid;
            }

            try
            {
                var result = await _powerShellService.ExecuteScriptAsync(
                    "powercfg /getactivescheme"
                );
                var powerPlans = ParsePowerPlans(result);
                var activePlan = powerPlans.FirstOrDefault();
                _cachedActivePowerPlanGuid =
                    activePlan?.Guid ?? "381b4222-f694-41f0-9685-ff5bb260df2e"; // Default to Balanced
                return _cachedActivePowerPlanGuid;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error getting active power plan GUID: {ex.Message}"
                );
                return "381b4222-f694-41f0-9685-ff5bb260df2e"; // Default to Balanced
            }
        }

        /// <summary>
        /// Sets the active power plan.
        /// </summary>
        public async Task<bool> SetActivePowerPlanAsync(string powerPlanGuid)
        {
            try
            {
                var result = await _powerShellService.ExecuteScriptAsync(
                    $"powercfg /setactive {powerPlanGuid}"
                );
                bool success = !result.Contains("Error", StringComparison.OrdinalIgnoreCase);

                if (success)
                {
                    _cachedActivePowerPlanGuid = powerPlanGuid;
                }

                return success;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error setting active power plan: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a new power plan.
        /// </summary>
        public async Task<string?> CreatePowerPlanAsync(string name, string sourceGuid)
        {
            try
            {
                var result = await _powerShellService.ExecuteScriptAsync(
                    $"powercfg /duplicatescheme {sourceGuid} {name}"
                );

                // Extract the new GUID from the output
                var match = System.Text.RegularExpressions.Regex.Match(
                    result,
                    @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})"
                );
                return match.Success ? match.Groups[1].Value : null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error creating power plan: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes a power plan.
        /// </summary>
        public async Task<bool> DeletePowerPlanAsync(string powerPlanGuid)
        {
            try
            {
                var result = await _powerShellService.ExecuteScriptAsync(
                    $"powercfg /delete {powerPlanGuid}"
                );
                return !result.Contains("Error", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error deleting power plan: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applies a dynamic power setting change based on a SettingUIItem.
        /// Handles all business logic for extracting metadata and applying the setting.
        /// </summary>
        /// <param name="settingId">The ID of the setting to apply.</param>
        /// <param name="isSelected">For toggle controls, whether the setting is selected.</param>
        /// <param name="selectedValue">For value controls, the selected value.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ApplyDynamicPowerSettingAsync(
            string settingId,
            bool isSelected,
            object? selectedValue
        )
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"[PowerService] Applying dynamic power setting change - ID: {settingId}, IsSelected: {isSelected}, Value: {selectedValue}, ValueType: {selectedValue?.GetType().Name ?? "null"}"
                );

                // Extract metadata from the setting ID
                string settingGuid;
                if (settingId.Contains('_'))
                {
                    // Handle prefixed format: "PowerSetting_GUID"
                    var parts = settingId.Split('_');
                    settingGuid = parts[^1]; // Last part is the setting GUID
                    _logService.Log(
                        LogLevel.Debug,
                        $"[PowerService] Extracted GUID from prefixed format: {settingGuid}"
                    );
                }
                else
                {
                    // Handle raw GUID format: "GUID"
                    settingGuid = settingId;
                    _logService.Log(
                        LogLevel.Debug,
                        $"[PowerService] Using raw GUID format: {settingGuid}"
                    );
                }

                // Get the setting definition to extract subgroup GUID
                var targetSetting = PowerOptimizations.GetSettingByGuid(settingGuid);
                if (targetSetting == null)
                {
                    _logService.Log(
                        LogLevel.Error,
                        $"[PowerService] Power setting with GUID '{settingGuid}' not found in catalog"
                    );
                    throw new InvalidOperationException(
                        $"Power setting with GUID '{settingGuid}' not found"
                    );
                }

                _logService.Log(
                    LogLevel.Info,
                    $"[PowerService] Found target setting: '{targetSetting.DisplayName}' [{settingGuid}], Subgroup: {targetSetting.SubgroupGuid}, ControlType: {targetSetting.ControlType}"
                );

                string subgroupGuid = targetSetting.SubgroupGuid;

                // Get active power plan
                var activePlan = await GetActivePowerPlanAsync();
                if (activePlan == null)
                {
                    _logService.Log(LogLevel.Error, "[PowerService] No active power plan found");
                    throw new InvalidOperationException("No active power plan found");
                }

                _logService.Log(
                    LogLevel.Info,
                    $"[PowerService] Using active power plan: {activePlan.Name} [{activePlan.Guid}]"
                );

                // Convert UI value to power setting value based on control type
                int valueToApply = targetSetting.ControlType switch
                {
                    ControlType.BinaryToggle => isSelected ? 1 : 0,
                    ControlType.ComboBox => selectedValue != null
                    && int.TryParse(selectedValue.ToString(), out int comboValue)
                        ? comboValue
                        : 0,
                    ControlType.NumericUpDown => ConvertNumericUpDownValue(
                        targetSetting,
                        selectedValue
                    ),
                    ControlType.Slider => selectedValue != null
                    && int.TryParse(selectedValue.ToString(), out int sliderValue)
                        ? sliderValue
                        : 0,
                    _ => 0,
                };

                _logService.Log(
                    LogLevel.Info,
                    $"[PowerService] Converting value for {targetSetting.ControlType}: '{selectedValue}' -> {valueToApply}"
                );

                // For ComboBox settings, let's validate the mapping
                if (
                    targetSetting.ControlType == ControlType.ComboBox
                    && targetSetting.PossibleValues?.Count > 0
                )
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"[PowerService] Available ComboBox values for '{targetSetting.DisplayName}':"
                    );
                    foreach (var possibleValue in targetSetting.PossibleValues)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            $"[PowerService]   Index {possibleValue.Index} = '{possibleValue.FriendlyName}'"
                        );
                    }

                    var matchingValue = targetSetting.PossibleValues.FirstOrDefault(v =>
                        v.Index == valueToApply
                    );
                    if (matchingValue != null)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            $"[PowerService] Selected value {valueToApply} maps to '{matchingValue.FriendlyName}'"
                        );
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"[PowerService] Selected value {valueToApply} does not match any defined values!"
                        );
                    }
                }

                // Verify the setting exists on this system before trying to apply it
                bool settingExists = await DoesSettingExistAsync(
                    activePlan.Guid,
                    subgroupGuid,
                    settingGuid
                );
                if (!settingExists)
                {
                    _logService.Log(
                        LogLevel.Warning,
                        $"[PowerService] Power setting {settingGuid} does not exist on this system, skipping application"
                    );
                    return;
                }

                // Apply the setting using the existing method
                _logService.Log(
                    LogLevel.Info,
                    $"[PowerService] Applying power setting: Plan={activePlan.Guid}, Subgroup={subgroupGuid}, Setting={settingGuid}, AC/DC Value={valueToApply}"
                );
                await ApplyAdvancedPowerSettingAsync(
                    activePlan.Guid,
                    subgroupGuid,
                    settingGuid,
                    valueToApply,
                    valueToApply
                );

                _logService.Log(
                    LogLevel.Info,
                    $"[PowerService] Successfully applied dynamic power setting '{targetSetting.DisplayName}' = {valueToApply}"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"[PowerService] Error applying dynamic power setting with ID '{settingId}': {ex.Message}"
                );
                _logService.Log(LogLevel.Error, $"[PowerService] Stack trace: {ex.StackTrace}");
                throw; // Re-throw to allow ViewModel to handle user notification
            }
        }

        /// <summary>
        /// Converts a NumericUpDown value from UI units to the units expected by Windows.
        /// Handles unit conversions (e.g., minutes to seconds) based on the setting definition.
        /// </summary>
        /// <param name="setting">The power setting definition containing unit information.</param>
        /// <param name="uiValue">The value from the UI.</param>
        /// <returns>The converted value in Windows-expected units.</returns>
        private int ConvertNumericUpDownValue(PowerSettingDefinition setting, object? uiValue)
        {
            if (uiValue == null || !int.TryParse(uiValue.ToString(), out int numericValue))
            {
                return 0;
            }

            // Handle unit conversions based on the setting's units
            var convertedValue = setting.Units?.ToLowerInvariant() switch
            {
                "minutes" => numericValue * 60, // Convert minutes to seconds (Windows expectation)
                "seconds" => numericValue, // Already in seconds
                "hours" => numericValue * 3600, // Convert hours to seconds
                "milliseconds" => numericValue / 1000, // Convert milliseconds to seconds
                _ => numericValue, // Default: no conversion
            };

            _logService.Log(
                LogLevel.Info,
                $"[PowerService] Unit conversion for '{setting.DisplayName}': {numericValue} {setting.Units} -> {convertedValue} seconds"
            );

            return convertedValue;
        }

        /// <summary>
        /// Converts system units (typically seconds) back to UI display units for proper UI presentation.
        /// This is the reverse of ConvertNumericUpDownValue - converts Windows values to user-friendly units.
        /// </summary>
        /// <param name="setting">The power setting definition containing unit information.</param>
        /// <param name="systemValue">The value from Windows (typically in seconds).</param>
        /// <returns>The value converted to UI display units.</returns>
        private int ConvertFromSystemUnits(PowerSettingDefinition setting, int systemValue)
        {
            if (systemValue == 0)
            {
                return 0; // Zero is zero in any unit
            }

            // Handle unit conversions based on the setting's units
            var convertedValue = setting.Units?.ToLowerInvariant() switch
            {
                "minutes" => systemValue / 60, // Convert seconds to minutes for display
                "seconds" => systemValue, // Already in seconds
                "hours" => systemValue / 3600, // Convert seconds to hours for display
                "milliseconds" => systemValue * 1000, // Convert seconds to milliseconds for display
                _ => systemValue, // Default: no conversion
            };

            _logService.Log(
                LogLevel.Info,
                $"[PowerService] Reverse unit conversion for '{setting.DisplayName}': {systemValue} seconds -> {convertedValue} {setting.Units}"
            );

            // DEBUGGING: Additional validation
            if (
                setting.Units?.ToLowerInvariant() == "minutes"
                && systemValue > 0
                && convertedValue == systemValue
            )
            {
                _logService.Log(
                    LogLevel.Warning,
                    $"[PowerService] Unit conversion may have failed - systemValue and convertedValue are the same: {systemValue}"
                );
            }

            return convertedValue;
        }

        /// <summary>
        /// Gets the available timeout options for display and sleep settings.
        /// Domain service provides business data, following clean architecture principles.
        /// </summary>
        /// <returns>Collection of timeout option display strings.</returns>
        public IEnumerable<string> GetTimeoutOptions()
        {
            // Business logic belongs in domain service, not ViewModel
            return new[]
            {
                "1 minute",
                "2 minutes",
                "3 minutes",
                "5 minutes",
                "10 minutes",
                "15 minutes",
                "20 minutes",
                "25 minutes",
                "30 minutes",
                "45 minutes",
                "1 hour",
                "2 hours",
                "3 hours",
                "4 hours",
                "5 hours",
                "Never",
            };
        }

        /// <summary>
        /// Parses power plans from powercfg output with [Active] suffix for active plan.
        /// </summary>
        private List<PowerPlan> ParsePowerPlans(string output)
        {
            var powerPlans = new List<PowerPlan>();

            if (string.IsNullOrEmpty(output))
                return powerPlans;

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.Contains("Power Scheme GUID:"))
                {
                    // Extract GUID and name from line like "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)"
                    var guidMatch = System.Text.RegularExpressions.Regex.Match(
                        line,
                        @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})"
                    );
                    var nameMatch = System.Text.RegularExpressions.Regex.Match(
                        line,
                        @"\(([^)]+)\)"
                    );

                    if (guidMatch.Success && nameMatch.Success)
                    {
                        string guid = guidMatch.Groups[1].Value;
                        string name = nameMatch.Groups[1].Value.Trim();

                        // Active plans are marked with * at the beginning of the line
                        bool isActive = line.TrimStart().StartsWith("*");

                        // Add [Active] suffix to the active power plan name for UI display
                        string displayName = isActive ? $"{name} [Active]" : name;

                        powerPlans.Add(
                            new PowerPlan
                            {
                                Guid = guid,
                                Name = displayName, // Use display name with [Active] suffix
                                IsActive = isActive,
                            }
                        );
                    }
                }
            }

            // Sort power plans: Active first, then alphabetically
            return powerPlans
                .OrderByDescending(p => p.IsActive)
                .ThenBy(p => p.Name.Replace(" [Active]", ""))
                .ToList();
        }

        #endregion
    }
}
