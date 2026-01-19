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
        IEventBus eventBus,
        IWindowsRegistryService registryService,
        IPowerPlanComboBoxService powerPlanComboBoxService) : IPowerService
    {
        private IEnumerable<SettingDefinition>? _cachedSettings;
        private readonly object _cacheLock = new object();

        public string DomainName => FeatureIds.Power;

        public async Task<bool> TryApplySpecialSettingAsync(SettingDefinition setting, object value, bool additionalContext = false)
        {
            if (setting.Id == "power-plan-selection")
            {
                logService.Log(LogLevel.Info, "[PowerService] Applying power-plan-selection");

                if (value is Dictionary<string, object> planDict)
                {
                    var guid = planDict["Guid"].ToString();
                    var name = planDict["Name"].ToString();

                    logService.Log(LogLevel.Info, $"[PowerService] Config import: applying power plan {name} ({guid})");
                    await ApplyPowerPlanByGuidAsync(setting, guid, name);
                    return true;
                }

                if (value is int index)
                {
                    logService.Log(LogLevel.Info, $"[PowerService] UI selection: applying power plan at index {index}");

                    var resolution = await powerPlanComboBoxService.ResolvePowerPlanByIndexAsync(index);
                    if (!resolution.Success)
                    {
                        logService.Log(LogLevel.Error, $"[PowerService] Failed to resolve power plan index: {resolution.ErrorMessage}");
                        return false;
                    }

                    await ApplyPowerPlanSelectionAsync(setting, resolution.Guid, index, resolution.DisplayName);
                    return true;
                }

                logService.Log(LogLevel.Error, $"[PowerService] Invalid power plan value type: {value?.GetType().Name}");
                return false;
            }

            return false;
        }

        public async Task<Dictionary<string, Dictionary<string, object?>>> DiscoverSpecialSettingsAsync(IEnumerable<SettingDefinition> settings)
        {
            var results = new Dictionary<string, Dictionary<string, object?>>();

            var powerPlanSetting = settings.FirstOrDefault(s => s.Id == "power-plan-selection");
            if (powerPlanSetting != null)
            {
                var activePlan = await GetActivePowerPlanAsync();
                var rawValues = new Dictionary<string, object?>
                {
                    ["ActivePowerPlan"] = activePlan?.Name,
                    ["ActivePowerPlanGuid"] = activePlan?.Guid
                };
                results["power-plan-selection"] = rawValues;
            }

            return results;
        }

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

        public async Task<bool> DeletePowerPlanAsync(string powerPlanGuid)
        {
            try
            {
                logService.Log(LogLevel.Info, $"Attempting to delete power plan: {powerPlanGuid}");

                var activePlan = await GetActivePowerPlanAsync();
                if (activePlan != null && string.Equals(activePlan.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
                {
                    logService.Log(LogLevel.Warning, "Cannot delete active power plan");
                    return false;
                }

                var result = await commandService.ExecuteCommandAsync($"powercfg /delete {powerPlanGuid}");

                if (result.Success)
                {
                    powerCfgQueryService.InvalidateCache();
                    logService.Log(LogLevel.Info, $"Successfully deleted power plan: {powerPlanGuid}");
                    return true;
                }
                else
                {
                    logService.Log(LogLevel.Error, $"Failed to delete power plan: {powerPlanGuid}. Error: {result.Error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error deleting power plan: {ex.Message}");
                return false;
            }
        }

        private async Task ApplyPowerPlanSelectionAsync(SettingDefinition setting, string powerPlanGuid, int planIndex, string planName)
        {
            logService.Log(LogLevel.Info, $"[PowerService] Applying power plan: {planName} ({powerPlanGuid})");

            if (string.IsNullOrEmpty(powerPlanGuid))
            {
                throw new ArgumentException("Power plan GUID cannot be null or empty");
            }

            var previousPlan = await GetActivePowerPlanAsync();

            var systemPlans = await powerCfgQueryService.GetAvailablePowerPlansAsync();
            var planExists = systemPlans.Any(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

            bool success = false;

            if (!planExists)
            {
                var predefinedPlan = PowerPlanDefinitions.BuiltInPowerPlans
                    .FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

                if (predefinedPlan != null)
                {
                    logService.Log(LogLevel.Info, $"[PowerService] Plan '{predefinedPlan.Name}' not found, attempting import");
                    var importResult = await ImportPowerPlanAsync(predefinedPlan);

                    if (importResult.Success)
                    {
                        logService.Log(LogLevel.Info, $"[PowerService] Successfully imported '{predefinedPlan.Name}', activating");
                        await Task.Delay(200);

                        var activateCommand = await commandService.ExecuteCommandAsync($"powercfg /setactive {importResult.ImportedGuid}");
                        success = activateCommand.Success;

                        if (success)
                        {
                            powerCfgQueryService.InvalidateCache();
                            ClearSettingsCache();
                            logService.Log(LogLevel.Info, $"[PowerService] Successfully activated imported plan");
                        }
                        else
                        {
                            logService.Log(LogLevel.Warning, $"[PowerService] First activation failed, retrying...");
                            await Task.Delay(500);
                            activateCommand = await commandService.ExecuteCommandAsync($"powercfg /setactive {importResult.ImportedGuid}");
                            success = activateCommand.Success;

                            if (success)
                            {
                                powerCfgQueryService.InvalidateCache();
                                ClearSettingsCache();
                                logService.Log(LogLevel.Info, $"[PowerService] Successfully activated on retry");
                            }
                            else
                            {
                                logService.Log(LogLevel.Error, $"[PowerService] Failed to activate after import: {activateCommand.Error}");
                            }
                        }

                        powerPlanGuid = importResult.ImportedGuid;
                    }
                    else
                    {
                        logService.Log(LogLevel.Error, $"[PowerService] Failed to import plan: {importResult.ErrorMessage}");
                        throw new InvalidOperationException($"Failed to import power plan: {importResult.ErrorMessage}");
                    }
                }
                else
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Unknown power plan GUID: {powerPlanGuid}");
                    throw new InvalidOperationException($"Unknown power plan GUID: {powerPlanGuid}");
                }
            }
            else
            {
                success = await SetActivePowerPlanAsync(powerPlanGuid);
            }

            if (success)
            {
                logService.Log(LogLevel.Info, $"[PowerService] Publishing PowerPlanChangedEvent");

                eventBus.Publish(new PowerPlanChangedEvent
                {
                    PreviousPlanGuid = previousPlan?.Guid ?? string.Empty,
                    NewPlanGuid = powerPlanGuid,
                    NewPlanName = planName,
                    NewPlanIndex = planIndex
                });

                logService.Log(LogLevel.Info, $"[PowerService] Successfully applied power plan");
            }
        }

        private async Task ApplyPowerPlanByGuidAsync(SettingDefinition setting, string powerPlanGuid, string planName)
        {
            logService.Log(LogLevel.Info, $"[PowerService] Applying power plan by GUID: {planName} ({powerPlanGuid})");

            if (string.IsNullOrEmpty(powerPlanGuid))
            {
                throw new ArgumentException("Power plan GUID cannot be null or empty");
            }

            var previousPlan = await GetActivePowerPlanAsync();
            var systemPlans = await powerCfgQueryService.GetAvailablePowerPlansAsync();
            var planExists = systemPlans.Any(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

            bool success = false;

            if (!planExists)
            {
                logService.Log(LogLevel.Warning, $"[PowerService] Plan '{planName}' ({powerPlanGuid}) not found on system");

                var predefinedPlan = PowerPlanDefinitions.BuiltInPowerPlans
                    .FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

                if (predefinedPlan != null)
                {
                    logService.Log(LogLevel.Info, $"[PowerService] Importing predefined plan '{predefinedPlan.Name}'");
                    var importResult = await ImportPowerPlanAsync(predefinedPlan);

                    if (importResult.Success)
                    {
                        logService.Log(LogLevel.Info, "[PowerService] Successfully imported, now activating");
                        await Task.Delay(200);

                        success = await SetActivePowerPlanAsync(importResult.ImportedGuid);
                        powerPlanGuid = importResult.ImportedGuid;
                    }
                    else
                    {
                        logService.Log(LogLevel.Error, $"[PowerService] Failed to import plan: {importResult.ErrorMessage}");
                        throw new InvalidOperationException($"Failed to import power plan: {importResult.ErrorMessage}");
                    }
                }
                else
                {
                    logService.Log(LogLevel.Info, $"[PowerService] Custom power plan '{planName}' - creating by duplicating Balanced");

                    var balancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
                    var duplicateCmd = $"powercfg /duplicatescheme {balancedGuid} {powerPlanGuid}";
                    var result = await commandService.ExecuteCommandAsync(duplicateCmd);

                    if (result.Success)
                    {
                        await commandService.ExecuteCommandAsync($"powercfg /changename {powerPlanGuid} \"{planName}\"");

                        powerCfgQueryService.InvalidateCache();
                        logService.Log(LogLevel.Info, $"[PowerService] Successfully created custom plan '{planName}' with GUID {powerPlanGuid}");

                        success = await SetActivePowerPlanAsync(powerPlanGuid);
                    }
                    else
                    {
                        logService.Log(LogLevel.Error, $"[PowerService] Failed to create custom plan: {result.Error}");
                        throw new InvalidOperationException($"Failed to create custom power plan '{planName}': {result.Error}");
                    }
                }
            }
            else
            {
                success = await SetActivePowerPlanAsync(powerPlanGuid);
            }

            if (success)
            {
                var options = await powerPlanComboBoxService.GetPowerPlanOptionsAsync();
                var planIndex = options.FindIndex(o =>
                    string.Equals(o.SystemPlan?.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

                eventBus.Publish(new PowerPlanChangedEvent
                {
                    PreviousPlanGuid = previousPlan?.Guid ?? string.Empty,
                    NewPlanGuid = powerPlanGuid,
                    NewPlanName = planName,
                    NewPlanIndex = planIndex >= 0 ? planIndex : 0
                });

                logService.Log(LogLevel.Info, $"[PowerService] Successfully applied power plan '{planName}'");
            }
        }

        public async Task<Dictionary<string, int?>> RefreshCompatiblePowerSettingsAsync()
        {
            try
            {
                await GetSettingsAsync();

                var powerSettings = _cachedSettings?.Where(s => s.PowerCfgSettings?.Any() == true) ?? Enumerable.Empty<SettingDefinition>();
                if (!powerSettings.Any())
                    return new Dictionary<string, int?>();

                var allSettings = await powerCfgQueryService.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT");

                var results = new Dictionary<string, int?>();
                foreach (var setting in powerSettings)
                {
                    var powerCfgSetting = setting.PowerCfgSettings[0];
                    var key = powerCfgSetting.SettingGuid;
                    if (allSettings.TryGetValue(key, out var value))
                    {
                        results[key] = value.acValue;
                    }
                }

                return results;
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
                if (predefinedPlan.Name == "Winhance Power Plan")
                {
                    return await CreateWinhancePowerPlanAsync(predefinedPlan);
                }

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
                        powerCfgQueryService.InvalidateCache();

                        // Try to parse GUID directly from output to avoid race conditions
                        var actualGuid = OutputParser.PowerCfg.ExtractGuid(result.Output);

                        if (string.IsNullOrEmpty(actualGuid))
                        {
                            actualGuid = await FindNewlyCreatedPlanGuidAsync(predefinedPlan.Name, existingPlanNames);
                        }

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
                    var systemPlans = await powerCfgQueryService.GetAvailablePowerPlansAsync();
                    var existingPlan = systemPlans.FirstOrDefault(p =>
                        string.Equals(p.Guid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase));

                    if (existingPlan != null)
                    {
                        logService.Log(LogLevel.Info, $"Power plan '{predefinedPlan.Name}' already exists with GUID: {existingPlan.Guid}");
                        return new PowerPlanImportResult(true, existingPlan.Guid);
                    }

                    var existingPlanNames = new HashSet<string>(
                        systemPlans.Select(p => CleanPlanName(p.Name)),
                        StringComparer.OrdinalIgnoreCase);

                    logService.Log(LogLevel.Info, $"Attempting to duplicate power plan '{predefinedPlan.Name}' using GUID {predefinedPlan.Guid}");
                    var duplicateResult = await commandService.ExecuteCommandAsync($"powercfg /duplicatescheme {predefinedPlan.Guid}");

                    if (duplicateResult.Success)
                    {
                        powerCfgQueryService.InvalidateCache();

                        // Try to parse GUID directly from output to avoid race conditions
                        var actualGuid = OutputParser.PowerCfg.ExtractGuid(duplicateResult.Output);

                        if (string.IsNullOrEmpty(actualGuid))
                        {
                            actualGuid = await FindNewlyCreatedPlanGuidAsync(predefinedPlan.Name, existingPlanNames);
                        }

                        if (!string.IsNullOrEmpty(actualGuid))
                        {
                            logService.Log(LogLevel.Info, $"Successfully duplicated power plan '{predefinedPlan.Name}' with GUID: {actualGuid}");
                            return new PowerPlanImportResult(true, actualGuid);
                        }
                    }

                    logService.Log(LogLevel.Warning, $"Duplicate scheme failed for '{predefinedPlan.Name}', falling back to backup/restore method");
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

                powerCfgQueryService.InvalidateCache();

                if (Directory.Exists(backupDir))
                {
                    Directory.Delete(backupDir, true);
                }

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

        private async Task<PowerPlanImportResult> CreateWinhancePowerPlanAsync(PredefinedPowerPlan predefinedPlan)
        {
            var ultimatePerformancePlan = PowerPlanDefinitions.BuiltInPowerPlans
                .FirstOrDefault(p => p.Name == "Ultimate Performance");

            if (ultimatePerformancePlan == null)
            {
                return new PowerPlanImportResult(false, "", "Ultimate Performance plan not found");
            }

            try
            {
                var systemPlans = await powerCfgQueryService.GetAvailablePowerPlansAsync();
                var existingPlan = systemPlans.FirstOrDefault(p =>
                    string.Equals(p.Guid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase));

                if (existingPlan != null)
                {
                    logService.Log(LogLevel.Info, $"Winhance Power Plan already exists with GUID: {existingPlan.Guid}");
                    return new PowerPlanImportResult(true, existingPlan.Guid);
                }

                logService.Log(LogLevel.Info, "Creating Winhance Power Plan from Ultimate Performance");
                var duplicateResult = await commandService.ExecuteCommandAsync(
                    $"powercfg /duplicatescheme {ultimatePerformancePlan.Guid} {predefinedPlan.Guid}");

                if (!duplicateResult.Success)
                {
                    logService.Log(LogLevel.Error, $"Failed to duplicate plan: {duplicateResult.Error}");
                    return new PowerPlanImportResult(false, "", duplicateResult.Error ?? "Failed to create plan");
                }

                await commandService.ExecuteCommandAsync(
                    $"powercfg /changename {predefinedPlan.Guid} \"{predefinedPlan.Name}\" \"{predefinedPlan.Description}\"");

                await ApplyRecommendedSettingsToPlanAsync(predefinedPlan.Guid);

                powerCfgQueryService.InvalidateCache();

                logService.Log(LogLevel.Info, $"Successfully created Winhance Power Plan: {predefinedPlan.Guid}");
                return new PowerPlanImportResult(true, predefinedPlan.Guid);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error creating Winhance Power Plan: {ex.Message}");
                return new PowerPlanImportResult(false, "", ex.Message);
            }
        }

        private async Task ApplyRecommendedSettingsToPlanAsync(string planGuid)
        {
            logService.Log(LogLevel.Info, $"Applying recommended settings to plan: {planGuid}");

            try
            {
                var allSettings = await GetSettingsAsync();
                int appliedCount = 0;
                int registrySettingsCount = 0;

                foreach (var setting in allSettings)
                {
                    try
                    {
                        var powerCfgWithRecommended = setting.PowerCfgSettings?.FirstOrDefault(ps =>
                            ps.RecommendedValueAC.HasValue || ps.RecommendedValueDC.HasValue);

                        if (powerCfgWithRecommended != null)
                        {
                            var acValue = powerCfgWithRecommended.RecommendedValueAC ?? powerCfgWithRecommended.RecommendedValueDC ?? 0;
                            var dcValue = powerCfgWithRecommended.RecommendedValueDC ?? powerCfgWithRecommended.RecommendedValueAC ?? 0;

                            logService.Log(LogLevel.Debug, $"Applying {setting.Id} - AC: {acValue}, DC: {dcValue}");

                            await commandService.ExecuteCommandAsync(
                                $"powercfg /setacvalueindex {planGuid} {powerCfgWithRecommended.SubgroupGuid} {powerCfgWithRecommended.SettingGuid} {acValue}");
                            await commandService.ExecuteCommandAsync(
                                $"powercfg /setdcvalueindex {planGuid} {powerCfgWithRecommended.SubgroupGuid} {powerCfgWithRecommended.SettingGuid} {dcValue}");

                            appliedCount++;
                            continue;
                        }

                        if (setting.InputType == InputType.Selection &&
                            setting.CustomProperties?.TryGetValue("RecommendedOptionAC", out var recommendedOptionACObj) == true &&
                            setting.PowerCfgSettings?.Any() == true)
                        {
                            var recommendedOptionAC = recommendedOptionACObj.ToString();
                            var recommendedOptionDC = setting.CustomProperties.TryGetValue("RecommendedOptionDC", out var recommendedOptionDCObj)
                                ? recommendedOptionDCObj.ToString()
                                : recommendedOptionAC;

                            var displayNames = setting.CustomProperties.TryGetValue(CustomPropertyKeys.ComboBoxDisplayNames, out var displayNamesObj) &&
                                              displayNamesObj is string[] names ? names : null;

                            if (displayNames != null)
                            {
                                var indexAC = Array.IndexOf(displayNames, recommendedOptionAC);
                                var indexDC = Array.IndexOf(displayNames, recommendedOptionDC);

                                var valueMappings = setting.CustomProperties.TryGetValue(CustomPropertyKeys.ValueMappings, out var mappingsObj) &&
                                                   mappingsObj is Dictionary<int, Dictionary<string, int?>> mappings ? mappings : null;

                                if (valueMappings != null)
                                {
                                    int? acValue = null, dcValue = null;

                                    if (indexAC >= 0 && valueMappings.TryGetValue(indexAC, out var valueDictAC))
                                        acValue = valueDictAC.TryGetValue("PowerCfgValue", out var powerCfgValueAC) ? powerCfgValueAC : null;

                                    if (indexDC >= 0 && valueMappings.TryGetValue(indexDC, out var valueDictDC))
                                        dcValue = valueDictDC.TryGetValue("PowerCfgValue", out var powerCfgValueDC) ? powerCfgValueDC : null;

                                    if (acValue.HasValue && dcValue.HasValue)
                                    {
                                        var powerCfgSetting = setting.PowerCfgSettings[0];

                                        logService.Log(LogLevel.Debug, $"Applying {setting.Id} - AC: {recommendedOptionAC} ({acValue}), DC: {recommendedOptionDC} ({dcValue})");

                                        await commandService.ExecuteCommandAsync(
                                            $"powercfg /setacvalueindex {planGuid} {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {acValue}");
                                        await commandService.ExecuteCommandAsync(
                                            $"powercfg /setdcvalueindex {planGuid} {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {dcValue}");

                                        appliedCount++;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logService.Log(LogLevel.Warning, $"Failed to apply recommended setting '{setting.Id}': {ex.Message}");
                    }
                }

                foreach (var setting in allSettings)
                {
                    if (setting.RegistrySettings?.Any() == true)
                    {
                        foreach (var regSetting in setting.RegistrySettings)
                        {
                            if (regSetting.RecommendedValue != null)
                            {
                                try
                                {
                                    var success = registryService.SetValue(
                                        regSetting.KeyPath,
                                        regSetting.ValueName ?? string.Empty,
                                        regSetting.RecommendedValue,
                                        regSetting.ValueType);

                                    if (success)
                                    {
                                        registrySettingsCount++;
                                        logService.Log(LogLevel.Debug, $"Applied registry setting: {setting.Id} = {regSetting.RecommendedValue}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logService.Log(LogLevel.Warning, $"Failed to apply registry setting '{setting.Id}': {ex.Message}");
                                }
                            }
                        }
                    }

                    if (setting.CommandSettings?.Any() == true)
                    {
                        foreach (var cmdSetting in setting.CommandSettings)
                        {
                            if (cmdSetting.RecommendedState.HasValue)
                            {
                                try
                                {
                                    var command = cmdSetting.RecommendedState.Value
                                        ? cmdSetting.EnabledCommand
                                        : cmdSetting.DisabledCommand;

                                    var result = await commandService.ExecuteCommandAsync(command);
                                    if (result.Success)
                                    {
                                        registrySettingsCount++;
                                        logService.Log(LogLevel.Debug, $"Applied command setting: {setting.Id} = {cmdSetting.RecommendedState.Value}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logService.Log(LogLevel.Warning, $"Failed to apply command setting '{setting.Id}': {ex.Message}");
                                }
                            }
                        }
                    }
                }

                logService.Log(LogLevel.Info, $"Applied {appliedCount} power settings and {registrySettingsCount} registry settings to Winhance Power Plan");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error applying recommended settings: {ex.Message}");
            }
        }

    }
}