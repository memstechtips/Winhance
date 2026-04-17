namespace Winhance.Core.Features.Common.Interfaces;

public interface IWindowsVersionService
{
    int GetWindowsBuildNumber();
    int GetWindowsBuildRevision();
    bool IsWindows11();
    bool IsWindowsServer();
}
