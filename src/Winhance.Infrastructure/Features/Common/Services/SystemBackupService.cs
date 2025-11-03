using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class SystemBackupService : ISystemBackupService
    {
        private readonly IPowerShellExecutionService _psService;
        private readonly ILogService _logService;
        private readonly IUserPreferencesService _prefsService;

        private const string RestorePointName = "Winhance Initial Restore Point";
        private const string BackupDirectory = @"C:\ProgramData\Winhance\Backups";
        private const long MinimumFreeSpaceBytes = 2L * 1024 * 1024 * 1024;

        public SystemBackupService(
            IPowerShellExecutionService psService,
            ILogService logService,
            IUserPreferencesService prefsService)
        {
            _psService = psService;
            _logService = logService;
            _prefsService = prefsService;
        }

        public async Task<BackupResult> EnsureInitialBackupsAsync(
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new BackupResult();

            try
            {
                var registryBackupCompleted = await _prefsService.GetPreferenceAsync("RegistryBackupCompleted", false);

                if (!registryBackupCompleted)
                {
                    _logService.Log(LogLevel.Info, "Starting first-run registry backup...");

                    if (!await CheckDiskSpaceAsync())
                    {
                        result.Warnings.Add("Insufficient disk space for registry backup (2GB required)");
                        _logService.Log(LogLevel.Warning, "Insufficient disk space for registry backup");
                    }
                    else
                    {
                        await CreateRegistryBackupsAsync(result, progress, cancellationToken);
                        await _prefsService.SetPreferenceAsync("RegistryBackupCompleted", true);
                        _logService.Log(LogLevel.Info, "Registry backup completed and marked as done");
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Info, "Registry backup already completed previously");
                    await CheckExistingRegistryBackupsAsync(result);
                }

                _logService.Log(LogLevel.Info, "Starting backup process - checking for existing restore point...");

                // Report: Checking restore point
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = "Checking system restore point...",
                    IsIndeterminate = true
                });

                var existingPoint = await FindRestorePointAsync(RestorePointName);
                if (existingPoint != null)
                {
                    _logService.Log(LogLevel.Info, $"Restore point '{RestorePointName}' already exists (created: {existingPoint.Value}). Skipping creation.");
                    result.RestorePointExisted = true;
                    result.RestorePointDate = existingPoint.Value;
                    result.Success = true;
                    result.SystemRestoreEnabled = true;
                    return result;
                }

                _logService.Log(LogLevel.Info, $"No existing restore point found with name '{RestorePointName}'");

                // Report: Checking if System Restore is enabled
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = "Checking System Restore status...",
                    IsIndeterminate = true
                });

                var isEnabled = await CheckSystemRestoreEnabledAsync();
                if (!isEnabled)
                {
                    _logService.Log(LogLevel.Warning, "System Restore is currently disabled");
                    result.SystemRestoreWasDisabled = true;

                    // Report: Enabling System Restore
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = "Enabling System Restore...",
                        IsIndeterminate = true
                    });

                    _logService.Log(LogLevel.Info, "Attempting to enable System Restore...");
                    var enabled = await EnableSystemRestoreAsync();
                    if (!enabled)
                    {
                        result.Warnings.Add("Failed to enable System Restore");
                        _logService.Log(LogLevel.Error, "Failed to enable System Restore - cannot create restore point");
                        result.Success = true;
                        result.SystemRestoreEnabled = false;
                        return result;
                    }

                    _logService.Log(LogLevel.Info, "System Restore enabled successfully");
                    result.SystemRestoreEnabled = true;
                }
                else
                {
                    _logService.Log(LogLevel.Info, "System Restore is already enabled");
                    result.SystemRestoreEnabled = true;
                }

                // Report: Creating restore point
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = "Creating system restore point...",
                    IsIndeterminate = true
                });

                _logService.Log(LogLevel.Info, $"Creating new restore point with name '{RestorePointName}'...");

                var created = await CreateRestorePointAsync(RestorePointName);

                if (created)
                {
                    result.RestorePointCreated = true;
                    result.RestorePointDate = DateTime.Now;
                    result.Success = true;
                    _logService.Log(LogLevel.Info, $"Successfully created restore point '{RestorePointName}'");
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to create system restore point";
                    _logService.Log(LogLevel.Error, $"Failed to create restore point '{RestorePointName}'");
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logService.Log(LogLevel.Error, $"Error ensuring initial backups: {ex.Message}");
            }

            return result;
        }

        public async Task<BackupStatus> GetBackupStatusAsync()
        {
            var status = new BackupStatus();

            try
            {
                status.SystemRestoreEnabled = await CheckSystemRestoreEnabledAsync();

                var existingPoint = await FindRestorePointAsync(RestorePointName);
                status.RestorePointExists = existingPoint.HasValue;
                status.RestorePointDate = existingPoint;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting backup status: {ex.Message}");
            }

            return status;
        }

        private async Task<bool> CheckSystemRestoreEnabledAsync()
        {
            var script = "Get-ComputerRestorePoint -ErrorAction SilentlyContinue | Select-Object -First 1";
            try
            {
                var output = await _psService.ExecuteScriptAsync(script);
                return !string.IsNullOrWhiteSpace(output);
            }
            catch
            {
                return false;
            }
        }

        private async Task<DateTime?> FindRestorePointAsync(string description)
        {
            var script = $@"
$rp = Get-ComputerRestorePoint -ErrorAction SilentlyContinue | Where-Object {{ $_.Description -eq '{description}' }} | Select-Object -First 1
if ($rp) {{ 'EXISTS' }} else {{ 'NOT_FOUND' }}";

            try
            {
                _logService.Log(LogLevel.Info, $"Querying for restore point: '{description}'");
                var output = await _psService.ExecuteScriptAsync(script);

                if (string.IsNullOrWhiteSpace(output) || output.Trim() == "NOT_FOUND")
                {
                    _logService.Log(LogLevel.Info, $"No restore point found with description: '{description}'");
                    return null;
                }

                if (output.Trim() == "EXISTS")
                {
                    _logService.Log(LogLevel.Info, $"Found existing restore point: '{description}'");
                    return DateTime.Now;
                }

                _logService.Log(LogLevel.Warning, $"Unexpected output from restore point query: '{output.Trim()}'");
                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error querying restore point: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> CreateRestorePointAsync(string description)
        {
            var script = $"Checkpoint-Computer -Description '{description}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop";
            try
            {
                await _psService.ExecuteScriptAsync(script);
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to create restore point: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> EnableSystemRestoreAsync()
        {
            var script = @"
$systemDrive = $env:SystemDrive
Enable-ComputerRestore -Drive $systemDrive -ErrorAction Stop
vssadmin Resize ShadowStorage /For=$systemDrive /On=$systemDrive /MaxSize=10GB | Out-Null";

            try
            {
                await _psService.ExecuteScriptAsync(script);
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to enable System Restore: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> CheckDiskSpaceAsync()
        {
            var script = @"
$systemDrive = $env:SystemDrive
$drive = Get-PSDrive ($systemDrive -replace ':', '')
$freeSpace = $drive.Free
$freeSpace";

            try
            {
                var output = await _psService.ExecuteScriptAsync(script);
                if (long.TryParse(output?.Trim(), out long freeSpace))
                {
                    _logService.Log(LogLevel.Info, $"Available disk space: {freeSpace / 1024 / 1024 / 1024:F2} GB");
                    return freeSpace >= MinimumFreeSpaceBytes;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Could not check disk space: {ex.Message}");
                return true;
            }
        }

        private async Task CreateRegistryBackupsAsync(
            BackupResult result,
            IProgress<TaskProgressDetail>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                var createDirScript = $@"
if (-not (Test-Path '{BackupDirectory}')) {{
    New-Item -ItemType Directory -Path '{BackupDirectory}' -Force | Out-Null
}}";
                await _psService.ExecuteScriptAsync(createDirScript);

                var backups = new[]
                {
                    ("HKLM", "Winhance_HKLM_Backup.reg"),
                    ("HKCU", "Winhance_HKCU_Backup.reg"),
                    ("HKCR", "Winhance_HKCR_Backup.reg")
                };

                foreach (var (hive, fileName) in backups)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var filePath = Path.Combine(BackupDirectory, fileName);

                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = $"Backing up {hive} registry hive...",
                        IsIndeterminate = true
                    });

                    var success = await ExportRegistryHiveAsync(hive, filePath);

                    if (success)
                    {
                        result.RegistryBackupPaths.Add(filePath);
                        _logService.Log(LogLevel.Info, $"Successfully exported {hive} to {filePath}");
                    }
                    else
                    {
                        result.Warnings.Add($"Failed to export {hive} registry hive");
                        _logService.Log(LogLevel.Warning, $"Failed to export {hive}");
                    }
                }

                result.RegistryBackupCreated = result.RegistryBackupPaths.Count > 0;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Registry backup error: {ex.Message}");
                _logService.Log(LogLevel.Error, $"Error creating registry backups: {ex.Message}");
            }
        }

        private async Task<bool> ExportRegistryHiveAsync(string hive, string outputPath)
        {
            var script = $@"
$result = & reg.exe export {hive} '{outputPath}' /y 2>&1
if ($LASTEXITCODE -eq 0) {{ 'SUCCESS' }} else {{ $result }}";

            try
            {
                var output = await _psService.ExecuteScriptAsync(script);
                return output?.Trim() == "SUCCESS";
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to export {hive}: {ex.Message}");
                return false;
            }
        }

        private async Task CheckExistingRegistryBackupsAsync(BackupResult result)
        {
            var script = $@"
$files = @(
    '{BackupDirectory}\Winhance_HKLM_Backup.reg',
    '{BackupDirectory}\Winhance_HKCU_Backup.reg',
    '{BackupDirectory}\Winhance_HKCR_Backup.reg'
)
$existing = $files | Where-Object {{ Test-Path $_ }}
$existing -join '|'";

            try
            {
                var output = await _psService.ExecuteScriptAsync(script);
                if (!string.IsNullOrWhiteSpace(output))
                {
                    var paths = output.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    result.RegistryBackupPaths.AddRange(paths);
                    result.RegistryBackupExisted = paths.Length > 0;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Error checking existing registry backups: {ex.Message}");
            }
        }
    }
}
