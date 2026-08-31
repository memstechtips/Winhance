using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class SystemParametersService : ISystemParametersService
{
    // The int-typed parameters are Core's contract and stay that way: casting at this boundary is
    // what keeps Windows.Win32 types out of Winhance.Core.
    public unsafe int SystemParametersInfo(int uAction, int uParam, string? lpvParam, int fuWinIni)
    {
        fixed (char* pvParam = lpvParam)
        {
            return PInvoke.SystemParametersInfo(
                (SYSTEM_PARAMETERS_INFO_ACTION)uAction,
                (uint)uParam,
                pvParam,
                (SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS)fuWinIni);
        }
    }
}
