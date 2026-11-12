using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Winhance.Core.Features.Common.Enums;
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

    public bool SetSysColors(int index, uint colorRef) =>
        PInvoke.SetSysColors([index], [(COLORREF)colorRef]);

    // fWinIni 0: the catalog's own registry write persists the value, and SPIF_UPDATEINIFILE would write back the
    // whole in-memory UserPreferencesMask, carrying any bit that was changed in the registry but not yet live.
    public unsafe bool SetLive(SystemParameter parameter, bool on)
    {
        const SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS noIniWrite = 0;
        switch (parameter)
        {
            case SystemParameter.MinimizeAnimation:
                var info = new ANIMATIONINFO { cbSize = (uint)sizeof(ANIMATIONINFO), iMinAnimate = on ? 1 : 0 };
                return PInvoke.SystemParametersInfo(
                    SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETANIMATION, info.cbSize, &info, noIniWrite);

            // These two take the value in uiParam; a value passed in pvParam is ignored and reads as off.
            case SystemParameter.DragFullWindows:
            case SystemParameter.FontSmoothing:
                var action = parameter == SystemParameter.DragFullWindows
                    ? SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETDRAGFULLWINDOWS
                    : SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETFONTSMOOTHING;
                return PInvoke.SystemParametersInfo(action, on ? 1u : 0u, null, noIniWrite);

            default:
                return PInvoke.SystemParametersInfo(
                    MaskAction(parameter), 0, (void*)(nint)(on ? 1 : 0), noIniWrite);
        }
    }

    // The UserPreferencesMask bits, whose SET actions take the BOOL itself in pvParam.
    private static SYSTEM_PARAMETERS_INFO_ACTION MaskAction(SystemParameter parameter) => parameter switch
    {
        SystemParameter.ClientAreaAnimation => SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCLIENTAREAANIMATION,
        SystemParameter.MenuAnimation => SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETMENUANIMATION,
        SystemParameter.ComboBoxAnimation => SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCOMBOBOXANIMATION,
        SystemParameter.ListBoxSmoothScrolling => SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETLISTBOXSMOOTHSCROLLING,
        SystemParameter.SelectionFade => SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETSELECTIONFADE,
        SystemParameter.ToolTipAnimation => SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETTOOLTIPANIMATION,
        SystemParameter.CursorShadow => SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCURSORSHADOW,
        SystemParameter.DropShadow => SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETDROPSHADOW,
        _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter, null),
    };
}
