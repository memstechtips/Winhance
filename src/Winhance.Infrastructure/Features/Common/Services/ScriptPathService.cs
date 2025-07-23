using System;
using System.IO;
using System.Reflection;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service for managing script paths with support for both installed and portable versions.
    /// Handles external media detection and provides appropriate fallback locations.
    /// </summary>
    public class ScriptPathService : IScriptPathService
    {
        private readonly ILogService _logService;
        private readonly string _fallbackScriptsPath;
        private readonly string _applicationDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptPathService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        public ScriptPathService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            
            // Use PROGRAMDATA for system-wide access since admin rights are always available
            _fallbackScriptsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Winhance", "Scripts");

            _applicationDirectory = GetApplicationDirectory();
            
            _logService.LogInformation($"ScriptPathService initialized. App directory: {_applicationDirectory}");
            _logService.LogInformation($"Fallback scripts path: {_fallbackScriptsPath}");
        }

        /// <inheritdoc/>
        public string GetScriptsDirectory()
        {
            try
            {
                var appDirectory = _applicationDirectory;
                var appDrive = Path.GetPathRoot(appDirectory);
                
                _logService.LogInformation($"Checking drive type for: {appDrive}");

                // Check if running from external/removable media
                if (IsRemovableMedia(appDrive))
                {
                    _logService.LogInformation($"External media detected ({appDrive}). Using fallback location: {_fallbackScriptsPath}");
                    
                    // Ensure fallback directory exists
                    EnsureDirectoryExists(_fallbackScriptsPath);
                    
                    // Copy scripts from application directory to fallback if they exist
                    CopyScriptsToFallbackLocation(appDirectory, _fallbackScriptsPath);
                    
                    return _fallbackScriptsPath;
                }
                
                // Use application directory for local installations
                var scriptsPath = Path.Combine(appDirectory, "Scripts");
                _logService.LogInformation($"Local installation detected. Using scripts path: {scriptsPath}");
                
                EnsureDirectoryExists(scriptsPath);
                return scriptsPath;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error determining scripts directory. Using fallback: {_fallbackScriptsPath}", ex);
                EnsureDirectoryExists(_fallbackScriptsPath);
                return _fallbackScriptsPath;
            }
        }

        /// <inheritdoc/>
        public string GetScriptPath(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
                throw new ArgumentException("Script name cannot be null or empty", nameof(scriptName));

            var scriptsDirectory = GetScriptsDirectory();
            var scriptFileName = scriptName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) 
                ? scriptName 
                : $"{scriptName}.ps1";
                
            return Path.Combine(scriptsDirectory, scriptFileName);
        }

        /// <inheritdoc/>
        public bool IsRunningFromExternalMedia()
        {
            try
            {
                var appDrive = Path.GetPathRoot(_applicationDirectory);
                return IsRemovableMedia(appDrive);
            }
            catch (Exception ex)
            {
                _logService.LogError("Error checking if running from external media", ex);
                return false; // Assume local if detection fails
            }
        }

        /// <inheritdoc/>
        public string GetApplicationDirectory()
        {
            try
            {
                // Get the directory where the current executable is located
                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var location = assembly.Location;
                
                if (string.IsNullOrEmpty(location))
                {
                    // Fallback for single-file deployments
                    location = AppContext.BaseDirectory;
                }
                
                return Path.GetDirectoryName(location) ?? AppContext.BaseDirectory;
            }
            catch (Exception ex)
            {
                _logService.LogError("Error getting application directory", ex);
                return AppContext.BaseDirectory;
            }
        }

        /// <summary>
        /// Checks if the specified drive path is removable or network media.
        /// </summary>
        /// <param name="drivePath">The drive path to check.</param>
        /// <returns>True if the drive is removable or network media, false otherwise.</returns>
        private bool IsRemovableMedia(string drivePath)
        {
            try
            {
                if (string.IsNullOrEmpty(drivePath))
                    return false;

                var drive = new DriveInfo(drivePath);
                var isRemovable = drive.DriveType == DriveType.Removable || 
                                 drive.DriveType == DriveType.Network;
                
                _logService.LogInformation($"Drive {drivePath} type: {drive.DriveType}, IsRemovable: {isRemovable}");
                return isRemovable;
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Could not determine drive type for {drivePath}: {ex.Message}");
                return false; // Assume local if detection fails
            }
        }

        /// <summary>
        /// Ensures the specified directory exists, creating it if necessary.
        /// </summary>
        /// <param name="directoryPath">The directory path to ensure exists.</param>
        private void EnsureDirectoryExists(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    _logService.LogInformation($"Creating directory: {directoryPath}");
                    Directory.CreateDirectory(directoryPath);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error creating directory {directoryPath}", ex);
                throw;
            }
        }

        /// <summary>
        /// Copies scripts from the application directory to the fallback location if they exist and are newer.
        /// </summary>
        /// <param name="appDirectory">The application directory.</param>
        /// <param name="fallbackPath">The fallback scripts directory.</param>
        private void CopyScriptsToFallbackLocation(string appDirectory, string fallbackPath)
        {
            try
            {
                var sourceScriptsPath = Path.Combine(appDirectory, "Scripts");
                
                if (!Directory.Exists(sourceScriptsPath))
                {
                    _logService.LogInformation($"No source scripts directory found at: {sourceScriptsPath}");
                    return;
                }

                var scriptFiles = Directory.GetFiles(sourceScriptsPath, "*.ps1");
                _logService.LogInformation($"Found {scriptFiles.Length} script files to copy");

                foreach (var sourceScript in scriptFiles)
                {
                    var fileName = Path.GetFileName(sourceScript);
                    var targetScript = Path.Combine(fallbackPath, fileName);

                    // Copy if target doesn't exist or source is newer
                    if (!File.Exists(targetScript) || 
                        File.GetLastWriteTime(sourceScript) > File.GetLastWriteTime(targetScript))
                    {
                        _logService.LogInformation($"Copying script: {fileName}");
                        File.Copy(sourceScript, targetScript, true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Error copying scripts to fallback location: {ex.Message}");
                // Don't throw - this is not critical for operation
            }
        }
    }
}
