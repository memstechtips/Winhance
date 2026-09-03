using Windows.Win32;
using Windows.Win32.System.Power;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class HardwareDetectionService : IHardwareDetectionService
{
    // SYSTEM_POWER_STATUS.BatteryFlag values. Unknown is 0xFF, which carries the NoBattery bit as
    // well, so it has to be tested for equality before the bit test runs.
    private const byte BatteryFlagNoBattery = 128;
    private const byte BatteryFlagUnknown = 255;

    private readonly ILogService _logService;

    // ExecutionAndPublication so a second caller arriving mid-query waits rather than starting its own
    // lookup.
    private readonly Lazy<bool?> _hasBattery;
    private readonly Lazy<bool> _supportsHybridSleep;

    public HardwareDetectionService(ILogService logService)
    {
        _logService = logService;
        _hasBattery = new Lazy<bool?>(QueryHasBattery, LazyThreadSafetyMode.ExecutionAndPublication);
        _supportsHybridSleep = new Lazy<bool>(QuerySupportsHybridSleep, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool? HasBattery() => _hasBattery.Value;

    private bool? QueryHasBattery()
    {
        try
        {
            // null, not false, on every "could not tell" branch: "no battery" and "could not tell"
            // lead callers to different defaults.
            if (!PInvoke.GetSystemPowerStatus(out SYSTEM_POWER_STATUS status))
            {
                _logService.Log(LogLevel.Warning, "GetSystemPowerStatus call failed");
                return null;
            }

            var hasBattery = InterpretBatteryFlag(status.BatteryFlag);
            if (hasBattery is null)
            {
                _logService.Log(LogLevel.Warning, "GetSystemPowerStatus reported an unknown battery state");
            }

            return hasBattery;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error detecting battery: {ex.Message}");
            return null;
        }
    }

    // Split out from the P/Invoke so the whole decision is testable without one: the flag byte is all
    // there is to it. Unknown must be matched by equality before the bit test, because 255 carries
    // the NoBattery bit and would otherwise read as a desktop.
    internal static bool? InterpretBatteryFlag(byte batteryFlag) =>
        batteryFlag == BatteryFlagUnknown
            ? null
            : (batteryFlag & BatteryFlagNoBattery) == 0;

    public bool SupportsHybridSleep() => _supportsHybridSleep.Value;

    private bool QuerySupportsHybridSleep()
    {
        try
        {
            if (!PInvoke.GetPwrCapabilities(out SYSTEM_POWER_CAPABILITIES caps))
            {
                _logService.Log(LogLevel.Warning, "GetPwrCapabilities call failed");
                return false;
            }

            bool supported = caps.FastSystemS4;
            _logService.Log(LogLevel.Info, $"Hybrid sleep supported (FastSystemS4): {supported}");
            return supported;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error detecting hybrid sleep support: {ex.Message}");
            return false;
        }
    }
}
