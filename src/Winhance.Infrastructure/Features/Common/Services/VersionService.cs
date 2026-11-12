using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class VersionService : IVersionService
{
    internal const string LatestInstallerUrl = "https://github.com/memstechtips/Winhance/releases/latest/download/Winhance.Installer.exe";

    private const string InstallerAssetName = "Winhance.Installer.exe";
    private const string Sha256DigestPrefix = "sha256:";

#if DEBUG
    // Debug builds only: when WINHANCE_LOCAL_INSTALLER points at an existing file,
    // the updater skips GitHub and stages that file, to test the silent-install flow
    // without publishing a release.
    private const string LocalInstallerEnvVar = "WINHANCE_LOCAL_INSTALLER";
#endif

    private readonly ILogService _logService;
    private readonly IFileSystemService _fileSystemService;
    private readonly HttpClient _httpClient;
    private readonly string _latestReleaseApiUrl = "https://api.github.com/repos/memstechtips/Winhance/releases/latest";
    private readonly string _userAgent = "Winhance-Update-Checker";
    private string? _downloadedInstallerPath;
    private string? _expectedInstallerSha256;
    // Held open until the app exits so nothing can replace the installer before it starts.
    private FileStream? _lockedInstaller;

    public VersionService(ILogService logService, IFileSystemService fileSystemService, HttpClient httpClient)
    {
        _logService = logService;
        _fileSystemService = fileSystemService;
        _httpClient = httpClient;
    }

    public VersionInfo GetCurrentVersion()
    {
        try
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            string? location = assembly.Location;

            if (string.IsNullOrEmpty(location))
            {
                _logService.Log(LogLevel.Error, "Could not determine assembly location for version check");
                return CreateDefaultVersion();
            }

            // Get the InformationalVersion which can include the -beta tag
            var versionInfo = FileVersionInfo.GetVersionInfo(location);
            return VersionInfo.FromProductVersion(versionInfo.ProductVersion ?? versionInfo.FileVersion ?? "v0.0.0");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error getting current version: {ex.Message}", ex);
            return CreateDefaultVersion();
        }
    }

    public async Task<VersionInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
#if DEBUG
        if (TryGetLocalInstallerOverride() is { } localPath)
        {
            _logService.Log(LogLevel.Warning,
                $"Dev mode: {LocalInstallerEnvVar} set to {localPath}. Skipping GitHub release lookup and reporting a synthetic update.");
            return new VersionInfo
            {
                Version = "v99.99.99-localdev",
                ReleaseDate = DateTime.UtcNow,
                IsUpdateAvailable = true,
            };
        }
#endif

        var latestVersion = await GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(latestVersion.Version))
            return latestVersion;

        VersionInfo currentVersion = GetCurrentVersion();
        latestVersion = latestVersion with { IsUpdateAvailable = latestVersion.IsNewerThan(currentVersion) };

        _logService.Log(LogLevel.Info, $"Current version: {currentVersion.Version}, Latest version: {latestVersion.Version}, Update available: {latestVersion.IsUpdateAvailable}");

        return latestVersion;
    }

    public async Task<VersionInfo> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
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

                string responseBody = await GetLatestReleaseJsonAsync(cancellationToken).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(responseBody);

                string tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "v0.0.0";
                string htmlUrl = doc.RootElement.GetProperty("html_url").GetString() ?? string.Empty;
                DateTime publishedAt = doc.RootElement.TryGetProperty("published_at", out JsonElement publishedElement) &&
                                      DateTime.TryParse(publishedElement.GetString(), out DateTime published)
                                      ? published
                                      : DateTime.MinValue;

                return VersionInfo.FromTag(tagName);
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

    private async Task<string> GetLatestReleaseJsonAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _latestReleaseApiUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
#if DEBUG
        if (TryGetLocalInstallerOverride() is { } localPath)
        {
            _logService.Log(LogLevel.Warning,
                $"Dev mode: {LocalInstallerEnvVar} set to {localPath}. Skipping HTTP download and using the local file as the staged installer.");
            _downloadedInstallerPath = localPath;
            await using var localStream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            _expectedInstallerSha256 = Convert.ToHexString(await SHA256.HashDataAsync(localStream, cancellationToken).ConfigureAwait(false));
            return;
        }
