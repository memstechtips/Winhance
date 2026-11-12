using Winhance.Core.Features.Common.Enums;

namespace Winhance.UI.Helpers;

public enum PageGate
{
    Open,
    EnterBuilder,
    SwitchToAutounattend,
}

internal static class NavLockPolicy
{
    // Unattend and WIMUtil both build the answer file, so both live only in Builder's Autounattend session.
    public static PageGate GateFor(string? tag, WinhanceMode mode, BuilderTarget target) => tag switch
    {
        "Autounattend" or "WimUtil" => mode switch
        {
            WinhanceMode.Builder => target == BuilderTarget.Autounattend ? PageGate.Open : PageGate.SwitchToAutounattend,
            _ => PageGate.EnterBuilder,
        },
        _ => PageGate.Open,
    };
}
