using System.Management;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal sealed class SystemRestoreService : ISystemRestoreService
{
    private const string SystemRestoreClientGuid = "{09F7EDC5-294E-4180-AF6A-FB0E6A0E9513}";
    private const string SppClientsKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SPP\Clients";
    private const string SystemRestorePolicyKeyPath = @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore";
    private const string DisableSrValueName = "DisableSR";

    private readonly ILogService _logService;

    // C:'s volume GUID cannot change while the process lives, but the enabled state can - so only the WMI
    // lookup is cached and both registry reads stay live. That removes the per-batch round trip without a
    // staleness window: the setting toggles through a PowerShell ScriptEffect, so no service owns an
    // invalidation hook to call.
    private readonly Lazy<string?> _cDeviceId;

    public SystemRestoreService(ILogService logService)
    {
        _logService = logService;
        _cDeviceId = new Lazy<string?>(QueryCDeviceId, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool IsEnabledForC()
    {
        try
        {
            using (var policyKey = Registry.LocalMachine.OpenSubKey(SystemRestorePolicyKeyPath))
            {
                if (policyKey?.GetValue(DisableSrValueName) is int p && p == 1)
                {
                    _logService.Log(LogLevel.Info,
                        "[SystemRestoreService] DisableSR group policy is set; reporting Disabled");
                    return false;
                }
            }

            var cDeviceId = _cDeviceId.Value;
            if (string.IsNullOrEmpty(cDeviceId))
            {
                _logService.Log(LogLevel.Warning,
                    "[SystemRestoreService] Could not resolve C: volume DeviceID; reporting Disabled");
                return false;
            }

            using var sppKey = Registry.LocalMachine.OpenSubKey(SppClientsKeyPath);
            if (sppKey?.GetValue(SystemRestoreClientGuid) is not string[] entries)
            {
                _logService.Log(LogLevel.Info,
                    "[SystemRestoreService] SPP\\Clients value missing or not REG_MULTI_SZ; reporting Disabled");
                return false;
            }

            var enabled = entries.Any(e =>
                !string.IsNullOrEmpty(e) &&
                e.StartsWith(cDeviceId, StringComparison.OrdinalIgnoreCase));

            _logService.Log(LogLevel.Info,
                $"[SystemRestoreService] IsEnabledForC = {enabled} (cDeviceId={cDeviceId}, entries={entries.Length})");
            return enabled;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning,
                $"[SystemRestoreService] IsEnabledForC threw {ex.GetType().Name}: {ex.Message}; reporting Disabled");
            return false;
        }
    }

    private string? QueryCDeviceId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID FROM Win32_Volume WHERE DriveLetter='C:'");
            using var collection = searcher.Get();
            foreach (ManagementObject mo in collection)
            {
                using (mo)
                    return mo["DeviceID"] as string;
            }
            return null;
        }
        catch (Exception ex)
        {
            // Caught here rather than left to propagate: ExecutionAndPublication caches a thrown exception
            // and would rethrow it for the life of the process. Null falls through to the Disabled report.
            _logService.Log(LogLevel.Warning,
                $"[SystemRestoreService] C: volume lookup threw {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}
