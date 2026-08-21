using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IPowerCfgApplier
{
    // scheme null means the ACTIVE scheme, which is then committed by re-activating it; a named scheme is
    // written without a commit, because Windows only requires re-activation for the scheme in use. A DC write
    // is skipped on a battery-less machine. Synchronous: P/Invoke plus a cached battery check.
    bool WriteValueIndex(PowerCfgTarget target, PowerContext context, int value, Guid? scheme = null);

    // Holds back the active-scheme commit until the scope closes, so a burst of writes re-activates once
    // instead of once each. One re-activation commits every write that preceded it.
    IDisposable BeginBatch();
}
