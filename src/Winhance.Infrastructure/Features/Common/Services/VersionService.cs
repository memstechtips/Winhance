using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class VersionService : IVersionService
    {
        private readonly ILogService _logService;
        private readonly IProcessExecutor _processExecutor;
        private readonly IFileSystemService _fileSystemService;
        private readonly HttpClient _httpClient;
        private readonly string _latestReleaseApiUrl = "https://api.github.com/repos/memstechtips/Winhance/releases/latest";
        private readonly string _latestReleaseDownloadUrl = "https://github.com/memstechtips/Winhance/releases/latest/download/Winhance.Installer.exe";
        private readonly string _userAgent = "Winhance-Update-Checker";

        public VersionService(ILogService logService, IProcessExecutor processExecutor, IFileSystemService fileSystemService, HttpClient httpClient)
        {
            _logService = logService;
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
            _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public VersionInfo GetCurrentVersion()
        {
            try
            {
                // Get the assembly version
                Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                string? location = assembly.Location;

                if (string.IsNullOrEmpty(location))
                {
                    _logService.Log(LogLevel.Error, "Could not determine assembly location for version check");
                    return CreateDefaultVersion();
                }

                // Get the InformationalVersion which can include the -beta tag
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(location);
                string version = versionInfo.ProductVersion ?? versionInfo.FileVersion ?? "v0.0.0";

                // Trim any build metadata (anything after the + symbol)
                int plusIndex = version.IndexOf('+');
                if (plusIndex > 0)
                {
                    version = version.Substring(0, plusIndex);
                }

                // If the version doesn't start with 'v', add it
                if (!version.StartsWith("v"))
                {
                    version = $"v{version}";
                }

                return VersionInfo.FromTag(version);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting current version: {ex.Message}", ex);
                return CreateDefaultVersion();
            }
        }

        public async Task<VersionInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            const int maxRetries = 3;
            int delayMs = 2000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logService.Log(LogLevel.Info, attempt == 1
                        ? "Checking for updates..."
                        : $"Checking for updates (attempt {attempt}/{maxRetries})...");

                    // Get the latest release information from GitHub API
                    using var request = new HttpRequestMessage(HttpMethod.Get, _latestReleaseApiUrl);
                    request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
                    HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    using JsonDocument doc = JsonDocument.Parse(responseBody);

                    // Extract the tag name (version) from the response
                    string tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "v0.0.0";
                    string htmlUrl = doc.RootElement.GetProperty("html_url").GetString() ?? string.Empty;
                    DateTime publishedAt = doc.RootElement.TryGetProperty("published_at", out JsonElement publishedElement) &&
                                          DateTime.TryParse(publishedElement.GetString(), out DateTime published)
                                          ? published
                                          : DateTime.MinValue;

                    VersionInfo latestVersion = VersionInfo.FromTag(tagName);

                    // Compare with current version
                    VersionInfo currentVersion = GetCurrentVersion();
                    latestVersion = latestVersion with { IsUpdateAvailable = latestVersion.IsNewerThan(currentVersion) };

                    _logService.Log(LogLevel.Info, $"Current version: {currentVersion.Version}, Latest version: {latestVersion.Version}, Update available: {latestVersion.IsUpdateAvailable}");

                    return latestVersion;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Real cancellation — don't retry, propagate immediately
                    throw;
                }
                catch (Exception ex) when (attempt < maxRetries && IsTransientError(ex))
                {
                    _logService.Log(LogLevel.Warning, $"Update check attempt {attempt}/{maxRetries} failed: {ex.Message}. Retrying in {delayMs / 1000}s...");
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                    delayMs *= 2;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error checking for updates: {ex.Message}", ex);
                    return new VersionInfo { IsUpdateAvailable = false };
                }
            }

            return new VersionInfo { IsUpdateAvailable = false };
        }

        private static bool IsTransientError(Exception ex)
        {
            // DNS resolution failures, timeouts, and connection refused are transient
            if (ex is HttpRequestException)
                return true;
            // Only retry on HTTP timeout, not on user-initiated cancellation
            if (ex is TaskCanceledException tce && tce.InnerException is TimeoutException)
                return true;
            return false;
        }

        public async Task DownloadAndInstallUpdateAsync(CancellationToken cancellationToken = default)
        {
            _logService.Log(LogLevel.Info, "Downloading update...");

            // Create a temporary file to download the installer
            string tempPath = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), "Winhance.Installer.exe");

            // Download the installer using streaming to avoid loading the entire file into memory
            using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, _latestReleaseDownloadUrl);
            downloadRequest.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
            using var response = await _httpClient.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            await contentStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

            _logService.Log(LogLevel.Info, $"Update downloaded to {tempPath}, launching installer...");

            // Launch the installer
            await _processExecutor.ShellExecuteAsync(tempPath).ConfigureAwait(false);

            _logService.Log(LogLevel.Info, "Installer launched successfully");
        }

        private VersionInfo CreateDefaultVersion()
        {
            // Create a default version based on the current date
            DateTime now = DateTime.Now;
            string versionTag = $"v{now.Year - 2000:D2}.{now.Month:D2}.{now.Day:D2}";

            return new VersionInfo
            {
                Version = versionTag,
                ReleaseDate = now
            };
        }
    }
}
