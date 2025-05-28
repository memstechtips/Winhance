using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Models.Enums;

// We're now using the UacLevel from Models.Enums directly

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Contains detailed information about the Windows version
    /// </summary>
    public class WindowsVersionInfo
    {
        /// <summary>
        /// The full OS version
        /// </summary>
        public Version Version { get; set; }

        /// <summary>
        /// The major version number
        /// </summary>
        public int MajorVersion { get; set; }

        /// <summary>
        /// The minor version number
        /// </summary>
        public int MinorVersion { get; set; }

        /// <summary>
        /// The build number
        /// </summary>
        public int BuildNumber { get; set; }

        /// <summary>
        /// The product name from registry
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// Whether this is Windows 11 based on build number
        /// </summary>
        public bool IsWindows11ByBuild { get; set; }

        /// <summary>
        /// Whether this is Windows 11 based on product name
        /// </summary>
        public bool IsWindows11ByProductName { get; set; }

        /// <summary>
        /// Whether this is Windows 11 (combined determination)
        /// </summary>
        public bool IsWindows11 { get; set; }

        /// <summary>
        /// Whether this is Windows 10
        /// </summary>
        public bool IsWindows10 { get; set; }
    }

    public class WindowsSystemService : ISystemServices
    {
        // Dependencies
        private readonly IRegistryService _registryService;
        private readonly ILogService _logService;
        private readonly IThemeService _themeService;
        private readonly IUacSettingsService _uacSettingsService;
        private readonly IInternetConnectivityService _connectivityService;

        public WindowsSystemService(
            IRegistryService registryService,
            ILogService logService,
            IInternetConnectivityService connectivityService,
            IThemeService themeService = null,
            IUacSettingsService uacSettingsService = null
        ) // Optional to maintain backward compatibility
        {
            _registryService =
                registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _connectivityService =
                connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
            _themeService = themeService; // May be null if not provided
            _uacSettingsService = uacSettingsService; // May be null if not provided
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

                _logService.LogInformation(
                    $"Administrator check completed. Is Administrator: {isAdmin}"
                );
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

        /// <summary>
        /// Gets detailed Windows version information including build number and product name
        /// </summary>
        /// <returns>A WindowsVersionInfo object containing detailed version information</returns>
        private WindowsVersionInfo GetWindowsVersionInfo()
        {
            var result = new WindowsVersionInfo();

            try
            {
                var osVersion = Environment.OSVersion;
                result.Version = osVersion.Version;
                result.MajorVersion = osVersion.Version.Major;
                result.MinorVersion = osVersion.Version.Minor;
                result.BuildNumber = osVersion.Version.Build;

                // Check if Windows 11 using build number
                result.IsWindows11ByBuild =
                    result.MajorVersion == 10 && result.BuildNumber >= 22000;

                // Check registry ProductName for more reliable detection
                try
                {
                    using (
                        var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"
                        )
                    )
                    {
                        if (key != null)
                        {
                            result.ProductName = key.GetValue("ProductName") as string;
                            result.IsWindows11ByProductName =
                                result.ProductName != null
                                && result.ProductName.Contains("Windows 11");

                            // Log the actual product name for debugging
                            _logService.LogInformation(
                                $"Windows registry ProductName: {result.ProductName}"
                            );
                        }
                    }
                }
                catch (Exception regEx)
                {
                    _logService.LogWarning($"Error reading registry ProductName: {regEx.Message}");
                }

                // Log detailed version information
                _logService.LogInformation(
                    $"Windows build number: {result.BuildNumber}, IsWin11ByBuild: {result.IsWindows11ByBuild}, IsWin11ByProductName: {result.IsWindows11ByProductName}"
                );

                // Determine if this is Windows 11 using both methods, with ProductName taking precedence if available
                result.IsWindows11 =
                    result.IsWindows11ByProductName
                    || (result.IsWindows11ByBuild && !result.IsWindows11ByProductName == false);
                result.IsWindows10 = result.MajorVersion == 10 && !result.IsWindows11;
            }
            catch (Exception ex)
            {
                _logService.LogError("Error getting Windows version info", ex);
            }

            return result;
        }

        public string GetWindowsVersion()
        {
            try
            {
                var versionInfo = GetWindowsVersionInfo();
                string versionString;

                if (versionInfo.MajorVersion == 10)
                {
                    versionString = versionInfo.IsWindows11 ? "Windows 11" : "Windows 10";
                }
                else
                {
                    versionString = $"Windows {versionInfo.Version}";
                }

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
                static extern bool SystemParametersInfo(
                    uint uiAction,
                    uint uiParam,
                    IntPtr pvParam,
                    uint fWinIni
                );

                const uint SPI_SETDESKWALLPAPER = 0x0014;
                const uint SPIF_UPDATEINIFILE = 0x01;
                const uint SPIF_SENDCHANGE = 0x02;

                _logService.LogInformation("Attempting to refresh desktop");
                SystemParametersInfo(
                    SPI_SETDESKWALLPAPER,
                    0,
                    IntPtr.Zero,
                    SPIF_UPDATEINIFILE | SPIF_SENDCHANGE
                );

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
                    _logService.LogInformation(
                        $"Killing process {processName} (PID: {process.Id})"
                    );
                    process.Kill();
                }

                _logService.LogSuccess($"Killed all instances of {processName}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to kill process {processName}", ex);
            }
        }

        // IsInternetConnected method has been moved to InternetConnectivityService

        // IsInternetConnectedAsync method has been moved to InternetConnectivityService

        public bool IsWindows11()
        {
            try
            {
                // Use our centralized version detection method
                var versionInfo = GetWindowsVersionInfo();

                _logService.LogInformation(
                    $"Windows 11 check completed. Is Windows 11: {versionInfo.IsWindows11} (By build: {versionInfo.IsWindows11ByBuild}, By ProductName: {versionInfo.IsWindows11ByProductName})"
                );
                return versionInfo.IsWindows11;
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
                    _logService.LogWarning(
                        "Application requires administrator privileges. Attempting to elevate."
                    );

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Environment.CurrentDirectory,
                        FileName =
                            Process.GetCurrentProcess().MainModule?.FileName
                            ?? throw new InvalidOperationException("MainModule is null"),
                        Verb = "runas",
                    };

                    try
                    {
                        // Start the elevated process and capture the Process object
                        Process elevatedProcess = Process.Start(startInfo);

                        // If elevatedProcess is null, it means the UAC prompt was canceled or denied
                        if (elevatedProcess == null)
                        {
                            _logService.LogWarning(
                                "User denied UAC elevation. Application will exit."
                            );
                            Environment.Exit(1); // Exit with error code to indicate denial
                            return false;
                        }

                        _logService.LogInformation(
                            "Elevation request accepted. Exiting current process."
                        );
                        Environment.Exit(0);
                    }
                    catch (System.ComponentModel.Win32Exception w32Ex)
                        when (w32Ex.NativeErrorCode == 1223)
                    {
                        // Error code 1223 specifically means "The operation was canceled by the user"
                        // This happens when the user clicks "No" on the UAC prompt
                        _logService.LogWarning("User denied UAC elevation. Application will exit.");
                        Environment.Exit(1); // Exit with error code to indicate denial
                        return false;
                    }
                    catch (Exception elevationEx)
                    {
                        _logService.LogError("Failed to elevate privileges", elevationEx);
                        Environment.Exit(1); // Exit with error code to indicate failure
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
                    _logService.Log(
                        LogLevel.Warning,
                        $"Error using ThemeService.IsDarkModeEnabled: {ex.Message}. Falling back to legacy implementation."
                    );
                    // Fall through to legacy implementation
                }
            }

            // Legacy implementation
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    WindowsThemeSettings.Registry.ThemesPersonalizeSubKey
                );

                if (key == null)
                {
                    _logService.LogWarning("Could not open registry key for dark mode check");
                    return false;
                }

                var value = key.GetValue(WindowsThemeSettings.Registry.AppsUseLightThemeName);
                bool isDarkMode = value != null && (int)value == 0;

                _logService.LogInformation(
                    $"Dark mode check completed. Is Dark Mode: {isDarkMode}"
                );
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
                    _logService.Log(
                        LogLevel.Warning,
                        $"Error using ThemeService.SetThemeMode: {ex.Message}. Falling back to legacy implementation."
                    );
                    // Fall through to legacy implementation
                }
            }

            // Legacy implementation
            try
            {
                _logService.LogInformation(
                    $"Attempting to {(enabled ? "enable" : "disable")} dark mode"
                );

                string[] keys = new[] { WindowsThemeSettings.Registry.ThemesPersonalizeSubKey };

                string[] values = new[]
                {
                    WindowsThemeSettings.Registry.AppsUseLightThemeName,
                    WindowsThemeSettings.Registry.SystemUsesLightThemeName,
                };

                foreach (var key in keys)
                {
                    using var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        key,
                        true
                    );
                    if (registryKey == null)
                    {
                        _logService.LogWarning($"Could not open registry key: {key}");
                        continue;
                    }

                    foreach (var value in values)
                    {
                        registryKey.SetValue(
                            value,
                            enabled ? 0 : 1,
                            Microsoft.Win32.RegistryValueKind.DWord
                        );
                        _logService.LogInformation($"Set {value} to {(enabled ? 0 : 1)}");
                    }
                }

                _logService.LogSuccess(
                    $"Dark mode {(enabled ? "enabled" : "disabled")} successfully"
                );
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to {(enabled ? "enable" : "disable")} dark mode", ex);
            }
        }

        public void SetUacLevel(Winhance.Core.Models.Enums.UacLevel level)
        {
            try
            {
                // No need to convert as we're already using the Core UacLevel
                Winhance.Core.Models.Enums.UacLevel coreLevel = level;

                // Special handling for Custom UAC level
                if (coreLevel == Winhance.Core.Models.Enums.UacLevel.Custom)
                {
                    // For Custom level, try to get the saved custom settings and apply them
                    if (_uacSettingsService != null && _uacSettingsService.TryGetCustomUacValues(
                        out int customConsentPromptValue, 
                        out int customSecureDesktopValue))
                    {
                        _logService.Log(
                            LogLevel.Info,
                            $"Applying saved custom UAC settings: ConsentPrompt={customConsentPromptValue}, SecureDesktop={customSecureDesktopValue}"
                        );
                        
                        string registryPath = $"HKLM\\{UacOptimizations.RegistryPath}";
                        
                        // Set the ConsentPromptBehaviorAdmin value
                        _registryService.SetValue(
                            registryPath,
                            UacOptimizations.ConsentPromptName,
                            customConsentPromptValue,
                            UacOptimizations.ValueKind
                        );
                        
                        // Set the PromptOnSecureDesktop value
                        _registryService.SetValue(
                            registryPath,
                            UacOptimizations.SecureDesktopName,
                            customSecureDesktopValue,
                            UacOptimizations.ValueKind
                        );
                        
                        return;
                    }
                    else
                    {
                        // No saved custom settings found
                        _logService.Log(
                            LogLevel.Warning,
                            "Custom UAC level selected but no saved settings found - preserving current registry settings"
                        );
                        return;
                    }
                }

                // Get both registry values for the selected UAC level
                if (
                    !UacOptimizations.UacLevelToConsentPromptValue.TryGetValue(
                        coreLevel,
                        out int consentPromptValue
                    )
                    || !UacOptimizations.UacLevelToSecureDesktopValue.TryGetValue(
                        coreLevel,
                        out int secureDesktopValue
                    )
                )
                {
                    throw new ArgumentException($"Invalid UAC level: {level}");
                }

                string fullPath = $"HKLM\\{UacOptimizations.RegistryPath}";

                bool keyExists = _registryService.KeyExists(fullPath);
                if (!keyExists)
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"UAC registry key doesn't exist, creating: {fullPath}"
                    );
                    _registryService.CreateKey(fullPath);
                }

                // Set the ConsentPromptBehaviorAdmin value
                _registryService.SetValue(
                    fullPath,
                    UacOptimizations.ConsentPromptName,
                    consentPromptValue,
                    UacOptimizations.ValueKind
                );

                // Set the PromptOnSecureDesktop value
                _registryService.SetValue(
                    fullPath,
                    UacOptimizations.SecureDesktopName,
                    secureDesktopValue,
                    UacOptimizations.ValueKind
                );

                string levelName = UacOptimizations.UacLevelNames.TryGetValue(
                    coreLevel,
                    out string name
                )
                    ? name
                    : coreLevel.ToString();
                _logService.Log(
                    LogLevel.Info,
                    $"UAC level set to {levelName} (ConsentPrompt: {consentPromptValue}, SecureDesktop: {secureDesktopValue})"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error setting UAC level: {ex.Message}");
                throw;
            }
        }

        public Winhance.Core.Models.Enums.UacLevel GetUacLevel()
        {
            try
            {
                string fullPath = $"HKLM\\{UacOptimizations.RegistryPath}";

                // Get both registry values needed to determine the UAC level
                var consentPromptValue = _registryService.GetValue(
                    fullPath,
                    UacOptimizations.ConsentPromptName
                );
                var secureDesktopValue = _registryService.GetValue(
                    fullPath,
                    UacOptimizations.SecureDesktopName
                );

                // Convert to integers with appropriate defaults if values are null
                var consentPromptInt = Convert.ToInt32(consentPromptValue ?? 5); // Default to 5 (Notify)
                var secureDesktopInt = Convert.ToInt32(secureDesktopValue ?? 1); // Default to 1 (Enabled)

                _logService.Log(
                    LogLevel.Info,
                    $"UAC registry values retrieved: ConsentPrompt={consentPromptInt}, SecureDesktop={secureDesktopInt}"
                );

                // Determine the UacLevel from both registry values
                Winhance.Core.Models.Enums.UacLevel coreLevel =
                    UacOptimizations.GetUacLevelFromRegistryValues(
                        consentPromptInt,
                        secureDesktopInt,
                        _uacSettingsService
                    );
                string levelName = UacOptimizations.UacLevelNames.TryGetValue(
                    coreLevel,
                    out string name
                )
                    ? name
                    : coreLevel.ToString();

                _logService.Log(LogLevel.Info, $"UAC level mapped to: {levelName}");

                // Return the Core UacLevel directly
                return coreLevel;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting UAC level: {ex.Message}");
                return Winhance.Core.Models.Enums.UacLevel.NotifyChangesOnly; // Default value
            }
        }

        public async Task<bool> RefreshWindowsGUI(bool killExplorer)
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Refreshing Windows GUI (killExplorer: {killExplorer})"
                );

                // Define Windows message constants
                const int HWND_BROADCAST = 0xffff;
                const uint WM_SYSCOLORCHANGE = 0x0015;
                const uint WM_SETTINGCHANGE = 0x001A;
                const uint WM_THEMECHANGE = 0x031A;

                // Import Windows API functions
                [DllImport("user32.dll", CharSet = CharSet.Auto)]
                static extern IntPtr SendMessage(
                    IntPtr hWnd,
                    uint Msg,
                    IntPtr wParam,
                    IntPtr lParam
                );

                [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
                static extern IntPtr SendMessageTimeout(
                    IntPtr hWnd,
                    uint Msg,
                    IntPtr wParam,
                    IntPtr lParam,
                    uint fuFlags,
                    uint uTimeout,
                    out IntPtr lpdwResult
                );

                SendMessage((IntPtr)HWND_BROADCAST, WM_SYSCOLORCHANGE, IntPtr.Zero, IntPtr.Zero);
                SendMessage((IntPtr)HWND_BROADCAST, WM_THEMECHANGE, IntPtr.Zero, IntPtr.Zero);

                if (killExplorer)
                {
                    _logService.Log(
                        LogLevel.Info,
                        "Refreshing Windows GUI by terminating Explorer process"
                    );

                    await Task.Delay(500);

                    bool explorerWasRunning = Process.GetProcessesByName("explorer").Length > 0;

                    if (explorerWasRunning)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            "Terminating Explorer processes - Windows will restart it automatically"
                        );

                        foreach (var process in Process.GetProcessesByName("explorer"))
                        {
                            try
                            {
                                process.Kill();
                                _logService.Log(
                                    LogLevel.Info,
                                    $"Killed Explorer process (PID: {process.Id})"
                                );
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(
                                    LogLevel.Warning,
                                    $"Failed to kill Explorer process: {ex.Message}"
                                );
                            }
                        }

                        _logService.Log(
                            LogLevel.Info,
                            "Waiting for Windows to automatically restart Explorer"
                        );

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
                                _logService.Log(
                                    LogLevel.Info,
                                    "Explorer process restarted automatically"
                                );
                            }
                            else
                            {
                                _logService.Log(
                                    LogLevel.Warning,
                                    $"Explorer not restarted yet, waiting... (Attempt {retryCount + 1}/{maxRetries})"
                                );
                                retryCount++;
                                await Task.Delay(1000);
                            }
                        }

                        // If Explorer didn't restart automatically, start it manually
                        if (!explorerRestarted)
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                "Explorer did not restart automatically, starting it manually"
                            );
                            try
                            {
                                Process.Start("explorer.exe");
                                _logService.Log(LogLevel.Info, "Explorer process started manually");

                                // Wait for Explorer to initialize
                                await Task.Delay(2000);
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(
                                    LogLevel.Error,
                                    $"Failed to start Explorer manually: {ex.Message}"
                                );
                                return false;
                            }
                        }
                    }
                }
                else
                {
                    _logService.Log(
                        LogLevel.Info,
                        "Refreshing Windows GUI without killing Explorer"
                    );
                }

                string themeChanged = "ImmersiveColorSet";
                IntPtr themeChangedPtr = Marshal.StringToHGlobalUni(themeChanged);

                try
                {
                    IntPtr result;
                    SendMessageTimeout(
                        (IntPtr)HWND_BROADCAST,
                        WM_SETTINGCHANGE,
                        IntPtr.Zero,
                        themeChangedPtr,
                        0x0000,
                        1000,
                        out result
                    );

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

        /// <summary>
        /// Checks if the system has an active internet connection.
        /// </summary>
        /// <param name="forceCheck">If true, bypasses the cache and performs a fresh check.</param>
        /// <returns>True if internet is connected, false otherwise.</returns>
        public bool IsInternetConnected(bool forceCheck = false)
        {
            return _connectivityService.IsInternetConnected(forceCheck);
        }

        /// <summary>
        /// Asynchronously checks if the system has an active internet connection.
        /// </summary>
        /// <param name="forceCheck">If true, bypasses the cache and performs a fresh check.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if internet is connected, false otherwise.</returns>
        public async Task<bool> IsInternetConnectedAsync(
            bool forceCheck = false,
            CancellationToken cancellationToken = default,
            bool userInitiatedCancellation = false
        )
        {
            return await _connectivityService.IsInternetConnectedAsync(
                forceCheck,
                cancellationToken,
                userInitiatedCancellation
            );
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
                        CreateNoWindow = true,
                    },
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
