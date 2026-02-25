// Native C# WinGet installer using PowerShell Add-AppxProvisionedPackage for machine-wide provisioning,
// with PackageManager WinRT API fallback.
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Management.Deployment;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

public class WinGetInstaller
{
    private readonly ILogService? _logService;
    private readonly ILocalizationService? _localization;
    private readonly ITaskProgressService? _taskProgressService;
    private readonly IPowerShellRunner _powerShellRunner;
    private readonly IFileSystemService _fileSystemService;
    private readonly HttpClient _httpClient;

    private const string GitHubBaseUrl = "https://github.com/microsoft/winget-cli/releases/latest/download";
    private const string DependenciesFileName = "DesktopAppInstaller_Dependencies.zip";
    private const string InstallerFileName = "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle";
    private const string LicenseFileName = "e53e159d00e04f729cc2180cffd1c02e_License1.xml";

    public WinGetInstaller(IPowerShellRunner powerShellRunner, HttpClient httpClient, ILogService? logService = null, ILocalizationService? localization = null, ITaskProgressService? taskProgressService = null, IFileSystemService? fileSystemService = null)
    {
        _powerShellRunner = powerShellRunner;
        _logService = logService;
        _localization = localization;
        _taskProgressService = taskProgressService;
        _fileSystemService = fileSystemService!;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<(bool Success, string Message)> InstallAsync(
        CancellationToken cancellationToken = default)
    {
        // Option 1: Use bundled winget to install Microsoft.AppInstaller (fastest, no download needed)
        var bundledResult = await TryInstallViaBundledWinGetAsync(cancellationToken).ConfigureAwait(false);
        if (bundledResult.Success)
        {
            return bundledResult;
        }

        // Option 2a: Try to provision the existing staged App Installer package
        ReportProgress(0, GetString("Progress_WinGet_CheckingExisting"));
        var existingResult = await TryProvisionExistingPackageAsync(cancellationToken).ConfigureAwait(false);
        if (existingResult.Success)
        {
            return existingResult;
        }

        // Option 2b: Download from GitHub and provision
        _logService?.LogInformation("No existing App Installer package found, downloading from GitHub...");

        var tempDir = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), "WinGetInstall");

