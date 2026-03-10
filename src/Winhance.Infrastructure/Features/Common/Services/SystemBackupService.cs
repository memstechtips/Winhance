using System;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Services;

public class SystemBackupService : ISystemBackupService
{
    private readonly ILogService _logService;
    private readonly ILocalizationService _localization;
    private readonly IProcessExecutor _processExecutor;

    private const string RestorePointName = "Winhance Initial Restore Point";
    private const int VerificationMaxRetries = 10;
    private static readonly TimeSpan VerificationRetryDelay = TimeSpan.FromSeconds(3);

    public SystemBackupService(
        ILogService logService,
        ILocalizationService localization,
        IProcessExecutor processExecutor)
    {
        _logService = logService;
        _localization = localization;
        _processExecutor = processExecutor;
    }

    public async Task<BackupResult> EnsureInitialBackupsAsync(
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logService.Log(LogLevel.Info, "Starting backup process - checking for existing restore point...");

            // Report: Checking restore point
            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_CheckingRestorePoint"),
                IsIndeterminate = true
            });

            var existingPoint = await FindRestorePointAsync(RestorePointName).ConfigureAwait(false);
            if (existingPoint != null)
            {
                _logService.Log(LogLevel.Info, $"Restore point '{RestorePointName}' already exists (created: {existingPoint.Value}). Skipping creation.");
                return BackupResult.CreateSuccess(restorePointDate: existingPoint.Value);
            }

            _logService.Log(LogLevel.Info, $"No existing restore point found with name '{RestorePointName}'");

            // Report: Checking if System Restore is enabled
            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_CheckingRestoreStatus"),
                IsIndeterminate = true
            });

            bool systemRestoreWasDisabled = false;
            var isEnabled = await CheckSystemRestoreEnabledAsync().ConfigureAwait(false);
            if (!isEnabled)
            {
                _logService.Log(LogLevel.Warning, "System Restore is currently disabled");
                systemRestoreWasDisabled = true;

                // Report: Enabling System Restore
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_EnablingRestore"),
                    IsIndeterminate = true
                });

                _logService.Log(LogLevel.Info, "Attempting to enable System Restore...");
                var enabled = await EnableSystemRestoreAsync().ConfigureAwait(false);
                if (!enabled)
                {
                    _logService.Log(LogLevel.Error, "Failed to enable System Restore - cannot create restore point");
                    return BackupResult.CreateFailure(
                        "Failed to enable System Restore - cannot create restore point",
                        systemRestoreWasDisabled: true);
                }

                _logService.Log(LogLevel.Info, "System Restore enabled successfully");
            }
            else
            {
                _logService.Log(LogLevel.Info, "System Restore is already enabled");
            }

            // Report: Creating restore point
            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_CreatingRestorePoint"),
                IsIndeterminate = true
            });

            _logService.Log(LogLevel.Info, $"Creating new restore point with name '{RestorePointName}'...");

            var (apiSuccess, statusCode) = await CreateRestorePointAsync(RestorePointName).ConfigureAwait(false);

            if (!apiSuccess)
            {
                var statusDesc = SrClientApi.GetStatusDescription(statusCode);
                _logService.Log(LogLevel.Error, $"Failed to create restore point '{RestorePointName}'. Status: {statusCode} ({statusDesc})");
                return BackupResult.CreateFailure(
                    $"Failed to create system restore point: {statusDesc}",
                    systemRestoreWasDisabled: systemRestoreWasDisabled);
            }

            if (statusCode != SrClientApi.ERROR_SUCCESS)
            {
                var statusDesc = SrClientApi.GetStatusDescription(statusCode);
                _logService.Log(LogLevel.Warning, $"SRSetRestorePointW returned success but status code is {statusCode} ({statusDesc})");
            }

            // Verify the restore point was actually created by polling WMI
            _logService.Log(LogLevel.Info, "Restore point API call succeeded, verifying creation...");

            var verifiedDate = await VerifyRestorePointCreatedAsync(RestorePointName, cancellationToken).ConfigureAwait(false);

            if (verifiedDate != null)
            {
                _logService.Log(LogLevel.Info, $"Successfully verified restore point '{RestorePointName}' exists (created: {verifiedDate.Value})");
                return BackupResult.CreateSuccess(
                    restorePointDate: verifiedDate.Value,
                    restorePointCreated: true,
                    systemRestoreWasDisabled: systemRestoreWasDisabled);
            }
            else
            {
                _logService.Log(LogLevel.Error, $"Restore point '{RestorePointName}' could not be verified after creation. The API reported success but the point was not found via WMI query.");
                return BackupResult.CreateFailure(
                    "System restore point creation could not be verified",
                    systemRestoreWasDisabled: systemRestoreWasDisabled);
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error ensuring initial backups: {ex.Message}");
            return BackupResult.CreateFailure(ex.Message);
        }
    }

    private async Task<DateTime?> VerifyRestorePointCreatedAsync(string description, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= VerificationMaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(VerificationRetryDelay, cancellationToken).ConfigureAwait(false);

            var found = await FindRestorePointAsync(description).ConfigureAwait(false);
            if (found != null)
            {
                _logService.Log(LogLevel.Info, $"Restore point verified on attempt {attempt}/{VerificationMaxRetries}");
                return found;
            }

            _logService.Log(LogLevel.Info, $"Restore point not yet visible (attempt {attempt}/{VerificationMaxRetries}), retrying...");
        }

        return null;
    }

    private async Task<bool> CheckSystemRestoreEnabledAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\default",
                    "SELECT * FROM SystemRestore");
                using var results = searcher.Get();
                return results.Count > 0;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed to check System Restore status: {ex.Message}");
                return false;
            }
        }).ConfigureAwait(false);
    }

    private async Task<DateTime?> FindRestorePointAsync(string description)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Querying for restore point: '{description}'");

                var escapedDescription = description.Replace("'", "\\'");
                using var searcher = new ManagementObjectSearcher(
                    @"root\default",
                    $"SELECT * FROM SystemRestore WHERE Description = '{escapedDescription}'");
                using var results = searcher.Get();

                foreach (ManagementObject obj in results)
                {
                    using (obj)
                    {
                        _logService.Log(LogLevel.Info, $"Found existing restore point: '{description}'");

                        var creationTimeStr = obj["CreationTime"]?.ToString();
                        if (creationTimeStr != null)
                        {
                            return (DateTime?)ManagementDateTimeConverter.ToDateTime(creationTimeStr);
                        }
                        return (DateTime?)DateTime.Now;
                    }
                }

                _logService.Log(LogLevel.Info, $"No restore point found with description: '{description}'");
                return (DateTime?)null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error querying restore point: {ex.Message}");
                return (DateTime?)null;
            }
        }).ConfigureAwait(false);
    }

    private async Task<(bool Success, int StatusCode)> CreateRestorePointAsync(string description)
    {
        return await Task.Run(() =>
        {
            try
            {
                var restorePointInfo = new SrClientApi.RESTOREPOINTINFO
                {
                    dwEventType = SrClientApi.BEGIN_SYSTEM_CHANGE,
                    dwRestorePtType = SrClientApi.MODIFY_SETTINGS,
                    llSequenceNumber = 0,
                    szDescription = description
                };

                var success = SrClientApi.SRSetRestorePointW(ref restorePointInfo, out var status);
                if (!success)
                {
                    _logService.Log(LogLevel.Error, $"Failed to create restore point. Status: {status.nStatus} ({SrClientApi.GetStatusDescription(status.nStatus)})");
                }
                return (success, status.nStatus);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to create restore point: {ex.Message}");
                return (false, -1);
            }
        }).ConfigureAwait(false);
    }

    private async Task<bool> EnableSystemRestoreAsync()
    {
        try
        {
            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";

            // Enable System Restore via WMI (blocking COM call, run on thread pool)
            await Task.Run(() =>
            {
                using var restoreClass = new ManagementClass(
                    new ManagementScope(@"\\.\root\default"),
                    new ManagementPath("SystemRestore"),
                    new ObjectGetOptions());

                var inParams = restoreClass.GetMethodParameters("Enable");
                inParams["Drive"] = systemDrive + "\\";
                restoreClass.InvokeMethod("Enable", inParams, null);
            }).ConfigureAwait(false);

            _logService.Log(LogLevel.Info, "System Restore enabled via WMI");

            // Resize shadow storage
            await _processExecutor.ExecuteAsync(
                "vssadmin",
                $"Resize ShadowStorage /For={systemDrive} /On={systemDrive} /MaxSize=10GB")
                .ConfigureAwait(false);

            _logService.Log(LogLevel.Info, "Shadow storage resized");
            return true;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Failed to enable System Restore: {ex.Message}");
            return false;
        }
    }
}
