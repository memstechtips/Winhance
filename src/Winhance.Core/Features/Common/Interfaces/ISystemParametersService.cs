namespace Winhance.Core.Features.Common.Interfaces;

public interface ISystemParametersService
{
    int SystemParametersInfo(int uAction, int uParam, string? lpvParam, int fuWinIni);

    // index is a SYS_COLOR_INDEX; colorRef is Windows' 0x00BBGGRR form, not RGB. The change lasts the session -
    // what survives a sign-out is the registry value beside it.
    bool SetSysColors(int index, uint colorRef);
}