        try
        {
            if (_fileSystemService.DirectoryExists(tempDir))
                _fileSystemService.DeleteDirectory(tempDir, true);
            _fileSystemService.CreateDirectory(tempDir);

            var dependenciesPath = _fileSystemService.CombinePath(tempDir, DependenciesFileName);
            var installerPath = _fileSystemService.CombinePath(tempDir, InstallerFileName);
            var licensePath = _fileSystemService.CombinePath(tempDir, LicenseFileName);

            // Download all files in parallel (0-45%)
            // Only the installer (largest file) drives the progress bar; deps & license download silently alongside it.
            ReportProgress(0, GetString("Progress_WinGet_DownloadingComponents"));
            await Task.WhenAll(
                DownloadFileAsync($"{GitHubBaseUrl}/{DependenciesFileName}", dependenciesPath, "Dependencies", false, 0, 0, cancellationToken),
                DownloadFileAsync($"{GitHubBaseUrl}/{InstallerFileName}", installerPath, "WinGet Installer", true, 0, 45, cancellationToken),
                DownloadFileAsync($"{GitHubBaseUrl}/{LicenseFileName}", licensePath, "License", false, 0, 0, cancellationToken)
            ).ConfigureAwait(false);

            // Extract dependencies (45-55%)
            ReportProgress(45, GetString("Progress_WinGet_ExtractingDependencies"));
            var extractPath = _fileSystemService.CombinePath(tempDir, "Dependencies");
            await ExtractDependenciesAsync(dependenciesPath, extractPath).ConfigureAwait(false);

            // Provision for all users (55-100%)
            ReportProgress(55, GetString("Progress_WinGet_InstallingMachineWide"));
            await InstallProvisionedAsync(installerPath, extractPath, licensePath, cancellationToken).ConfigureAwait(false);

            ReportProgress(100, GetString("Progress_WinGet_InstalledSuccessfully"));
            _logService?.LogInformation("WinGet installation completed successfully");
            return (true, GetString("Progress_WinGet_InstalledSuccessfully"));
        }
        catch (OperationCanceledException)
        {
            _logService?.LogWarning("WinGet installation was cancelled");
            return (false, GetString("Progress_WinGet_InstallCancelled"));
        }
        catch (Exception ex)
        {
            _logService?.LogError($"WinGet installation failed: {ex.Message}", ex);
            return (false, $"Installation failed: {ex.Message}");
        }
        finally
        {
            try
            {
                if (_fileSystemService.DirectoryExists(tempDir))
                    _fileSystemService.DeleteDirectory(tempDir, true);
            }
            catch (Exception ex) { _logService?.LogDebug($"Best-effort temp directory cleanup failed: {ex.Message}"); }
        }
    }

    private async Task<(bool Success, string Message)> TryInstallViaBundledWinGetAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var bundledPath = WinGetCliRunner.GetBundledWinGetExePath();
            if (bundledPath == null)
            {
                _logService?.LogInformation("Bundled winget not found — skipping Option 1");
                return (false, "Bundled winget not available");
            }

            _logService?.LogInformation($"Attempting to install AppInstaller via bundled winget: {bundledPath}");
            ReportProgress(5, GetString("Progress_WinGet_Installing"));

            var arguments = "install Microsoft.AppInstaller --exact --silent --accept-source-agreements --accept-package-agreements --force --disable-interactivity --source winget";

            var lastProgressReport = DateTime.MinValue;

            var result = await WinGetCliRunner.RunAsync(
                arguments,
                onOutputLine: line =>
                {
                    try
                    {
                        // Translate raw resource keys to human-readable text
                        var displayLine = WinGetProgressParser.TranslateLine(line);

                        // Log the translated line (skip noise and suppressed lines)
                        if (!IsWinGetOutputNoise(line) && displayLine != null)
                            _logService?.LogInformation($"[winget-bundled] {displayLine}");

                        // Parse progress and report to the TaskProgressControl terminal output
                        var parsed = WinGetProgressParser.ParseLine(line);
                        if (parsed != null)
                        {
                            var progressPercent = parsed.Phase switch
                            {
                                WinGetProgressParser.WinGetPhase.Found => 15,
                                WinGetProgressParser.WinGetPhase.Downloading => 15 + (int)((parsed.Percent ?? 0) * 0.55),
                                WinGetProgressParser.WinGetPhase.Installing => 70 + (int)((parsed.Percent ?? 0) * 0.25),
                                WinGetProgressParser.WinGetPhase.Complete => 95,
                                _ => 0
                            };

                            if (progressPercent > 0)
                            {
                                // Throttle progress updates to avoid flooding the UI (allow Complete through unconditionally)
                                var now = DateTime.UtcNow;
                                if (parsed.Phase == WinGetProgressParser.WinGetPhase.Complete
                                    || (now - lastProgressReport).TotalMilliseconds >= 250)
                                {
                                    lastProgressReport = now;
                                    _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                                    {
                                        Progress = progressPercent,
                                        StatusText = GetString("Progress_WinGet_Installing"),
                                        TerminalOutput = parsed.Percent.HasValue && parsed.Phase != WinGetProgressParser.WinGetPhase.Complete
                                            ? null : (displayLine ?? line),
                                    });
                                }
                            }
                        }
                        else if (displayLine != null && !IsWinGetOutputNoise(line))
                        {
                            // Non-progress meaningful lines bypass throttle (infrequent, contain important info)
                            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                            {
                                TerminalOutput = displayLine
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        // Never let progress reporting errors kill the install
                        _logService?.LogWarning($"Progress reporting error (ignored): {ex.Message}");
                    }
                },
                onErrorLine: line => _logService?.LogWarning($"[winget-bundled-err] {line}"),
                cancellationToken: cancellationToken,
                timeoutMs: 300_000,
                exePathOverride: bundledPath,
                onProgressLine: line =>
                {
                    try
                    {
                        var displayLine = WinGetProgressParser.TranslateLine(line);
                        _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                        {
                            TerminalOutput = displayLine ?? line,
                            IsProgressIndicator = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _logService?.LogWarning($"Progress reporting error (ignored): {ex.Message}");
                    }
                }).ConfigureAwait(false);

            if (WinGetExitCodes.IsSuccess(result.ExitCode))
            {
                _logService?.LogInformation($"AppInstaller installed via bundled winget (exit code: 0x{result.ExitCode:X8})");
                ReportProgress(100, GetString("Progress_WinGet_InstalledSuccessfully"));
                return (true, GetString("Progress_WinGet_InstalledSuccessfully"));
            }

            _logService?.LogWarning($"Bundled winget install failed with exit code: 0x{result.ExitCode:X8}");
            return (false, $"Bundled winget install failed (exit code: 0x{result.ExitCode:X8})");
        }
        catch (Exception ex)
        {
            _logService?.LogWarning($"Option 1 (bundled winget) failed: {ex.Message}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Returns true if the winget output line is visual noise (progress bars, spinners, blank lines)
    /// that should not be written to the log file.
    /// </summary>
    private static bool IsWinGetOutputNoise(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return true;

        var trimmed = line.Trim();

        // Progress bar characters
        if (trimmed.Contains('█') || trimmed.Contains('▒'))
            return true;

        // Spinner characters (single char lines like " - ", " \ ", " | ", " / ")
        if (trimmed.Length <= 2 && (trimmed == "-" || trimmed == "\\" || trimmed == "|" || trimmed == "/"))
            return true;

        return false;
    }

    private async Task<(bool Success, string Message)> TryProvisionExistingPackageAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            _logService?.LogInformation("Checking for existing staged App Installer package...");

            // Look for existing App Installer package in common locations
            var existingPackagePath = FindExistingAppInstallerPackage();

            if (string.IsNullOrEmpty(existingPackagePath))
            {
                _logService?.LogInformation("No existing App Installer package found in common locations");
                return (false, "No existing package found");
            }

            _logService?.LogInformation($"Found existing App Installer package: {existingPackagePath}");
            ReportProgress(50, GetString("Progress_WinGet_ProvisioningExisting"));

            _logService?.LogInformation($"Provisioning existing package: {existingPackagePath}");

            await ProvisionWithPowerShellAsync(existingPackagePath, null, null, cancellationToken).ConfigureAwait(false);

            _logService?.LogInformation("Existing App Installer package provisioned successfully");

            ReportProgress(100, GetString("Progress_WinGet_InstalledSuccessfully"));
            return (true, GetString("Progress_WinGet_InstalledSuccessfully"));
        }
        catch (Exception ex)
        {
            _logService?.LogWarning($"Failed to provision existing package: {ex.Message}");
            return (false, ex.Message);
        }
    }

    private string? FindExistingAppInstallerPackage()
    {
        // Common locations where App Installer package might exist
        var searchPaths = new[]
        {
            // Windows provisioning packages directory
            @"C:\Windows\Provisioning\Packages",
            // System apps staging area
            @"C:\Windows\SystemApps",
            // InboxApps staging (Windows 10/11)
            @"C:\Windows\InboxApps",
        };

        var packagePatterns = new[]
        {
            "Microsoft.DesktopAppInstaller*.msixbundle",
            "Microsoft.DesktopAppInstaller*.appxbundle",
            "Microsoft.DesktopAppInstaller*.msix",
            "Microsoft.DesktopAppInstaller*.appx",
        };

        foreach (var basePath in searchPaths)
        {
            if (!_fileSystemService.DirectoryExists(basePath))
                continue;

            foreach (var pattern in packagePatterns)
            {
                try
                {
                    var files = _fileSystemService.GetFiles(basePath, pattern, SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        // Return the most recently modified file
                        return files.OrderByDescending(f => _fileSystemService.GetLastWriteTime(f)).First();
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip directories we can't access
                }
                catch (Exception ex)
                {
                    _logService?.LogWarning($"Error searching {basePath}: {ex.Message}");
                }
            }
        }

        return null;
    }

    private async Task DownloadFileAsync(
        string url,
        string destinationPath,
        string displayName,
        bool reportProgress,
        int progressStart,
        int progressEnd,
        CancellationToken cancellationToken)
    {
        _logService?.LogInformation($"Downloading {displayName} from {url}");

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var downloadedBytes = 0L;
        var lastReportTime = DateTime.UtcNow;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            downloadedBytes += bytesRead;

            if (reportProgress && totalBytes > 0 && (DateTime.UtcNow - lastReportTime).TotalMilliseconds > 200)
            {
                var downloadProgress = (double)downloadedBytes / totalBytes;
                var overallProgress = progressStart + (int)((progressEnd - progressStart) * downloadProgress);
                _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                {
                    Progress = overallProgress,
                    StatusText = GetString("Progress_WinGet_DownloadingComponents"),
                    TerminalOutput = $"{overallProgress}%"
                });
                lastReportTime = DateTime.UtcNow;
            }
        }

        _logService?.LogInformation($"Downloaded {displayName} ({downloadedBytes} bytes)");
    }

    private Task ExtractDependenciesAsync(string zipPath, string extractPath)
    {
        return Task.Run(() =>
        {
            if (_fileSystemService.DirectoryExists(extractPath))
                _fileSystemService.DeleteDirectory(extractPath, true);

            ZipFile.ExtractToDirectory(zipPath, extractPath);
            _logService?.LogInformation($"Extracted dependencies to {extractPath}");
        });
    }

    private async Task InstallProvisionedAsync(
        string installerPath,
        string dependenciesPath,
        string licensePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Get architecture-specific dependencies
        var arch = GetCurrentArchitecture();
        var allAppxFiles = _fileSystemService.GetFiles(dependenciesPath, "*.appx", SearchOption.AllDirectories);
        var dependencyPackages = allAppxFiles
            .Where(f => IsRelevantForArchitecture(f, arch))
            .ToArray();

        _logService?.LogInformation($"Found {dependencyPackages.Length} dependencies for {arch} architecture");

        var licensePaths = !string.IsNullOrEmpty(licensePath)
            ? new[] { licensePath }
            : null;

        // Try PowerShell Add-AppxProvisionedPackage first, fall back to PackageManager WinRT API
        try
        {
            ReportProgress(60, GetString("Progress_WinGet_Provisioning"));
            await ProvisionWithPowerShellAsync(installerPath, dependencyPackages, licensePaths, cancellationToken).ConfigureAwait(false);
            _logService?.LogInformation("WinGet provisioned successfully for all users");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService?.LogWarning($"PowerShell provisioning failed ({ex.Message}), falling back to PackageManager");
            ReportProgress(70, GetString("Progress_WinGet_Provisioning"));
            await InstallWithPackageManagerAsync(installerPath, dependencyPackages, cancellationToken).ConfigureAwait(false);
            _logService?.LogInformation("WinGet installed successfully via PackageManager");
        }

        ReportProgress(95, GetString("Progress_WinGet_ProvisionedSuccessfully"));
    }

    /// <summary>
    /// Provisions an AppX package machine-wide using the PowerShell Add-AppxProvisionedPackage cmdlet.
    /// </summary>
    private async Task ProvisionWithPowerShellAsync(
        string packagePath,
        string[]? dependencyPackages,
        string[]? licensePaths,
        CancellationToken cancellationToken)
    {
        var script = new StringBuilder();
        script.AppendLine("$ErrorActionPreference = 'Stop'");
        script.Append($"Add-AppxProvisionedPackage -Online -PackagePath '{packagePath.Replace("'", "''")}'");

        if (dependencyPackages is { Length: > 0 })
        {
            var deps = string.Join(",", dependencyPackages.Select(p => $"'{p.Replace("'", "''")}'"));
            script.Append($" -DependencyPackagePath {deps}");
        }

        if (licensePaths is { Length: > 0 })
        {
            script.Append($" -LicensePath '{licensePaths[0].Replace("'", "''")}'");
        }
        else
        {
            script.Append(" -SkipLicense");
        }

        script.AppendLine();
        script.AppendLine("Write-Host 'Package provisioned successfully for all users'");

        _logService?.LogInformation($"Provisioning via PowerShell: {packagePath}");

        await _powerShellRunner.RunScriptAsync(script.ToString(), ct: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Installs an AppX package for the current user using the PackageManager WinRT API.
    /// Used as a fallback when DismAddProvisionedAppxPackage is unavailable.
    /// </summary>
    private async Task InstallWithPackageManagerAsync(
        string packagePath,
        string[]? dependencyPackages,
        CancellationToken cancellationToken)
    {
        var packageManager = new PackageManager();
        var packageUri = new Uri(packagePath);

        List<Uri>? dependencyUris = null;
        if (dependencyPackages is { Length: > 0 })
        {
            dependencyUris = dependencyPackages.Select(p => new Uri(p)).ToList();

            // Install dependencies first individually (some may already be installed)
            foreach (var depUri in dependencyUris)
            {
                try
                {
                    _logService?.LogInformation($"Installing dependency: {depUri.LocalPath}");
                    await packageManager.AddPackageAsync(
                        depUri, null,
                        DeploymentOptions.ForceApplicationShutdown).AsTask(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logService?.LogWarning($"Dependency install failed (may already be installed): {ex.Message}");
                }
            }
        }

        _logService?.LogInformation($"Installing package via PackageManager: {packagePath}");
        await packageManager.AddPackageAsync(
            packageUri, dependencyUris,
            DeploymentOptions.ForceApplicationShutdown).AsTask(cancellationToken).ConfigureAwait(false);
    }

    private static string GetCurrentArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => "x64"
        };
    }

    private bool IsRelevantForArchitecture(string filePath, string targetArch)
    {
        var fileName = _fileSystemService.GetFileName(filePath).ToLowerInvariant();

        if (fileName.Contains(targetArch))
            return true;

        // Include neutral packages (no architecture specified)
        if (!fileName.Contains("x64") && !fileName.Contains("x86") && !fileName.Contains("arm"))
            return true;

        return false;
    }

    private void ReportProgress(int percent, string status)
    {
        _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
        {
            Progress = percent,
            StatusText = status,
            TerminalOutput = percent > 0 && percent < 100 ? $"{percent}%" : null
        });
    }

    private string GetString(string key, params object[] args)
    {
        if (_localization == null)
            return args.Length > 0 ? string.Format(key, args) : key;
        return _localization.GetString(key, args);
    }
}
