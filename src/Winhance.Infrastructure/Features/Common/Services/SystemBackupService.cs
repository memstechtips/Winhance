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

    private const int VerificationMaxRetries = 10;
    private static readonly TimeSpan VerificationRetryDelay = TimeSpan.FromSeconds(3);

    // Below this free share of shadow storage, the max size is doubled before creating a restore point.
    private const double MinFreeStoragePercent = 15.0;

    private const string SystemRestoreNamespace = @"root\default";

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
                    return BackupResult.CreateFailure(
                        "Failed to enable System Restore - cannot create restore point");
                }

                _logService.Log(LogLevel.Info, "System Restore enabled successfully");
            }

            await EnsureSufficientShadowStorageAsync().ConfigureAwait(false);

            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_CreatingRestorePoint"),
                IsIndeterminate = true
            });

            var (apiSuccess, statusCode) = await CreateRestorePointNativeAsync(restorePointName).ConfigureAwait(false);

            if (!apiSuccess)
            {
                var statusDesc = GetStatusDescription(statusCode);
                _logService.Log(LogLevel.Error, $"Failed to create restore point. Status: {statusCode} ({statusDesc})");
                return BackupResult.CreateFailure($"Failed to create system restore point: {statusDesc}");
            }

            if (statusCode != (int)WIN32_ERROR.ERROR_SUCCESS)
            {
                var statusDesc = GetStatusDescription(statusCode);
                _logService.Log(LogLevel.Warning, $"SRSetRestorePointW returned success but status code is {statusCode} ({statusDesc})");
            }

            _logService.Log(LogLevel.Info, "Restore point API call succeeded, verifying creation...");

            var verifiedDate = await VerifyRestorePointCreatedAsync(restorePointName, cancellationToken).ConfigureAwait(false);

            if (verifiedDate != null)
            {
                _logService.Log(LogLevel.Info, $"Successfully verified restore point '{restorePointName}' (created: {verifiedDate.Value})");
                return BackupResult.CreateSuccess(
                    restorePointDate: verifiedDate.Value,
                    restorePointCreated: true);
            }
            else
            {
                _logService.Log(LogLevel.Error, $"Restore point '{restorePointName}' could not be verified after creation.");
                return BackupResult.CreateFailure("System restore point creation could not be verified");
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error creating restore point: {ex.Message}");
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

    private async Task<(bool Success, int StatusCode)> CreateRestorePointNativeAsync(string description)
    {
        return await Task.Run(() =>
        {
            try
            {
                var (success, statusCode) = _restorePointWriter.CreateRestorePoint(description);
                if (!success)
                {
                    _logService.Log(LogLevel.Error, $"Failed to create restore point. Status: {statusCode} ({GetStatusDescription(statusCode)})");
                }
                return (success, statusCode);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to create restore point: {ex.Message}");
                return (false, -1);
            }
        }).ConfigureAwait(false);
    }

    private static string GetStatusDescription(int statusCode)
    {
        return statusCode switch
        {
            (int)WIN32_ERROR.ERROR_SUCCESS => "Success",
            (int)WIN32_ERROR.ERROR_SERVICE_DISABLED => "System Restore service is disabled",
            (int)WIN32_ERROR.ERROR_DISK_FULL => "Insufficient disk space for restore point",
            (int)WIN32_ERROR.ERROR_INTERNAL_ERROR => "Internal error in System Restore service",
            (int)WIN32_ERROR.ERROR_TIMEOUT => "Operation timed out",
            _ => $"Unknown status code ({statusCode})"
        };
    }

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
}
