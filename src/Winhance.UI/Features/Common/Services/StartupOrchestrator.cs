using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
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
    private readonly ICatalogSettingsRegistry _catalogSettingsRegistry;
    private readonly IUserPreferencesService _preferencesService;
    private readonly IConfigurationService _configurationService;
    private readonly IScriptMigrationService _migrationService;
    private readonly IRemovalScriptUpdateService _updateService;
    private readonly INewBadgeService _newBadgeService;
    private readonly ILogService _logService;

    public StartupOrchestrator(
        ICatalogSettingsRegistry catalogSettingsRegistry,
        IUserPreferencesService preferencesService,
        IConfigurationService configurationService,
        IScriptMigrationService migrationService,
        IRemovalScriptUpdateService updateService,
        INewBadgeService newBadgeService,
        ILogService logService)
    {
        _catalogSettingsRegistry = catalogSettingsRegistry;
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

        // 1. Initialize the catalog settings registry
        statusProgress.Report("Loading_InitializingSettings");
        StartupLogger.Log("StartupOrchestrator", "Phase 1: Initializing settings registry...");
        try
        {
            // Isolated try - a catalog-init failure must not abort the rest of startup; the
            // registry's own EnsureInitialized guard then turns any use-before-init into a loud,
            // accurate error rather than a silent empty-membership answer.
            try
            {
                await _catalogSettingsRegistry.InitializeAsync().ConfigureAwait(false);
                StartupLogger.Log("StartupOrchestrator", "Phase 1: Catalog settings registry initialized");
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Failed to initialize catalog settings registry: {ex.Message}");
            }

            // Initialize new badge service (data-driven: uses the highest AddedInVersion
            // across the loaded registry to detect effective upgrades, so dev builds
            // behave identically to release builds).
            try
            {
                var allAddedInVersions = CollectAddedInVersions();
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
            StartupLogger.Log("StartupOrchestrator", $"Phase 1 FAILED: {ex.Message}");
            _logService.LogWarning($"Startup phase 1 failed: {ex.Message}");
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
    /// Enumerates <c>AddedInVersion</c> across every catalog setting - the badge service only cares about the
    /// maximum, and the set is machine-independent, without depending on registry readiness.
    /// </summary>
    private static IEnumerable<string?> CollectAddedInVersions() =>
        SettingCatalog.All.Select(s => s.Display.AddedInVersion);
}
