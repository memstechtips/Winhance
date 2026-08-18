using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Services;

public class PowerCfgApplier(
    IHardwareDetectionService hardwareDetectionService,
    ILogService logService) : IPowerCfgApplier
{

    public bool WriteValueIndex(PowerCfgTarget target, PowerContext context, int value)
    {
        // Per-(target, context) write for a single context: a DC write is skipped on a battery-less machine; the
        // active scheme is resolved, the value index written, and the scheme committed by re-activating it. The
        // apply engine emits one PowerCfgSetOp per context, so the writer calls this once per context (AC then
        // DC). Writing the same value again is a no-op on disk, so it is omitted here.
        // Unknown battery state attempts the write: a wasted DC write on a desktop is harmless, a skipped
        // one on a laptop loses the setting.
        bool hasBattery = hardwareDetectionService.HasBattery() ?? true;
        if (context == PowerContext.DC && !hasBattery)
        {
            logService.Log(LogLevel.Debug, $"[PowerCfgApplier] Skipping DC write for {target.SettingGuid} - no battery present");
            return true;
        }

        var activeSchemeResult = PowerProf.PowerGetActiveScheme(IntPtr.Zero, out var activeSchemePtr);
        if (activeSchemeResult != PowerProf.ERROR_SUCCESS)
        {
            logService.Log(LogLevel.Error, "[PowerCfgApplier] Failed to get active power scheme");
            return false;
        }

        var activeSchemeGuid = Marshal.PtrToStructure<Guid>(activeSchemePtr);
        PowerProf.LocalFree(activeSchemePtr);

        var subgroupGuid = Guid.Parse(target.SubgroupGuid);
        var settingGuid = Guid.Parse(target.SettingGuid);

        var rc = context == PowerContext.DC
            ? PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)value)
            : PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)value);

        // The write lands in the registry; the scheme has to be re-activated before it takes effect.
        var commitRc = PowerProf.PowerSetActiveScheme(IntPtr.Zero, ref activeSchemeGuid);

        var applied = rc == PowerProf.ERROR_SUCCESS && commitRc == PowerProf.ERROR_SUCCESS;
        logService.Log(applied ? LogLevel.Info : LogLevel.Error,
            $"[PowerCfgApplier] {(applied ? "Wrote" : "Failed to write")} {context} value index {value} for setting {target.SettingGuid} (rc={rc}, commit rc={commitRc})");
        return applied;
    }
}
