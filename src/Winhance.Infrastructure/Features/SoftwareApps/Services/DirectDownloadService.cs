using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class DirectDownloadService : IDirectDownloadService
{
    private readonly ILogService _logService;
    private readonly IPowerShellExecutionService _powerShellService;
    private readonly HttpClient _httpClient;

    public DirectDownloadService(
        ILogService logService,
        IPowerShellExecutionService powerShellService)
    {
        _logService = logService;
        _powerShellService = powerShellService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Winhance-Download-Manager");
    }

    public async Task<bool> DownloadAndInstallAsync(
        ItemDefinition item,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"Winhance_{item.Id}_{Guid.NewGuid():N}");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Directory.CreateDirectory(tempPath);
            _logService?.LogInformation($"Created temporary directory: {tempPath}");

            progress?.Report(new TaskProgressDetail
            {
                Progress = 5,
                StatusText = $"Preparing to download {item.Name}...",
                TerminalOutput = "Resolving download URL...",
                IsActive = true
            });

            cancellationToken.ThrowIfCancellationRequested();

            var downloadUrl = await ResolveDownloadUrlAsync(item, cancellationToken);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Failed to resolve download URL for {item.Name}",
                    IsActive = false
                });
                return false;
            }

            _logService?.LogInformation($"Resolved download URL: {downloadUrl}");

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new TaskProgressDetail
            {
                Progress = 10,
                StatusText = $"Downloading {item.Name}...",
                TerminalOutput = "Starting download...",
                IsActive = true
            });

            var downloadedFile = await DownloadFileAsync(downloadUrl, tempPath, item.Name, progress, cancellationToken);
            if (string.IsNullOrEmpty(downloadedFile))
            {
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Failed to download {item.Name}",
                    IsActive = false
                });
                return false;
            }

            _logService?.LogInformation($"Successfully downloaded: {downloadedFile}");

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new TaskProgressDetail
            {
                Progress = 80,
                StatusText = $"Installing {item.Name}...",
                TerminalOutput = "Starting installation...",
                IsActive = true
            });

            var installSuccess = await InstallDownloadedFileAsync(downloadedFile, item.Name, progress, cancellationToken);

            if (installSuccess)
            {
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = $"Successfully installed {item.Name}",
                    TerminalOutput = "Installation complete",
                    IsActive = false
                });
                return true;
            }

            progress?.Report(new TaskProgressDetail
            {
                Progress = 0,
                StatusText = $"Failed to install {item.Name}",
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
                StatusText = $"Download of {item.Name} was cancelled",
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
                StatusText = $"Error: {ex.Message}",
                IsActive = false
            });
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                    _logService?.LogInformation($"Cleaned up temporary directory: {tempPath}");
                }
            }
            catch (Exception ex)
            {
                _logService?.LogWarning($"Failed to cleanup temporary directory {tempPath}: {ex.Message}");
            }
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
                    cancellationToken);
            }
        }

        return SelectDownloadUrl(item);
    }

    private string SelectDownloadUrl(ItemDefinition item)
    {
        var arch = GetCurrentArchitecture();

        if (item.CustomProperties.TryGetValue($"DownloadUrl_{arch}", out var archUrl))
            return archUrl.ToString()!;

        if (item.CustomProperties.TryGetValue("DownloadUrl", out var genericUrl))
            return genericUrl.ToString()!;

        throw new Exception("No download URL found in item CustomProperties");
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

        var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
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
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            fileName,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private async Task<string> DownloadFileAsync(
        string url,
        string downloadPath,
        string displayName,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(new Uri(url).LocalPath);
        var filePath = Path.Combine(downloadPath, fileName);

        try
        {
            _logService?.LogInformation($"Downloading {fileName} from {url}...");

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var totalMB = totalBytes / (1024.0 * 1024.0);

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;
            int lastProgress = 0;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
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
                            StatusText = $"Downloading {displayName}: {downloadedMB:F2} MB / {totalMB:F2} MB",
                            TerminalOutput = $"{downloadedMB:F2} MB of {totalMB:F2} MB",
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
            return null!;
        }
    }

    private async Task<bool> InstallDownloadedFileAsync(
        string filePath,
        string displayName,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        progress?.Report(new TaskProgressDetail
        {
            Progress = 80,
            StatusText = $"Installing {displayName}...",
            TerminalOutput = $"Installing {extension} file...",
            IsActive = true
        });

        return extension switch
        {
            ".msi" => await InstallMsiAsync(filePath, displayName, progress, cancellationToken),
            ".exe" => await InstallExeAsync(filePath, displayName, progress, cancellationToken),
            ".zip" => await InstallZipAsync(filePath, displayName, progress, cancellationToken),
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
            _logService?.LogInformation($"Installing MSI: {msiPath}");

            var escapedPath = msiPath.Replace("'", "''");
            var script = $@"
$process = Start-Process msiexec.exe -ArgumentList '/i', '{escapedPath}', '/quiet', '/norestart' -Wait -NoNewWindow -PassThru
if ($process.ExitCode -eq 0) {{
    Write-Output 'Installation completed successfully'
}} else {{
    throw ""Installation failed with exit code $($process.ExitCode)""
}}
";

            await _powerShellService.ExecuteScriptAsync(script, progress, cancellationToken);

            progress?.Report(new TaskProgressDetail
            {
                Progress = 95,
                StatusText = $"Installing {displayName}...",
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

    private async Task<bool> InstallExeAsync(
        string exePath,
        string displayName,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            _logService?.LogInformation($"Installing EXE: {exePath}");

            var escapedPath = exePath.Replace("'", "''");
            var silentArgs = new[] { "/S", "/SILENT /NORESTART", "/VERYSILENT /NORESTART", "/quiet /norestart" };

            foreach (var args in silentArgs)
            {
                try
                {
                    _logService?.LogInformation($"Trying silent install with args: {args}");

                    var script = $@"
$process = Start-Process '{escapedPath}' -ArgumentList '{args}' -Wait -NoNewWindow -PassThru
if ($process.ExitCode -eq 0) {{
    Write-Output 'Installation completed successfully'
    exit 0
}}
";

                    await _powerShellService.ExecuteScriptAsync(script, progress, cancellationToken);

                    progress?.Report(new TaskProgressDetail
                    {
                        Progress = 95,
                        StatusText = $"Installing {displayName}...",
                        TerminalOutput = "EXE installation completed",
                        IsActive = true
                    });

                    return true;
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
                StatusText = $"Launching installer for {displayName}...",
                TerminalOutput = "Launching interactive installer (requires user interaction)",
                IsActive = true
            });

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });

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

            var extractPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance",
                "Apps",
                displayName
            );

            var escapedZipPath = zipPath.Replace("'", "''");
            var escapedExtractPath = extractPath.Replace("'", "''");

            var script = $@"
if (Test-Path '{escapedExtractPath}') {{
    Remove-Item -Path '{escapedExtractPath}' -Recurse -Force
}}
New-Item -ItemType Directory -Path '{escapedExtractPath}' -Force | Out-Null
Expand-Archive -Path '{escapedZipPath}' -DestinationPath '{escapedExtractPath}' -Force
Write-Output 'Extracted to {extractPath}'
";

            await _powerShellService.ExecuteScriptAsync(script, progress, cancellationToken);

            progress?.Report(new TaskProgressDetail
            {
                Progress = 95,
                StatusText = $"Extracting {displayName}...",
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
