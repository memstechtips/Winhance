using Windows.Win32;
using Windows.Win32.Foundation;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Helpers;

namespace Winhance.Infrastructure.Features.Optimize.Services;

internal class PowerPlanActivationService(
    ILogService logService,
    IPowerSettingsQueryService powerSettingsQueryService,
    IPowerSchemeOperations powerSchemeOperations,
    IProcessExecutor processExecutor,
    IPowerCfgApplier powerCfgApplier,
    IFileSystemService fileSystemService,
    ICatalogSettingsRegistry catalogSettingsRegistry) : IPowerPlanActivationService
{
    // A custom plan is duplicated from Balanced, the one scheme every Windows install ships with.
    private const string BalancedSchemeGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    private volatile IReadOnlyList<Setting>? _cachedSettings;
    private readonly object _cacheLock = new object();

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
                if (cleanupResult == (uint)WIN32_ERROR.ERROR_SUCCESS)
                {
                    logService.Log(LogLevel.Info, $"[PowerService] Cleaned up ghost plan entry with GUID {powerPlanGuid}");
                }

                var dupRc = powerSchemeOperations.DuplicateScheme(
                    Guid.Parse(BalancedSchemeGuid), Guid.Parse(powerPlanGuid), out var createdGuid);

                if (dupRc == (uint)WIN32_ERROR.ERROR_SUCCESS)
                {
                    // The API may hand back a different GUID than the one asked for, so the created one wins.
                    var actualGuid = createdGuid.ToString("D");
                    if (!string.Equals(actualGuid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        logService.Log(LogLevel.Warning, $"[PowerService] Windows assigned GUID {actualGuid} instead of requested {powerPlanGuid}");
                    }

                    SetPowerPlanName(Guid.Parse(actualGuid), planName);

                    powerSettingsQueryService.InvalidateCache();
                    logService.Log(LogLevel.Info, $"[PowerService] Successfully created custom plan '{planName}' with GUID {actualGuid}");

                    powerPlanGuid = actualGuid;
                    success = await SetActivePowerPlanAsync(powerPlanGuid).ConfigureAwait(false);
                }
                else
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Failed to create custom plan '{planName}' with GUID {powerPlanGuid} (rc={dupRc})");
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

            if (result == (uint)WIN32_ERROR.ERROR_SUCCESS)
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

    // Duplication for built-in plans, backup/restore fallback when duplication fails. Never throws.
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
                var dupResult = powerSchemeOperations.DuplicateScheme(sourceGuid, null, out var newGuid);

                if (dupResult == (uint)WIN32_ERROR.ERROR_SUCCESS)
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
                var duplicateResult = powerSchemeOperations.DuplicateScheme(srcGuid, null, out var dupNewGuid);

                if (duplicateResult == (uint)WIN32_ERROR.ERROR_SUCCESS)
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
            if (cleanupResult == (uint)WIN32_ERROR.ERROR_SUCCESS)
            {
                logService.Log(LogLevel.Info, existingPlan != null
                    ? $"[PowerService] Deleted corrupt Winhance plan (name was: '{existingPlan.Name}')"
                    : "[PowerService] Cleaned up ghost Winhance power plan entry");
                powerSettingsQueryService.InvalidateCache();
            }

            logService.Log(LogLevel.Info, "Creating Winhance Power Plan from Ultimate Performance");

            var dupRc = powerSchemeOperations.DuplicateScheme(
                Guid.Parse(ultimatePerformancePlan.Guid), Guid.Parse(predefinedPlan.Guid), out var createdGuid);

            if (dupRc != (uint)WIN32_ERROR.ERROR_SUCCESS)
            {
                logService.Log(LogLevel.Error, $"Failed to duplicate plan for Winhance Power Plan (rc={dupRc})");
                return new PowerPlanImportResult(false, "", "Failed to create plan");
            }

            // The API may hand back a different GUID than the one asked for, so the created one wins.
            var actualGuid = createdGuid.ToString("D");
            if (!string.Equals(actualGuid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Warning, $"[PowerService] Windows assigned GUID {actualGuid} instead of requested {predefinedPlan.Guid}");
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

            var restoreResult = (uint)PInvoke.PowerRestoreDefaultPowerSchemes();
            if (restoreResult != (uint)WIN32_ERROR.ERROR_SUCCESS)
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
            ImportScheme(file);
            await Task.Delay(200).ConfigureAwait(false);
        }
    }

    // The API allocates the imported scheme's GUID; nothing here needs it, so it is freed immediately.
    private static unsafe void ImportScheme(string file)
    {
        Guid* imported = null;
        if (PInvoke.PowerImportPowerScheme(null, file, ref imported) == WIN32_ERROR.ERROR_SUCCESS
            && imported is not null)
        {
            PInvoke.LocalFree((HLOCAL)(IntPtr)imported);
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
                    var target = new PowerCfgTarget("Power", w.SubgroupGuid, w.SettingGuid, PowerModeSupport.Separate);

                    bool ac = powerCfgApplier.WriteValueIndex(target, PowerContext.AC, w.Ac, planSchemeGuid);
                    bool dc = powerCfgApplier.WriteValueIndex(target, PowerContext.DC, w.Dc, planSchemeGuid);
                    if (!ac || !dc)
                    {
                        logService.Log(LogLevel.Warning, $"Recommended setting '{setting.Id}' did not fully apply to the new plan");
                        continue;
                    }

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

    // Expected format: "Power Scheme GUID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx (Name)".
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

}
