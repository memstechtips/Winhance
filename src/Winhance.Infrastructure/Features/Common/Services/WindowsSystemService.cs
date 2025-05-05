using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Customize.Enums;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class WindowsSystemService : ISystemServices
    {
        private readonly IRegistryService _registryService;
        private readonly ILogService _logService;
        private readonly IThemeService _themeService;

        public WindowsSystemService(
            IRegistryService registryService,
            ILogService logService,
            IThemeService themeService = null) // Optional to maintain backward compatibility
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _themeService = themeService; // May be null if not provided
        }

        /// <summary>
        /// Gets the registry service.
        /// </summary>
        public IRegistryService RegistryService => _registryService;

        public bool IsAdministrator()
        {
            try
            {
#if WINDOWS
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

                _logService.LogInformation($"Administrator check completed. Is Administrator: {isAdmin}");
                return isAdmin;
#else
                _logService.LogWarning("Administrator check is not supported on this platform.");
                return false;
#endif
            }
            catch (Exception ex)
            {
                _logService.LogError("Error checking administrator status", ex);
                return false;
            }
        }

        public string GetWindowsVersion()
        {
            try
            {
                var osVersion = Environment.OSVersion;
                var version = osVersion.Version;

                string versionString = version.Major == 10
                    ? (version.Build >= 22000 ? "Windows 11" : "Windows 10")
                    : $"Windows {osVersion.Version}";

                _logService.LogInformation($"Detected Windows version: {versionString}");
                return versionString;
            }
            catch (Exception ex)
            {
                _logService.LogError("Error detecting Windows version", ex);
                return "Unknown Windows Version";
            }
        }

        public void RestartExplorer()
        {
            try
            {
                _logService.LogInformation("Attempting to restart Explorer");

                // Kill all explorer processes
                var explorerProcesses = Process.GetProcessesByName("explorer");
                foreach (var process in explorerProcesses)
                {
                    _logService.LogInformation($"Killing Explorer process (PID: {process.Id})");
                    process.Kill();
                }

                // Wait a moment - using Thread.Sleep since we can't use await anymore
                Thread.Sleep(1000);

                // Restart explorer
                Process.Start("explorer.exe");

                _logService.LogSuccess("Explorer restarted successfully");
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to restart Explorer", ex);
            }
        }

        public void RefreshDesktop()
        {
            try
            {
                [DllImport("user32.dll", SetLastError = true)]
                static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

                const uint SPI_SETDESKWALLPAPER = 0x0014;
                const uint SPIF_UPDATEINIFILE = 0x01;
                const uint SPIF_SENDCHANGE = 0x02;

                _logService.LogInformation("Attempting to refresh desktop");
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                _logService.LogSuccess("Desktop refreshed successfully");
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to refresh desktop", ex);
            }
        }

        public bool IsProcessRunning(string processName)
        {
            try
            {
                bool isRunning = Process.GetProcessesByName(processName).Length > 0;
                _logService.LogInformation($"Process check for {processName}: {isRunning}");
                return isRunning;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error checking if process {processName} is running", ex);
                return false;
            }
        }

        public void KillProcess(string processName)
        {
            try
            {
                _logService.LogInformation($"Attempting to kill process: {processName}");

                var processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                {
                    _logService.LogInformation($"Killing process {processName} (PID: {process.Id})");
                    process.Kill();
                }

                _logService.LogSuccess($"Killed all instances of {processName}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to kill process {processName}", ex);
            }
        }

        public bool IsWindows11()
        {
            try
            {
                bool isWindows11 = Environment.OSVersion.Version.Build >= 22000;
                _logService.LogInformation($"Windows 11 check completed. Is Windows 11: {isWindows11}");
                return isWindows11;
            }
            catch (Exception ex)
            {
                _logService.LogError("Error checking Windows 11 version", ex);
                return false;
            }
        }

        public bool RequireAdministrator()
        {
            try
            {
                if (!IsAdministrator())
                {
                    _logService.LogWarning("Application requires administrator privileges. Attempting to elevate.");

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Environment.CurrentDirectory,
                        FileName = Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("MainModule is null"),
                        Verb = "runas"
                    };

                    try
                    {
                        Process.Start(startInfo);
                        _logService.LogInformation("Elevation request sent. Exiting current process.");
                        Environment.Exit(0);
                    }
                    catch (Exception elevationEx)
                    {
                        _logService.LogError("Failed to elevate privileges", elevationEx);
                        return false;
                    }
                }

                _logService.LogInformation("Application is running with administrator privileges");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError("Unexpected error during privilege elevation", ex);
                return false;
            }
        }

        public bool IsDarkModeEnabled()
        {
            // Delegate to ThemeService if available, otherwise use legacy implementation
            if (_themeService != null)
            {
                try
                {
                    return _themeService.IsDarkModeEnabled();
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Error using ThemeService.IsDarkModeEnabled: {ex.Message}. Falling back to legacy implementation.");
                    // Fall through to legacy implementation
                }
            }

            // Legacy implementation
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    WindowsThemeSettings.Registry.ThemesPersonalizeSubKey);

                if (key == null)
                {
                    _logService.LogWarning("Could not open registry key for dark mode check");
                    return false;
                }

                var value = key.GetValue(WindowsThemeSettings.Registry.AppsUseLightThemeName);
                bool isDarkMode = value != null && (int)value == 0;

                _logService.LogInformation($"Dark mode check completed. Is Dark Mode: {isDarkMode}");
                return isDarkMode;
            }
            catch (Exception ex)
            {
                _logService.LogError("Error checking dark mode status", ex);
                return false;
            }
        }

        public void SetDarkMode(bool enabled)
        {
            // Delegate to ThemeService if available, otherwise use legacy implementation
            if (_themeService != null)
            {
                try
                {
                    _themeService.SetThemeMode(enabled);
                    return;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Error using ThemeService.SetThemeMode: {ex.Message}. Falling back to legacy implementation.");
                    // Fall through to legacy implementation
                }
            }

            // Legacy implementation
            try
            {
                _logService.LogInformation($"Attempting to {(enabled ? "enable" : "disable")} dark mode");

                string[] keys = new[]
                {
                    WindowsThemeSettings.Registry.ThemesPersonalizeSubKey
                };

                string[] values = new[]
                {
                    WindowsThemeSettings.Registry.AppsUseLightThemeName,
                    WindowsThemeSettings.Registry.SystemUsesLightThemeName
                };

                foreach (var key in keys)
                {
                    using var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(key, true);
                    if (registryKey == null)
                    {
                        _logService.LogWarning($"Could not open registry key: {key}");
                        continue;
                    }

                    foreach (var value in values)
                    {
                        registryKey.SetValue(value, enabled ? 0 : 1, Microsoft.Win32.RegistryValueKind.DWord);
                        _logService.LogInformation($"Set {value} to {(enabled ? 0 : 1)}");
                    }
                }

                _logService.LogSuccess($"Dark mode {(enabled ? "enabled" : "disabled")} successfully");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to {(enabled ? "enable" : "disable")} dark mode", ex);
            }
        }

        public void SetUacLevel(UacLevel level)
        {
            try
            {
                int levelInt = (int)level;
                if (!UacOptimizations.LevelToRegistryValue.ContainsKey(levelInt))
                {
                    throw new ArgumentException($"Invalid UAC level: {level}");
                }

                int registryValue = UacOptimizations.LevelToRegistryValue[levelInt];
                
                string fullPath = $"HKLM\\{UacOptimizations.RegistryPath}";

                bool keyExists = _registryService.KeyExists(fullPath);
                if (!keyExists)
                {
                    _logService.Log(LogLevel.Info, $"UAC registry key doesn't exist, creating: {fullPath}");
                    _registryService.CreateKey(fullPath);
                }

                _registryService.SetValue(
                    fullPath,
                    UacOptimizations.RegistryName,
                    registryValue,
                    UacOptimizations.ValueKind
                );

                _logService.Log(LogLevel.Info, $"UAC level set to {level} (registry value: {registryValue})");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error setting UAC level: {ex.Message}");
                throw;
            }
        }

        public UacLevel GetUacLevel()
        {
            try
            {
                string fullPath = $"HKLM\\{UacOptimizations.RegistryPath}";

                var value = _registryService.GetValue(fullPath, UacOptimizations.RegistryName);
                var registryValue = Convert.ToInt32(value ?? 5);

                _logService.Log(LogLevel.Info, $"UAC registry value retrieved: {registryValue}");

                if (UacOptimizations.RegistryValueToLevel.TryGetValue(registryValue, out int level))
                {
                    _logService.Log(LogLevel.Info, $"UAC level mapped to: {level}");
                    return (UacLevel)level;
                }
                else
                {
                    _logService.Log(LogLevel.Warning, $"Unknown UAC registry value ({registryValue}) - defaulting to moderate (1)");
                    return UacLevel.Moderate;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting UAC level: {ex.Message}");
                return UacLevel.Moderate;
            }
        }

        public async Task<bool> RefreshWindowsGUI(bool killExplorer)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Refreshing Windows GUI (killExplorer: {killExplorer})");
                
                // Define Windows message constants
                const int HWND_BROADCAST = 0xffff;
                const uint WM_SYSCOLORCHANGE = 0x0015;
                const uint WM_SETTINGCHANGE = 0x001A;
                const uint WM_THEMECHANGE = 0x031A;
                
                // Import Windows API functions
                [DllImport("user32.dll", CharSet = CharSet.Auto)]
                static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
                
                [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
                static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, 
                                                       uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

                SendMessage((IntPtr)HWND_BROADCAST, WM_SYSCOLORCHANGE, IntPtr.Zero, IntPtr.Zero);
                SendMessage((IntPtr)HWND_BROADCAST, WM_THEMECHANGE, IntPtr.Zero, IntPtr.Zero);
                
                if (killExplorer)
                {
                    _logService.Log(LogLevel.Info, "Refreshing Windows GUI by terminating Explorer process");
                    
                    await Task.Delay(500);

                    bool explorerWasRunning = Process.GetProcessesByName("explorer").Length > 0;
                    
                    if (explorerWasRunning)
                    {
                        _logService.Log(LogLevel.Info, "Terminating Explorer processes - Windows will restart it automatically");
                        
                        foreach (var process in Process.GetProcessesByName("explorer"))
                        {
                            try 
                            {
                                process.Kill();
                                _logService.Log(LogLevel.Info, $"Killed Explorer process (PID: {process.Id})");
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(LogLevel.Warning, $"Failed to kill Explorer process: {ex.Message}");
                            }
                        }
                        
                        _logService.Log(LogLevel.Info, "Waiting for Windows to automatically restart Explorer");
                        
                        // Wait for Explorer to be terminated completely
                        await Task.Delay(1000);
                        
                        // Check if Explorer has restarted automatically
                        int retryCount = 0;
                        const int maxRetries = 5;
                        bool explorerRestarted = false;
                        
                        while (retryCount < maxRetries && !explorerRestarted)
                        {
                            if (Process.GetProcessesByName("explorer").Length > 0)
                            {
                                explorerRestarted = true;
                                _logService.Log(LogLevel.Info, "Explorer process restarted automatically");
                            }
                            else
                            {
                                _logService.Log(LogLevel.Warning, $"Explorer not restarted yet, waiting... (Attempt {retryCount + 1}/{maxRetries})");
                                retryCount++;
                                await Task.Delay(1000);
                            }
                        }
                        
                        // If Explorer didn't restart automatically, start it manually
                        if (!explorerRestarted)
                        {
                            _logService.Log(LogLevel.Warning, "Explorer did not restart automatically, starting it manually");
                            try
                            {
                                Process.Start("explorer.exe");
                                _logService.Log(LogLevel.Info, "Explorer process started manually");
                                
                                // Wait for Explorer to initialize
                                await Task.Delay(2000);
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(LogLevel.Error, $"Failed to start Explorer manually: {ex.Message}");
                                return false;
                            }
                        }
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Info, "Refreshing Windows GUI without killing Explorer");
                }
                
                string themeChanged = "ImmersiveColorSet";
                IntPtr themeChangedPtr = Marshal.StringToHGlobalUni(themeChanged);
                
                try
                {
                    IntPtr result;
                    SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, themeChangedPtr, 
                                       0x0000, 1000, out result);
                    
                    SendMessage((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, IntPtr.Zero);
                }
                finally
                {
                    Marshal.FreeHGlobal(themeChangedPtr);
                }

                _logService.Log(LogLevel.Info, "Windows GUI refresh completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error refreshing Windows GUI: {ex.Message}");
                return false;
            }
        }

        public Task<bool> RefreshWindowsGUI()
        {
            return RefreshWindowsGUI(true);
        }

        private async Task RunCommand(string command, string arguments)
        {
            try
            {
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logService.Log(LogLevel.Warning, $"Command failed: {command} {arguments}");
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error running command: {ex.Message}");
                throw;
            }
        }
    }
}
