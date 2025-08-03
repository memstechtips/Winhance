using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    /// <summary>
    /// Service for managing Windows power plans.
    /// </summary>
    public class PowerPlanService : IPowerPlanService
    {
        private readonly IPowerShellExecutionService _powerShellService;
        private readonly ILogService _logService;
        
        // Dictionary to cache applied settings state
        private Dictionary<string, bool> AppliedSettings { get; } = new Dictionary<string, bool>();

        /// <summary>
        /// GUID for the Balanced power plan.
        /// </summary>
        public static readonly string BALANCED_PLAN_GUID = "381b4222-f694-41f0-9685-ff5bb260df2e";

        /// <summary>
        /// GUID for the High Performance power plan.
        /// </summary>
        public static readonly string HIGH_PERFORMANCE_PLAN_GUID = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

        /// <summary>
        /// GUID for the Ultimate Performance power plan.
        /// This is not readonly because it may be updated at runtime when the plan is created.
        /// </summary>
        public static string ULTIMATE_PERFORMANCE_PLAN_GUID = "e9a42b02-d5df-448d-aa00-03f14749eb61";

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerPlanService"/> class.
        /// </summary>
        /// <param name="powerShellService">The PowerShell execution service.</param>
        /// <param name="logService">The log service.</param>
        public PowerPlanService(IPowerShellExecutionService powerShellService, ILogService logService)
        {
            _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <inheritdoc/>
        public async Task<string> GetActivePowerPlanGuidAsync()
        {
            try
            {
                // Use a cancellation token with a timeout to prevent hanging
                using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var cancellationToken = cancellationTokenSource.Token;
        
                // Execute powercfg /getactivescheme to get the active power plan
                var executeTask = _powerShellService.ExecuteScriptAsync("powercfg /getactivescheme");
                
                // Wait for the task to complete with a timeout
                await Task.WhenAny(executeTask, Task.Delay(5000, cancellationToken));
                
                if (!executeTask.IsCompleted)
                {
                    _logService.Log(LogLevel.Warning, "Getting active power plan timed out, defaulting to Balanced");
                    return BALANCED_PLAN_GUID;
                }
                
                var result = await executeTask;
        
                // Parse the output to extract the GUID
                // Example output: "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)"
                if (result.Contains("GUID:"))
                {
                    int guidStart = result.IndexOf("GUID:") + 5;
                    int guidEnd = result.IndexOf("  (", guidStart);
                    if (guidEnd > guidStart)
                    {
                        string guid = result.Substring(guidStart, guidEnd - guidStart).Trim();
                        return guid;
                    }
                }
        
                _logService.Log(LogLevel.Warning, "Failed to parse active power plan GUID, defaulting to Balanced");
                return BALANCED_PLAN_GUID; // Default to Balanced if parsing fails
            }
            catch (TaskCanceledException)
            {
                _logService.Log(LogLevel.Warning, "Operation timed out while getting active power plan, defaulting to Balanced");
                return BALANCED_PLAN_GUID;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting active power plan: {ex.Message}");
                return BALANCED_PLAN_GUID; // Default to Balanced on error
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SetPowerPlanAsync(string planGuid)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Setting power plan to GUID: {planGuid}");
        
                // Use a cancellation token with a timeout to prevent hanging
                using var cancellationTokenSource = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                var cancellationToken = cancellationTokenSource.Token;
        
                // Special handling for Ultimate Performance plan
                if (planGuid == ULTIMATE_PERFORMANCE_PLAN_GUID)
                {
                    _logService.Log(LogLevel.Info, "Ultimate Performance plan selected, applying with custom GUID and settings");
                    
                    // Use the custom GUID for Ultimate Performance plan
                    const string customUltimateGuid = "99999999-9999-9999-9999-999999999999";
                    
                    try
                    {
                        // Create the plan with the custom GUID
                        var createTask = _powerShellService.ExecuteScriptAsync(
                            $"powercfg /duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 {customUltimateGuid}");
                        
                        // Wait for the task to complete with a timeout
                        await Task.WhenAny(createTask, Task.Delay(5000, cancellationToken));
                        
                        if (!createTask.IsCompleted)
                        {
                            _logService.Log(LogLevel.Warning, "Creating Ultimate Performance plan timed out");
                            return false;
                        }
                        
                        var createResult = await createTask;
                        if (createResult.Contains("Error") || createResult.Contains("error"))
                        {
                            _logService.Log(LogLevel.Error, $"Error creating Ultimate Performance plan: {createResult}");
                            return false;
                        }
                        
                        // Set it as the active plan
                        var setActiveTask = _powerShellService.ExecuteScriptAsync(
                            $"powercfg /setactive {customUltimateGuid}");
                        
                        // Wait for the task to complete with a timeout
                        await Task.WhenAny(setActiveTask, Task.Delay(5000, cancellationToken));
                        
                        if (!setActiveTask.IsCompleted)
                        {
                            _logService.Log(LogLevel.Warning, "Setting Ultimate Performance plan as active timed out");
                            return false;
                        }
                        
                        var setActiveResult = await setActiveTask;
                        if (setActiveResult.Contains("Error") || setActiveResult.Contains("error"))
                        {
                            _logService.Log(LogLevel.Error, $"Error setting Ultimate Performance plan as active: {setActiveResult}");
                            return false;
                        }
                        
                        // Apply Ultimate Performance preset settings using the new architecture
                        _logService.Log(LogLevel.Info, "Applying Ultimate Performance settings");
                        
                        // The Ultimate Performance plan has been created and activated
                        // Additional power settings will be applied through the ApplicationSettingsService
                        // when the user applies the Ultimate Performance preset
                        bool allCommandsSucceeded = true;
                        
                        if (!allCommandsSucceeded)
                        {
                            _logService.Log(LogLevel.Warning, "Some Ultimate Performance settings could not be applied");
                        }
                        
                        // Update the static GUID to use our custom one
                        ULTIMATE_PERFORMANCE_PLAN_GUID = customUltimateGuid;
                        
                        // Also update the PowerPlans class to use this new GUID
                        var field = typeof(PowerPlans).GetField("UltimatePerformance");
                        if (field != null)
                        {
                            var ultimatePerformancePlan = field.GetValue(null) as PowerPlan;
                            if (ultimatePerformancePlan != null)
                            {
                                // Use reflection to update the Guid property
                                var guidProperty = typeof(PowerPlan).GetProperty("Guid");
                                if (guidProperty != null)
                                {
                                    guidProperty.SetValue(ultimatePerformancePlan, customUltimateGuid);
                                    _logService.Log(LogLevel.Info, "Updated PowerOptimizations.PowerPlans.UltimatePerformance.Guid");
                                }
                            }
                        }
                        
                        // Verify the plan was set correctly
                        var verifyTask = GetActivePowerPlanGuidAsync();
                        
                        // Wait for the task to complete with a timeout
                        await Task.WhenAny(verifyTask, Task.Delay(3000, cancellationToken));
                        
                        if (!verifyTask.IsCompleted)
                        {
                            _logService.Log(LogLevel.Warning, "Verifying active power plan timed out");
                            return false;
                        }
                        
                        var currentPlan = await verifyTask;
                        if (currentPlan != customUltimateGuid)
                        {
                            _logService.Log(LogLevel.Warning, $"Failed to set Ultimate Performance plan. Expected: {customUltimateGuid}, Actual: {currentPlan}");
                            return false;
                        }
                        
                        _logService.Log(LogLevel.Info, $"Successfully set Ultimate Performance plan with GUID: {customUltimateGuid}");
                        return true;
                    }
                    catch (TaskCanceledException)
                    {
                        _logService.Log(LogLevel.Warning, "Operation timed out while setting Ultimate Performance plan");
                        return false;
                    }
                }
                else
                {
                    try
                    {
                        // For other plans, ensure they exist before trying to set them
                        var ensureTask = EnsurePowerPlanExistsAsync(planGuid);
                        
                        // Wait for the task to complete with a timeout
                        await Task.WhenAny(ensureTask, Task.Delay(5000, cancellationToken));
                        
                        if (!ensureTask.IsCompleted)
                        {
                            _logService.Log(LogLevel.Warning, "Ensuring power plan exists timed out");
                            return false;
                        }
                        
                        bool planExists = await ensureTask;
                        if (!planExists)
                        {
                            _logService.Log(LogLevel.Error, $"Failed to ensure power plan exists: {planGuid}");
                            return false;
                        }
                        
                        // Set the active power plan
                        var setTask = _powerShellService.ExecuteScriptAsync($"powercfg /setactive {planGuid}");
                        
                        // Wait for the task to complete with a timeout
                        await Task.WhenAny(setTask, Task.Delay(5000, cancellationToken));
                        
                        if (!setTask.IsCompleted)
                        {
                            _logService.Log(LogLevel.Warning, "Setting power plan timed out");
                            return false;
                        }
                        
                        var result = await setTask;
                        
                        // Check for errors in the result
                        if (result.Contains("Error") || result.Contains("error"))
                        {
                            _logService.Log(LogLevel.Error, $"Error setting power plan: {result}");
                            return false;
                        }
                        
                        // Verify the plan was set correctly
                        var verifyTask = GetActivePowerPlanGuidAsync();
                        
                        // Wait for the task to complete with a timeout
                        await Task.WhenAny(verifyTask, Task.Delay(3000, cancellationToken));
                        
                        if (!verifyTask.IsCompleted)
                        {
                            _logService.Log(LogLevel.Warning, "Verifying active power plan timed out");
                            return false;
                        }
                        
                        var currentPlan = await verifyTask;
                        if (currentPlan != planGuid)
                        {
                            _logService.Log(LogLevel.Warning, $"Failed to set power plan. Expected: {planGuid}, Actual: {currentPlan}");
                            return false;
                        }
                        
                        _logService.Log(LogLevel.Info, $"Successfully set power plan to GUID: {planGuid}");
                        return true;
                    }
                    catch (TaskCanceledException)
                    {
                        _logService.Log(LogLevel.Warning, "Operation timed out while setting power plan");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error setting power plan: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> EnsurePowerPlanExistsAsync(string planGuid, string sourcePlanGuid = null)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Ensuring power plan exists: {planGuid}");

                // Check if the plan exists
                var plansResult = await _powerShellService.ExecuteScriptAsync("powercfg /list");
                
                if (!plansResult.Contains(planGuid))
                {
                    _logService.Log(LogLevel.Info, $"Power plan {planGuid} does not exist, creating it");

                    // Plan doesn't exist, create it based on the plan type
                    if (planGuid == BALANCED_PLAN_GUID)
                    {
                        // Restore default schemes to ensure Balanced plan exists
                        await _powerShellService.ExecuteScriptAsync("powercfg -restoredefaultschemes");
                        _logService.Log(LogLevel.Info, "Restored default power schemes to ensure Balanced plan exists");
                    }
                    else if (planGuid == HIGH_PERFORMANCE_PLAN_GUID)
                    {
                        // Restore default schemes to ensure High Performance plan exists
                        await _powerShellService.ExecuteScriptAsync("powercfg -restoredefaultschemes");
                        _logService.Log(LogLevel.Info, "Restored default power schemes to ensure High Performance plan exists");
                    }
                    else if (planGuid == ULTIMATE_PERFORMANCE_PLAN_GUID)
                    {
                        // Ultimate Performance is a hidden power plan that needs to be created with a special command
                        // First restore default schemes to ensure we have a clean state
                        await _powerShellService.ExecuteScriptAsync("powercfg -restoredefaultschemes");
                        
                        // Create the Ultimate Performance plan using the Windows built-in command
                        // This is the official way to create this plan
                        var result = await _powerShellService.ExecuteScriptAsync("powercfg -duplicatescheme 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                        
                        // Extract the GUID of the newly created plan from the result
                        // Example output: "Power Scheme GUID: 11111111-2222-3333-4444-555555555555  (Copy of High Performance)"
                        string newGuid = string.Empty;
                        if (result.Contains("Power Scheme GUID:"))
                        {
                            int guidStart = result.IndexOf("Power Scheme GUID:") + 18;
                            int guidEnd = result.IndexOf("  (", guidStart);
                            if (guidEnd > guidStart)
                            {
                                newGuid = result.Substring(guidStart, guidEnd - guidStart).Trim();
                                _logService.Log(LogLevel.Info, $"Created new power plan with GUID: {newGuid}");
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(newGuid))
                        {
                            // Rename it to "Ultimate Performance"
                            await _powerShellService.ExecuteScriptAsync($"powercfg -changename {newGuid} \"Ultimate Performance\" \"Provides ultimate performance on Windows.\"");
                            
                            // Update our constant to use this new GUID for future operations
                            // Note: This is a static field, so it will be updated for the lifetime of the application
                            ULTIMATE_PERFORMANCE_PLAN_GUID = newGuid;
                            
                            // Also update the PowerPlans class to use this new GUID
                            // This is needed because the PowerOptimizationsViewModel uses PowerPlans
                            var field = typeof(PowerPlans).GetField("UltimatePerformance");
                            if (field != null)
                            {
                                var ultimatePerformancePlan = field.GetValue(null) as PowerPlan;
                                if (ultimatePerformancePlan != null)
                                {
                                    // Use reflection to update the Guid property
                                    var guidProperty = typeof(PowerPlan).GetProperty("Guid");
                                    if (guidProperty != null)
                                    {
                                        guidProperty.SetValue(ultimatePerformancePlan, newGuid);
                                        _logService.Log(LogLevel.Info, "Updated PowerOptimizations.PowerPlans.UltimatePerformance.Guid");
                                    }
                                }
                            }
                            
                            _logService.Log(LogLevel.Info, $"Created and renamed Ultimate Performance power plan with GUID: {newGuid}");
                            
                            // Return true since we've created the plan, but with a different GUID
                            return true;
                        }
                        
                        _logService.Log(LogLevel.Warning, "Failed to create Ultimate Performance power plan");
                    }
                    else if (sourcePlanGuid != null)
                    {
                        // Create a custom plan from the source plan
                        await _powerShellService.ExecuteScriptAsync($"powercfg /duplicatescheme {sourcePlanGuid} {planGuid}");
                        _logService.Log(LogLevel.Info, $"Created custom power plan with GUID {planGuid} from source {sourcePlanGuid}");
                    }
                    else
                    {
                        _logService.Log(LogLevel.Warning, $"Cannot create power plan with GUID {planGuid} - no source plan specified");
                        return false;
                    }
                    
                    // Verify the plan was created
                    plansResult = await _powerShellService.ExecuteScriptAsync("powercfg /list");
                    if (!plansResult.Contains(planGuid))
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to create power plan with GUID {planGuid}");
                        return false;
                    }
                }
                
                _logService.Log(LogLevel.Info, $"Power plan {planGuid} exists");
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error ensuring power plan exists: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<List<PowerPlan>> GetAvailablePowerPlansAsync()
        {
            var powerPlans = new List<PowerPlan>();
            
            try
            {
                _logService.Log(LogLevel.Info, "Getting available power plans");
                var result = await _powerShellService.ExecuteScriptAsync("powercfg /list");
                
                // Parse the output to extract power plans
                // Example output:
                // Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)
                // Power Scheme GUID: 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  (High Performance)
                
                string[] lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("Power Scheme GUID:"))
                    {
                        int guidStart = line.IndexOf("GUID:") + 5;
                        int guidEnd = line.IndexOf("  (", guidStart);
                        if (guidEnd > guidStart)
                        {
                            string guid = line.Substring(guidStart, guidEnd - guidStart).Trim();
                            
                            int nameStart = line.IndexOf("(", guidEnd) + 1;
                            int nameEnd = line.IndexOf(")", nameStart);
                            if (nameEnd > nameStart)
                            {
                                string name = line.Substring(nameStart, nameEnd - nameStart).Trim();
                                
                                powerPlans.Add(new PowerPlan { Guid = guid, Name = name });
                                _logService.Log(LogLevel.Info, $"Found power plan: {name} ({guid})");
                            }
                        }
                    }
                }
                
                return powerPlans;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting available power plans: {ex.Message}");
                return powerPlans;
            }
        }
        
        /// <inheritdoc/>
        public async Task<bool> ExecutePowerCfgCommandAsync(string command)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Executing PowerCfg command: {command}");
                
                // Execute the PowerCfg command
                var result = await _powerShellService.ExecuteScriptAsync($"powercfg {command}");
                
                // Check for errors in the result
                if (result.Contains("Error") || result.Contains("error"))
                {
                    // Check if this is a hardware-dependent setting that doesn't exist
                    if (result.Contains("specified does not exist"))
                    {
                        _logService.Log(LogLevel.Warning, $"Hardware-dependent setting not available on this system: {result}");
                        // Return true because this is an expected condition, not a failure
                        return true;
                    }
                    else
                    {
                        _logService.Log(LogLevel.Error, $"Error executing PowerCfg command: {result}");
                        return false;
                    }
                }
                
                _logService.Log(LogLevel.Info, $"PowerCfg command executed successfully: {command}");
                return true;
            }
            catch (Exception ex)
            {
                // Check if this is a hardware-dependent setting that doesn't exist
                if (ex.Message.Contains("specified does not exist"))
                {
                    _logService.Log(LogLevel.Warning, $"Hardware-dependent setting not available on this system: {ex.Message}");
                    // Return true because this is an expected condition, not a failure
                    return true;
                }
                else
                {
                    _logService.Log(LogLevel.Error, $"Error executing PowerCfg command: {ex.Message}");
                    return false;
                }
            }
        }
        
        /// <inheritdoc/>
        public async Task<bool> ApplyPowerSettingAsync(string subgroupGuid, string settingGuid, string value, bool isAcSetting)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying power setting: subgroup={subgroupGuid}, setting={settingGuid}, value={value}, isAC={isAcSetting}");
                
                // Determine the command prefix based on whether this is an AC or DC setting
                string prefix = isAcSetting ? "/setacvalueindex" : "/setdcvalueindex";
                
                // Get the active power plan GUID
                string planGuid = await GetActivePowerPlanGuidAsync();
                
                // Execute the PowerCfg command to set the value
                var result = await _powerShellService.ExecuteScriptAsync($"powercfg {prefix} {planGuid} {subgroupGuid} {settingGuid} {value}");
                
                // Check for errors in the result
                if (result.Contains("Error") || result.Contains("error"))
                {
                    // Check if this is a hardware-dependent setting that doesn't exist
                    if (result.Contains("specified does not exist"))
                    {
                        _logService.Log(LogLevel.Warning, $"Hardware-dependent setting not available on this system: subgroup={subgroupGuid}, setting={settingGuid}");
                        // Return true because this is an expected condition, not a failure
                        return true;
                    }
                    else
                    {
                        _logService.Log(LogLevel.Error, $"Error applying power setting: {result}");
                        return false;
                    }
                }
                
                _logService.Log(LogLevel.Info, $"Power setting applied successfully");
                return true;
            }
            catch (Exception ex)
            {
                // Check if this is a hardware-dependent setting that doesn't exist
                if (ex.Message.Contains("specified does not exist"))
                {
                    _logService.Log(LogLevel.Warning, $"Hardware-dependent setting not available on this system: {ex.Message}");
                    // Return true because this is an expected condition, not a failure
                    return true;
                }
                else
                {
                    _logService.Log(LogLevel.Error, $"Error applying power setting: {ex.Message}");
                    return false;
                }
            }
        }
        
        /// <inheritdoc/>
        public async Task<bool> IsPowerCfgSettingAppliedAsync(PowerCfgSetting setting)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Checking if PowerCfg setting is applied: {setting.Description}");
                
                // Extract the command type from the setting
                string command = setting.Command;
                
                // Get the active power plan GUID
                string activePlanGuid = await GetActivePowerPlanGuidAsync();
                
                // Replace placeholder GUID with active power plan GUID
                command = command.Replace("{active_guid}", activePlanGuid);
                
                // Create a unique key for this setting
                string settingKey = $"{setting.Description}:{setting.EnabledValue}";
                
                // Special handling for Desktop Slideshow setting
                if (setting.Description.Contains("desktop slideshow"))
                {
                    // Extract subgroup and setting GUIDs
                    var parts = command.Split(' ');
                    if (parts.Length >= 5)
                    {
                        string subgroupGuid = parts[2];
                        string settingGuid = parts[3];
                        string expectedValue = parts[4];
                        
                        // Query the current value
                        var result = await _powerShellService.ExecuteScriptAsync($"powercfg /query {activePlanGuid} {subgroupGuid} {settingGuid}");
                        
                        // For Desktop Slideshow, value 0 means "Available" (slideshow enabled)
                        // and value 1 means "Paused" (slideshow disabled)
                        // This is counter-intuitive, so we need special handling
                        
                        // Extract the current value
                        string currentValue = ExtractPowerSettingValue(result, command.Contains("setacvalueindex"));
                        
                        if (!string.IsNullOrEmpty(currentValue))
                        {
                            // Normalize the values for comparison
                            string normalizedCurrentValue = NormalizeHexValue(currentValue);
                            string normalizedExpectedValue = NormalizeHexValue(expectedValue);
                            
                            // For Desktop Slideshow, we need to check if the current value matches the expected value
                            // Value 0 means "Available" (slideshow enabled)
                            // Value 1 means "Paused" (slideshow disabled)
                            bool settingApplied = string.Equals(normalizedCurrentValue, normalizedExpectedValue, StringComparison.OrdinalIgnoreCase);
                            
                            _logService.Log(LogLevel.Info, $"Desktop Slideshow setting check: current={normalizedCurrentValue}, expected={normalizedExpectedValue}, isApplied={settingApplied}");
                            
                            // Cache the result
                            AppliedSettings[settingKey] = settingApplied;
                            return settingApplied;
                        }
                    }
                }
                
                if (command.Contains("hibernate"))
                {
                    // Check hibernate state
                    var result = await _powerShellService.ExecuteScriptAsync("powercfg /a");
                    
                    if (command.Contains("off"))
                    {
                        // If command is to disable hibernate, check if hibernate is not available
                        bool isHibernateDisabled = result.Contains("Hibernation has been disabled");
                        _logService.Log(LogLevel.Info, $"Hibernate state check: disabled={isHibernateDisabled}");
                        
                        // Cache the result
                        AppliedSettings[settingKey] = isHibernateDisabled;
                        return isHibernateDisabled;
                    }
                    else
                    {
                        // If command is to enable hibernate, check if hibernate is available
                        bool isHibernateEnabled = result.Contains("Hibernation") && !result.Contains("Hibernation has been disabled");
                        _logService.Log(LogLevel.Info, $"Hibernate state check: enabled={isHibernateEnabled}");
                        
                        // Cache the result
                        AppliedSettings[settingKey] = isHibernateEnabled;
                        return isHibernateEnabled;
                    }
                }
                else if (command.Contains("setacvalueindex") || command.Contains("setdcvalueindex"))
                {
                    // Extract subgroup, setting, and value GUIDs
                    var parts = command.Split(' ');
                    if (parts.Length >= 5)
                    {
                        string subgroupGuid = parts[2];
                        string settingGuid = parts[3];
                        string expectedValue = parts[4];
                        
                        // Query the current value
                        var result = await _powerShellService.ExecuteScriptAsync($"powercfg /query {activePlanGuid} {subgroupGuid} {settingGuid}");
                        
                        // Extract the current value
                        bool isAcSetting = command.Contains("setacvalueindex");
                        string currentValue = ExtractPowerSettingValue(result, isAcSetting);
                        
                        if (!string.IsNullOrEmpty(currentValue))
                        {
                            // Normalize the values for comparison
                            string normalizedCurrentValue = NormalizeHexValue(currentValue);
                            string normalizedExpectedValue = NormalizeHexValue(expectedValue);
                            
                            // Compare case-insensitive
                            bool settingApplied = string.Equals(normalizedCurrentValue, normalizedExpectedValue, StringComparison.OrdinalIgnoreCase);
                            _logService.Log(LogLevel.Info, $"PowerCfg setting check: current={normalizedCurrentValue}, expected={normalizedExpectedValue}, isApplied={settingApplied}");
                            
                            // Cache the result
                            AppliedSettings[settingKey] = settingApplied;
                            return settingApplied;
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning, $"Could not find current value in query result for {subgroupGuid} {settingGuid}");
                            
                            // For settings we can't determine, check if the setting exists in the power plan
                            var queryResult = await _powerShellService.ExecuteScriptAsync($"powercfg /query {activePlanGuid} {subgroupGuid} {settingGuid}");
                            
                            // If the query returns information about the setting, it exists
                            bool settingExists = !queryResult.Contains("does not exist") && queryResult.Contains(settingGuid);
                            
                            _logService.Log(LogLevel.Info, $"PowerCfg setting existence check: {settingExists}");
                            
                            if (settingExists)
                            {
                                // If the setting exists but we couldn't extract the value,
                                // assume it's not applied so the user can apply it
                                AppliedSettings[settingKey] = false;
                                return false;
                            }
                            else
                            {
                                // If the setting doesn't exist, it might be hardware-dependent
                                // In this case, we should return false so the user can apply it if needed
                                AppliedSettings[settingKey] = false;
                                return false;
                            }
                        }
                    }
                }
                else if (command.Contains("CHANGEPOWERPLAN"))
                {
                    // For CHANGEPOWERPLAN commands, we can't easily check the state
                    // Assume they're applied if we've tried to apply them before
                    _logService.Log(LogLevel.Info, $"CHANGEPOWERPLAN command detected, assuming applied: {setting.Description}");
                    
                    // Cache the result
                    AppliedSettings[settingKey] = true;
                    return true;
                }
                
                // For any other commands we can't determine, make a best effort check
                _logService.Log(LogLevel.Warning, $"Could not determine state for PowerCfg setting: {setting.Description}, checking via command output");
                
                // Execute a query command to get all power settings
                var allPowerSettings = await _powerShellService.ExecuteScriptAsync("powercfg /q");
                
                // Try to extract the command parameters to check if they're in the query result
                string[] cmdParts = command.Split(' ');
                bool isApplied = false;
                
                // If the command has parameters, check if they appear in the query result
                if (cmdParts.Length > 1)
                {
                    // Check if any of the command parameters appear in the query result
                    // This is a heuristic approach but better than assuming always applied
                    for (int i = 1; i < cmdParts.Length; i++)
                    {
                        if (cmdParts[i].Length > 8 && allPowerSettings.Contains(cmdParts[i]))
                        {
                            isApplied = true;
                            break;
                        }
                    }
                }
                
                // Cache the result
                AppliedSettings[settingKey] = isApplied;
                return isApplied;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking PowerCfg setting state: {ex.Message}");
                
                // On error, assume not applied so user can reapply
                return false;
            }
        }
        
        /// <summary>
        /// Extracts the power setting value from the query result.
        /// </summary>
        /// <param name="queryResult">The result of the powercfg /query command.</param>
        /// <param name="isAcSetting">Whether this is an AC setting (true) or DC setting (false).</param>
        /// <returns>The extracted value, or empty string if not found.</returns>
        private string ExtractPowerSettingValue(string queryResult, bool isAcSetting)
        {
            try
            {
                
                // Try multiple patterns to extract the current value
                
                // Pattern 1: Standard format "Current AC/DC Power Setting Index: 0x00000000"
                string acPattern = "Current AC Power Setting Index: (.+)";
                string dcPattern = "Current DC Power Setting Index: (.+)";
                
                string pattern = isAcSetting ? acPattern : dcPattern;
                var match = System.Text.RegularExpressions.Regex.Match(queryResult, pattern);
                
                if (match.Success)
                {
                    string value = match.Groups[1].Value.Trim();
                    return value;
                }
                
                // Pattern 2: Alternative format "Power Setting Index: 0x00000000"
                pattern = "Power Setting Index: (.+)";
                match = System.Text.RegularExpressions.Regex.Match(queryResult, pattern);
                
                if (match.Success)
                {
                    string value = match.Groups[1].Value.Trim();
                    return value;
                }
                
                // Pattern 3: Look for AC/DC value specifically
                if (isAcSetting)
                {
                    pattern = "AC:\\s*0x[0-9A-Fa-f]+";
                    match = System.Text.RegularExpressions.Regex.Match(queryResult, pattern);
                    
                    if (match.Success)
                    {
                        string fullMatch = match.Value.Trim();
                        string value = fullMatch.Substring(fullMatch.IndexOf("0x"));
                        return value;
                    }
                }
                else
                {
                    pattern = "DC:\\s*0x[0-9A-Fa-f]+";
                    match = System.Text.RegularExpressions.Regex.Match(queryResult, pattern);
                    
                    if (match.Success)
                    {
                        string fullMatch = match.Value.Trim();
                        string value = fullMatch.Substring(fullMatch.IndexOf("0x"));
                        return value;
                    }
                }
                
                // Pattern 4: Look for any line with a hex value
                pattern = "0x[0-9A-Fa-f]+";
                match = System.Text.RegularExpressions.Regex.Match(queryResult, pattern);
                
                if (match.Success)
                {
                    string value = match.Value.Trim();
                    return value;
                }
                
                // If no pattern matches, return empty string
                _logService.Log(LogLevel.Warning, "Could not extract power setting value from query result");
                return string.Empty;
            }
            catch (Exception ex)
            {
                // If an error occurs, log it and return empty string
                _logService.Log(LogLevel.Error, $"Error extracting power setting value: {ex.Message}");
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Normalizes a hex value for comparison.
        /// </summary>
        /// <param name="value">The hex value to normalize.</param>
        /// <returns>The normalized value.</returns>
        private string NormalizeHexValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "0";
            }
            
            // Remove 0x prefix if present
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(2);
            }
            
            // Remove leading zeros
            value = value.TrimStart('0');
            
            // If the value is empty after trimming zeros, it's zero
            if (string.IsNullOrEmpty(value))
            {
                return "0";
            }
            
            return value;
        }
        
        /// <inheritdoc/>
        public async Task<bool> AreAllPowerCfgSettingsAppliedAsync(List<PowerCfgSetting> settings)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Checking if all PowerCfg settings are applied: {settings.Count} settings");
                
                // Get the active power plan GUID once for all settings
                string activePlanGuid = await GetActivePowerPlanGuidAsync();
                _logService.Log(LogLevel.Info, $"Active power plan GUID: {activePlanGuid}");
                
                // Track which settings are not applied
                List<string> notAppliedSettings = new List<string>();
                
                foreach (var setting in settings)
                {
                    bool isApplied = await IsPowerCfgSettingAppliedAsync(setting);
                    
                    if (!isApplied)
                    {
                        _logService.Log(LogLevel.Info, $"PowerCfg setting not applied: {setting.Description}");
                        notAppliedSettings.Add(setting.Description);
                    }
                }
                
                if (notAppliedSettings.Count > 0)
                {
                    _logService.Log(LogLevel.Info, $"{notAppliedSettings.Count} PowerCfg settings not applied: {string.Join(", ", notAppliedSettings)}");
                    return false;
                }
                
                _logService.Log(LogLevel.Info, $"All PowerCfg settings are applied");
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking PowerCfg settings: {ex.Message}");
                return false;
            }
        }
        
        /// <inheritdoc/>
        public async Task<bool> ApplyPowerCfgSettingsAsync(List<PowerCfgSetting> settings)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying {settings.Count} PowerCfg settings");
                
                bool allSucceeded = true;
                
                // Get the active power plan GUID
                string activePlanGuid = await GetActivePowerPlanGuidAsync();
                _logService.Log(LogLevel.Info, $"Using active power plan GUID: {activePlanGuid}");
                
                foreach (var setting in settings)
                {
                    _logService.Log(LogLevel.Info, $"Applying PowerCfg setting: {setting.Description}");
                    
                    // Extract the command without the "powercfg " prefix
                    string command = setting.Command;
                    if (command.StartsWith("powercfg "))
                    {
                        command = command.Substring(9);
                    }
                    
                    // Replace placeholder GUID with active power plan GUID
                    command = command.Replace("{active_guid}", activePlanGuid);
                    
                    // Execute the command
                    bool success = await ExecutePowerCfgCommandAsync(command);
                    
                    if (!success)
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to apply PowerCfg setting: {setting.Description}");
                        allSucceeded = false;
                    }
                }
                
                return allSucceeded;
            }
            catch (Exception ex)
            {
                // Check if this is a hardware-dependent setting that doesn't exist
                if (ex.Message.Contains("specified does not exist"))
                {
                    _logService.Log(LogLevel.Warning, $"Hardware-dependent setting not available on this system: {ex.Message}");
                    // Return true because this is an expected condition, not a failure
                    return true;
                }
                else
                {
                    _logService.Log(LogLevel.Error, $"Error applying PowerCfg settings: {ex.Message}");
                    return false;
                }
            }
        }
    }
}