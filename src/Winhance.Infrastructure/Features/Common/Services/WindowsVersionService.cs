using System;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class WindowsVersionService : IWindowsVersionService
    {
        private readonly ILogService _logService;

        public WindowsVersionService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public string GetWindowsVersion()
        {
            try
            {
                var os = Environment.OSVersion;
                if (os.Version.Major == 10)
                {
                    return IsWindows11() ? "Windows 11" : "Windows 10";
                }
                return $"Windows {os.Version}";
            }
            catch (Exception ex)
            {
                _logService.LogError("Error detecting Windows version", ex);
                return "Unknown Windows Version";
            }
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

        public string GetOsVersionString()
        {
            return IsWindows11() ? "Windows 11" : "Windows 10";
        }

        public string GetOsBuildString()
        {
            return GetWindowsBuildNumber().ToString();
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
            catch
            {
                return false;
            }
        }

        public bool IsWindows10()
        {
            var os = Environment.OSVersion;
            return os.Version.Major == 10 && !IsWindows11();
        }
    }
}