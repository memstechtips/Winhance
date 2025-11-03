using System;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class WindowsThemeQueryService : IWindowsThemeQueryService
    {
        private readonly ILogService _logService;

        public WindowsThemeQueryService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public bool IsDarkModeEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
                
                if (key == null)
                {
                    return false;
                }

                var value = key.GetValue("AppsUseLightTheme");
                return value != null && (int)value == 0;
            }
            catch (Exception ex)
            {
                _logService.LogError("Error checking dark mode status", ex);
                return false;
            }
        }

        public void SetDarkMode(bool enabled)
        {
            try
            {
                string[] keys = new[]
                {
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                };

                string[] values = new[] { "AppsUseLightTheme", "SystemUsesLightTheme" };

                foreach (var key in keys)
                {
                    using var registryKey = Registry.CurrentUser.OpenSubKey(key, true);
                    if (registryKey == null) continue;

                    foreach (var value in values)
                    {
                        registryKey.SetValue(value, enabled ? 0 : 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to {(enabled ? "enable" : "disable")} dark mode", ex);
            }
        }
    }
}