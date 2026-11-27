using System.Globalization;
using System.Management;
using Windows.Win32.Foundation;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class SystemBackupService : ISystemBackupService
{
    private readonly ILogService _logService;
    private readonly ILocalizationService _localization;
    private readonly IProcessExecutor _processExecutor;
    private readonly ISystemRestoreService _systemRestoreService;
    private readonly IWmiApi _wmiApi;
    private readonly ISystemRestorePointWriter _restorePointWriter;

    private const int MaxRetries = 10;
    private static readonly TimeSpan VerificationRetryDelay = TimeSpan.FromSeconds(3);

    // SRSetRestorePointW returns before System Restore registers the point, so removing it at once answers
    // ERROR_FILE_NOT_FOUND. Measured on a VM: at once = not found, 500 ms later = removed.
    private static readonly TimeSpan RemovalRetryDelay = TimeSpan.FromMilliseconds(500);

    // Below this free share of shadow storage, the max size is doubled before creating a restore point.
    private const double MinFreeStoragePercent = 15.0;

    private const string SystemRestoreNamespace = @"root\default";

    // A Volume Shadow Copy error (0x800423xx is vsserror.h's range). SRSetRestorePointW hands it back in nStatus
    // when the VSS service is disabled, although STATEMGRSTATUS documents no VSS codes.
    private const int VssUnexpectedError = unchecked((int)0x80042302);

    public SystemBackupService(
        ILogService logService,
        ILocalizationService localization,
        IProcessExecutor processExecutor,
        ISystemRestoreService systemRestoreService,
        IWmiApi wmiApi,
        ISystemRestorePointWriter restorePointWriter)
    {
        _logService = logService;
        _localization = localization;
        _processExecutor = processExecutor;
        _systemRestoreService = systemRestoreService;
        _wmiApi = wmiApi;
        _restorePointWriter = restorePointWriter;
    }

    public async Task<BackupResult> CreateRestorePointAsync(
        string? name = null,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var turnedOn = false;
        try
        {
            var restorePointName = name
                ?? $"Winhance Restore Point - {DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}";

            _logService.Log(LogLevel.Info, $"Creating restore point '{restorePointName}'...");

            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_CheckingRestoreStatus"),
                IsIndeterminate = true
            });

            cancellationToken.ThrowIfCancellationRequested();
            var isEnabled = _systemRestoreService.IsEnabledForC();
            if (!isEnabled)
            {
                _logService.Log(LogLevel.Warning, "System Restore is currently disabled, enabling...");

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_EnablingRestore"),
                    IsIndeterminate = true
                });

                var enabled = await EnableSystemRestoreAsync().ConfigureAwait(false);
                if (!enabled)
                {
                    _logService.Log(LogLevel.Error, "Failed to enable System Restore");
                    return BackupResult.CreateFailure(_localization.GetString("RestorePoint_Error_EnableFailed"));
                }

                _logService.Log(LogLevel.Info, "System Restore enabled successfully");
                turnedOn = true;
            }

            // Cancel means the machine is left as it was: the point this run made is removed and protection this run
            // turned on goes back off. The native write cannot be interrupted, so a cancel during it lands once it returns.
            uint? createdSequenceNumber = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await EnsureSufficientShadowStorageAsync().ConfigureAwait(false);

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CreatingRestorePoint"),
                    IsIndeterminate = true
                });

                cancellationToken.ThrowIfCancellationRequested();

                var (apiSuccess, statusCode, sequenceNumber) = await CreateRestorePointNativeAsync(restorePointName).ConfigureAwait(false);
                createdSequenceNumber = sequenceNumber;

                if (!apiSuccess)
                {
                    _logService.Log(LogLevel.Error, $"Failed to create restore point. Status: 0x{statusCode:X8}");
                    return BackupResult.CreateFailure(DescribeStatus(statusCode), turnedOn);
                }

                if (statusCode != (int)WIN32_ERROR.ERROR_SUCCESS)
                {
                    _logService.Log(LogLevel.Warning, $"SRSetRestorePointW returned success but status code is 0x{statusCode:X8}");
                }

                _logService.Log(LogLevel.Info, "Restore point API call succeeded, verifying creation...");

                var verifiedDate = await VerifyRestorePointCreatedAsync(restorePointName, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                if (verifiedDate != null)
                {
                    _logService.Log(LogLevel.Info, $"Successfully verified restore point '{restorePointName}' (created: {verifiedDate.Value})");
                    return BackupResult.CreateSuccess(
                        restorePointDate: verifiedDate.Value,
                        restorePointCreated: true);
                }

                _logService.Log(LogLevel.Error, $"Restore point '{restorePointName}' could not be verified after creation.");
                return BackupResult.CreateFailure(_localization.GetString("RestorePoint_Error_NotVerified"), turnedOn);
            }
            catch (OperationCanceledException)
            {
                if (createdSequenceNumber is uint created)
                {
                    await RemoveRestorePointAsync(created).ConfigureAwait(false);
                }

                if (turnedOn)
                {
                    await TurnOffSystemRestoreAsync().ConfigureAwait(false);
                }

                throw;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService.Log(LogLevel.Error, $"Error creating restore point: {ex.Message}");
            return BackupResult.CreateFailure(DescribeStatus(ex.HResult), turnedOn);
        }
    }

    private Task<DateTime?> VerifyRestorePointCreatedAsync(string description, CancellationToken cancellationToken) =>
        RetryUntilAnsweredAsync(VerificationRetryDelay, async attempt =>
        {
            var found = await FindRestorePointAsync(description).ConfigureAwait(false);
            _logService.Log(LogLevel.Info, found != null
                ? $"Restore point verified on attempt {attempt}/{MaxRetries}"
                : $"Restore point not yet visible (attempt {attempt}/{MaxRetries}), retrying...");
            return found;
        }, cancellationToken);

    private static async Task<T?> RetryUntilAnsweredAsync<T>(
        TimeSpan delay, Func<int, Task<T?>> attempt, CancellationToken cancellationToken)
        where T : struct
    {
        for (int i = 1; i <= MaxRetries; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

            if (await attempt(i).ConfigureAwait(false) is T answer)
            {
                return answer;
            }
        }

        return null;
    }

    internal async Task<DateTime?> FindRestorePointAsync(string description)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Querying for restore point: '{description}'");

                var escapedDescription = description.Replace("'", "\\'");
                var results = _wmiApi.Query(
                    SystemRestoreNamespace, "SystemRestore", $"Description = '{escapedDescription}'");

                if (results.Count > 0)
                {
                    using var found = results[0];
                    foreach (var extra in results.Skip(1))
                    {
                        extra.Dispose();
                    }

                    _logService.Log(LogLevel.Info, $"Found existing restore point: '{description}'");

                    var creationTimeStr = found.Get("CreationTime")?.ToString();
                    if (creationTimeStr != null)
                    {
                        return (DateTime?)ManagementDateTimeConverter.ToDateTime(creationTimeStr);
                    }
                    return (DateTime?)DateTime.Now;
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

    private Task<(bool Success, int StatusCode, uint? SequenceNumber)> CreateRestorePointNativeAsync(string description) =>
        Task.Run(() => _restorePointWriter.CreateRestorePoint(description));

    private async Task RemoveRestorePointAsync(uint sequenceNumber)
    {
        try
        {
            var status = await RetryUntilAnsweredAsync(RemovalRetryDelay, async _ =>
            {
                var removal = await Task.Run(() => _restorePointWriter.RemoveRestorePoint(sequenceNumber)).ConfigureAwait(false);
                return removal == (int)WIN32_ERROR.ERROR_FILE_NOT_FOUND ? null : (int?)removal;
            }, CancellationToken.None).ConfigureAwait(false) ?? (int)WIN32_ERROR.ERROR_FILE_NOT_FOUND;

            if (status == (int)WIN32_ERROR.ERROR_SUCCESS)
            {
                _logService.Log(LogLevel.Info, $"Restore point cancelled, removed restore point {sequenceNumber}");
            }
            else
            {
                _logService.Log(LogLevel.Error, $"Failed to remove restore point {sequenceNumber} after cancel. Status: {status}");
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Failed to remove restore point {sequenceNumber} after cancel: {ex.Message}");
        }
    }

    // The nStatus codes STATEMGRSTATUS documents, plus the VSS code a disabled shadow copy service gives.
    internal string DescribeStatus(int statusCode) => statusCode switch
    {
        (int)WIN32_ERROR.ERROR_SERVICE_DISABLED => _localization.GetString("RestorePoint_Error_ServiceDisabled"),
        (int)WIN32_ERROR.ERROR_DISK_FULL => _localization.GetString("RestorePoint_Error_DiskFull"),
        (int)WIN32_ERROR.ERROR_BAD_ENVIRONMENT => _localization.GetString("RestorePoint_Error_SafeMode"),
        (int)WIN32_ERROR.ERROR_TIMEOUT => _localization.GetString("RestorePoint_Error_Timeout"),
        (int)WIN32_ERROR.ERROR_INTERNAL_ERROR => _localization.GetString("RestorePoint_Error_Internal"),
        VssUnexpectedError => _localization.GetString("RestorePoint_Error_ShadowCopy"),
        _ => _localization.GetString("RestorePoint_Error_WindowsCode", $"0x{statusCode:X8}"),
    };

    private async Task EnsureSufficientShadowStorageAsync()
    {
        try
        {
            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";

            var result = await _processExecutor.ExecuteAsync(
                "vssadmin",
                $"list shadowstorage /For={systemDrive}\\").ConfigureAwait(false);

            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                _logService.Log(LogLevel.Warning, "Could not query shadow storage usage, skipping resize check");
                return;
            }

            var (usedBytes, maxBytes) = ParseShadowStorageOutput(result.StandardOutput);
            if (usedBytes < 0 || maxBytes <= 0)
            {
                _logService.Log(LogLevel.Warning, "Could not parse shadow storage values, skipping resize check");
                return;
            }

            var freePercent = (1.0 - (double)usedBytes / maxBytes) * 100.0;
            _logService.Log(LogLevel.Info, $"Shadow storage: used {usedBytes / (1024 * 1024)} MB / max {maxBytes / (1024 * 1024)} MB ({freePercent:F1}% free)");

            if (freePercent < MinFreeStoragePercent)
            {
                var newMaxBytes = maxBytes * 2;
                var newMaxGb = newMaxBytes / (1024L * 1024 * 1024);
                newMaxGb = Math.Clamp(newMaxGb, 1, 64);

                _logService.Log(LogLevel.Info, $"Shadow storage nearly full ({freePercent:F1}% free), resizing max to {newMaxGb} GB");

                await _processExecutor.ExecuteAsync(
                    "vssadmin",
                    $"Resize ShadowStorage /For={systemDrive}\\ /On={systemDrive}\\ /MaxSize={newMaxGb}GB").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning, $"Shadow storage check failed: {ex.Message}");
        }
    }

    private static (long UsedBytes, long MaxBytes) ParseShadowStorageOutput(string output)
    {
        long usedBytes = -1;
        long maxBytes = -1;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.StartsWith("Used Shadow Copy Storage space:", StringComparison.OrdinalIgnoreCase))
            {
                usedBytes = ParseByteValue(line);
            }
            else if (line.StartsWith("Maximum Shadow Copy Storage space:", StringComparison.OrdinalIgnoreCase))
            {
                maxBytes = ParseByteValue(line);
            }
        }

        return (usedBytes, maxBytes);
    }

    private static long ParseByteValue(string line)
    {
        // Extract the part after the colon, e.g. " 9.25 GB (14%)"
        var colonIndex = line.IndexOf(':');
        if (colonIndex < 0) return -1;

        var valuePart = line[(colonIndex + 1)..].Trim();

        var parenIndex = valuePart.IndexOf('(');
        if (parenIndex > 0)
            valuePart = valuePart[..parenIndex].Trim();

        var parts = valuePart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return -1;

        if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return -1;

        var unit = parts[1].ToUpperInvariant();
        var multiplier = unit switch
        {
            "KB" => 1024L,
            "MB" => 1024L * 1024,
            "GB" => 1024L * 1024 * 1024,
            "TB" => 1024L * 1024 * 1024 * 1024,
            _ => -1L
        };

        if (multiplier < 0) return -1;
        return (long)(number * multiplier);
    }

    internal async Task<bool> EnableSystemRestoreAsync()
    {
        try
        {
            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";

            // Enable System Restore via WMI (blocking COM call, run on thread pool)
            await Task.Run(() =>
            {
                using var result = _wmiApi.InvokeClassMethod(
                    SystemRestoreNamespace, "SystemRestore", "Enable",
                    new Dictionary<string, object> { ["Drive"] = systemDrive + "\\" });
            }).ConfigureAwait(false);

            _logService.Log(LogLevel.Info, "System Restore enabled via WMI");

            await _processExecutor.ExecuteAsync(
                "vssadmin",
                $"Resize ShadowStorage /For={systemDrive}\\ /On={systemDrive}\\ /MaxSize=20GB")
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

    public async Task TurnOffSystemRestoreAsync()
    {
        try
        {
            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            await Task.Run(() =>
            {
                using var result = _wmiApi.InvokeClassMethod(
                    SystemRestoreNamespace, "SystemRestore", "Disable",
                    new Dictionary<string, object> { ["Drive"] = systemDrive + "\\" });
            }).ConfigureAwait(false);

            _logService.Log(LogLevel.Info, "System Restore turned back off");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Failed to turn System Restore back off: {ex.Message}");
        }
    }
}
