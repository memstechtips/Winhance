using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Downloads Microsoft Store packages using the store.rg-adguard.net API.
/// This service bypasses geographic market restrictions by accessing packages directly from Microsoft's CDN.
/// </summary>
public class StoreDownloadService : IStoreDownloadService
{
    private readonly ITaskProgressService _taskProgressService;
    private readonly ILogService? _logService;
    private readonly ILocalizationService _localization;
    private readonly IFileSystemService _fileSystemService;
    private readonly HttpClient _httpClient;

    private const string StoreApiUrl = "https://store.rg-adguard.net/api/GetFiles";

    public StoreDownloadService(
        ITaskProgressService taskProgressService,
        ILocalizationService localization,
        IFileSystemService fileSystemService,
        HttpClient httpClient,
        ILogService? logService = null)
    {
        _taskProgressService = taskProgressService;
        _localization = localization;
        _fileSystemService = fileSystemService;
        _logService = logService;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<bool> DownloadAndInstallPackageAsync(
        string productId,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        displayName ??= productId;
        var tempPath = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), $"Winhance_{productId}_{Guid.NewGuid():N}");

        try
        {
            // Create temporary directory
            _fileSystemService.CreateDirectory(tempPath);
            _logService?.LogInformation($"Created temporary directory: {tempPath}");

            // Download the package
            _taskProgressService?.UpdateProgress(5, _localization.GetString("Progress_Store_FetchingLinks", displayName));
            var packagePath = await DownloadPackageAsync(productId, tempPath, displayName, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(packagePath))
            {
                _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_Store_FailedDownload", displayName));
                return false;
            }

            // If we reach here, the package was downloaded but not installed yet (no dependencies found)
            // This shouldn't happen with the new flow, but handle it just in case
            _taskProgressService?.UpdateProgress(80, _localization.GetString("Progress_Store_Installing", displayName));
            var installSuccess = await InstallPackageAsync(packagePath, displayName, cancellationToken).ConfigureAwait(false);

            if (installSuccess)
            {
                _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_Store_InstalledSuccess", displayName));
                return true;
            }

            _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_Store_FailedInstall", displayName));
            return false;
        }
        catch (OperationCanceledException)
        {
            _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_Store_DownloadCancelled", displayName));
            throw;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Error downloading/installing {productId}: {ex.Message}");
            _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_Store_InstallError", displayName, ex.Message));
            return false;
        }
        finally
        {
            // Cleanup temporary directory
            try
            {
                if (_fileSystemService.DirectoryExists(tempPath))
                {
                    _fileSystemService.DeleteDirectory(tempPath, true);
                    _logService?.LogInformation($"Cleaned up temporary directory: {tempPath}");
                }
            }
            catch (Exception ex)
            {
                _logService?.LogWarning($"Failed to cleanup temporary directory {tempPath}: {ex.Message}");
            }
        }
    }

    private async Task<string?> DownloadPackageAsync(
        string productId,
        string downloadPath,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        displayName ??= productId;

        try
        {
            // Step 1: Get download links from store.rg-adguard.net API
            _taskProgressService?.UpdateProgress(10, _localization.GetString("Progress_Store_RequestingInfo", displayName));
            var downloadLinks = await GetDownloadLinksAsync(productId, cancellationToken).ConfigureAwait(false);

            // Check for cancellation after API call
            cancellationToken.ThrowIfCancellationRequested();

            if (!downloadLinks.Any())
            {
                _logService?.LogError($"No download links found for {productId}");
                return null;
            }

            _logService?.LogInformation($"Found {downloadLinks.Count} package file(s) for {productId}");

            // Step 2: Separate main packages and dependencies
            var currentArch = GetCurrentArchitecture();
            var mainPackages = FilterPackageLinks(downloadLinks, currentArch, isDependency: false);
            var allDependencies = FilterPackageLinks(downloadLinks, currentArch, isDependency: true);

            if (!mainPackages.Any())
            {
                _logService?.LogError($"No suitable packages found for architecture {currentArch}");
                return null;
            }

            // Step 3: Download the main package first (prefer bundles)
            _taskProgressService?.UpdateProgress(30, _localization.GetString("Progress_Store_DownloadingMain", displayName));
            var mainPackage = mainPackages
                .OrderByDescending(x => x.FileName.Contains("bundle", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(x => x.FileName.Contains(currentArch, StringComparison.OrdinalIgnoreCase))
                .First();

            var downloadedFile = await DownloadFileAsync(
                mainPackage.Url,
                downloadPath,
                mainPackage.FileName,
                displayName,
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(downloadedFile))
            {
                return null;
            }

            _logService?.LogInformation($"Successfully downloaded main package: {downloadedFile}");

            // Step 4: Try installing without dependencies first
            _taskProgressService?.UpdateProgress(80, _localization.GetString("Progress_Store_Installing", displayName));
            _logService?.LogInformation("Attempting installation without dependencies first...");

            var (success, errorMessage) = await TryInstallPackageAsync(downloadedFile, displayName, cancellationToken).ConfigureAwait(false);

            if (success)
            {
                _logService?.LogInformation($"Successfully installed {displayName} without additional dependencies");
                return downloadedFile;
            }

            // Check for non-recoverable errors (EOL, incompatible, etc.)
            if (IsNonRecoverableError(errorMessage))
            {
                _logService?.LogError($"Cannot install {displayName}: {GetFriendlyErrorMessage(errorMessage)}");
                _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_Store_NonRecoverableError", GetFriendlyErrorMessage(errorMessage)));
                return null;
            }

            // Step 5: Iteratively resolve and install dependencies (max 5 rounds)
            // Some apps need multiple dependencies that depend on each other
            var allDownloadedDependencies = new List<string>();
            const int maxDependencyRounds = 5;
            int currentRound = 0;

            while (currentRound < maxDependencyRounds)
            {
                currentRound++;
                var missingDependencies = ParseMissingDependencies(errorMessage);

                if (!missingDependencies.Any())
                {
                    _logService?.LogError($"Installation failed: {errorMessage}");
                    return null;
                }

                _logService?.LogInformation($"Dependency round {currentRound}: Missing {string.Join(", ", missingDependencies)}");
                _taskProgressService?.UpdateProgress(50 + (currentRound * 5), _localization.GetString("Progress_Store_DownloadingDependencies", missingDependencies.Count));

                var newDependencies = new List<string>();
                foreach (var depName in missingDependencies)
                {
                    // Check for cancellation before downloading each dependency
                    cancellationToken.ThrowIfCancellationRequested();

                    _logService?.LogInformation($"Searching for dependency: {depName}");
                    _logService?.LogInformation($"Total filtered dependencies available: {allDependencies.Count}");

                    // Find matching dependency
                    var matchingDeps = allDependencies.Where(d =>
                    {
                        var fileName = d.FileName;

                        // Try exact prefix match first (case-insensitive)
                        if (fileName.StartsWith(depName + "_", StringComparison.OrdinalIgnoreCase))
                        {
                            _logService?.LogInformation($"  Exact match found: {fileName}");
                            return true;
                        }

                        // Try matching the base framework name and version
                        var depParts = depName.Split('.');
                        if (depParts.Length >= 2)
                        {
                            // Get the base name and version
                            var baseName = string.Join(".", depParts.Take(depParts.Length - 2));
                            var majorVersion = depParts[depParts.Length - 2];
                            var minorVersion = depParts[depParts.Length - 1];

                            // Check if filename contains the base name and version
                            bool hasBaseName = fileName.Contains(baseName, StringComparison.OrdinalIgnoreCase);
                            bool hasVersion = fileName.Contains($".{majorVersion}.{minorVersion}", StringComparison.OrdinalIgnoreCase);

                            if (hasBaseName && hasVersion)
                            {
                                _logService?.LogInformation($"  Pattern match found: {fileName}");
                                return true;
                            }
                        }

                        return false;
                    }).ToList();

                    if (!matchingDeps.Any())
                    {
                        _logService?.LogWarning($"Could not find matching dependency for: {depName}");
                        _logService?.LogInformation($"Available dependencies:");
                        foreach (var dep in allDependencies.Take(10))
                        {
                            _logService?.LogInformation($"  - {dep.FileName}");
                        }
                        if (allDependencies.Count > 10)
                        {
                            _logService?.LogInformation($"  ... and {allDependencies.Count - 10} more");
                        }
                        continue;
                    }

                    // If multiple matches, prefer the one that's an exact prefix match, or the first one
                    var matchingDep = matchingDeps.FirstOrDefault(d =>
                        d.FileName.StartsWith(depName + "_", StringComparison.OrdinalIgnoreCase))
                        ?? matchingDeps.First();

                    _logService?.LogInformation($"Selected dependency: {matchingDep.FileName}");
                    var depPath = await DownloadFileAsync(
                        matchingDep.Url,
                        downloadPath,
                        matchingDep.FileName,
                        displayName,
                        cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(depPath))
                    {
                        newDependencies.Add(depPath);
                    }
                }

                if (!newDependencies.Any())
                {
                    _logService?.LogError($"Could not download required dependencies");
                    return null;
                }

                // Add newly downloaded dependencies to the full list
                allDownloadedDependencies.AddRange(newDependencies);

                // Try installing with all dependencies collected so far
                _taskProgressService?.UpdateProgress(80, _localization.GetString("Progress_Store_InstallingWithDependencies", displayName, allDownloadedDependencies.Count));
                var (retrySuccess, retryError) = await TryInstallPackageWithDependenciesAsync(
                    downloadedFile, allDownloadedDependencies, displayName, cancellationToken).ConfigureAwait(false);

                if (retrySuccess)
                {
                    _logService?.LogInformation($"Successfully installed {displayName} with {allDownloadedDependencies.Count} dependencies");
                    return downloadedFile;
                }

                // Update error message for next round
                errorMessage = retryError;
                _logService?.LogInformation($"Installation attempt {currentRound} failed, checking for more dependencies...");
            }

            _logService?.LogError($"Installation failed after {maxDependencyRounds} dependency resolution rounds");
            return null;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Error downloading package {productId}: {ex.Message}");
            return null;
        }
    }

    private async Task<List<PackageLink>> GetDownloadLinksAsync(string productId, CancellationToken cancellationToken)
    {
        var productUrl = $"https://apps.microsoft.com/detail/{productId}";
        var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "type", "url" },
            { "url", productUrl },
            { "ring", "RP" },  // Retail Production
            { "lang", "en-US" }
        });

        _logService?.LogInformation($"Requesting download links from store.rg-adguard.net API for {productId}");

        var response = await _httpClient.PostAsync(StoreApiUrl, requestContent, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var htmlContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // Parse HTML for download links
        // Pattern: <a href="URL">FILENAME</a>
        var pattern = @"<a\s+href=""(?<url>[^""]+)""\s*[^>]*>(?<filename>[^<]+)</a>";
        var matches = Regex.Matches(htmlContent, pattern, RegexOptions.IgnoreCase);

        var links = new List<PackageLink>();
        foreach (Match match in matches)
        {
            var url = match.Groups["url"].Value;
            var filename = match.Groups["filename"].Value;

            // Only include package files
            if (IsPackageFile(filename))
            {
                links.Add(new PackageLink
                {
                    Url = url,
                    FileName = filename
                });
            }
        }

        return links;
    }

    private List<PackageLink> FilterPackageLinks(List<PackageLink> links, string currentArch, bool isDependency)
    {
        var filtered = links.Where(link =>
        {
            var filename = link.FileName.ToLowerInvariant();

            // Identify common framework dependencies
            bool isFrameworkDependency = filename.Contains("microsoft.ui.xaml") ||
                                        filename.Contains("microsoft.vclibs") ||
                                        filename.Contains("microsoft.net.native") ||
                                        filename.Contains("microsoft.services.store.engagement");

            // If looking for dependencies, only return framework packages
            if (isDependency && !isFrameworkDependency)
                return false;

            // If looking for main packages, exclude framework packages
            if (!isDependency && isFrameworkDependency)
                return false;

            // Include if it matches current architecture or is neutral
            var matchesArch = filename.Contains($"_{currentArch.ToLowerInvariant()}_") ||
                             filename.Contains("_neutral_");

            // Include common package extensions
            var isPackageType = filename.EndsWith(".appx") ||
                               filename.EndsWith(".appxbundle") ||
                               filename.EndsWith(".msix") ||
                               filename.EndsWith(".msixbundle");

            return matchesArch && isPackageType;
        }).ToList();

        return filtered;
    }


    private async Task<string?> DownloadFileAsync(
        string url,
        string downloadPath,
        string fileName,
        string displayName,
        CancellationToken cancellationToken)
    {
        var filePath = _fileSystemService.CombinePath(downloadPath, fileName);

        try
        {
            _logService?.LogInformation($"Downloading {fileName} from Microsoft CDN...");

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var totalMB = totalBytes / (1024.0 * 1024.0);

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;
            var lastProgress = 0;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    var progressPercent = (int)((totalRead * 40.0 / totalBytes) + 30);
                    if (progressPercent > lastProgress)
                    {
                        lastProgress = progressPercent;
                        var downloadedMB = totalRead / (1024.0 * 1024.0);
                        var statusText = _localization.GetString("Progress_DownloadProgress", displayName, downloadedMB.ToString("F2"), totalMB.ToString("F2"));
                        var terminalOutput = $"{downloadedMB:F2} MB of {totalMB:F2} MB";

                        _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                        {
                            StatusText = statusText,
                            TerminalOutput = terminalOutput,
                            IsActive = true,
                            IsIndeterminate = false
                        });
                    }
                }
            }

            _logService?.LogInformation($"Downloaded {fileName} successfully ({totalMB:F2} MB)");
            return filePath;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Failed to download {fileName}: {ex.Message}");
            return null;
        }
    }

    private async Task<(bool Success, string ErrorMessage)> TryInstallPackageAsync(
        string packagePath,
        string displayName,
        CancellationToken cancellationToken)
    {
        try
        {
            _logService?.LogInformation($"Installing package: {packagePath}");

            var packageManager = new Windows.Management.Deployment.PackageManager();
            var packageUri = new Uri(packagePath);
            await packageManager.AddPackageAsync(packageUri, null, Windows.Management.Deployment.DeploymentOptions.None).AsTask().ConfigureAwait(false);

            _logService?.LogInformation($"Successfully installed {displayName}");
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logService?.LogWarning($"Installation attempt failed: {ex.Message}");
            return (false, ex.Message);
        }
    }

    private async Task<bool> InstallPackageAsync(string packagePath, string displayName, CancellationToken cancellationToken)
    {
        var (success, _) = await TryInstallPackageAsync(packagePath, displayName, cancellationToken).ConfigureAwait(false);
        return success;
    }

    private async Task<(bool Success, string ErrorMessage)> TryInstallPackageWithDependenciesAsync(
        string packagePath,
        List<string> dependencyPaths,
        string displayName,
        CancellationToken cancellationToken)
    {
        try
        {
            _logService?.LogInformation($"Installing package with {dependencyPaths.Count} dependencies: {packagePath}");

            var packageManager = new Windows.Management.Deployment.PackageManager();
            var packageUri = new Uri(packagePath);
            var dependencyUris = dependencyPaths.Select(p => new Uri(p)).ToList();

            await packageManager.AddPackageAsync(packageUri, dependencyUris, Windows.Management.Deployment.DeploymentOptions.None).AsTask().ConfigureAwait(false);

            _logService?.LogInformation($"Successfully installed {displayName} with dependencies");
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logService?.LogWarning($"Installation attempt with dependencies failed: {ex.Message}");
            return (false, ex.Message);
        }
    }

    private bool IsNonRecoverableError(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return false;

        var lowerError = errorMessage.ToLowerInvariant();

        // Check for End-of-Life packages
        if (lowerError.Contains("end of life") || lowerError.Contains("0x80073cfd"))
            return true;

        // Check for incompatible architecture
        if (lowerError.Contains("not supported on this architecture"))
            return true;

        // Check for package conflicts (already installed newer version)
        if (lowerError.Contains("a higher version") && lowerError.Contains("already installed"))
            return true;

        return false;
    }

    private string GetFriendlyErrorMessage(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return "Installation failed due to an unknown error";

        var lowerError = errorMessage.ToLowerInvariant();

        if (lowerError.Contains("end of life") || lowerError.Contains("0x80073cfd"))
            return "This app has been deprecated by Microsoft and can no longer be installed";

        if (lowerError.Contains("not supported on this architecture"))
            return "This app is not compatible with your system architecture";

        if (lowerError.Contains("a higher version") && lowerError.Contains("already installed"))
            return "A newer version of this app is already installed";

        return "Installation failed";
    }

    private List<string> ParseMissingDependencies(string errorMessage)
    {
        var dependencies = new List<string>();

        if (string.IsNullOrEmpty(errorMessage))
            return dependencies;

        // Pattern 1: "Provide the framework "Microsoft.UI.Xaml.2.3" published by"
        var frameworkPattern = @"Provide the framework ""([^""]+)""";
        var frameworkMatches = Regex.Matches(errorMessage, frameworkPattern);
        foreach (Match match in frameworkMatches)
        {
            if (match.Success && match.Groups.Count > 1)
            {
                var depName = match.Groups[1].Value;
                _logService?.LogInformation($"Detected missing framework dependency: {depName}");
                dependencies.Add(depName);
            }
        }

        // Pattern 2: "could not be found. Provide the framework" (alternative format)
        var altFrameworkPattern = @"framework that could not be found[.\s]*Provide the framework\s+""?([^""]+)""?";
        var altMatches = Regex.Matches(errorMessage, altFrameworkPattern, RegexOptions.Singleline);
        foreach (Match match in altMatches)
        {
            if (match.Success && match.Groups.Count > 1)
            {
                var depName = match.Groups[1].Value.Trim();
                if (!dependencies.Contains(depName))
                {
                    _logService?.LogInformation($"Detected missing framework dependency: {depName}");
                    dependencies.Add(depName);
                }
            }
        }

        return dependencies.Distinct().ToList();
    }

    private static bool IsPackageFile(string filename)
    {
        if (string.IsNullOrEmpty(filename))
            return false;

        var lower = filename.ToLowerInvariant();
        return lower.EndsWith(".appx") ||
               lower.EndsWith(".appxbundle") ||
               lower.EndsWith(".msix") ||
               lower.EndsWith(".msixbundle");
    }

    private static string GetCurrentArchitecture()
    {
        var arch = RuntimeInformation.OSArchitecture;
        return arch switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => "x64" // Default to x64
        };
    }

    private class PackageLink
    {
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
