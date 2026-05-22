using System.Management;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

public sealed class SystemRestoreService(ILogService logService) : ISystemRestoreService
{
    private const string SystemRestoreClientGuid = "{09F7EDC5-294E-4180-AF6A-FB0E6A0E9513}";
    private const string SppClientsKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SPP\Clients";
    private const string SystemRestorePolicyKeyPath = @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore";
    private const string DisableSrValueName = "DisableSR";

    public bool IsEnabledForC()
    {
        try
        {
            // Group-policy override.
            using (var policyKey = Registry.LocalMachine.OpenSubKey(SystemRestorePolicyKeyPath))
            {
                if (policyKey?.GetValue(DisableSrValueName) is int p && p == 1)
                {
                    logService.Log(LogLevel.Info,
                        "[SystemRestoreService] DisableSR group policy is set; reporting Disabled");
                    return false;
                }
            }

            // C: volume DeviceID via WMI.
            string? cDeviceId = null;
            using (var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID FROM Win32_Volume WHERE DriveLetter='C:'"))
            using (var collection = searcher.Get())
            {
                foreach (ManagementObject mo in collection)
                {
                    using (mo)
                    {
                        cDeviceId = mo["DeviceID"] as string;
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(cDeviceId))
            {
                logService.Log(LogLevel.Warning,
                    "[SystemRestoreService] Could not resolve C: volume DeviceID; reporting Disabled");
                return false;
            }

            // SPP\Clients\{SR GUID} REG_MULTI_SZ.
            using var sppKey = Registry.LocalMachine.OpenSubKey(SppClientsKeyPath);
            if (sppKey?.GetValue(SystemRestoreClientGuid) is not string[] entries)
            {
                logService.Log(LogLevel.Info,
                    "[SystemRestoreService] SPP\\Clients value missing or not REG_MULTI_SZ; reporting Disabled");
                return false;
            }

            var enabled = entries.Any(e =>
                !string.IsNullOrEmpty(e) &&
                e.StartsWith(cDeviceId, StringComparison.OrdinalIgnoreCase));

            logService.Log(LogLevel.Info,
                $"[SystemRestoreService] IsEnabledForC = {enabled} (cDeviceId={cDeviceId}, entries={entries.Length})");
            return enabled;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning,
                $"[SystemRestoreService] IsEnabledForC threw {ex.GetType().Name}: {ex.Message}; reporting Disabled");
            return false;
        }
    }
}
