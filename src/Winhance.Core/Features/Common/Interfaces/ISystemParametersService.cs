namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Abstraction over Windows SystemParametersInfo P/Invoke.
/// </summary>
public interface ISystemParametersService
{
    int SystemParametersInfo(int uAction, int uParam, string? lpvParam, int fuWinIni);
}
