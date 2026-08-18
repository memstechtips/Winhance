using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IPowerCfgApplier
{
    // On the ACTIVE scheme, then commits. A DC write is skipped on a battery-less machine. Synchronous: P/Invoke plus a cached battery check.
    bool WriteValueIndex(PowerCfgTarget target, PowerContext context, int value);
}
