using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services
{
    public class WimCustomizationService : IWimCustomizationService
    {
        private readonly IFileSystemService _fileSystemService;
        private readonly ILogService _logService;
        private readonly HttpClient _httpClient;
        private readonly ILocalizationService _localization;
        private readonly IDriverCategorizer _driverCategorizer;
        private readonly IDismProcessRunner _dismProcessRunner;

        private const string UnattendedWinstallXmlUrl = "https://raw.githubusercontent.com/memstechtips/UnattendedWinstall/main/autounattend.xml";

        public WimCustomizationService(
            IFileSystemService fileSystemService,
            ILogService logService,
            HttpClient httpClient,
            ILocalizationService localization,
            IDriverCategorizer driverCategorizer,
            IDismProcessRunner dismProcessRunner)
        {
            _fileSystemService = fileSystemService;
            _logService = logService;
            _httpClient = httpClient;
            _localization = localization;
            _driverCategorizer = driverCategorizer;
            _dismProcessRunner = dismProcessRunner;
        }

        public async Task<bool> AddDriversAsync(
            string workingDirectory,
            string? driverSourcePath = null,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string sourceDirectory;

                if (string.IsNullOrEmpty(driverSourcePath))
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_ExportingDrivers"),
                        TerminalOutput = "This may take several minutes"
                    });

                    var tempDriverPath = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), $"WinhanceDrivers_{Guid.NewGuid()}");
                    _fileSystemService.CreateDirectory(tempDriverPath);

                    try
                    {
                        var arguments = $"/Online /Export-Driver /Destination:\"{tempDriverPath}\"";

                        progress?.Report(new TaskProgressDetail
                        {
                            TerminalOutput = "Exporting drivers from current system..."
                        });

                        var (exitCode, _) = await _dismProcessRunner.RunProcessWithProgressAsync("dism.exe", arguments, progress, cancellationToken).ConfigureAwait(false);
                        if (exitCode != 0)
                        {
                            throw new Exception($"DISM Export-Driver failed with exit code: {exitCode}");
                        }

                        sourceDirectory = tempDriverPath;
                    }
                    catch (Exception ex)
                    {
                        try { _fileSystemService.DeleteDirectory(tempDriverPath, recursive: true); } catch (Exception cleanupEx) { _logService.LogDebug($"Best-effort temp driver directory cleanup failed: {cleanupEx.Message}"); }
                        _logService.LogError($"Failed to export system drivers: {ex.Message}", ex);
                        return false;
                    }
                }
                else
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_ValidatingDrivers"),
                        TerminalOutput = driverSourcePath
                    });

                    if (!_fileSystemService.DirectoryExists(driverSourcePath))
                    {
                        _logService.LogError($"Driver source path does not exist: {driverSourcePath}");
                        return false;
                    }

                    sourceDirectory = driverSourcePath;
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CategorizingDrivers"),
                    TerminalOutput = "Separating storage and post-install drivers"
                });

                var winpeDriverPath = _fileSystemService.CombinePath(workingDirectory, "sources", "$WinpeDriver$");
                var oemDriverPath = _fileSystemService.CombinePath(workingDirectory, "sources", "$OEM$", "$$", "Drivers");

                _logService.LogInformation($"Searching for drivers in: {sourceDirectory}");

                int copiedCount = await Task.Run(() => _driverCategorizer.CategorizeAndCopyDrivers(
                    sourceDirectory,
                    winpeDriverPath,
                    oemDriverPath,
                    workingDirectory
                ), cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrEmpty(driverSourcePath))
                {
                    try
                    {
                        _fileSystemService.DeleteDirectory(sourceDirectory, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        _logService.LogWarning($"Could not delete temp directory: {ex.Message}");
                    }
                }

                if (copiedCount == 0)
                {
                    _logService.LogWarning($"No drivers were found or copied from: {sourceDirectory}");
                    return false;
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CreatingDriverScript"),
                    TerminalOutput = "Setting up SetupComplete.cmd"
                });

                CreateSetupCompleteScript(workingDirectory);

                _logService.LogInformation($"Successfully added {copiedCount} driver(s) - WinPE: {winpeDriverPath}, OEM: {oemDriverPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding drivers: {ex.Message}", ex);
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_DriverAdditionFailed"),
                    TerminalOutput = ex.Message
                });
                return false;
            }
        }

        public async Task<bool> AddXmlToImageAsync(string xmlPath, string workingDirectory)
        {
            try
            {
                if (!_fileSystemService.FileExists(xmlPath))
                {
                    _logService.LogError($"XML file not found: {xmlPath}");
                    return false;
                }

                if (!_fileSystemService.DirectoryExists(workingDirectory))
                {
                    _logService.LogError($"Working directory not found: {workingDirectory}");
                    return false;
                }

                var destPath = _fileSystemService.CombinePath(workingDirectory, "autounattend.xml");

                var xmlContent = await _fileSystemService.ReadAllTextAsync(xmlPath).ConfigureAwait(false);
                await _fileSystemService.WriteAllTextAsync(destPath, xmlContent).ConfigureAwait(false);

                _logService.LogInformation($"Added autounattend.xml to image: {destPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding XML to image: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<string> DownloadUnattendedWinstallXmlAsync(
            string destinationPath,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_DownloadingXml"),
                    TerminalOutput = UnattendedWinstallXmlUrl
                });

                var xmlContent = await _httpClient.GetStringAsync(UnattendedWinstallXmlUrl, cancellationToken).ConfigureAwait(false);

                _fileSystemService.CreateDirectory(_fileSystemService.GetDirectoryName(destinationPath)!);
                await _fileSystemService.WriteAllTextAsync(destinationPath, xmlContent, cancellationToken).ConfigureAwait(false);

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_XmlDownloaded"),
                    TerminalOutput = $"Saved to: {destinationPath}"
                });

                _logService.LogInformation($"Downloaded UnattendedWinstall XML to: {destinationPath}");
                return destinationPath;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error downloading UnattendedWinstall XML: {ex.Message}", ex);
                throw;
            }
        }

        private void CreateSetupCompleteScript(string workingDirectory)
        {
            try
            {
                var scriptsPath = _fileSystemService.CombinePath(workingDirectory, "sources", "$OEM$", "$$", "Setup", "Scripts");
                _fileSystemService.CreateDirectory(scriptsPath);

                var setupCompleteScript = @"@echo off
REM Winhance Automatic Driver Installation Script
REM This script is executed automatically by Windows Setup

set LOGFILE=C:\Windows\Logs\DriverInstall.log

echo ================================================== > %LOGFILE%
echo Winhance Driver Installation Log >> %LOGFILE%
echo Date: %DATE% %TIME% >> %LOGFILE%
echo ================================================== >> %LOGFILE%
echo. >> %LOGFILE%

echo Installing drivers from C:\Windows\Drivers... >> %LOGFILE%
pnputil /add-driver C:\Windows\Drivers\*.inf /subdirs /install >> %LOGFILE% 2>&1

echo. >> %LOGFILE%
echo Driver installation completed >> %LOGFILE%
echo Exit Code: %ERRORLEVEL% >> %LOGFILE%

exit
";

                var scriptPath = _fileSystemService.CombinePath(scriptsPath, "SetupComplete.cmd");
                _fileSystemService.WriteAllText(scriptPath, setupCompleteScript);

                _logService.LogInformation($"Created SetupComplete.cmd at: {scriptPath}");
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Could not create SetupComplete.cmd: {ex.Message}");
            }
        }
    }
}
