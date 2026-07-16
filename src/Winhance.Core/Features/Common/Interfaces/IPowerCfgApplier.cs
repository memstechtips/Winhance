using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IPowerCfgApplier
{
    /// <summary>Writes one AC or DC value index for a powercfg setting on the ACTIVE scheme and commits it
    /// (PowerWriteAC/DCValueIndex then PowerSetActiveScheme). A DC write is skipped on a battery-less machine,
    /// mirroring the old ExecutePowerCfgSettings hasBattery gate. Returns false if the active scheme cannot be
    /// resolved or the native write fails. The per-(target, context) primitive the new catalog IStateWriter
    /// delegates to (the new apply engine emits one PowerCfgSetOp per context).</summary>
    Task<bool> WriteValueIndexAsync(PowerCfgTarget target, PowerContext context, int value);
}
