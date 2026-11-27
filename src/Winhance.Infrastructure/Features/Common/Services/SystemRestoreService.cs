using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal sealed class SystemRestoreService : ISystemRestoreService
{
    private const string SystemRestoreClientGuid = "{09F7EDC5-294E-4180-AF6A-FB0E6A0E9513}";
    private const string SppClientsKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SPP\Clients";
    private const string SystemRestorePolicyKeyPath = @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore";
    private const string DisableSrValueName = "DisableSR";

    // The documented sizing for a volume GUID path buffer.
    private const int VolumeNameBufferLength = 50;

    private readonly ILogService _logService;

    // C:'s volume GUID cannot change while the process lives, but the enabled state can - so only the
    // volume lookup is cached and both registry reads stay live. That removes the per-batch round trip
    // without a staleness window: the setting toggles through a PowerShell ScriptEffect, so no service
    // owns an invalidation hook to call.
    private readonly Lazy<string?> _cDeviceId;
    private readonly IWmiApi _wmiApi;

    public SystemRestoreService(ILogService logService, IWmiApi wmiApi)
    {
        _logService = logService;
        _wmiApi = wmiApi;
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
                        "DisableSR group policy is set; reporting Disabled");
                    return false;
                }
            }

            var cDeviceId = _cDeviceId.Value;
            if (string.IsNullOrEmpty(cDeviceId))
            {
                _logService.Log(LogLevel.Warning,
                    "Could not resolve C: volume DeviceID; reporting Disabled");
                return false;
            }

            using var sppKey = Registry.LocalMachine.OpenSubKey(SppClientsKeyPath);
            if (sppKey?.GetValue(SystemRestoreClientGuid) is not string[] entries)
            {
                _logService.Log(LogLevel.Info,
                    "SPP\\Clients value missing or not REG_MULTI_SZ; reporting Disabled");
                return false;
            }

            var enabled = entries.Any(e =>
                !string.IsNullOrEmpty(e) &&
                e.StartsWith(cDeviceId, StringComparison.OrdinalIgnoreCase));

            _logService.Log(LogLevel.Info,
                $"IsEnabledForC = {enabled} (cDeviceId={cDeviceId}, entries={entries.Length})");
            return enabled;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning,
                $"IsEnabledForC threw {ex.GetType().Name}: {ex.Message}; reporting Disabled");
            return false;
        }
    }

    private string? QueryCDeviceId() => QueryCDeviceIdNative() ?? QueryCDeviceIdFromWmi();

    // Same \\?\Volume{guid}\ string Win32_Volume.DeviceID reports, read in-process instead of over a
    // COM round trip to WmiPrvSE. The function has no answer on ReFS or SMB, so WMI stays behind it.
    internal unsafe string? QueryCDeviceIdNative(string mountPoint = @"C:\")
    {
        try
        {
            Span<char> buffer = stackalloc char[VolumeNameBufferLength];
            if (!PInvoke.GetVolumeNameForVolumeMountPoint(mountPoint, buffer))
            {
                _logService.Log(LogLevel.Info,
                    $"GetVolumeNameForVolumeMountPoint failed (error {Marshal.GetLastWin32Error()}); falling back to WMI");
                return null;
            }

            var end = buffer.IndexOf('\0');
            return new string(buffer[..(end < 0 ? buffer.Length : end)]);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning,
                $"C: volume native lookup threw {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    internal string? QueryCDeviceIdFromWmi()
    {
        try
        {
            var volumes = _wmiApi.Query(WmiScope.Cimv2, "Win32_Volume", "DriveLetter='C:'");
            if (volumes.Count == 0)
            {
                return null;
            }

            using var found = volumes[0];
            foreach (var extra in volumes.Skip(1))
            {
                extra.Dispose();
            }

            return found.Get("DeviceID") as string;
        }
        catch (Exception ex)
        {
            // Caught here rather than left to propagate: ExecutionAndPublication caches a thrown exception
            // and would rethrow it for the life of the process. Null falls through to the Disabled report.
            _logService.Log(LogLevel.Warning,
                $"C: volume lookup threw {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}
