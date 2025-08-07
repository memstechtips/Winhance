using System;
using System.IO;
using System.Reflection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service for detecting script paths with thread-safe caching.
    /// </summary>
    public sealed class ScriptPathDetectionService : IScriptPathDetectionService
    {
        private readonly ILogService _logService;
        private readonly Lazy<ScriptPathInfo> _cachedScriptPathInfo;
        private readonly string _fallbackScriptsPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptPathDetectionService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        public ScriptPathDetectionService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            
            _fallbackScriptsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Winhance", "Scripts");

            _cachedScriptPathInfo = new Lazy<ScriptPathInfo>(
                DetectScriptPathInfo,
                System.Threading.LazyThreadSafetyMode.PublicationOnly);
        }

        /// <inheritdoc/>
        public ScriptPathInfo GetScriptPathInfo() => _cachedScriptPathInfo.Value;

        /// <inheritdoc/>
        public string GetScriptsDirectory() => _cachedScriptPathInfo.Value.ScriptsDirectory;

        private ScriptPathInfo DetectScriptPathInfo()
        {
            try
            {
                _logService.LogInformation("Initializing script path detection...");

                var applicationDirectory = GetApplicationDirectory();
                var isExternalMedia = IsRunningFromExternalMediaInternal(applicationDirectory);
                var scriptsDirectory = DetermineScriptsDirectory(applicationDirectory, isExternalMedia);

                var info = new ScriptPathInfo(
                    scriptsDirectory,
                    applicationDirectory,
                    isExternalMedia,
                    _fallbackScriptsPath);

                _logService.LogInformation($"Script path detection complete:");
                _logService.LogInformation($"  Application directory: {info.ApplicationDirectory}");
                _logService.LogInformation($"  Scripts directory: {info.ScriptsDirectory}");
                _logService.LogInformation($"  External media: {info.IsRunningFromExternalMedia}");
                _logService.LogInformation($"  Fallback path: {info.FallbackScriptsPath}");

                return info;
            }
            catch (Exception ex)
            {
                _logService.LogError("Error during script path detection, using fallback", ex);
                var fallback = _fallbackScriptsPath;
                EnsureDirectoryExists(fallback);
                return new ScriptPathInfo(fallback, GetApplicationDirectory(), false, fallback);
            }
        }

        private string DetermineScriptsDirectory(string applicationDirectory, bool isExternalMedia)
        {
            try
            {
                if (isExternalMedia)
                {
                    _logService.LogInformation($"External media detected. Using fallback: {_fallbackScriptsPath}");
                    EnsureDirectoryExists(_fallbackScriptsPath);
                    CopyScriptsToFallbackLocation(applicationDirectory, _fallbackScriptsPath);
                    return _fallbackScriptsPath;
                }

                var scriptsPath = Path.Combine(applicationDirectory, "Scripts");
                _logService.LogInformation($"Local installation detected. Using: {scriptsPath}");
                EnsureDirectoryExists(scriptsPath);
                return scriptsPath;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error determining scripts directory, using fallback: {_fallbackScriptsPath}", ex);
                EnsureDirectoryExists(_fallbackScriptsPath);
                return _fallbackScriptsPath;
            }
        }

        private bool IsRunningFromExternalMediaInternal(string applicationDirectory)
        {
            try
            {
                var appDrive = Path.GetPathRoot(applicationDirectory);
                if (string.IsNullOrEmpty(appDrive))
                    return false;

                var drive = new DriveInfo(appDrive);
                var isRemovable = drive.DriveType == DriveType.Removable || 
                                 drive.DriveType == DriveType.Network;
                
                _logService.LogInformation($"Drive {appDrive} type: {drive.DriveType}, IsRemovable: {isRemovable}");
                return isRemovable;
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Could not determine drive type: {ex.Message}");
                return false;
            }
        }

        private string GetApplicationDirectory()
        {
            try
            {
                var location = Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrEmpty(location))
                {
                    return Path.GetDirectoryName(location) ?? AppContext.BaseDirectory;
                }
                return AppContext.BaseDirectory;
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Error getting application directory: {ex.Message}");
                return AppContext.BaseDirectory;
            }
        }

        private void EnsureDirectoryExists(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    _logService.LogInformation($"Created directory: {directoryPath}");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error creating directory {directoryPath}", ex);
                throw;
            }
        }

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
                if (scriptFiles.Length == 0)
                {
                    _logService.LogInformation("No script files found to copy");
                    return;
                }

                _logService.LogInformation($"Found {scriptFiles.Length} script files to copy");

                foreach (var sourceScript in scriptFiles)
                {
                    var fileName = Path.GetFileName(sourceScript);
                    var targetScript = Path.Combine(fallbackPath, fileName);

                    if (!File.Exists(targetScript) || 
                        File.GetLastWriteTime(sourceScript) > File.GetLastWriteTime(targetScript))
                    {
                        File.Copy(sourceScript, targetScript, true);
                        _logService.LogInformation($"Copied script: {fileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Error copying scripts to fallback: {ex.Message}");
            }
        }
    }
}
