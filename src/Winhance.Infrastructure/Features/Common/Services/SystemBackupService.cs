using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class SystemBackupService : ISystemBackupService
    {
        private readonly ILogService _logService;
        private readonly ILocalizationService _localization;

        private const string RestorePointName = "Winhance Initial Restore Point";

        public SystemBackupService(
            ILogService logService,
            ILocalizationService localization)
        {
            _logService = logService;
            _localization = localization;
        }

        public async Task<BackupResult> EnsureInitialBackupsAsync(
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new BackupResult();

            try
            {
                _logService.Log(LogLevel.Info, "Starting backup process - checking for existing restore point...");

                // Report: Checking restore point
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CheckingRestorePoint"),
                    IsIndeterminate = true
                });

                var existingPoint = await FindRestorePointAsync(RestorePointName);
                if (existingPoint != null)
                {
                    _logService.Log(LogLevel.Info, $"Restore point '{RestorePointName}' already exists (created: {existingPoint.Value}). Skipping creation.");
                    result.RestorePointDate = existingPoint.Value;
                    result.Success = true;
                    return result;
                }

                _logService.Log(LogLevel.Info, $"No existing restore point found with name '{RestorePointName}'");

                // Report: Checking if System Restore is enabled
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CheckingRestoreStatus"),
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
                        StatusText = _localization.GetString("Progress_EnablingRestore"),
                        IsIndeterminate = true
                    });

                    _logService.Log(LogLevel.Info, "Attempting to enable System Restore...");
                    var enabled = await EnableSystemRestoreAsync();
                    if (!enabled)
                    {
                        _logService.Log(LogLevel.Error, "Failed to enable System Restore - cannot create restore point");
                        result.Success = true;
                        return result;
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
                catch
                {
                    return false;
                }
            });
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
                        _logService.Log(LogLevel.Info, $"Found existing restore point: '{description}'");
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
            });
        }

        [DllImport("SrClient.dll", CharSet = CharSet.Unicode)]
        private static extern bool SRSetRestorePointW(
            ref RESTOREPOINTINFO pRestorePtSpec,
            out STATEMGRSTATUS pSMgrStatus);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RESTOREPOINTINFO
        {
            public int dwEventType;
            public int dwRestorePtType;
            public long llSequenceNumber;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szDescription;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STATEMGRSTATUS
        {
            public int nStatus;
            public long llSequenceNumber;
        }

        private const int BEGIN_SYSTEM_CHANGE = 100;
        private const int MODIFY_SETTINGS = 12;

        private async Task<bool> CreateRestorePointAsync(string description)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var restorePointInfo = new RESTOREPOINTINFO
                    {
                        dwEventType = BEGIN_SYSTEM_CHANGE,
                        dwRestorePtType = MODIFY_SETTINGS,
                        llSequenceNumber = 0,
                        szDescription = description
                    };

                    var success = SRSetRestorePointW(ref restorePointInfo, out var status);
                    if (!success)
                    {
                        _logService.Log(LogLevel.Error, $"Failed to create restore point. Status: {status.nStatus}");
                    }
                    return success;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Failed to create restore point: {ex.Message}");
                    return false;
                }
            });
        }

        private async Task<bool> EnableSystemRestoreAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";

                    // Enable System Restore via WMI
                    using var restoreClass = new ManagementClass(
                        new ManagementScope(@"\\.\root\default"),
                        new ManagementPath("SystemRestore"),
                        new ObjectGetOptions());

                    var inParams = restoreClass.GetMethodParameters("Enable");
                    inParams["Drive"] = systemDrive + "\\";
                    restoreClass.InvokeMethod("Enable", inParams, null);

                    _logService.Log(LogLevel.Info, "System Restore enabled via WMI");

                    // Resize shadow storage
                    using var process = new Process();
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "vssadmin",
                        Arguments = $"Resize ShadowStorage /For={systemDrive} /On={systemDrive} /MaxSize=10GB",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };
                    process.Start();
                    process.WaitForExit(30000);

                    _logService.Log(LogLevel.Info, "Shadow storage resized");
                    return true;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Failed to enable System Restore: {ex.Message}");
                    return false;
                }
            });
        }
    }
}
