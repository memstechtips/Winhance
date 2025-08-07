using System;
using System.IO;
using System.Management.Automation;
using System.Runtime.InteropServices;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service for detecting PowerShell configuration with thread-safe caching.
    /// </summary>
    public sealed class PowerShellDetectionService : IPowerShellDetectionService
    {
        private readonly ISystemServices _systemServices;
        private readonly ILogService _logService;
        private readonly Lazy<PowerShellInfo> _cachedPowerShellInfo;

        private const string WindowsPowerShellPath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
        private const string PowerShellCorePath = @"C:\Program Files\PowerShell\7\pwsh.exe";

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerShellDetectionService"/> class.
        /// </summary>
        /// <param name="systemServices">The system services for OS detection.</param>
        /// <param name="logService">The logging service.</param>
        public PowerShellDetectionService(ISystemServices systemServices, ILogService logService)
        {
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            
            _cachedPowerShellInfo = new Lazy<PowerShellInfo>(
                DetectPowerShellInfo,
                System.Threading.LazyThreadSafetyMode.PublicationOnly);
        }

        /// <inheritdoc/>
        public PowerShellInfo GetPowerShellInfo() => _cachedPowerShellInfo.Value;

        /// <inheritdoc/>
        public bool ShouldUseWindowsPowerShell() => _cachedPowerShellInfo.Value.UseWindowsPowerShell;

        private PowerShellInfo DetectPowerShellInfo()
        {
            try
            {
                _logService.LogInformation("Initializing PowerShell detection...");
                
                // Get OS version first to include in logs
                var osVersion = GetOSVersion();
                _logService.LogInformation($"Operating System is {osVersion}");
                
                // Determine which PowerShell to use
                var useWindowsPowerShell = ShouldUseWindowsPowerShellInternal();
                
                // Log the decision based on OS
                if (useWindowsPowerShell)
                {
                    _logService.LogInformation("Using Standard Windows PowerShell for compatibility.");
                }
                else
                {
                    _logService.LogInformation("Using PowerShell Core.");
                }
                
                // Verify paths exist
                var powerShellPath = useWindowsPowerShell ? WindowsPowerShellPath : PowerShellCorePath;
                var version = useWindowsPowerShell ? "5.1" : GetPowerShellCoreVersion(powerShellPath);

                var info = new PowerShellInfo(useWindowsPowerShell, powerShellPath, version, osVersion);
                
                _logService.LogInformation($"PowerShell detection complete: {info.PowerShellPath} (v{info.Version})");
                
                return info;
            }
            catch (Exception ex)
            {
                _logService.LogError("Error during PowerShell detection, falling back to Windows PowerShell", ex);
                return new PowerShellInfo(true, WindowsPowerShellPath, "5.1", GetOSVersion());
            }
        }

        private bool ShouldUseWindowsPowerShellInternal()
        {
            try
            {
                // Use the centralized Windows version detection
                var isWindows11 = _systemServices.IsWindows11();
                
                // Check if PowerShell Core exists
                bool powerShellCoreExists = File.Exists(PowerShellCorePath);
                
                if (!powerShellCoreExists)
                {
                    // If PowerShell Core doesn't exist, always use Windows PowerShell
                    return true;
                }
                
                // On Windows 10 or earlier, use Windows PowerShell for compatibility
                // On Windows 11, use PowerShell Core if it exists
                return !isWindows11;
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Error detecting Windows version, defaulting to Windows PowerShell: {ex.Message}");
                return true;
            }
        }

        private string GetOSVersion()
        {
            try
            {
                var osVersion = Environment.OSVersion;
                var isWindows11 = _systemServices.IsWindows11();
                
                return isWindows11 
                    ? $"Windows 11 (Build {osVersion.Version.Build})"
                    : $"Windows 10 (Build {osVersion.Version.Build})";
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Error getting OS version: {ex.Message}");
                return $"Windows (Build {Environment.OSVersion.Version.Build})";
            }
        }

        private string GetPowerShellCoreVersion(string powerShellPath)
        {
            try
            {
                if (!File.Exists(powerShellPath))
                {
                    // If PowerShell Core path doesn't exist, check if Windows PowerShell exists
                    if (File.Exists(WindowsPowerShellPath))
                    {
                        _logService.LogWarning($"PowerShell Core not found at {powerShellPath}, falling back to Windows PowerShell");
                        return "5.1";
                    }
                    else
                    {
                        _logService.LogWarning("Neither PowerShell Core nor Windows PowerShell found at expected paths");
                        return "Unknown";
                    }
                }

                // PowerShell Core exists at the expected path
                return "7.x";
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Error detecting PowerShell version: {ex.Message}");

                return "7.x";
            }
        }
    }
}
