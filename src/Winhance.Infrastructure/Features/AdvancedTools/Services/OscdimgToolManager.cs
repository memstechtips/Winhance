using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services
{
    public class OscdimgToolManager : IOscdimgToolManager
    {
        private readonly IFileSystemService _fileSystemService;
        private readonly ILogService _logService;
        private readonly HttpClient _httpClient;
        private readonly IWinGetPackageInstaller _winGetPackageInstaller;
        private readonly IWinGetBootstrapper _winGetBootstrapper;
        private readonly ILocalizationService _localization;
        private readonly IDismProcessRunner _dismProcessRunner;

        private static readonly string[] AdkDownloadSources = new[]
        {
            "https://go.microsoft.com/fwlink/?linkid=2289980",
            "https://download.microsoft.com/download/2/d/9/2d9c8902-3fcd-48a6-a22a-432b08bed61e/ADK/adksetup.exe"
        };

        public OscdimgToolManager(
            IFileSystemService fileSystemService,
            ILogService logService,
            HttpClient httpClient,
            IWinGetPackageInstaller winGetPackageInstaller,
            IWinGetBootstrapper winGetBootstrapper,
            ILocalizationService localization,
            IDismProcessRunner dismProcessRunner)
        {
            _fileSystemService = fileSystemService;
            _logService = logService;
            _httpClient = httpClient;
            _winGetPackageInstaller = winGetPackageInstaller;
            _winGetBootstrapper = winGetBootstrapper;
            _localization = localization;
            _dismProcessRunner = dismProcessRunner;
        }

        public string GetOscdimgPath()
        {
            var searchPaths = new[]
            {
                // ADK paths
                @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
                @"C:\Program Files (x86)\Windows Kits\11\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
                @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\x86\Oscdimg\oscdimg.exe",
                // Winget Links paths
                @"C:\Program Files\WinGet\Links\oscdimg.exe",
                _fileSystemService.CombinePath(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Microsoft\WinGet\Links\oscdimg.exe"),
            };

            foreach (var path in searchPaths)
            {
                if (_fileSystemService.FileExists(path))
                {
                    return path;
                }
            }

            // Scan winget Packages directories for Microsoft.OSCDIMG
            var wingetPackageDirs = new[]
            {
                @"C:\Program Files\WinGet\Packages",
                _fileSystemService.CombinePath(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Microsoft\WinGet\Packages"),
            };

            foreach (var packagesDir in wingetPackageDirs)
            {
                if (!_fileSystemService.DirectoryExists(packagesDir))
                    continue;

                try
                {
                    var matchingDirs = Directory.GetDirectories(packagesDir, "Microsoft.OSCDIMG_*");
                    foreach (var dir in matchingDirs)
                    {
                        var candidate = _fileSystemService.CombinePath(dir, "oscdimg.exe");
                        if (_fileSystemService.FileExists(candidate))
                        {
                            return candidate;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogDebug($"Error scanning winget packages directory {packagesDir}: {ex.Message}");
                }
            }

            return string.Empty;
        }

        public Task<bool> IsOscdimgAvailableAsync()
        {
            var oscdimgPath = GetOscdimgPath();
            if (string.IsNullOrEmpty(oscdimgPath))
            {
                _logService.LogInformation("oscdimg.exe not found in Windows Kits directories");
                return Task.FromResult(false);
            }

            _logService.LogInformation($"oscdimg.exe found at: {oscdimgPath}");
            return Task.FromResult(true);
        }

        public async Task<bool> EnsureOscdimgAvailableAsync(
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (await IsOscdimgAvailableAsync().ConfigureAwait(false))
            {
                _logService.LogInformation("oscdimg.exe already available");
                return true;
            }

            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_PreparingInstallAdk"),
                TerminalOutput = "Checking installation methods"
            });

            if (await InstallOscdimgPackageViaWingetAsync(progress, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            _logService.LogWarning("Microsoft.OSCDIMG package installation failed, trying direct ADK installation...");
            if (await InstallAdkDeploymentToolsAsync(progress, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            _logService.LogWarning("Standard ADK installation failed, trying winget...");
            if (await InstallAdkViaWingetAsync(progress, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            _logService.LogError("All methods to install oscdimg.exe failed");
            return false;
        }

        private async Task<string?> DownloadAdkSetupAsync(
            IProgress<TaskProgressDetail>? progress,
            CancellationToken cancellationToken)
        {
            var tempPath = _fileSystemService.GetTempPath();
            var adkSetupPath = _fileSystemService.CombinePath(tempPath, "adksetup.exe");

            foreach (var sourceUrl in AdkDownloadSources)
            {
                try
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_DownloadingAdkInstaller"),
                        TerminalOutput = $"Source: {sourceUrl}"
                    });

                    _httpClient.Timeout = TimeSpan.FromMinutes(30);
                    var response = await _httpClient.GetAsync(sourceUrl, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    var setupBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                    await _fileSystemService.WriteAllBytesAsync(adkSetupPath, setupBytes, cancellationToken).ConfigureAwait(false);

                    _logService.LogInformation($"ADK installer downloaded successfully from: {sourceUrl}");
                    return adkSetupPath;
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Failed to download from {sourceUrl}: {ex.Message}");
                }
            }

            return null;
        }

        private async Task<bool> InstallAdkDeploymentToolsAsync(
            IProgress<TaskProgressDetail>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                var adkSetupPath = await DownloadAdkSetupAsync(progress, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(adkSetupPath))
                {
                    _logService.LogError("Failed to download ADK installer from all sources");
                    return false;
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_InstallingAdkTools"),
                    TerminalOutput = "This may take several minutes"
                });

                var logPath = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), "adk_install.log");
                var arguments = $"/quiet /norestart /features OptionId.DeploymentTools /ceip off /log \"{logPath}\"";

                progress?.Report(new TaskProgressDetail
                {
                    TerminalOutput = "Starting ADK Deployment Tools installation..."
                });

                var (exitCode, _) = await _dismProcessRunner.RunProcessWithProgressAsync(adkSetupPath, arguments, progress, cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new Exception($"ADK installation failed with exit code: {exitCode}");
                }

                if (await IsOscdimgAvailableAsync().ConfigureAwait(false))
                {
                    _logService.LogInformation("ADK installed and oscdimg.exe found");
                    return true;
                }

                _logService.LogError("ADK installed but oscdimg.exe not found");
                return false;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error installing ADK: {ex.Message}", ex);
                return false;
            }
            finally
            {
                try
                {
                    var adkSetupPath = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), "adksetup.exe");
                    if (_fileSystemService.FileExists(adkSetupPath))
                    {
                        _fileSystemService.DeleteFile(adkSetupPath);
                    }
                }
                catch (Exception ex) { _logService.LogDebug($"Best-effort ADK setup file cleanup failed: {ex.Message}"); }
            }
        }

        private async Task<bool> InstallAdkViaWingetAsync(
            IProgress<TaskProgressDetail>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CheckingWinget"),
                    TerminalOutput = "Verifying winget availability"
                });

                var wingetInstalled = await _winGetPackageInstaller.IsWinGetInstalledAsync(cancellationToken).ConfigureAwait(false);

                if (!wingetInstalled)
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_InstallingWinget"),
                        TerminalOutput = "winget is required for this installation method"
                    });

                    var wingetInstallSuccess = await _winGetBootstrapper.InstallWinGetAsync(cancellationToken).ConfigureAwait(false);
                    if (!wingetInstallSuccess)
                    {
                        _logService.LogError("Failed to install winget");
                        return false;
                    }
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_InstallingAdkViaWinget"),
                    TerminalOutput = "This may take several minutes"
                });

                var logPath = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), "adk_winget_install.log");
                var arguments = $"install Microsoft.WindowsADK --exact --silent --accept-package-agreements --accept-source-agreements --override \"/quiet /norestart /features OptionId.DeploymentTools /ceip off\" --log \"{logPath}\"";

                progress?.Report(new TaskProgressDetail
                {
                    TerminalOutput = "Starting ADK installation via winget..."
                });

                // Prefer system winget (kept up-to-date via Store) over bundled CLI
                var systemAvailable = _winGetBootstrapper.IsSystemWinGetAvailable
                    || WinGetCliRunner.IsSystemWinGetAvailable();

                string wingetExe;
                if (systemAvailable)
                {
                    // GetWinGetExePath checks system PATH → WindowsApps → bundled, in order
                    wingetExe = WinGetCliRunner.GetWinGetExePath() ?? "winget";
                    _logService.LogInformation($"Using system winget for ADK install: {wingetExe}");
                }
                else
                {
                    wingetExe = WinGetCliRunner.GetBundledWinGetExePath()
                        ?? WinGetCliRunner.GetWinGetExePath()
                        ?? "winget";
                    _logService.LogInformation($"No system winget — using bundled CLI for ADK install: {wingetExe}");
                }

                var (exitCode, _) = await _dismProcessRunner.RunProcessWithProgressAsync(wingetExe, arguments, progress, cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new Exception($"winget install failed with exit code: {exitCode}");
                }

                if (await IsOscdimgAvailableAsync().ConfigureAwait(false))
                {
                    _logService.LogInformation("ADK installed via winget and oscdimg.exe found");
                    return true;
                }

                _logService.LogError("ADK installed via winget but oscdimg.exe not found");
                return false;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error installing ADK via winget: {ex.Message}", ex);
                return false;
            }
        }

        private async Task<bool> InstallOscdimgPackageViaWingetAsync(
            IProgress<TaskProgressDetail>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CheckingWinget"),
                    TerminalOutput = "Verifying winget availability"
                });

                var wingetInstalled = await _winGetPackageInstaller.IsWinGetInstalledAsync(cancellationToken).ConfigureAwait(false);

                if (!wingetInstalled)
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_InstallingWinget"),
                        TerminalOutput = "winget is required for this installation method"
                    });

                    var wingetInstallSuccess = await _winGetBootstrapper.InstallWinGetAsync(cancellationToken).ConfigureAwait(false);
                    if (!wingetInstallSuccess)
                    {
                        _logService.LogError("Failed to install winget");
                        return false;
                    }
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = "Installing Microsoft.OSCDIMG package...",
                    TerminalOutput = "Installing lightweight oscdimg package via winget"
                });

                var arguments = "install Microsoft.OSCDIMG --exact --silent --scope machine --accept-package-agreements --accept-source-agreements";

                // Prefer system winget (kept up-to-date via Store) over bundled CLI
                var systemAvailable = _winGetBootstrapper.IsSystemWinGetAvailable
                    || WinGetCliRunner.IsSystemWinGetAvailable();

                string wingetExe;
                if (systemAvailable)
                {
                    wingetExe = WinGetCliRunner.GetWinGetExePath() ?? "winget";
                    _logService.LogInformation($"Using system winget for OSCDIMG install: {wingetExe}");
                }
                else
                {
                    wingetExe = WinGetCliRunner.GetBundledWinGetExePath()
                        ?? WinGetCliRunner.GetWinGetExePath()
                        ?? "winget";
                    _logService.LogInformation($"No system winget — using bundled CLI for OSCDIMG install: {wingetExe}");
                }

                var (exitCode, _) = await _dismProcessRunner.RunProcessWithProgressAsync(wingetExe, arguments, progress, cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new Exception($"winget install Microsoft.OSCDIMG failed with exit code: {exitCode}");
                }

                if (await IsOscdimgAvailableAsync().ConfigureAwait(false))
                {
                    _logService.LogInformation("Microsoft.OSCDIMG package installed via winget and oscdimg.exe found");
                    return true;
                }

                _logService.LogError("Microsoft.OSCDIMG package installed via winget but oscdimg.exe not found");
                return false;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error installing Microsoft.OSCDIMG package via winget: {ex.Message}", ex);
                return false;
            }
        }
    }
}
