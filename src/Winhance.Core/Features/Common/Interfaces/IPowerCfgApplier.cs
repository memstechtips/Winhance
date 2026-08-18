using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IPowerCfgApplier
{
    /// <summary>Writes one AC or DC value index on the ACTIVE scheme and commits it. A DC write is skipped
    /// on a battery-less machine. False if the active scheme cannot be resolved or the native write fails.
    /// Synchronous: the body is P/Invoke into powrprof.dll plus a cached battery check.</summary>
    bool WriteValueIndex(PowerCfgTarget target, PowerContext context, int value);
}
