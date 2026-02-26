namespace Winhance.Core.Features.Common.Interfaces;

public interface IWindowsVersionService
{
    int GetWindowsBuildNumber();
    bool IsWindows11();
    bool IsWindowsServer();
}
