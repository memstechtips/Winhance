using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class DirectDownloadService : IDirectDownloadService
{
    private readonly ILogService _logService;
    private readonly HttpClient _httpClient;
    private readonly ILocalizationService _localization;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IProcessExecutor _processExecutor;
    private readonly IFileSystemService _fileSystemService;

    public DirectDownloadService(
        ILogService logService,
        ILocalizationService localization,
        IInteractiveUserService interactiveUserService,
        IProcessExecutor processExecutor,
        IFileSystemService fileSystemService,
        HttpClient httpClient)
    {
        _logService = logService;
        _localization = localization;
        _interactiveUserService = interactiveUserService;
        _processExecutor = processExecutor;
        _fileSystemService = fileSystemService;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    private static readonly string UserAgent = "Winhance-Download-Manager";
    private static readonly ConcurrentDictionary<string, Regex> PatternCache = new();

    public async Task<bool> DownloadAndInstallAsync(
        ItemDefinition item,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var tempPath = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), $"Winhance_{item.Id}_{Guid.NewGuid():N}");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            _fileSystemService.CreateDirectory(tempPath);
            _logService?.LogInformation($"Created temporary directory: {tempPath}");

            progress?.Report(new TaskProgressDetail
            {
                Progress = 5,
                StatusText = _localization.GetString("Progress_PreparingDownload", item.Name),
                TerminalOutput = "Resolving download URL...",
                IsActive = true
            });

            cancellationToken.ThrowIfCancellationRequested();

            var downloadUrl = await ResolveDownloadUrlAsync(item, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = _localization.GetString("Progress_FailedResolveUrl", item.Name),
                    IsActive = false
                });
                return false;
            }

            _logService?.LogInformation($"Resolved download URL: {downloadUrl}");

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new TaskProgressDetail
            {
                Progress = 10,
                StatusText = _localization.GetString("Progress_Downloading", item.Name),
                TerminalOutput = "Starting download...",
                IsActive = true
            });

            var downloadedFile = await DownloadFileAsync(downloadUrl, tempPath, item.Name, progress, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(downloadedFile))
            {
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = _localization.GetString("Progress_FailedDownload", item.Name),
                    IsActive = false
                });
                return false;
            }

            _logService?.LogInformation($"Successfully downloaded: {downloadedFile}");

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new TaskProgressDetail
            {
                Progress = 80,
                StatusText = _localization.GetString("Progress_Installing", item.Name),
                TerminalOutput = "Starting installation...",
                IsActive = true
            });

            var installSuccess = await InstallDownloadedFileAsync(downloadedFile, item.Name, progress, cancellationToken).ConfigureAwait(false);

            if (installSuccess)
            {
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = _localization.GetString("Progress_InstalledSuccess", item.Name),
                    TerminalOutput = "Installation complete",
                    IsActive = false
                });
                return true;
            }

            progress?.Report(new TaskProgressDetail
            {
                Progress = 0,
                StatusText = _localization.GetString("Progress_FailedInstall", item.Name),
                IsActive = false
            });
            return false;
        }
        catch (OperationCanceledException)
        {
            _logService?.LogInformation($"Download of {item.Name} was cancelled");
            progress?.Report(new TaskProgressDetail
            {
                Progress = 0,
                StatusText = _localization.GetString("Progress_DownloadCancelled", item.Name),
                IsActive = false
            });
            throw;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Error downloading/installing {item.Name}: {ex.Message}");
            progress?.Report(new TaskProgressDetail
            {
                Progress = 0,
                StatusText = _localization.GetString("Progress_Error", ex.Message),
                IsActive = false
            });
            return false;
        }
        finally
        {
            // Leave temp files in place — the Windows Installer service may still
            // reference the MSI source after msiexec.exe returns, and the OS will
            // clean up %TEMP% on its own schedule.
        }
    }

    private async Task<string> ResolveDownloadUrlAsync(ItemDefinition item, CancellationToken cancellationToken)
    {
        var isGitHubRelease = item.CustomProperties.TryGetValue("IsGitHubRelease", out var isGitHub)
            && isGitHub is bool isGitHubBool && isGitHubBool;

        if (isGitHubRelease)
        {
            if (item.CustomProperties.TryGetValue("DownloadUrl", out var githubUrl) &&
                item.CustomProperties.TryGetValue("AssetPattern", out var pattern))
            {
                return await ResolveGitHubReleaseUrlAsync(
                    githubUrl.ToString()!,
                    pattern.ToString()!,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return SelectDownloadUrl(item);
    }

    private string SelectDownloadUrl(ItemDefinition item)
    {
        var arch = GetCurrentArchitecture();
        _logService?.LogInformation($"Detecting architecture: {arch}");

        if (item.CustomProperties.TryGetValue($"DownloadUrl_{arch}", out var archUrl))
        {
            _logService?.LogInformation($"Found architecture-specific URL for {arch}");
            return archUrl.ToString()!;
        }

        if (item.CustomProperties.TryGetValue("DownloadUrl", out var genericUrl))
        {
            _logService?.LogWarning($"No {arch}-specific download found for {item.Name}, using generic URL. This may not work on this architecture.");
            return genericUrl.ToString()!;
        }

        _logService?.LogError($"No download URL found for {item.Name} (architecture: {arch})");
        throw new Exception($"No download URL found for {item.Name}. This app may not support {arch} architecture.");
    }

    private async Task<string> ResolveGitHubReleaseUrlAsync(
        string githubUrl,
        string assetPattern,
        CancellationToken cancellationToken)
    {
        _logService?.LogInformation($"Resolving GitHub release URL from: {githubUrl}");

        var apiUrl = githubUrl
            .Replace("github.com", "api.github.com/repos")
            .Replace("/releases/latest", "/releases/latest");

        using var apiRequest = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        apiRequest.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        using var response = await _httpClient.SendAsync(apiRequest, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var assets = doc.RootElement.GetProperty("assets").EnumerateArray();
        foreach (var asset in assets)
        {
            var name = asset.GetProperty("name").GetString();
            var downloadUrl = asset.GetProperty("browser_download_url").GetString();

            if (MatchesPattern(name!, assetPattern))
            {
                _logService?.LogInformation($"Matched asset: {name} -> {downloadUrl}");
                return downloadUrl!;
            }
        }

        throw new Exception($"No matching GitHub release asset found for pattern: {assetPattern}");
    }

    private bool MatchesPattern(string fileName, string pattern)
    {
        var regex = PatternCache.GetOrAdd(pattern, p =>
        {
            var regexPattern = "^" + Regex.Escape(p)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            return new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        });

        return regex.IsMatch(fileName);
    }

    private async Task<string?> DownloadFileAsync(
        string url,
        string downloadPath,
        string displayName,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var fileName = _fileSystemService.GetFileName(new Uri(url).LocalPath);
        var filePath = _fileSystemService.CombinePath(downloadPath, fileName);

        try
        {
            _logService?.LogInformation($"Downloading {fileName} from {url}...");

            using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, url);
            downloadRequest.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            using var response = await _httpClient.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var totalMB = totalBytes / (1024.0 * 1024.0);

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;
            int lastProgress = 0;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    var progressPercent = (int)((totalRead * 60.0 / totalBytes) + 10);
                    if (progressPercent > lastProgress)
                    {
                        lastProgress = progressPercent;
                        var downloadedMB = totalRead / (1024.0 * 1024.0);

                        progress?.Report(new TaskProgressDetail
                        {
                            Progress = progressPercent,
                            StatusText = _localization.GetString("Progress_Downloading", displayName),
                            TerminalOutput = $"{downloadedMB:F2} / {totalMB:F2} MB",
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
            _logService?.LogInformation($"Download of {fileName} was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Failed to download {fileName}: {ex.Message}");
            return null;
        }
    }

    private async Task<bool> InstallDownloadedFileAsync(
        string filePath,
        string displayName,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var extension = _fileSystemService.GetExtension(filePath).ToLowerInvariant();

        progress?.Report(new TaskProgressDetail
        {
            Progress = 80,
            StatusText = _localization.GetString("Progress_Installing", displayName),
            TerminalOutput = $"Installing {extension} file...",
            IsActive = true
        });

        return extension switch
        {
            ".msi" => await InstallMsiAsync(filePath, displayName, progress, cancellationToken).ConfigureAwait(false),
            ".exe" => await InstallExeAsync(filePath, displayName, progress, cancellationToken).ConfigureAwait(false),
            ".zip" => await InstallZipAsync(filePath, displayName, progress, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"File type {extension} is not supported for installation")
        };
    }

    private async Task<bool> InstallMsiAsync(
        string msiPath,
        string displayName,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var logPath = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), $"Winhance_MSI_{Guid.NewGuid():N}.log");
            _logService?.LogInformation($"Installing MSI: {msiPath} (log: {logPath})");

            var exitCode = await RunMsiExecAsync(
                $"/i \"{msiPath}\" /qn /norestart /l*v \"{logPath}\"",
                cancellationToken).ConfigureAwait(false);

            // 1612 = "Installation source not available" — stale registration from a previous
            // install whose temp directory was cleaned up. Uninstall the stale entry using
            // the ProductCode GUID (which doesn't need source files), then retry fresh.
            if (exitCode == 1612)
            {
                _logService?.LogInformation($"MSI returned 1612 (stale source). Removing old registration and retrying...");
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 85,
                    StatusText = _localization.GetString("Progress_Installing", displayName),
                    TerminalOutput = "Removing stale registration...",
                    IsActive = true
                });

                // Read the ProductCode from the new MSI so we can uninstall by GUID.
                // Uninstalling by GUID bypasses the stale source check entirely.
                var productCode = GetProductCodeFromMsi(msiPath);
                if (!string.IsNullOrEmpty(productCode))
                {
                    _logService?.LogInformation($"Found ProductCode {productCode}, uninstalling stale registration by GUID...");
                    await RunMsiExecAsync($"/x {productCode} /qn /norestart", cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _logService?.LogWarning("Could not read ProductCode from MSI, falling back to file-based uninstall");
                    await RunMsiExecAsync($"/x \"{msiPath}\" /qn /norestart", cancellationToken).ConfigureAwait(false);
                }

                logPath = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), $"Winhance_MSI_{Guid.NewGuid():N}.log");
                exitCode = await RunMsiExecAsync(
                    $"/i \"{msiPath}\" /qn /norestart /l*v \"{logPath}\"",
                    cancellationToken).ConfigureAwait(false);
            }

            // Exit code 0 = success, 3010 = success but reboot required
            if (exitCode != 0 && exitCode != 3010)
            {
                _logService?.LogError($"MSI installation failed with exit code {exitCode}. See log: {logPath}");
                return false;
            }

            if (exitCode == 3010)
            {
                _logService?.LogInformation($"MSI installed successfully (reboot required). Log: {logPath}");
            }

            progress?.Report(new TaskProgressDetail
            {
                Progress = 95,
                StatusText = _localization.GetString("Progress_Installing", displayName),
                TerminalOutput = "MSI installation completed",
                IsActive = true
            });

            return true;
        }
        catch (OperationCanceledException)
        {
            _logService?.LogInformation($"MSI installation of {displayName} was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Failed to install MSI {displayName}: {ex.Message}");
            return false;
        }
    }

    private async Task<int> RunMsiExecAsync(string arguments, CancellationToken cancellationToken)
    {
        var result = await _processExecutor.ExecuteAsync("msiexec.exe", arguments, cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }

    /// <summary>
    /// Reads the ProductCode GUID from an MSI file using the Windows Installer API.
    /// Opens the package in query-only mode (IGNOREMACHINESTATE) so no install logic runs.
    /// </summary>
    private static string? GetProductCodeFromMsi(string msiPath)
    {
        const uint MSIOPENPACKAGEFLAGS_IGNOREMACHINESTATE = 1;

        uint result = MsiApi.MsiOpenPackageEx(msiPath, MSIOPENPACKAGEFLAGS_IGNOREMACHINESTATE, out var hProduct);
        if (result != 0)
            return null;

        try
        {
            var buffer = new StringBuilder(39); // GUID format {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} = 38 chars + null
            uint bufferSize = 39;
            result = MsiApi.MsiGetProductProperty(hProduct, "ProductCode", buffer, ref bufferSize);
            return result == 0 ? buffer.ToString() : null;
        }
        finally
        {
            MsiApi.MsiCloseHandle(hProduct);
        }
    }

    private async Task<bool> InstallExeAsync(
        string exePath,
        string displayName,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            _logService?.LogInformation($"Installing EXE: {exePath}");

            var silentArgs = new[] { "/S", "/SILENT /NORESTART", "/VERYSILENT /NORESTART", "/quiet /norestart" };

            foreach (var args in silentArgs)
            {
                try
                {
                    _logService?.LogInformation($"Trying silent install with args: {args}");

                    var result = await _processExecutor.ExecuteAsync(exePath, args, cancellationToken).ConfigureAwait(false);

                    if (result.ExitCode == 0)
                    {
                        progress?.Report(new TaskProgressDetail
                        {
                            Progress = 95,
                            StatusText = _localization.GetString("Progress_Installing", displayName),
                            TerminalOutput = "EXE installation completed",
                            IsActive = true
                        });
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logService?.LogWarning($"Silent install attempt with '{args}' failed: {ex.Message}");
                    continue;
                }
            }

            _logService?.LogWarning($"All silent installation attempts failed for {displayName}, launching interactive installer");

            progress?.Report(new TaskProgressDetail
            {
                Progress = 90,
                StatusText = _localization.GetString("Progress_LaunchingInstaller", displayName),
                TerminalOutput = "Launching interactive installer (requires user interaction)",
                IsActive = true
            });

            await _processExecutor.ShellExecuteAsync(exePath).ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException)
        {
            _logService?.LogInformation($"EXE installation of {displayName} was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Failed to install EXE {displayName}: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> InstallZipAsync(
        string zipPath,
        string displayName,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            _logService?.LogInformation($"Extracting ZIP: {zipPath}");

            var extractPath = _fileSystemService.CombinePath(
                _interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance",
                "Apps",
                displayName
            );

            await Task.Run(() =>
            {
                if (_fileSystemService.DirectoryExists(extractPath))
                    _fileSystemService.DeleteDirectory(extractPath, true);

                _fileSystemService.CreateDirectory(extractPath);
                ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);
            }, cancellationToken).ConfigureAwait(false);

            progress?.Report(new TaskProgressDetail
            {
                Progress = 95,
                StatusText = _localization.GetString("Progress_Extracting", displayName),
                TerminalOutput = $"Extracted to: {extractPath}",
                IsActive = true
            });

            _logService?.LogInformation($"ZIP extracted to {extractPath}. Manual setup may be required.");

            return true;
        }
        catch (OperationCanceledException)
        {
            _logService?.LogInformation($"ZIP extraction of {displayName} was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Failed to extract ZIP {displayName}: {ex.Message}");
            return false;
        }
    }

    private string GetCurrentArchitecture()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => "x64"
        };
    }
}
