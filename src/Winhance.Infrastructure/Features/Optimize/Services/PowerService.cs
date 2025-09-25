using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Utils;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    public class PowerService(
        ILogService logService,
        ICommandService commandService,
        IPowerCfgQueryService powerCfgQueryService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        IEventBus eventBus) : IPowerService
    {
        private IEnumerable<SettingDefinition>? _cachedSettings;
        private readonly object _cacheLock = new object();

        public string DomainName => FeatureIds.Power;

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            lock (_cacheLock)
            {
                if (_cachedSettings != null)
                    return _cachedSettings;

                try
                {
                    logService.Log(LogLevel.Info, "Loading Power settings");
                    _cachedSettings = compatibleSettingsRegistry.GetFilteredSettings(FeatureIds.Power);
                    return _cachedSettings;
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Error, $"Error loading Power settings: {ex.Message}");
                    return Enumerable.Empty<SettingDefinition>();
                }
            }
        }

        public void ClearSettingsCache()
        {
            lock (_cacheLock)
            {
                _cachedSettings = null;
            }
        }


        public async Task<PowerPlan?> GetActivePowerPlanAsync()
        {
            try
            {
                return await powerCfgQueryService.GetActivePowerPlanAsync();
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Error getting active power plan: {ex.Message}");
                return null;
            }
        }

        public async Task ApplyAdvancedPowerSettingAsync(string powerPlanGuid, string subgroupGuid, string settingGuid, int acValue, int dcValue)
        {
            try
            {
                await commandService.ExecuteCommandAsync($"powercfg /setacvalueindex {powerPlanGuid} {subgroupGuid} {settingGuid} {acValue}");
                await commandService.ExecuteCommandAsync($"powercfg /setdcvalueindex {powerPlanGuid} {subgroupGuid} {settingGuid} {dcValue}");
                await commandService.ExecuteCommandAsync($"powercfg /setactive {powerPlanGuid}");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error applying advanced power setting: {ex.Message}");
                throw;
            }
        }


        public async Task<IEnumerable<object>> GetAvailablePowerPlansAsync()
        {
            try
            {
                var powerPlans = await powerCfgQueryService.GetAvailablePowerPlansAsync();
                return powerPlans.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Error getting available power plans: {ex.Message}");
                return Enumerable.Empty<object>();
            }
        }


        public async Task<bool> SetActivePowerPlanAsync(string powerPlanGuid)
        {
            try
            {
                var currentActivePlan = await powerCfgQueryService.GetActivePowerPlanAsync();
                if (currentActivePlan != null && string.Equals(currentActivePlan.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
                {
                    logService.Log(LogLevel.Info, $"Power plan {powerPlanGuid} is already active, skipping application");
                    return true;
                }

                var result = await commandService.ExecuteCommandAsync($"powercfg /setactive {powerPlanGuid}");
                
                if (result.Success)
                {
                    powerCfgQueryService.InvalidateCache();
                    ClearSettingsCache();
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Error setting active power plan: {ex.Message}");
                return false;
            }
        }

        public async Task<(int acValue, int dcValue)> GetSettingValueAsync(string powerPlanGuid, string subgroupGuid, string settingGuid)
        {
            try
            {
                var acCommand = $"powercfg /query {powerPlanGuid} {subgroupGuid} {settingGuid}";
                var result = await commandService.ExecuteCommandAsync(acCommand);

                int acValue = OutputParser.PowerCfg.ParsePowerSettingValue(result.Output, "Current AC Power Setting Index:") ?? 0;
                int dcValue = OutputParser.PowerCfg.ParsePowerSettingValue(result.Output, "Current DC Power Setting Index:") ?? 0;
                return (acValue, dcValue);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Error getting setting value: {ex.Message}");
                return (0, 0);
            }
        }

        public async Task ApplySettingWithContextAsync(string settingId, bool enable, object? value, SettingOperationContext context)
        {
            logService.Log(LogLevel.Info, $"[PowerService] Applying setting with context - Enable: {enable}");
            
            if (value is string powerPlanGuid)
            {
                var previousPlan = await GetActivePowerPlanAsync();
                
                // Check if plan exists on system first
                var availablePlans = await powerCfgQueryService.GetAvailablePowerPlansAsync();
                var planExists = availablePlans.Any(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));
                
                bool success = false;
                
                if (!planExists)
                {
                    // Try to import the missing predefined plan
                    var predefinedPlan = PowerPlanDefinitions.BuiltInPowerPlans
                        .FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));
                        
                    if (predefinedPlan != null)
                    {
                        logService.Log(LogLevel.Info, $"Power plan '{predefinedPlan.Name}' not found on system, attempting import");
                        var importResult = await ImportPowerPlanAsync(predefinedPlan);
                        
                        if (importResult.Success)
                        {
                            // Use the actual imported GUID (might differ from predefined GUID)
                            success = await SetActivePowerPlanAsync(importResult.ImportedGuid);
                            powerPlanGuid = importResult.ImportedGuid; // Update for event
                        }
                        else
                        {
                            logService.Log(LogLevel.Error, $"Failed to import power plan: {importResult.ErrorMessage}");
                            return;
                        }
                    }
                    else
                    {
                        logService.Log(LogLevel.Error, $"Unknown power plan GUID: {powerPlanGuid}");
                        return;
                    }
                }
                else
                {
                    // Plan exists, activate normally
                    success = await SetActivePowerPlanAsync(powerPlanGuid);
                }
                
                if (success)
                {
                    var planIndex = context.AdditionalParameters.TryGetValue("PlanIndex", out var indexObj) ? (int)indexObj : -1;
                    var planName = context.AdditionalParameters.TryGetValue("PlanName", out var nameObj) ? (string)nameObj : "Unknown Plan";
                    
                    eventBus.Publish(new PowerPlanChangedEvent
                    {
                        PreviousPlanGuid = previousPlan?.Guid ?? string.Empty,
                        NewPlanGuid = powerPlanGuid,
                        NewPlanName = planName,
                        NewPlanIndex = planIndex
                    });
                }
            }
            else
            {
                throw new ArgumentException($"Expected power plan GUID but got {value?.GetType()}");
            }
        }

        public async Task<Dictionary<string, int?>> RefreshCompatiblePowerSettingsAsync()
        {
            try
            {
                // Ensure settings are loaded
                await GetSettingsAsync();
                
                var powerSettings = _cachedSettings?.Where(s => s.PowerCfgSettings?.Any() == true) ?? Enumerable.Empty<SettingDefinition>();
                if (!powerSettings.Any())
                    return new Dictionary<string, int?>();

                return await powerCfgQueryService.GetCompatiblePowerSettingsStateAsync(powerSettings);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error refreshing compatible power settings: {ex.Message}");
                return new Dictionary<string, int?>();
            }
        }

        public async Task<PowerPlanImportResult> ImportPowerPlanAsync(PredefinedPowerPlan predefinedPlan)
        {
            try
            {
                if (predefinedPlan.Name == "Ultimate Performance")
                {
                    var systemPlans = await powerCfgQueryService.GetAvailablePowerPlansAsync();
                    var existingPlan = systemPlans.FirstOrDefault(p => IsUltimatePerformancePlan(p.Name));

                    if (existingPlan != null)
                    {
                        logService.Log(LogLevel.Info, $"Ultimate Performance plan already exists with GUID: {existingPlan.Guid}");
                        return new PowerPlanImportResult(true, existingPlan.Guid);
                    }

                    var existingPlanNames = new HashSet<string>(
                        systemPlans.Select(p => CleanPlanName(p.Name)),
                        StringComparer.OrdinalIgnoreCase);

                    var command = $"powercfg /duplicatescheme {predefinedPlan.Guid}";
                    var result = await commandService.ExecuteCommandAsync(command);

                    if (result.Success)
                    {
                        var actualGuid = await FindNewlyCreatedPlanGuidAsync(predefinedPlan.Name, existingPlanNames);
                        if (!string.IsNullOrEmpty(actualGuid))
                        {
                            await commandService.ExecuteCommandAsync($"powercfg /changename {actualGuid} \"{predefinedPlan.Name}\" \"{predefinedPlan.Description}\"");
                            return new PowerPlanImportResult(true, actualGuid);
                        }
                    }

                    return new PowerPlanImportResult(false, "", result.Output ?? result.Error ?? "Ultimate Performance creation failed");
                }
                else
                {
                    // Get existing plans before duplication for non-Ultimate Performance plans too
                    var systemPlans = await powerCfgQueryService.GetAvailablePowerPlansAsync();
                    var existingPlanNames = new HashSet<string>(
                        systemPlans.Select(p => CleanPlanName(p.Name)),
                        StringComparer.OrdinalIgnoreCase);

                    var directResult = await commandService.ExecuteCommandAsync($"powercfg /duplicatescheme {predefinedPlan.Guid}");
                    if (directResult.Success)
                    {
                        var actualGuid = await FindNewlyCreatedPlanGuidAsync(predefinedPlan.Name, existingPlanNames);
                        if (!string.IsNullOrEmpty(actualGuid))
                        {
                            return new PowerPlanImportResult(true, actualGuid);
                        }
                    }

                    return await SimpleBackupRestore(predefinedPlan);
                }
            }
            catch (Exception ex)
            {
                return new PowerPlanImportResult(false, "", ex.Message);
            }
        }

        private async Task<PowerPlanImportResult> SimpleBackupRestore(PredefinedPowerPlan targetPlan)
        {
            var backupDir = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Winhance\Backup\PowerPlans");

            try
            {
                await BackupCustomPlansAsync(backupDir);

                var restoreResult = await commandService.ExecuteCommandAsync("powercfg /restoredefaultschemes");
                if (!restoreResult.Success)
                    return new PowerPlanImportResult(false, "", "Failed to restore default schemes");

                await Task.Delay(1000);
                await RestoreCustomPlansAsync(backupDir);

                Directory.Delete(backupDir, true);

                var plans = await powerCfgQueryService.GetAvailablePowerPlansAsync();
                var targetGuid = plans.FirstOrDefault(p =>
                    string.Equals(CleanPlanName(p.Name), targetPlan.Name, StringComparison.OrdinalIgnoreCase))?.Guid;

                return !string.IsNullOrEmpty(targetGuid)
                    ? new PowerPlanImportResult(true, targetGuid)
                    : new PowerPlanImportResult(false, "", "Target plan not found after restore");
            }
            catch (Exception ex)
            {
                return new PowerPlanImportResult(false, "", ex.Message);
            }
        }



        private async Task BackupCustomPlansAsync(string backupFolder)
        {
            Directory.CreateDirectory(backupFolder);

            var allPlans = await powerCfgQueryService.GetAvailablePowerPlansAsync();
            var customPlans = IdentifyCustomPlans(allPlans);

            foreach (var plan in customPlans)
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"{SanitizeFilename(plan.Name)}_{timestamp}.pow";
                var filepath = Path.Combine(backupFolder, filename);

                await commandService.ExecuteCommandAsync($"powercfg /export \"{filepath}\" {plan.Guid}");
            }
        }

        private async Task RestoreCustomPlansAsync(string backupFolder)
        {
            if (!Directory.Exists(backupFolder)) return;

            var backupFiles = Directory.GetFiles(backupFolder, "*.pow");
            foreach (var file in backupFiles)
            {
                await commandService.ExecuteCommandAsync($"powercfg /import \"{file}\"");
                await Task.Delay(200);
            }

            Directory.Delete(backupFolder, true);
        }

        private List<PowerPlan> IdentifyCustomPlans(List<PowerPlan> allPlans)
        {
            var builtInGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "a1841308-3541-4fab-bc81-f71556f20b4a",
                "381b4222-f694-41f0-9685-ff5bb260df2e",
                "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
            };

            var builtInNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Power Saver", "Balanced", "High Performance"
            };

            return allPlans.Where(plan =>
                !builtInGuids.Contains(plan.Guid) ||
                !builtInNames.Contains(CleanPlanName(plan.Name))
            ).ToList();
        }

        private string SanitizeFilename(string filename)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", filename.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }

        private async Task<string> FindNewlyCreatedPlanGuidAsync(string targetPlanName, HashSet<string> existingPlanNames)
        {
            await Task.Delay(500);

            var plansAfter = await powerCfgQueryService.GetAvailablePowerPlansAsync();

            var newPlans = plansAfter.Where(p => !existingPlanNames.Contains(CleanPlanName(p.Name))).ToList();
            if (newPlans.Count > 0)
            {
                logService.Log(LogLevel.Info, $"Found newly created power plan with GUID: {newPlans[0].Guid}");
                return newPlans[0].Guid;
            }

            var matchingPlan = plansAfter.FirstOrDefault(p =>
                string.Equals(CleanPlanName(p.Name), targetPlanName, StringComparison.OrdinalIgnoreCase));

            return matchingPlan?.Guid ?? string.Empty;
        }

        private bool IsUltimatePerformancePlan(string planName)
        {
            var cleanName = CleanPlanName(planName).ToLowerInvariant();

            var knownNames = new[]
            {
                "ultimate performance",
                "rendimiento máximo",
                "prestazioni ottimali",
                "höchstleistung",
                "performances optimales",
                "desempenho máximo",
                "ultieme prestaties",
                "максимальная производительность"
            };

            if (knownNames.Contains(cleanName))
                return true;

            var ultimateWords = new[] { "ultimate", "ultieme", "máximo", "optimal", "höchst" };
            var performanceWords = new[] { "performance", "prestazioni", "leistung", "performances", "desempenho" };

            bool hasUltimateWord = ultimateWords.Any(word => cleanName.Contains(word));
            bool hasPerformanceWord = performanceWords.Any(word => cleanName.Contains(word));

            return hasUltimateWord && hasPerformanceWord;
        }

        private string CleanPlanName(string name)
        {
            return name?.Trim() ?? string.Empty;
        }

    }
}