using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Helpers;

namespace Winhance.Infrastructure.Features.Optimize.Services;

/// <summary>Holds the power-plan activation orchestration extracted from PowerService (Phase 6.7 Slice 8b-1).
/// Behaviour-preserving verbatim move: ensure-installed + activate + the power-settings cache, MINUS the
/// post-activation side-effects (event publish + recommended re-apply) that stay with the caller (D1/D2).
/// Six leaf dependencies; no PowerService / no IStateWriter reference, so it is DI-cycle-safe.</summary>
public class PowerPlanActivationService(
    ILogService logService,
    IPowerSettingsQueryService powerSettingsQueryService,
    IPowerSchemeOperations powerSchemeOperations,
    IProcessExecutor processExecutor,
    IFileSystemService fileSystemService,
    ICatalogSettingsRegistry catalogSettingsRegistry) : IPowerPlanActivationService
{
    private volatile IReadOnlyList<Setting>? _cachedSettings;
    private readonly object _cacheLock = new object();

    /// <summary>
    /// Ensures the plan with the given GUID is installed and active (importing a predefined-but-not-installed
    /// plan first), returning success plus the final activated GUID. Verbatim move of the old
    /// PowerService.ApplyPowerPlanByGuidAsync detection+activation body, MINUS its post-activation tail
    /// (PowerPlanChangedEvent + recommended re-apply), which the caller now owns (Slice 8b-1).
    /// </summary>
    public async Task<(bool Success, string ActivatedGuid)> EnsureActivatedAsync(string powerPlanGuid, string? planName = null)
    {
        planName ??= powerPlanGuid;
        logService.Log(LogLevel.Info, $"[PowerService] Applying power plan by GUID: {planName} ({powerPlanGuid})");

        if (string.IsNullOrEmpty(powerPlanGuid))
        {
            throw new ArgumentException("Power plan GUID cannot be null or empty");
        }

        var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
        var planExists = systemPlans.Any(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

        bool success = false;

        if (!planExists)
        {
            logService.Log(LogLevel.Warning, $"[PowerService] Plan '{planName}' ({powerPlanGuid}) not found on system");

            var predefinedPlan = PowerPlanCatalog.BuiltInPowerPlans
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
                    return (false, powerPlanGuid);
                }
            }
            else
            {
                logService.Log(LogLevel.Info, $"[PowerService] Custom power plan '{planName}' - creating by duplicating Balanced");

                // Clean up any ghost/corrupt plan entry that may block duplication with this GUID
                var targetGuid = Guid.Parse(powerPlanGuid);
                var cleanupResult = powerSchemeOperations.DeleteScheme(targetGuid);
                if (cleanupResult == PowerProf.ERROR_SUCCESS)
                {
                    logService.Log(LogLevel.Info, $"[PowerService] Cleaned up ghost plan entry with GUID {powerPlanGuid}");
                }

                // Use powercfg for specific-GUID duplication (P/Invoke doesn't support destination GUID)
                var (dupSuccess, dupOutput) = await RunPowercfgAsync($"/duplicatescheme 381b4222-f694-41f0-9685-ff5bb260df2e {powerPlanGuid}").ConfigureAwait(false);

                if (dupSuccess)
                {
                    // Parse the actual GUID — powercfg may assign a different one
                    var actualGuid = ParseGuidFromPowercfgOutput(dupOutput) ?? powerPlanGuid;
                    if (!string.Equals(actualGuid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        logService.Log(LogLevel.Warning, $"[PowerService] powercfg assigned GUID {actualGuid} instead of requested {powerPlanGuid}");
                    }

                    SetPowerPlanName(Guid.Parse(actualGuid), planName);

                    powerSettingsQueryService.InvalidateCache();
                    logService.Log(LogLevel.Info, $"[PowerService] Successfully created custom plan '{planName}' with GUID {actualGuid}");

                    powerPlanGuid = actualGuid;
                    success = await SetActivePowerPlanAsync(powerPlanGuid).ConfigureAwait(false);
                }
                else
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Failed to create custom plan '{planName}' with GUID {powerPlanGuid}");
                    return (false, powerPlanGuid);
                }
            }
        }
        else
        {
            success = await SetActivePowerPlanAsync(powerPlanGuid).ConfigureAwait(false);
        }

        return (success, powerPlanGuid);
    }

    public async Task<bool> SetActivePowerPlanAsync(string powerPlanGuid)
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
            var result = powerSchemeOperations.SetActiveScheme(schemeGuid);

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
                var existingPlan = systemPlans.FirstOrDefault(p => Common.Utilities.PowerPlanHelper.IsUltimatePerformancePlan(p.Name));

                if (existingPlan != null)
                {
                    logService.Log(LogLevel.Info, $"Ultimate Performance plan already exists with GUID: {existingPlan.Guid}");
                    return new PowerPlanImportResult(true, existingPlan.Guid);
                }

                var sourceGuid = Guid.Parse(predefinedPlan.Guid);
                var dupResult = powerSchemeOperations.DuplicateScheme(sourceGuid, out var newGuid);

                if (dupResult == PowerProf.ERROR_SUCCESS)
                {
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

                logService.Log(LogLevel.Info, $"Attempting to duplicate power plan '{predefinedPlan.Name}' using GUID {predefinedPlan.Guid}");
                var srcGuid = Guid.Parse(predefinedPlan.Guid);
                var duplicateResult = powerSchemeOperations.DuplicateScheme(srcGuid, out var dupNewGuid);

                if (duplicateResult == PowerProf.ERROR_SUCCESS)
                {
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

    private async Task<PowerPlanImportResult> CreateWinhancePowerPlanAsync(PredefinedPowerPlan predefinedPlan)
    {
        var ultimatePerformancePlan = PowerPlanCatalog.BuiltInPowerPlans
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

            // Check if plan exists AND is valid (not a ghost/corrupt entry)
            if (existingPlan != null &&
                string.Equals(existingPlan.Name?.Trim(), "Winhance Power Plan", StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Info, $"Winhance Power Plan already exists with GUID: {existingPlan.Guid}");
                return new PowerPlanImportResult(true, existingPlan.Guid);
            }

            // Clean up any ghost/corrupt plan entry (visible or invisible to enumeration)
            // that may block duplication with this GUID
            var winhanceGuid = Guid.Parse(predefinedPlan.Guid);
            var cleanupResult = powerSchemeOperations.DeleteScheme(winhanceGuid);
            if (cleanupResult == PowerProf.ERROR_SUCCESS)
            {
                logService.Log(LogLevel.Info, existingPlan != null
                    ? $"[PowerService] Deleted corrupt Winhance plan (name was: '{existingPlan.Name}')"
                    : "[PowerService] Cleaned up ghost Winhance power plan entry");
                powerSettingsQueryService.InvalidateCache();
            }

            logService.Log(LogLevel.Info, "Creating Winhance Power Plan from Ultimate Performance");

            // Use powercfg for specific-GUID duplication (P/Invoke doesn't support destination GUID)
            var (dupSuccess, dupOutput) = await RunPowercfgAsync($"/duplicatescheme {ultimatePerformancePlan.Guid} {predefinedPlan.Guid}").ConfigureAwait(false);

            if (!dupSuccess)
            {
                logService.Log(LogLevel.Error, "Failed to duplicate plan for Winhance Power Plan");
                return new PowerPlanImportResult(false, "", "Failed to create plan");
            }

            // Parse the actual GUID from powercfg output — it may differ from the requested one
            var actualGuid = ParseGuidFromPowercfgOutput(dupOutput) ?? predefinedPlan.Guid;
            if (!string.Equals(actualGuid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Warning, $"[PowerService] powercfg assigned GUID {actualGuid} instead of requested {predefinedPlan.Guid}");
            }

            SetPowerPlanNameAndDescription(Guid.Parse(actualGuid), predefinedPlan.Name, predefinedPlan.Description);

            await ApplyRecommendedSettingsToPlanAsync(actualGuid).ConfigureAwait(false);

            powerSettingsQueryService.InvalidateCache();

            logService.Log(LogLevel.Info, $"Successfully created Winhance Power Plan: {actualGuid}");
            return new PowerPlanImportResult(true, actualGuid);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error creating Winhance Power Plan: {ex.Message}");
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
                string.Equals(Common.Utilities.PowerPlanHelper.CleanPlanName(p.Name), targetPlan.Name, StringComparison.OrdinalIgnoreCase))?.Guid;

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

            // PowerExportPowerScheme is not a reliable P/Invoke export, use powercfg
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
            !builtInNames.Contains(Common.Utilities.PowerPlanHelper.CleanPlanName(plan.Name))
        ).ToList();
    }

    private string SanitizeFilename(string filename)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", filename.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
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
                    var write = RecommendedSettingsResolver.ComputePlanRecommendedWrite(setting);
                    if (write is not { } w)
                        continue;

                    logService.Log(LogLevel.Debug, $"Applying {setting.Id} - AC: {w.Ac}, DC: {w.Dc}");

                    var planSchemeGuid = Guid.Parse(planGuid);
                    var subgroupGuid = Guid.Parse(w.SubgroupGuid);
                    var settGuid = Guid.Parse(w.SettingGuid);

                    PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref planSchemeGuid, ref subgroupGuid, ref settGuid, (uint)w.Ac);
                    PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref planSchemeGuid, ref subgroupGuid, ref settGuid, (uint)w.Dc);

                    appliedCount++;
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

    private void SetPowerPlanName(Guid schemeGuid, string name)
    {
        powerSchemeOperations.WriteFriendlyName(schemeGuid, name);
    }

    private void SetPowerPlanNameAndDescription(Guid schemeGuid, string name, string description)
    {
        powerSchemeOperations.WriteFriendlyName(schemeGuid, name);

        if (!string.IsNullOrEmpty(description))
        {
            powerSchemeOperations.WriteDescription(schemeGuid, description);
        }
    }

    /// <summary>
    /// Parses a power scheme GUID from powercfg output.
    /// Expected format: "Power Scheme GUID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx  (Name)"
    /// </summary>
    private static string? ParseGuidFromPowercfgOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;

        var match = Regex.Match(output, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
        return match.Success ? match.Groups[1].Value : null;
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
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"powercfg {arguments} failed: {ex.Message}");
            return (false, string.Empty);
        }
    }

    /// <summary>
    /// Returns the cached power settings, loading them on first call. Used internally
    /// by ApplyRecommendedSettingsToPlanAsync when populating a freshly-imported plan.
    /// </summary>
    /// <returns>
    /// The filtered settings for the power feature, or an empty enumerable
    /// if loading fails (failure is logged, never thrown).
    /// </returns>
    private Task<IReadOnlyList<Setting>> GetSettingsAsync()
    {
        if (_cachedSettings != null)
            return Task.FromResult(_cachedSettings);

        lock (_cacheLock)
        {
            if (_cachedSettings != null)
                return Task.FromResult(_cachedSettings);

            try
            {
                logService.Log(LogLevel.Info, "Loading Power settings");
                _cachedSettings = catalogSettingsRegistry.GetByFeature(FeatureIds.Power);
                return Task.FromResult(_cachedSettings);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error loading Power settings: {ex.Message}");
                return Task.FromResult<IReadOnlyList<Setting>>(System.Array.Empty<Setting>());
            }
        }
    }

    public void InvalidateSettingsCache()
    {
        lock (_cacheLock)
        {
            _cachedSettings = null;
        }
    }

}
