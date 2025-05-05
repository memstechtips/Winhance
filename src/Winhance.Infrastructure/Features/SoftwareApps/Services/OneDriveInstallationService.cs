using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Service that handles OneDrive installation.
/// </summary>
public class OneDriveInstallationService : IOneDriveInstallationService, IDisposable
{
    private readonly ILogService _logService;
    private readonly HttpClient _httpClient;
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "WinhanceInstaller");

    /// <summary>
    /// Initializes a new instance of the <see cref="OneDriveInstallationService"/> class.
    /// </summary>
    /// <param name="logService">The log service.</param>
    public OneDriveInstallationService(ILogService logService)
    {
        _logService = logService;
        _httpClient = new HttpClient();
        Directory.CreateDirectory(_tempDir);
    }

    /// <inheritdoc/>
    public async Task<bool> InstallOneDriveAsync(
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            progress?.Report(new TaskProgressDetail
            {
                Progress = 0,
                StatusText = "Starting OneDrive installation...",
                DetailedMessage = "Downloading OneDrive installer from Microsoft"
            });

            // Download OneDrive from the specific URL
            string downloadUrl = "https://go.microsoft.com/fwlink/p/?LinkID=2182910";
            string installerPath = Path.Combine(_tempDir, "OneDriveSetup.exe");

            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(downloadUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        Progress = 0,
                        StatusText = "Failed to download OneDrive installer",
                        DetailedMessage = $"HTTP error: {response.StatusCode}",
                        LogLevel = LogLevel.Error
                    });
                    return false;
                }

                using (var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fileStream, cancellationToken);
                }
            }

            progress?.Report(new TaskProgressDetail
            {
                Progress = 50,
                StatusText = "Installing OneDrive...",
                DetailedMessage = "Running OneDrive installer"
            });

            // Run the installer
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo.FileName = installerPath;
                process.StartInfo.Arguments = "/silent";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                await Task.Run(() => process.WaitForExit(), cancellationToken);

                bool success = process.ExitCode == 0;

                progress?.Report(new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = success ? "OneDrive installed successfully" : "OneDrive installation failed",
                    DetailedMessage = $"Installer exited with code: {process.ExitCode}",
                    LogLevel = success ? LogLevel.Success : LogLevel.Error
                });

                return success;
            }
        }
        catch (Exception ex)
        {
            progress?.Report(new TaskProgressDetail
            {
                Progress = 0,
                StatusText = "Error installing OneDrive",
                DetailedMessage = $"Exception: {ex.Message}",
                LogLevel = LogLevel.Error
            });
            return false;
        }
    }

    /// <summary>
    /// Disposes the resources used by the service.
    /// </summary>
    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
