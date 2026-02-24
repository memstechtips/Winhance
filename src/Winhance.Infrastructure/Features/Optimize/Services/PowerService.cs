using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Common.Utils;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    public class PowerService(
        ILogService logService,
        IPowerSettingsQueryService powerSettingsQueryService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        IEventBus eventBus,
        IPowerPlanComboBoxService powerPlanComboBoxService,
        IProcessExecutor processExecutor,
        IFileSystemService fileSystemService) : IPowerService, ISpecialSettingHandler
    {
        private IEnumerable<SettingDefinition>? _cachedSettings;
        private readonly object _cacheLock = new object();

        public string DomainName => FeatureIds.Power;

        /// <summary>
        /// Attempts to apply a special (non-registry) setting. For power-plan-selection,
        /// delegates to plan import/activation logic.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the setting was handled and applied successfully;
        /// <see langword="false"/> if the setting is not a special setting, the value type
        /// is unsupported, or the operation failed.
        /// Never throws for expected business failures; errors are logged internally.
        /// </returns>
        public async Task<bool> TryApplySpecialSettingAsync(SettingDefinition setting, object value, bool additionalContext = false, ISettingApplicationService? settingApplicationService = null)
        {
            if (setting.Id == "power-plan-selection")
            {
                logService.Log(LogLevel.Info, "[PowerService] Applying power-plan-selection");

                if (value is Dictionary<string, object> planDict)
                {
                    var guid = planDict["Guid"].ToString()!;
                    var name = planDict["Name"].ToString()!;

                    logService.Log(LogLevel.Info, $"[PowerService] Config import: applying power plan {name} ({guid})");
                    return await ApplyPowerPlanByGuidAsync(setting, guid, name, settingApplicationService).ConfigureAwait(false);
                }

                if (value is int index)
                {
                    logService.Log(LogLevel.Info, $"[PowerService] UI selection: applying power plan at index {index}");

                    var resolution = await powerPlanComboBoxService.ResolvePowerPlanByIndexAsync(index).ConfigureAwait(false);
                    if (!resolution.Success)
                    {
                        logService.Log(LogLevel.Error, $"[PowerService] Failed to resolve power plan index: {resolution.ErrorMessage}");
                        return false;
                    }

                    return await ApplyPowerPlanSelectionAsync(setting, resolution.Guid, index, resolution.DisplayName, settingApplicationService).ConfigureAwait(false);
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
                var activePlan = await GetActivePowerPlanAsync().ConfigureAwait(false);
                var rawValues = new Dictionary<string, object?>
                {
                    ["ActivePowerPlan"] = activePlan?.Name,
                    ["ActivePowerPlanGuid"] = activePlan?.Guid
                };
                results["power-plan-selection"] = rawValues;
            }

            return results;
        }

        /// <summary>
        /// Returns the cached power settings, loading them on first call.
        /// </summary>
        /// <returns>
        /// The filtered settings for the power domain, or an empty enumerable
        /// if loading fails (failure is logged, never thrown).
        /// </returns>
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

        public void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cachedSettings = null;
            }
        }


        /// <summary>
        /// Gets the currently active power plan.
        /// </summary>
        /// <returns>
        /// The active <see cref="PowerPlan"/>, or <see langword="null"/> if the
        /// query fails (failure is logged as a warning, never thrown).
        /// </returns>
        public async Task<PowerPlan?> GetActivePowerPlanAsync()
        {
            try
            {
                return await powerSettingsQueryService.GetActivePowerPlanAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Error getting active power plan: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all power plans available on the system.
        /// </summary>
        /// <returns>
        /// A list of power plan objects, or an empty enumerable if the query
        /// fails (failure is logged as a warning, never thrown).
        /// </returns>
        public async Task<IEnumerable<object>> GetAvailablePowerPlansAsync()
        {
            try
            {
                var powerPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
                return powerPlans.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Error getting available power plans: {ex.Message}");
                return Enumerable.Empty<object>();
            }
        }


        private async Task<bool> SetActivePowerPlanAsync(string powerPlanGuid)
        {
            try
            {
                var currentActivePlan = await powerSettingsQueryService.GetActivePowerPlanAsync().ConfigureAwait(false);
                if (currentActivePlan != null && string.Equals(currentActivePlan.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
                {
                    logService.Log(LogLevel.Info, $"Power plan {powerPlanGuid} is already active, skipping application");
                    return true;
                }

                var schemeGuid = Guid.Parse(powerPlanGuid);
                var result = PowerProf.PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid);

                if (result == PowerProf.ERROR_SUCCESS)
                {
                    powerSettingsQueryService.InvalidateCache();
                    return true;
                }

                logService.Log(LogLevel.Warning, $"PowerSetActiveScheme failed with code {result}");
                return false;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Error setting active power plan: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes a power plan by its GUID.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the plan was deleted;
        /// <see langword="false"/> if the plan is active, deletion failed, or an
        /// error occurred (all failures are logged, never thrown).
        /// </returns>
        public async Task<bool> DeletePowerPlanAsync(string powerPlanGuid)
        {
            try
            {
                logService.Log(LogLevel.Info, $"Attempting to delete power plan: {powerPlanGuid}");

                var activePlan = await GetActivePowerPlanAsync().ConfigureAwait(false);
                if (activePlan != null && string.Equals(activePlan.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
                {
                    logService.Log(LogLevel.Warning, "Cannot delete active power plan");
                    return false;
                }

                var schemeGuid = Guid.Parse(powerPlanGuid);
                var result = PowerProf.PowerDeleteScheme(IntPtr.Zero, ref schemeGuid);

                if (result == PowerProf.ERROR_SUCCESS)
                {
                    powerSettingsQueryService.InvalidateCache();
                    logService.Log(LogLevel.Info, $"Successfully deleted power plan: {powerPlanGuid}");
                    return true;
                }
                else
                {
                    logService.Log(LogLevel.Error, $"Failed to delete power plan: {powerPlanGuid}. Error code: {result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error deleting power plan: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applies a power plan selected via the UI combo box.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the plan was activated successfully;
        /// <see langword="false"/> if the plan could not be imported, found, or activated.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="powerPlanGuid"/> is null or empty (programmer error).
        /// </exception>
        private async Task<bool> ApplyPowerPlanSelectionAsync(SettingDefinition setting, string powerPlanGuid, int planIndex, string planName, ISettingApplicationService? settingApplicationService)
        {
            logService.Log(LogLevel.Info, $"[PowerService] Applying power plan: {planName} ({powerPlanGuid})");

            if (string.IsNullOrEmpty(powerPlanGuid))
            {
                throw new ArgumentException("Power plan GUID cannot be null or empty");
            }

            var previousPlan = await GetActivePowerPlanAsync().ConfigureAwait(false);

            var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            var planExists = systemPlans.Any(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

            bool success = false;

            if (!planExists)
            {
                var predefinedPlan = PowerPlanDefinitions.BuiltInPowerPlans
                    .FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

                if (predefinedPlan != null)
                {
                    logService.Log(LogLevel.Info, $"[PowerService] Plan '{predefinedPlan.Name}' not found, attempting import");
                    var importResult = await ImportPowerPlanAsync(predefinedPlan).ConfigureAwait(false);

                    if (importResult.Success)
                    {
                        logService.Log(LogLevel.Info, $"[PowerService] Successfully imported '{predefinedPlan.Name}', activating");
                        await Task.Delay(200).ConfigureAwait(false);

                        var importedSchemeGuid = Guid.Parse(importResult.ImportedGuid);
                        var activateResult = PowerProf.PowerSetActiveScheme(IntPtr.Zero, ref importedSchemeGuid);
                        success = activateResult == PowerProf.ERROR_SUCCESS;

                        if (success)
                        {
                            powerSettingsQueryService.InvalidateCache();
                            InvalidateCache();
                            logService.Log(LogLevel.Info, $"[PowerService] Successfully activated imported plan");
                        }
                        else
                        {
                            logService.Log(LogLevel.Warning, $"[PowerService] First activation failed, retrying...");
                            await Task.Delay(500).ConfigureAwait(false);
                            activateResult = PowerProf.PowerSetActiveScheme(IntPtr.Zero, ref importedSchemeGuid);
                            success = activateResult == PowerProf.ERROR_SUCCESS;

                            if (success)
                            {
                                powerSettingsQueryService.InvalidateCache();
                                InvalidateCache();
                                logService.Log(LogLevel.Info, $"[PowerService] Successfully activated on retry");
                            }
                            else
                            {
                                logService.Log(LogLevel.Error, $"[PowerService] Failed to activate after import. Error code: {activateResult}");
                            }
                        }

                        powerPlanGuid = importResult.ImportedGuid;
                    }
                    else
                    {
                        logService.Log(LogLevel.Error, $"[PowerService] Failed to import plan: {importResult.ErrorMessage}");
                        return false;
                    }
                }
                else
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Unknown power plan GUID: {powerPlanGuid}");
                    return false;
                }
            }
            else
            {
                success = await SetActivePowerPlanAsync(powerPlanGuid).ConfigureAwait(false);
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

                if (IsWinhancePowerPlan(powerPlanGuid))
                {
                    await ApplyWinhanceRecommendedSettingsAsync(settingApplicationService).ConfigureAwait(false);
                }

                logService.Log(LogLevel.Info, $"[PowerService] Successfully applied power plan");
            }

            return success;
        }

        /// <summary>
        /// Applies a power plan identified by its GUID (used during config import).
        /// Creates the plan by duplicating Balanced if not found as a predefined or existing plan.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the plan was activated successfully;
        /// <see langword="false"/> if the plan could not be imported, created, or activated.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="powerPlanGuid"/> is null or empty (programmer error).
        /// </exception>
        private async Task<bool> ApplyPowerPlanByGuidAsync(SettingDefinition setting, string powerPlanGuid, string planName, ISettingApplicationService? settingApplicationService)
        {
            logService.Log(LogLevel.Info, $"[PowerService] Applying power plan by GUID: {planName} ({powerPlanGuid})");

            if (string.IsNullOrEmpty(powerPlanGuid))
            {
                throw new ArgumentException("Power plan GUID cannot be null or empty");
            }

            var previousPlan = await GetActivePowerPlanAsync().ConfigureAwait(false);
            var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
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
                    var importResult = await ImportPowerPlanAsync(predefinedPlan).ConfigureAwait(false);

                    if (importResult.Success)
                    {
                        logService.Log(LogLevel.Info, "[PowerService] Successfully imported, now activating");
                        await Task.Delay(200).ConfigureAwait(false);

                        success = await SetActivePowerPlanAsync(importResult.ImportedGuid).ConfigureAwait(false);
                        powerPlanGuid = importResult.ImportedGuid;
                    }
                    else
                    {
                        logService.Log(LogLevel.Error, $"[PowerService] Failed to import plan: {importResult.ErrorMessage}");
                        return false;
                    }
                }
                else
                {
                    logService.Log(LogLevel.Info, $"[PowerService] Custom power plan '{planName}' - creating by duplicating Balanced");

                    var balancedGuid = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
                    var dupResult = PowerProf.PowerDuplicateScheme(IntPtr.Zero, ref balancedGuid, out var destPtr);

                    if (dupResult == PowerProf.ERROR_SUCCESS)
                    {
                        var newGuid = Marshal.PtrToStructure<Guid>(destPtr);
                        PowerProf.LocalFree(destPtr);

                        // If a specific GUID was requested, we need to use powercfg to create with that GUID
                        // PowerDuplicateScheme doesn't support specifying destination GUID directly
                        // Delete the auto-generated one and use Process.Start for specific GUID
                        PowerProf.PowerDeleteScheme(IntPtr.Zero, ref newGuid);

                        // Use process-based duplication for specific GUID
                        await RunPowercfgAsync($"/duplicatescheme {balancedGuid:D} {powerPlanGuid}").ConfigureAwait(false);

                        SetPowerPlanName(Guid.Parse(powerPlanGuid), planName);

                        powerSettingsQueryService.InvalidateCache();
                        logService.Log(LogLevel.Info, $"[PowerService] Successfully created custom plan '{planName}' with GUID {powerPlanGuid}");

                        success = await SetActivePowerPlanAsync(powerPlanGuid).ConfigureAwait(false);
                    }
                    else
                    {
                        logService.Log(LogLevel.Error, $"[PowerService] Failed to create custom plan. Error code: {dupResult}");
                        return false;
                    }
                }
            }
            else
            {
                success = await SetActivePowerPlanAsync(powerPlanGuid).ConfigureAwait(false);
            }

            if (success)
            {
                var options = await powerPlanComboBoxService.GetPowerPlanOptionsAsync().ConfigureAwait(false);
                var planIndex = options.FindIndex(o =>
                    string.Equals(o.SystemPlan?.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

                eventBus.Publish(new PowerPlanChangedEvent
                {
                    PreviousPlanGuid = previousPlan?.Guid ?? string.Empty,
                    NewPlanGuid = powerPlanGuid,
                    NewPlanName = planName,
                    NewPlanIndex = planIndex >= 0 ? planIndex : 0
                });

                if (IsWinhancePowerPlan(powerPlanGuid))
                {
                    await ApplyWinhanceRecommendedSettingsAsync(settingApplicationService).ConfigureAwait(false);
                }

                logService.Log(LogLevel.Info, $"[PowerService] Successfully applied power plan '{planName}'");
            }

            return success;
        }

        /// <summary>
        /// Imports a predefined power plan onto the system. Uses duplication for built-in
        /// plans and falls back to backup/restore when duplication fails.
        /// </summary>
        /// <returns>
        /// A <see cref="PowerPlanImportResult"/> indicating success or failure with an
        /// error message. Never throws; all exceptions are caught and returned as a
        /// failed result.
        /// </returns>
        public async Task<PowerPlanImportResult> ImportPowerPlanAsync(PredefinedPowerPlan predefinedPlan)
        {
            try
            {
                if (predefinedPlan.Name == "Winhance Power Plan")
                {
                    return await CreateWinhancePowerPlanAsync(predefinedPlan).ConfigureAwait(false);
                }

                if (predefinedPlan.Name == "Ultimate Performance")
                {
                    var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
                    var existingPlan = systemPlans.FirstOrDefault(p => IsUltimatePerformancePlan(p.Name));

                    if (existingPlan != null)
                    {
                        logService.Log(LogLevel.Info, $"Ultimate Performance plan already exists with GUID: {existingPlan.Guid}");
                        return new PowerPlanImportResult(true, existingPlan.Guid);
                    }

                    var existingPlanNames = new HashSet<string>(
                        systemPlans.Select(p => CleanPlanName(p.Name)),
                        StringComparer.OrdinalIgnoreCase);

                    var sourceGuid = Guid.Parse(predefinedPlan.Guid);
                    var dupResult = PowerProf.PowerDuplicateScheme(IntPtr.Zero, ref sourceGuid, out var destPtr);

                    if (dupResult == PowerProf.ERROR_SUCCESS)
                    {
                        var newGuid = Marshal.PtrToStructure<Guid>(destPtr);
                        PowerProf.LocalFree(destPtr);

                        powerSettingsQueryService.InvalidateCache();

                        var actualGuid = newGuid.ToString("D");

                        if (!string.IsNullOrEmpty(actualGuid))
                        {
                            SetPowerPlanNameAndDescription(newGuid, predefinedPlan.Name, predefinedPlan.Description);
                            return new PowerPlanImportResult(true, actualGuid);
                        }
                    }

                    return new PowerPlanImportResult(false, "", "Ultimate Performance creation failed");
                }
                else
                {
                    var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
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
                    var srcGuid = Guid.Parse(predefinedPlan.Guid);
                    var duplicateResult = PowerProf.PowerDuplicateScheme(IntPtr.Zero, ref srcGuid, out var dupDestPtr);

                    if (duplicateResult == PowerProf.ERROR_SUCCESS)
                    {
                        var dupNewGuid = Marshal.PtrToStructure<Guid>(dupDestPtr);
                        PowerProf.LocalFree(dupDestPtr);

                        powerSettingsQueryService.InvalidateCache();

                        var actualGuid = dupNewGuid.ToString("D");

                        if (!string.IsNullOrEmpty(actualGuid))
                        {
                            logService.Log(LogLevel.Info, $"Successfully duplicated power plan '{predefinedPlan.Name}' with GUID: {actualGuid}");
                            return new PowerPlanImportResult(true, actualGuid);
                        }
                    }

                    logService.Log(LogLevel.Warning, $"Duplicate scheme failed for '{predefinedPlan.Name}', falling back to backup/restore method");
                    return await SimpleBackupRestore(predefinedPlan).ConfigureAwait(false);
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
                await BackupCustomPlansAsync(backupDir).ConfigureAwait(false);

                var restoreResult = PowerProf.PowerRestoreDefaultPowerSchemes();
                if (restoreResult != PowerProf.ERROR_SUCCESS)
                    return new PowerPlanImportResult(false, "", "Failed to restore default schemes");

                await Task.Delay(1000).ConfigureAwait(false);
                await RestoreCustomPlansAsync(backupDir).ConfigureAwait(false);

                powerSettingsQueryService.InvalidateCache();

                if (fileSystemService.DirectoryExists(backupDir))
                {
                    fileSystemService.DeleteDirectory(backupDir, true);
                }

                var plans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
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
            fileSystemService.CreateDirectory(backupFolder);

            var allPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            var customPlans = IdentifyCustomPlans(allPlans);

            foreach (var plan in customPlans)
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"{SanitizeFilename(plan.Name)}_{timestamp}.pow";
                var filepath = fileSystemService.CombinePath(backupFolder, filename);

                // PowerExportPowerScheme is not available as P/Invoke, use Process.Start
                await RunPowercfgAsync($"/export \"{filepath}\" {plan.Guid}").ConfigureAwait(false);
            }
        }

        private async Task RestoreCustomPlansAsync(string backupFolder)
        {
            if (!fileSystemService.DirectoryExists(backupFolder)) return;

            var backupFiles = fileSystemService.GetFiles(backupFolder, "*.pow");
            foreach (var file in backupFiles)
            {
                var importResult = PowerProf.PowerImportPowerScheme(IntPtr.Zero, file, out var importedPtr);
                if (importResult == PowerProf.ERROR_SUCCESS)
                {
                    PowerProf.LocalFree(importedPtr);
                }
                await Task.Delay(200).ConfigureAwait(false);
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
                var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
                var existingPlan = systemPlans.FirstOrDefault(p =>
                    string.Equals(p.Guid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase));

                if (existingPlan != null)
                {
                    logService.Log(LogLevel.Info, $"Winhance Power Plan already exists with GUID: {existingPlan.Guid}");
                    return new PowerPlanImportResult(true, existingPlan.Guid);
                }

                logService.Log(LogLevel.Info, "Creating Winhance Power Plan from Ultimate Performance");
                // PowerDuplicateScheme doesn't support specifying destination GUID, use Process.Start
                var (dupSuccess, _) = await RunPowercfgAsync($"/duplicatescheme {ultimatePerformancePlan.Guid} {predefinedPlan.Guid}").ConfigureAwait(false);

                if (!dupSuccess)
                {
                    logService.Log(LogLevel.Error, "Failed to duplicate plan for Winhance Power Plan");
                    return new PowerPlanImportResult(false, "", "Failed to create plan");
                }

                SetPowerPlanNameAndDescription(Guid.Parse(predefinedPlan.Guid), predefinedPlan.Name, predefinedPlan.Description);

                await ApplyRecommendedSettingsToPlanAsync(predefinedPlan.Guid).ConfigureAwait(false);

                powerSettingsQueryService.InvalidateCache();

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
                var allSettings = await GetSettingsAsync().ConfigureAwait(false);
                int appliedCount = 0;

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

                            var planSchemeGuid = Guid.Parse(planGuid);
                            var subgroupGuid = Guid.Parse(powerCfgWithRecommended.SubgroupGuid);
                            var settGuid = Guid.Parse(powerCfgWithRecommended.SettingGuid);

                            PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref planSchemeGuid, ref subgroupGuid, ref settGuid, (uint)acValue);
                            PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref planSchemeGuid, ref subgroupGuid, ref settGuid, (uint)dcValue);

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

                                        var recPlanGuid = Guid.Parse(planGuid);
                                        var recSubGuid = Guid.Parse(powerCfgSetting.SubgroupGuid);
                                        var recSettGuid = Guid.Parse(powerCfgSetting.SettingGuid);

                                        PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref recPlanGuid, ref recSubGuid, ref recSettGuid, (uint)acValue.Value);
                                        PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref recPlanGuid, ref recSubGuid, ref recSettGuid, (uint)dcValue.Value);

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

                logService.Log(LogLevel.Info, $"Applied {appliedCount} PowerCfg settings to Winhance Power Plan");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error applying recommended settings: {ex.Message}");
            }
        }

        private static bool IsWinhancePowerPlan(string guid) =>
            string.Equals(guid, "57696e68-616e-6365-506f-776572000000", StringComparison.OrdinalIgnoreCase);

        private async Task ApplyWinhanceRecommendedSettingsAsync(ISettingApplicationService? settingApplicationService)
        {
            try
            {
                logService.Log(LogLevel.Info, "[PowerService] Applying recommended settings for Winhance Power Plan");
                await settingApplicationService!.ApplyRecommendedSettingsForDomainAsync("power-plan-selection").ConfigureAwait(false);
                logService.Log(LogLevel.Info, "[PowerService] Successfully applied recommended settings for Winhance Power Plan");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[PowerService] Failed to apply recommended settings: {ex.Message}");
            }
        }

        private void SetPowerPlanName(Guid schemeGuid, string name)
        {
            var nameBytes = (uint)(name.Length * 2 + 2);
            PowerProf.PowerWriteFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, name, nameBytes);
        }

        private void SetPowerPlanNameAndDescription(Guid schemeGuid, string name, string description)
        {
            var nameBytes = (uint)(name.Length * 2 + 2);
            PowerProf.PowerWriteFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, name, nameBytes);

            if (!string.IsNullOrEmpty(description))
            {
                var descBytes = (uint)(description.Length * 2 + 2);
                PowerProf.PowerWriteDescription(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, description, descBytes);
            }
        }

        private async Task<(bool Success, string Output)> RunPowercfgAsync(string arguments, bool useCmd = false)
        {
            try
            {
                string fileName;
                string args;

                if (useCmd)
                {
                    fileName = "cmd.exe";
                    args = $"/c {arguments}";
                }
                else
                {
                    fileName = "powercfg";
                    args = arguments;
                }

                var result = await processExecutor.ExecuteAsync(fileName, args).ConfigureAwait(false);
                return (result.Succeeded, result.StandardOutput.TrimEnd());
            }
            catch
            {
                return (false, string.Empty);
            }
        }

    }
}