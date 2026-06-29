using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.Common.Utilities;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Orchestrates the application startup sequence (phases 1-4).
/// Extracted from MainWindow.xaml.cs for testability.
/// </summary>
public class StartupOrchestrator : IStartupOrchestrator
{
    private readonly ICompatibleSettingsRegistry _settingsRegistry;
    private readonly IGlobalSettingsPreloader _settingsPreloader;
    private readonly IUserPreferencesService _preferencesService;
    private readonly IConfigurationService _configurationService;
    private readonly IScriptMigrationService _migrationService;
    private readonly IRemovalScriptUpdateService _updateService;
    private readonly INewBadgeService _newBadgeService;
    private readonly ILogService _logService;

    public StartupOrchestrator(
        ICompatibleSettingsRegistry settingsRegistry,
        IGlobalSettingsPreloader settingsPreloader,
        IUserPreferencesService preferencesService,
        IConfigurationService configurationService,
        IScriptMigrationService migrationService,
        IRemovalScriptUpdateService updateService,
        INewBadgeService newBadgeService,
        ILogService logService)
    {
        _settingsRegistry = settingsRegistry;
        _settingsPreloader = settingsPreloader;
        _preferencesService = preferencesService;
        _configurationService = configurationService;
        _migrationService = migrationService;
        _updateService = updateService;
        _newBadgeService = newBadgeService;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task<StartupResult> RunStartupSequenceAsync(
        IProgress<string> statusProgress,
        IProgress<TaskProgressDetail> detailedProgress)
    {
        bool isFirstLaunch = false;

        // 1. Initialize settings registry
        statusProgress.Report("Loading_InitializingSettings");
        StartupLogger.Log("StartupOrchestrator", "Phase 1: Initializing settings registry...");
        try
        {
            await _settingsRegistry.InitializeAsync().ConfigureAwait(false);
            await _settingsPreloader.PreloadAllSettingsAsync().ConfigureAwait(false);
            StartupLogger.Log("StartupOrchestrator", "Phase 1: Settings registry initialized");

        // Initialize new badge service (data-driven: uses the highest AddedInVersion
        // across the loaded registry to detect effective upgrades, so dev builds
        // behave identically to release builds).
        try
        {
            var allAddedInVersions = CollectAddedInVersions(_settingsRegistry);
            _newBadgeService.Initialize(allAddedInVersions);
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"New badge service init failed: {ex.Message}");
        }

            // Pre-cache regedit icon for Technical Details panel
            RegeditIconProvider.GetIconAsync().FireAndForget(_logService);
        }
        catch (Exception ex)
        {
            StartupLogger.Log("StartupOrchestrator", $"Phase 1: Settings registry FAILED: {ex.Message}");
            _logService.LogWarning($"Failed to initialize settings registry: {ex.Message}");
        }

        // 2. User backup config (first-run only)
        try
        {
            var backupCompleted = _preferencesService.GetPreference(
                UserPreferenceKeys.InitialConfigBackupCompleted, false);
            if (!backupCompleted)
            {
                statusProgress.Report("Loading_CreatingConfigBackup");
                StartupLogger.Log("StartupOrchestrator", "Phase 2: Creating user backup config...");

                var backupTask = _configurationService.CreateUserBackupConfigAsync();
                var completed = await Task.WhenAny(
                    backupTask, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);

                if (completed == backupTask)
                {
                    await backupTask; // observe exceptions
                    await _preferencesService.SetPreferenceAsync(
                        UserPreferenceKeys.InitialConfigBackupCompleted, true);
                    isFirstLaunch = true;
                    StartupLogger.Log("StartupOrchestrator", "Phase 2: User backup config done");
                }
                else
                {
                    StartupLogger.Log("StartupOrchestrator",
                        "Phase 2: User backup config TIMED OUT (will retry next launch)");
                    _logService.LogWarning(
                        "User backup config timed out after 30s — will retry next launch");
                }
            }
            else
            {
                StartupLogger.Log("StartupOrchestrator", "Phase 2: User backup config already completed");
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Log("StartupOrchestrator", $"Phase 2: User backup config FAILED: {ex.Message}");
            _logService.LogWarning($"User backup config failed: {ex.Message}");
        }

        // 3. Script migration
        try
        {
            statusProgress.Report("Loading_MigratingScripts");
            StartupLogger.Log("StartupOrchestrator", "Phase 3: Migrating scripts...");
            await _migrationService.MigrateFromOldPathsAsync().ConfigureAwait(false);
            StartupLogger.Log("StartupOrchestrator", "Phase 3: Script migration done");
        }
        catch (Exception ex)
        {
            StartupLogger.Log("StartupOrchestrator", $"Phase 3: Script migration FAILED: {ex.Message}");
            _logService.LogWarning($"Script migration failed: {ex.Message}");
        }

        // 4. Script updates
        try
        {
            statusProgress.Report("Loading_CheckingScripts");
            StartupLogger.Log("StartupOrchestrator", "Phase 4: Checking for script updates...");
            await _updateService.CheckAndUpdateScriptsAsync().ConfigureAwait(false);
            StartupLogger.Log("StartupOrchestrator", "Phase 4: Script update check done");
        }
        catch (Exception ex)
        {
            StartupLogger.Log("StartupOrchestrator", $"Phase 4: Script update check FAILED: {ex.Message}");
            _logService.LogWarning($"Script update check failed: {ex.Message}");
        }

        statusProgress.Report("Loading_PreparingApp");
        StartupLogger.Log("StartupOrchestrator", "All phases complete");

        return new StartupResult { IsFirstLaunch = isFirstLaunch };
    }

    /// <summary>
    /// Enumerates <c>AddedInVersion</c> across every setting in both the filtered
    /// and bypassed maps of the registry. Duplicates are fine — the badge service
    /// only cares about the maximum.
    /// </summary>
    private static IEnumerable<string?> CollectAddedInVersions(ICompatibleSettingsRegistry registry)
    {
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>>? filtered = null;
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>>? bypassed = null;

        try { filtered = registry.GetAllFilteredSettings(); } catch { /* registry not ready */ }
        try { bypassed = registry.GetAllBypassedSettings(); } catch { /* registry not ready */ }

        var results = new List<string?>();
        if (filtered is not null)
        {
            foreach (var kvp in filtered)
                foreach (var s in kvp.Value)
                    results.Add(s.AddedInVersion);
        }
        if (bypassed is not null)
        {
            foreach (var kvp in bypassed)
                foreach (var s in kvp.Value)
                    results.Add(s.AddedInVersion);
        }
        return results;
    }
}
