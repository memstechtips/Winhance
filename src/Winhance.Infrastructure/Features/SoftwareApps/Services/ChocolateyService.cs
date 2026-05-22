using System;
using System.Collections.Generic;
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
    private readonly IProcessExecutor _processExecutor;
    private readonly IFileSystemService _fileSystemService;
    private readonly object _installCheckLock = new();
    private bool? _isInstalled;

    public ChocolateyService(
        ILogService logService,
        ITaskProgressService taskProgressService,
        ILocalizationService localization,
        IProcessExecutor processExecutor,
        IFileSystemService fileSystemService)
    {
        _logService = logService;
        _taskProgressService = taskProgressService;
        _localization = localization;
        _processExecutor = processExecutor;
        _fileSystemService = fileSystemService;
    }

    public Task<bool> IsChocolateyInstalledAsync(CancellationToken cancellationToken = default)
    {
        lock (_installCheckLock)
        {
            if (_isInstalled.HasValue)
                return Task.FromResult(_isInstalled.Value);

            _isInstalled = FindChocoExecutable() != null;
            return Task.FromResult(_isInstalled.Value);
        }
    }

    public async Task<bool> InstallChocolateyAsync(CancellationToken cancellationToken = default)
    {
        if (await IsChocolateyInstalledAsync(cancellationToken).ConfigureAwait(false))
            return true;

        try
        {
            var statusText = _localization.GetString("Progress_Choco_Installing");
            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                Progress = 10,
                StatusText = statusText,
                TerminalOutput = "Installing Chocolatey package manager..."
            });
            _logService.LogInformation("Installing Chocolatey package manager...");

            var arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" +
                "[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; " +
                "iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))\"";

            var result = await _processExecutor.ExecuteWithStreamingAsync(
                "powershell.exe",
                arguments,
                onOutputLine: line =>
                {
                    _logService.LogInformation($"[choco-bootstrap] {line}");
                    _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                    {
                        Progress = 50,
                        StatusText = statusText,
                        TerminalOutput = line
                    });
                },
                ct: cancellationToken).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                lock (_installCheckLock) { _isInstalled = true; }
                _logService.LogInformation("Chocolatey installed successfully");
                _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = _localization.GetString("Progress_Choco_Installed"),
                    TerminalOutput = "Chocolatey installed successfully"
                });
                return true;
            }

            _logService.LogError($"Chocolatey installation failed (exit code {result.ExitCode}): {result.StandardError}");
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

            var (success, alreadyInstalled) = await RunChocoInstallAsync(chocoPath, chocoPackageId, displayName, cancellationToken).ConfigureAwait(false);

            // Choco exits 0 with "xxx already installed" when its lib folder still has the
            // package record even though the actual app is gone. We're in the install path
            // specifically because Winhance detection saw the app as NOT installed — so this
            // is a stale-ghost case (typically a prior out-of-band uninstall that didn't
            // reach Chocolatey's bookkeeping). Clear the record(s) and try once more.
            if (success && alreadyInstalled)
            {
                _logService.LogWarning($"Chocolatey reports '{chocoPackageId}' as already-installed but Winhance detection saw it as missing. Clearing stale record and retrying install.");
                await CleanupStalePackageRecordAsync(chocoPackageId, displayName, cancellationToken).ConfigureAwait(false);
                (success, alreadyInstalled) = await RunChocoInstallAsync(chocoPath, chocoPackageId, displayName, cancellationToken).ConfigureAwait(false);

                if (alreadyInstalled)
                {
                    // Cleanup didn't fully clear the ghost; don't pretend the install succeeded.
                    _logService.LogError($"Chocolatey still reports '{chocoPackageId}' as already-installed after ghost-cleanup. Install did not run.");
                    return false;
                }
            }

            if (success)
            {
                _logService.LogInformation($"Chocolatey successfully installed '{chocoPackageId}'");
                _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_InstalledSuccess", displayName));
                return true;
            }

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

    private async Task<(bool success, bool alreadyInstalled)> RunChocoInstallAsync(
        string chocoPath, string chocoPackageId, string displayName, CancellationToken ct)
    {
        bool alreadyInstalled = false;
        var result = await _processExecutor.ExecuteWithStreamingAsync(
            chocoPath,
            $"install {chocoPackageId} -y --no-progress --ignore-checksums",
            onOutputLine: line =>
            {
                _logService.LogInformation($"[choco] {line}");
                _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                {
                    Progress = 50,
                    StatusText = _localization.GetString("Progress_Choco_InstallingPackage", displayName),
                    TerminalOutput = line
                });
                if (line.Contains("already installed", StringComparison.OrdinalIgnoreCase))
                    alreadyInstalled = true;
            },
            ct: ct).ConfigureAwait(false);

        if (result.ExitCode != 0)
            _logService.LogError($"Chocolatey install of '{chocoPackageId}' failed (exit code {result.ExitCode}): {result.StandardError}");

        return (result.ExitCode == 0, alreadyInstalled);
    }

    public async Task<bool> UninstallPackageAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default)
    {
        displayName ??= chocoPackageId;

        if (FindChocoExecutable() == null)
        {
            _logService.LogError("Chocolatey executable not found");
            return false;
        }

        try
        {
            _taskProgressService?.UpdateProgress(10, _localization.GetString("Progress_Choco_UninstallingPackage", displayName));

            void OnOutput(string line)
            {
                _logService.LogInformation($"[choco] {line}");
                _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                {
                    Progress = 50,
                    StatusText = _localization.GetString("Progress_Choco_UninstallingPackage", displayName),
                    TerminalOutput = line
                });
            }

            // Primary uninstall keeps the autouninstaller so the app's real uninstaller runs.
            // --remove-dependencies cascades the meta -> .install/.portable bundle siblings,
            // auto-answering the interactive metapackage prompt that -y can't handle.
            const string flags = "-y --remove-dependencies --ignore-detected-reboot --no-progress";

            var (succeeded, failed, attempted) =
                await SweepChocoUninstallVariantsAsync(chocoPackageId, flags, OnOutput, cancellationToken)
                    .ConfigureAwait(false);

            // Fallback: installed-list lookup may have returned empty (timeout / choco glitch).
            // Try the bare ID directly so existing behavior is preserved when the variant sweep
            // can't see anything to act on.
            if (!attempted)
            {
                _logService.LogInformation($"Uninstalling '{chocoPackageId}' via Chocolatey (bare-ID fallback)...");
                var fallback = await RunChocoUninstallAsync(chocoPackageId, flags, OnOutput, cancellationToken).ConfigureAwait(false);
                if (fallback) succeeded++; else failed++;
            }

            var allOk = failed == 0 && (succeeded > 0 || !attempted);
            if (allOk)
            {
                _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_UninstalledSuccess", displayName));
            }
            return allOk;
        }
        catch (OperationCanceledException)
        {
            _logService.LogInformation($"Chocolatey uninstall of '{chocoPackageId}' was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error uninstalling '{chocoPackageId}' via Chocolatey: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sweeps Chocolatey's installed list for <paramref name="chocoPackageId"/> and its
    /// .install / .portable bundle siblings. Uninstalls them with the given <paramref name="flags"/>,
    /// meta-first so that --remove-dependencies (when included in flags) cascades cleanly without
    /// triggering the interactive "uninstall sibling as well?" prompt.
    /// </summary>
    /// <returns>
    /// (succeeded, failed, attempted) — attempted=false means the installed-list lookup
    /// returned empty and the caller should consider its own fallback.
    /// </returns>
    private async Task<(int succeeded, int failed, bool attempted)> SweepChocoUninstallVariantsAsync(
        string chocoPackageId,
        string flags,
        Action<string> onOutputLine,
        CancellationToken cancellationToken)
    {
        var installed = await GetInstalledPackageIdsAsync(cancellationToken).ConfigureAwait(false);

        // Meta first: --remove-dependencies cascades to siblings, no prompt fires.
        // .install / .portable fallbacks catch the edge case where the user installed
        // a suffix variant directly without the meta.
        var variantOrder = new[]
        {
            chocoPackageId,
            $"{chocoPackageId}.install",
            $"{chocoPackageId}.portable",
        };

        int succeeded = 0;
        int failed = 0;
        bool attempted = false;

        foreach (var candidate in variantOrder)
        {
            if (!installed.Contains(candidate))
                continue;

            attempted = true;
            _logService.LogInformation($"Uninstalling '{candidate}' via Chocolatey...");

            if (await RunChocoUninstallAsync(candidate, flags, onOutputLine, cancellationToken).ConfigureAwait(false))
            {
                succeeded++;
                // Cascade may have removed the .install/.portable sibling — refresh so
                // we don't try (and fail with "not installed") on the next iteration.
                installed = await GetInstalledPackageIdsAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                failed++;
            }
        }

        return (succeeded, failed, attempted);
    }

    private async Task<bool> RunChocoUninstallAsync(
        string chocoPackageId,
        string flags,
        Action<string> onOutputLine,
        CancellationToken cancellationToken)
    {
        var chocoPath = FindChocoExecutable();
        if (chocoPath == null) return false;

        var result = await _processExecutor.ExecuteWithStreamingAsync(
            chocoPath,
            $"uninstall {chocoPackageId} {flags}",
            onOutputLine: onOutputLine,
            ct: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            _logService.LogInformation($"Chocolatey successfully uninstalled '{chocoPackageId}'");
            return true;
        }

        _logService.LogWarning($"Chocolatey uninstall of '{chocoPackageId}' exited with code {result.ExitCode}: {result.StandardError}");
        return false;
    }

    public async Task<bool> CleanupStalePackageRecordAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default)
    {
        displayName ??= chocoPackageId;

        if (FindChocoExecutable() == null)
            return true;

        try
        {
            // --skip-autouninstaller / --skip-hooks: the app's uninstaller is already gone
            // (or not needed); we only want to clear Chocolatey's lib record so detection
            // stops reporting the ghost. --remove-dependencies cascades the meta -> .install
            // sibling so we don't leave the actual package half-cleaned.
            const string flags = "-y --remove-dependencies --skip-autouninstaller --skip-hooks --ignore-detected-reboot --no-progress";

            void OnOutput(string line)
            {
                _logService.LogInformation($"[choco-cleanup] {line}");
            }

            var (succeeded, failed, _) =
                await SweepChocoUninstallVariantsAsync(chocoPackageId, flags, OnOutput, cancellationToken)
                    .ConfigureAwait(false);

            if (failed > 0)
            {
                _logService.LogWarning($"Chocolatey cleanup for '{chocoPackageId}' had {failed} failure(s); ghost record may persist.");
                return false;
            }

            if (succeeded > 0)
                _logService.LogInformation($"Chocolatey record for '{chocoPackageId}' cleaned up ({succeeded} variant(s)).");

            return true;
        }
        catch (OperationCanceledException)
        {
            _logService.LogInformation($"Chocolatey cleanup for '{chocoPackageId}' was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Chocolatey cleanup for '{chocoPackageId}' errored: {ex.Message}");
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
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var execResult = await _processExecutor.ExecuteAsync(chocoPath, "list -r", cts.Token).ConfigureAwait(false);

            foreach (var line in execResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
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

    private string? FindChocoExecutable()
    {
        // Check the standard installation path first
        var standardPath = @"C:\ProgramData\chocolatey\bin\choco.exe";
        if (_fileSystemService.FileExists(standardPath))
            return standardPath;

        // Scan PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        return pathEnv
            .Split(Path.PathSeparator)
            .Select(dir => _fileSystemService.CombinePath(dir, "choco.exe"))
            .FirstOrDefault(_fileSystemService.FileExists);
    }
}