#endif

        _logService.Log(LogLevel.Info, "Downloading update...");

        string releaseJson = await GetLatestReleaseJsonAsync(cancellationToken).ConfigureAwait(false);
        (string downloadUrl, string expectedSha256) = ReadInstallerAsset(releaseJson);

        string tempPath = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), InstallerAssetName);

        using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        downloadRequest.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        using var response = await _httpClient.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string actualSha256;
        using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            int read;
            while ((read = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                hasher.AppendData(buffer, 0, read);
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            actualSha256 = Convert.ToHexString(hasher.GetHashAndReset());
        }

        if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(tempPath);
            _logService.Log(LogLevel.Error, $"Update installer hash mismatch: expected {expectedSha256}, got {actualSha256}. File deleted.");
            throw new InvalidOperationException("The downloaded update did not match the checksum GitHub published for it, so it was discarded.");
        }

        _downloadedInstallerPath = tempPath;
        _expectedInstallerSha256 = expectedSha256;
        _logService.Log(LogLevel.Info, $"Update downloaded to {tempPath} and verified.");
    }

    // Takes the URL and the digest from the same release response so the file and its hash always agree.
    private (string DownloadUrl, string Sha256) ReadInstallerAsset(string releaseJson)
    {
        using var doc = JsonDocument.Parse(releaseJson);
        if (doc.RootElement.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out JsonElement name) && name.GetString() == InstallerAssetName)
                {
                    string? digest = asset.TryGetProperty("digest", out JsonElement digestElement) ? digestElement.GetString() : null;
                    string? url = asset.TryGetProperty("browser_download_url", out JsonElement urlElement) ? urlElement.GetString() : null;
                    if (digest is not null && digest.StartsWith(Sha256DigestPrefix, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(url))
                        return (url, digest[Sha256DigestPrefix.Length..]);
                    break;
                }
            }
        }

        _logService.Log(LogLevel.Error, $"The latest release has no {InstallerAssetName} asset with a SHA-256 digest. Update aborted.");
        throw new InvalidOperationException("The latest release does not publish a checksum for the installer, so the update was not downloaded.");
    }

    // Opens the file with no write or delete sharing, then hashes that same handle, so the bytes
    // checked are the bytes that will run.
    internal static FileStream OpenVerifiedInstaller(string path, string expectedSha256)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            string actual = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The downloaded installer changed after it was verified, so it was not started.");
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public void LaunchInstallerAndRestart()
    {
        if (string.IsNullOrEmpty(_downloadedInstallerPath) || string.IsNullOrEmpty(_expectedInstallerSha256))
            throw new InvalidOperationException("No update has been downloaded.");

        _lockedInstaller?.Dispose();
        _lockedInstaller = OpenVerifiedInstaller(_downloadedInstallerPath, _expectedInstallerSha256);

        string appDir = AppContext.BaseDirectory;
        bool isPortable = _fileSystemService.FileExists(_fileSystemService.CombinePath(appDir, AppDataPaths.PortableMarkerFileName));
        string appExePath = _fileSystemService.CombinePath(appDir, "Winhance.exe");

        string installerArgs = BuildInstallerArgs(appDir, isPortable);

        _logService.Log(LogLevel.Info, $"Launching installer (portable: {isPortable}), app will restart after install...");

        // Use cmd /c to: run installer (wait for it to finish) then relaunch the app.
        // The caller should exit the application immediately after this call.
        var cmdArgs = $"/c start /wait \"\" \"{_downloadedInstallerPath}\" {installerArgs} && start \"\" \"{appExePath}\"";
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = cmdArgs,
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

#if DEBUG
    private string? TryGetLocalInstallerOverride()
    {
        string? path = Environment.GetEnvironmentVariable(LocalInstallerEnvVar);
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (!_fileSystemService.FileExists(path))
        {
            _logService.Log(LogLevel.Warning,
                $"Dev mode: {LocalInstallerEnvVar}={path} but the file does not exist. Falling back to normal update flow.");
            return null;
        }
        return path;
    }
#endif

    // Always pins the install directory via /DIR= (issue #649): without it Inno's UsePreviousAppDir/DefaultDirName
    // chain can resolve {app} to C:\Program Files\Winhance for a custom-path install, or ~\Desktop\Winhance for a
    // portable one, silently relocating the install. Winhance.Installer.iss skips its custom-dir-page sync when
    // /DIR= is passed silently.
    internal static string BuildInstallerArgs(string appDir, bool isPortable)
    {
        string dirArg = $"/DIR=\"{appDir.TrimEnd('\\', '/')}\"";
        return isPortable
            ? $"/SILENT /SUPPRESSMSGBOXES {dirArg} /MERGETASKS=\"portableinstall\""
            : $"/SILENT /SUPPRESSMSGBOXES {dirArg} /MERGETASKS=\"regularinstall\\desktopicon,regularinstall\\startmenuicon\"";
    }

    private VersionInfo CreateDefaultVersion()
    {
        DateTime now = DateTime.Now;
        string versionTag = $"v{now.Year - 2000:D2}.{now.Month:D2}.{now.Day:D2}";

        return new VersionInfo
        {
            Version = versionTag,
            ReleaseDate = now
        };
    }
}
