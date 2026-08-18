using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal class StoreDownloadService : IStoreDownloadService
{
    private readonly ITaskProgressService _taskProgressService;
    private readonly ILogService? _logService;
    private readonly ILocalizationService _localization;
    private readonly IFileSystemService _fileSystemService;
    private readonly HttpClient _httpClient;

    private const string StoreApiUrl = "https://store.rg-adguard.net/api/GetFiles";

    private static readonly Regex DownloadLinkRegex = new(
        @"<a\s+href=""(?<url>[^""]+)""\s*[^>]*>(?<filename>[^<]+)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FrameworkDependencyRegex = new(
        @"Provide the framework ""([^""]+)""",
        RegexOptions.Compiled);
    private static readonly Regex AltFrameworkDependencyRegex = new(
        @"framework that could not be found[.\s]*Provide the framework\s+""?([^""]+)""?",
        RegexOptions.Singleline | RegexOptions.Compiled);

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
        _httpClient = httpClient;
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
            _fileSystemService.CreateDirectory(tempPath);
            _logService?.LogInformation($"Created temporary directory: {tempPath}");

            _taskProgressService?.UpdateProgress(5, _localization.GetString("Progress_Store_FetchingLinks", displayName));
            var packagePath = await DownloadPackageAsync(productId, tempPath, displayName, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(packagePath))
            {
                _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_Store_FailedDownload", displayName));
                return false;
            }

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
            _taskProgressService?.UpdateProgress(10, _localization.GetString("Progress_Store_RequestingInfo", displayName));
            var downloadLinks = await GetDownloadLinksAsync(productId, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (downloadLinks.Count == 0)
            {
                _logService?.LogError($"No download links found for {productId}");
                return null;
            }

            _logService?.LogInformation($"Found {downloadLinks.Count} package file(s) for {productId}");

            var currentArch = Common.Utilities.ArchitectureHelper.GetCurrentArchitecture();
            var mainPackages = FilterPackageLinks(downloadLinks, currentArch, isDependency: false);
            var allDependencies = FilterPackageLinks(downloadLinks, currentArch, isDependency: true);

            if (mainPackages.Count == 0)
            {
                _logService?.LogError($"No suitable packages found for architecture {currentArch}");
                return null;
            }

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

            _taskProgressService?.UpdateProgress(80, _localization.GetString("Progress_Store_Installing", displayName));
            _logService?.LogInformation("Attempting installation without dependencies first...");

            var (success, errorMessage) = await TryInstallPackageAsync(downloadedFile, displayName, cancellationToken).ConfigureAwait(false);

            if (success)
            {
                _logService?.LogInformation($"Successfully installed {displayName} without additional dependencies");
                return downloadedFile;
            }

            if (IsNonRecoverableError(errorMessage))
            {
                _logService?.LogError($"Cannot install {displayName}: {GetFriendlyErrorMessage(errorMessage)}");
                _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_Store_NonRecoverableError", GetFriendlyErrorMessage(errorMessage)));
                return null;
            }

            // Some apps need multiple dependencies that depend on each other
            var allDownloadedDependencies = new List<string>();
            const int maxDependencyRounds = 5;
            int currentRound = 0;

            while (currentRound < maxDependencyRounds)
            {
                currentRound++;
                var missingDependencies = ParseMissingDependencies(errorMessage);

                if (missingDependencies.Count == 0)
                {
                    _logService?.LogError($"Installation failed: {errorMessage}");
                    return null;
                }

                _logService?.LogInformation($"Dependency round {currentRound}: Missing {string.Join(", ", missingDependencies)}");
                _taskProgressService?.UpdateProgress(50 + (currentRound * 5), _localization.GetString("Progress_Store_DownloadingDependencies", missingDependencies.Count));

                var newDependencies = new List<string>();
                foreach (var depName in missingDependencies)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    _logService?.LogInformation($"Searching for dependency: {depName}");
                    _logService?.LogInformation($"Total filtered dependencies available: {allDependencies.Count}");

                    var matchingDeps = allDependencies.Where(d =>
                    {
                        var fileName = d.FileName;

                        if (fileName.StartsWith(depName + "_", StringComparison.OrdinalIgnoreCase))
                        {
                            _logService?.LogInformation($"  Exact match found: {fileName}");
                            return true;
                        }

                        var depParts = depName.Split('.');
                        if (depParts.Length >= 2)
                        {
                            var baseName = string.Join(".", depParts.Take(depParts.Length - 2));
                            var majorVersion = depParts[depParts.Length - 2];
                            var minorVersion = depParts[depParts.Length - 1];

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

                    if (matchingDeps.Count == 0)
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

                if (newDependencies.Count == 0)
                {
                    _logService?.LogError($"Could not download required dependencies");
                    return null;
                }

                allDownloadedDependencies.AddRange(newDependencies);

                _taskProgressService?.UpdateProgress(80, _localization.GetString("Progress_Store_InstallingWithDependencies", displayName, allDownloadedDependencies.Count));
                var (retrySuccess, retryError) = await TryInstallPackageWithDependenciesAsync(
                    downloadedFile, allDownloadedDependencies, displayName, cancellationToken).ConfigureAwait(false);

                if (retrySuccess)
                {
                    _logService?.LogInformation($"Successfully installed {displayName} with {allDownloadedDependencies.Count} dependencies");
                    return downloadedFile;
                }

                errorMessage = retryError;
                _logService?.LogInformation($"Installation attempt {currentRound} failed, checking for more dependencies...");
            }

            _logService?.LogError($"Installation failed after {maxDependencyRounds} dependency resolution rounds");
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
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

        using var response = await _httpClient.PostAsync(StoreApiUrl, requestContent, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var htmlContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var matches = DownloadLinkRegex.Matches(htmlContent);

        var links = new List<PackageLink>();
        foreach (Match match in matches)
        {
            var url = match.Groups["url"].Value;
            var filename = match.Groups["filename"].Value;

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

            bool isFrameworkDependency = filename.Contains("microsoft.ui.xaml") ||
                                        filename.Contains("microsoft.vclibs") ||
                                        filename.Contains("microsoft.net.native") ||
                                        filename.Contains("microsoft.services.store.engagement");

            if (isDependency && !isFrameworkDependency)
                return false;

            if (!isDependency && isFrameworkDependency)
                return false;

            var matchesArch = filename.Contains($"_{currentArch.ToLowerInvariant()}_") ||
                             filename.Contains("_neutral_");

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

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
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
        catch (OperationCanceledException)
        {
            throw;
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
            await packageManager.AddPackageAsync(packageUri, null, Windows.Management.Deployment.DeploymentOptions.None).AsTask(cancellationToken).ConfigureAwait(false);

            _logService?.LogInformation($"Successfully installed {displayName}");
            return (true, string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
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

            await packageManager.AddPackageAsync(packageUri, dependencyUris, Windows.Management.Deployment.DeploymentOptions.None).AsTask(cancellationToken).ConfigureAwait(false);

            _logService?.LogInformation($"Successfully installed {displayName} with dependencies");
            return (true, string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
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

        if (lowerError.Contains("not supported on this architecture"))
            return true;

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
        var frameworkMatches = FrameworkDependencyRegex.Matches(errorMessage);
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
        var altMatches = AltFrameworkDependencyRegex.Matches(errorMessage);
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


    private class PackageLink
    {
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
