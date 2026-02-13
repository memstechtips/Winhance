using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class ChocolateyService : IChocolateyService
{
    private readonly ILogService _logService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly ILocalizationService _localization;
    private bool? _isInstalled;

    public ChocolateyService(
        ILogService logService,
        ITaskProgressService taskProgressService,
        ILocalizationService localization)
    {
        _logService = logService;
        _taskProgressService = taskProgressService;
        _localization = localization;
    }

    public Task<bool> IsChocolateyInstalledAsync(CancellationToken cancellationToken = default)
    {
        if (_isInstalled.HasValue)
            return Task.FromResult(_isInstalled.Value);

        _isInstalled = FindChocoExecutable() != null;
        return Task.FromResult(_isInstalled.Value);
    }

    public async Task<bool> InstallChocolateyAsync(CancellationToken cancellationToken = default)
    {
        if (await IsChocolateyInstalledAsync(cancellationToken))
            return true;

        try
        {
            _taskProgressService?.UpdateProgress(10, _localization.GetString("Progress_Choco_Installing"));
            _logService.LogInformation("Installing Chocolatey package manager...");

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" +
                    "[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; " +
                    "iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                _isInstalled = true;
                _logService.LogInformation("Chocolatey installed successfully");
                _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_Choco_Installed"));
                return true;
            }

            _logService.LogError($"Chocolatey installation failed (exit code {process.ExitCode}): {stderr}");
            return false;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Failed to install Chocolatey: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> InstallPackageAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default)
    {
        displayName ??= chocoPackageId;

        var chocoPath = FindChocoExecutable();
        if (chocoPath == null)
        {
            _logService.LogError("Chocolatey executable not found");
            return false;
        }

        try
        {
            _taskProgressService?.UpdateProgress(10, _localization.GetString("Progress_Choco_InstallingPackage", displayName));
            _logService.LogInformation($"Installing '{chocoPackageId}' via Chocolatey...");

            var startInfo = new ProcessStartInfo
            {
                FileName = chocoPath,
                Arguments = $"install {chocoPackageId} -y --no-progress --ignore-checksums",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                _logService.LogInformation($"[choco] {e.Data}");

                _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                {
                    Progress = 50,
                    StatusText = _localization.GetString("Progress_Choco_InstallingPackage", displayName),
                    TerminalOutput = e.Data
                });
            };

            process.Start();
            process.BeginOutputReadLine();

            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                _logService.LogInformation($"Chocolatey successfully installed '{chocoPackageId}'");
                _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_InstalledSuccess", displayName));
                return true;
            }

            _logService.LogError($"Chocolatey install of '{chocoPackageId}' failed (exit code {process.ExitCode}): {stderr}");
            return false;
        }
        catch (OperationCanceledException)
        {
            _logService.LogInformation($"Chocolatey install of '{chocoPackageId}' was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error installing '{chocoPackageId}' via Chocolatey: {ex.Message}");
            return false;
        }
    }

    public async Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var chocoPath = FindChocoExecutable();
        if (chocoPath == null)
            return result;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = chocoPath,
                Arguments = "list -r",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    result.Add(parts[0].Trim());
                }
            }

            _logService.LogInformation($"Chocolatey: Found {result.Count} installed packages");
        }
        catch (OperationCanceledException)
        {
            _logService.LogWarning("Chocolatey package list timed out");
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Error querying Chocolatey packages: {ex.Message}");
        }

        return result;
    }

    private static string? FindChocoExecutable()
    {
        // Check the standard installation path first
        var standardPath = @"C:\ProgramData\chocolatey\bin\choco.exe";
        if (File.Exists(standardPath))
            return standardPath;

        // Scan PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        return pathEnv
            .Split(Path.PathSeparator)
            .Select(dir => Path.Combine(dir, "choco.exe"))
            .FirstOrDefault(File.Exists);
    }
}
