using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Winhance.Infrastructure.Features.Common.EventHandlers;
using Winhance.UI.Features.Common.Utilities;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Orchestrates the application startup sequence (phases 1-5).
/// Extracted from MainWindow.xaml.cs for testability.
/// </summary>
public class StartupOrchestrator : IStartupOrchestrator
{
    private readonly ICompatibleSettingsRegistry _settingsRegistry;
    private readonly IGlobalSettingsPreloader _settingsPreloader;
    private readonly TooltipRefreshEventHandler _tooltipEventHandler;
    private readonly IUserPreferencesService _preferencesService;
    private readonly IConfigurationService _configurationService;
    private readonly ISystemBackupService _backupService;
    private readonly IScriptMigrationService _migrationService;
    private readonly IRemovalScriptUpdateService _updateService;
    private readonly ILogService _logService;

    public StartupOrchestrator(
        ICompatibleSettingsRegistry settingsRegistry,
        IGlobalSettingsPreloader settingsPreloader,
        TooltipRefreshEventHandler tooltipEventHandler,
        IUserPreferencesService preferencesService,
        IConfigurationService configurationService,
        ISystemBackupService backupService,
        IScriptMigrationService migrationService,
        IRemovalScriptUpdateService updateService,
        ILogService logService)
    {
        _settingsRegistry = settingsRegistry;
        _settingsPreloader = settingsPreloader;
        _tooltipEventHandler = tooltipEventHandler;
        _preferencesService = preferencesService;
        _configurationService = configurationService;
        _backupService = backupService;
        _migrationService = migrationService;
        _updateService = updateService;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task<StartupResult> RunStartupSequenceAsync(
        IProgress<string> statusProgress,
        IProgress<TaskProgressDetail> detailedProgress)
    {
        BackupResult? backupResult = null;

        // 1. Initialize settings registry
        statusProgress.Report("Loading_InitializingSettings");
        StartupLogger.Log("StartupOrchestrator", "Phase 1: Initializing settings registry...");
        try
        {
            await _settingsRegistry.InitializeAsync().ConfigureAwait(false);
            await _settingsPreloader.PreloadAllSettingsAsync().ConfigureAwait(false);
            StartupLogger.Log("StartupOrchestrator", "Phase 1: Settings registry initialized");

            // Initialize tooltip event handler (constructor subscribes to EventBus)
            // Accessing the injected instance ensures it's constructed and subscribed.
            _ = _tooltipEventHandler;

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
                UserPreferenceKeys.InitialConfigBackupCompleted, "false");
            if (!string.Equals(backupCompleted, "true", StringComparison.OrdinalIgnoreCase))
            {
                statusProgress.Report("Loading_CreatingConfigBackup");
                StartupLogger.Log("StartupOrchestrator", "Phase 2: Creating user backup config...");

                var backupTask = _configurationService.CreateUserBackupConfigAsync();
                var completed = await Task.WhenAny(
                    backupTask, Task.Delay(TimeSpan.FromSeconds(90))).ConfigureAwait(false);

                if (completed == backupTask)
                {
                    await backupTask; // observe exceptions
                    await _preferencesService.SetPreferenceAsync(
                        UserPreferenceKeys.InitialConfigBackupCompleted, "true");
                    StartupLogger.Log("StartupOrchestrator", "Phase 2: User backup config done");
                }
                else
                {
                    StartupLogger.Log("StartupOrchestrator",
                        "Phase 2: User backup config TIMED OUT (will retry next launch)");
                    _logService.LogWarning(
                        "User backup config timed out after 90s — will retry next launch");
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

        // 3. System restore point (respects SkipSystemBackup preference)
        try
        {
            var skipBackup = _preferencesService.GetPreference(UserPreferenceKeys.SkipSystemBackup, "false");
            if (!string.Equals(skipBackup, "true", StringComparison.OrdinalIgnoreCase))
            {
                statusProgress.Report("Loading_CheckingSystemProtection");
                StartupLogger.Log("StartupOrchestrator", "Phase 3: Checking system protection...");
                backupResult = await _backupService.EnsureInitialBackupsAsync(detailedProgress).ConfigureAwait(false);
                StartupLogger.Log("StartupOrchestrator", "Phase 3: System protection check done");
            }
            else
            {
                StartupLogger.Log("StartupOrchestrator", "Phase 3: System backup skipped (user preference)");
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Log("StartupOrchestrator", $"Phase 3: System backup FAILED: {ex.Message}");
            _logService.LogWarning($"System backup failed: {ex.Message}");
        }

        // 4. Script migration
        try
        {
            statusProgress.Report("Loading_MigratingScripts");
            StartupLogger.Log("StartupOrchestrator", "Phase 4: Migrating scripts...");
            await _migrationService.MigrateFromOldPathsAsync().ConfigureAwait(false);
            StartupLogger.Log("StartupOrchestrator", "Phase 4: Script migration done");
        }
        catch (Exception ex)
        {
            StartupLogger.Log("StartupOrchestrator", $"Phase 4: Script migration FAILED: {ex.Message}");
            _logService.LogWarning($"Script migration failed: {ex.Message}");
        }

        // 5. Script updates
        try
        {
            statusProgress.Report("Loading_CheckingScripts");
            StartupLogger.Log("StartupOrchestrator", "Phase 5: Checking for script updates...");
            await _updateService.CheckAndUpdateScriptsAsync().ConfigureAwait(false);
            StartupLogger.Log("StartupOrchestrator", "Phase 5: Script update check done");
        }
        catch (Exception ex)
        {
            StartupLogger.Log("StartupOrchestrator", $"Phase 5: Script update check FAILED: {ex.Message}");
            _logService.LogWarning($"Script update check failed: {ex.Message}");
        }

        statusProgress.Report("Loading_PreparingApp");
        StartupLogger.Log("StartupOrchestrator", "All phases complete");

        return new StartupResult { BackupResult = backupResult };
    }
}
