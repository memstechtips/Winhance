namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IWindowsVersionService
    {
        string GetWindowsVersion();
        int GetWindowsBuildNumber();
        string GetOsVersionString();
        string GetOsBuildString();
        bool IsWindows11();
        bool IsWindows10();
    }
}