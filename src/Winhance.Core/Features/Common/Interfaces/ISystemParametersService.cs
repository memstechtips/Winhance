namespace Winhance.Core.Features.Common.Interfaces;

public interface ISystemParametersService
{
    int SystemParametersInfo(int uAction, int uParam, string? lpvParam, int fuWinIni);
}
