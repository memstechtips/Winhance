using System;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

public class WindowsVersionService : IWindowsVersionService
{
    private readonly ILogService _logService;

    public WindowsVersionService(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    public int GetWindowsBuildNumber()
    {
        try
        {
            return Environment.OSVersion.Version.Build;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error getting Windows build number", ex);
            return 0;
        }
    }

    public bool IsWindows11()
    {
        try
        {
            var os = Environment.OSVersion;
            if (os.Version.Major != 10) return false;

            // Check build number first (most reliable)
            if (os.Version.Build >= 22000) return true;

            // Fallback to registry check
            using var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion");
            var productName = key?.GetValue("ProductName")?.ToString() ?? "";
            return productName.IndexOf("Windows 11", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error detecting Windows 11", ex);
            return false;
        }
    }

    public bool IsWindowsServer()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion");
            var productName = key?.GetValue("ProductName")?.ToString() ?? "";
            return productName.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error detecting Windows Server", ex);
            return false;
        }
    }

}
