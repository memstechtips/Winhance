// Native C# WinGet installer using ManagedDism API for machine-wide provisioning
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.Dism;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

public class WinGetInstaller
{
    private readonly ILogService? _logService;
    private readonly ILocalizationService? _localization;
    private readonly HttpClient _httpClient;

    private const string GitHubBaseUrl = "https://github.com/microsoft/winget-cli/releases/latest/download";
    private const string DependenciesFileName = "DesktopAppInstaller_Dependencies.zip";
    private const string InstallerFileName = "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle";
    private const string LicenseFileName = "e53e159d00e04f729cc2180cffd1c02e_License1.xml";

    public WinGetInstaller(ILogService? logService = null, ILocalizationService? localization = null)
    {
        _logService = logService;
        _localization = localization;
        _httpClient = new HttpClient();
    }

    public async Task<(bool Success, string Message)> InstallAsync(
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // First, try to provision the existing staged App Installer package
        ReportProgress(progress, 0, GetString("Progress_WinGet_CheckingExisting"));
        var existingResult = await TryProvisionExistingPackageAsync(progress, cancellationToken);
        if (existingResult.Success)
        {
            return existingResult;
        }

        _logService?.LogInformation("No existing App Installer package found, downloading from GitHub...");

        var tempDir = Path.Combine(Path.GetTempPath(), "WinGetInstall");

        try
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            var dependenciesPath = Path.Combine(tempDir, DependenciesFileName);
            var installerPath = Path.Combine(tempDir, InstallerFileName);
            var licensePath = Path.Combine(tempDir, LicenseFileName);

            // Download files (0-45%)
            ReportProgress(progress, 0, GetString("Progress_WinGet_DownloadingDependencies"));
            await DownloadFileAsync($"{GitHubBaseUrl}/{DependenciesFileName}", dependenciesPath, "Dependencies", progress, 0, 15, cancellationToken);

            ReportProgress(progress, 15, GetString("Progress_WinGet_DownloadingInstaller"));
            await DownloadFileAsync($"{GitHubBaseUrl}/{InstallerFileName}", installerPath, "WinGet Installer", progress, 15, 35, cancellationToken);

            ReportProgress(progress, 35, GetString("Progress_WinGet_DownloadingLicense"));
            await DownloadFileAsync($"{GitHubBaseUrl}/{LicenseFileName}", licensePath, "License", progress, 35, 45, cancellationToken);

            // Extract dependencies (45-55%)
            ReportProgress(progress, 45, GetString("Progress_WinGet_ExtractingDependencies"));
            var extractPath = Path.Combine(tempDir, "Dependencies");
            await ExtractDependenciesAsync(dependenciesPath, extractPath);

            // Install using DISM API (55-100%)
            ReportProgress(progress, 55, GetString("Progress_WinGet_InstallingMachineWide"));
            await InstallWithDismAsync(installerPath, extractPath, licensePath, progress, cancellationToken);

            ReportProgress(progress, 100, GetString("Progress_WinGet_InstalledSuccessfully"));
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
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch { }
        }
    }

    private async Task<(bool Success, string Message)> TryProvisionExistingPackageAsync(
        IProgress<TaskProgressDetail>? progress,
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
            ReportProgress(progress, 50, GetString("Progress_WinGet_ProvisioningExisting"));

            // Provision the existing package using DISM (no dependencies needed for re-provisioning)
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                DismApi.Initialize(DismLogLevel.LogErrors);
                try
                {
                    using var session = DismApi.OpenOnlineSession();

                    _logService?.LogInformation($"Provisioning existing package: {existingPackagePath}");

                    DismApi.AddProvisionedAppxPackage(
                        session,
                        existingPackagePath,
                        new List<string>(), // No additional dependencies needed
                        null, // No license file needed for existing package
                        customDataPath: null);

                    _logService?.LogInformation("Existing App Installer package provisioned successfully");
                }
                finally
                {
                    DismApi.Shutdown();
                }
            }, cancellationToken);

            ReportProgress(progress, 100, GetString("Progress_WinGet_InstalledSuccessfully"));
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
            if (!Directory.Exists(basePath))
                continue;

            foreach (var pattern in packagePatterns)
            {
                try
                {
                    var files = Directory.GetFiles(basePath, pattern, SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        // Return the most recently modified file
                        return files.OrderByDescending(File.GetLastWriteTime).First();
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
        IProgress<TaskProgressDetail>? progress,
        int progressStart,
        int progressEnd,
        CancellationToken cancellationToken)
    {
        _logService?.LogInformation($"Downloading {displayName} from {url}");

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var downloadedBytes = 0L;
        var lastReportTime = DateTime.UtcNow;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            downloadedBytes += bytesRead;

            if (totalBytes > 0 && (DateTime.UtcNow - lastReportTime).TotalMilliseconds > 200)
            {
                var downloadProgress = (double)downloadedBytes / totalBytes;
                var overallProgress = progressStart + (int)((progressEnd - progressStart) * downloadProgress);
                ReportProgress(progress, overallProgress, $"Downloading {displayName}... {downloadProgress:P0}");
                lastReportTime = DateTime.UtcNow;
            }
        }

        _logService?.LogInformation($"Downloaded {displayName} ({downloadedBytes} bytes)");
    }

    private Task ExtractDependenciesAsync(string zipPath, string extractPath)
    {
        return Task.Run(() =>
        {
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            ZipFile.ExtractToDirectory(zipPath, extractPath);
            _logService?.LogInformation($"Extracted dependencies to {extractPath}");
        });
    }

    private Task InstallWithDismAsync(
        string installerPath,
        string dependenciesPath,
        string licensePath,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get architecture-specific dependencies
            var arch = GetCurrentArchitecture();
            var allAppxFiles = Directory.GetFiles(dependenciesPath, "*.appx", SearchOption.AllDirectories);
            var dependencyPackages = allAppxFiles
                .Where(f => IsRelevantForArchitecture(f, arch))
                .ToList();

            _logService?.LogInformation($"Found {dependencyPackages.Count} dependencies for {arch} architecture");

            // Initialize DISM
            DismApi.Initialize(DismLogLevel.LogErrors);

            try
            {
                ReportProgress(progress, 60, GetString("Progress_WinGet_OpeningDismSession"));
                using var session = DismApi.OpenOnlineSession();

                cancellationToken.ThrowIfCancellationRequested();

                ReportProgress(progress, 70, GetString("Progress_WinGet_Provisioning"));
                _logService?.LogInformation($"Installing WinGet from {installerPath} with license {licensePath}");

                DismApi.AddProvisionedAppxPackage(
                    session,
                    installerPath,
                    dependencyPackages,
                    licensePath,
                    customDataPath: null);

                _logService?.LogInformation("WinGet provisioned successfully for all users");
                ReportProgress(progress, 95, GetString("Progress_WinGet_ProvisionedSuccessfully"));
            }
            finally
            {
                DismApi.Shutdown();
            }
        }, cancellationToken);
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

    private static bool IsRelevantForArchitecture(string filePath, string targetArch)
    {
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();

        if (fileName.Contains(targetArch))
            return true;

        // Include neutral packages (no architecture specified)
        if (!fileName.Contains("x64") && !fileName.Contains("x86") && !fileName.Contains("arm"))
            return true;

        return false;
    }

    private static void ReportProgress(IProgress<TaskProgressDetail>? progress, int percent, string status)
    {
        progress?.Report(new TaskProgressDetail
        {
            Progress = percent,
            StatusText = status
        });
    }

    private string GetString(string key, params object[] args)
    {
        if (_localization == null)
            return args.Length > 0 ? string.Format(key, args) : key;
        return _localization.GetString(key, args);
    }
}
