using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;

/// <summary>
/// Handles WinGet/AppInstaller bootstrapping, upgrade, and readiness checks.
/// </summary>
public class WinGetBootstrapper : IWinGetBootstrapper
{
    private readonly WinGetComSession _comSession;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localization;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IPowerShellRunner _powerShellRunner;
    private readonly IFileSystemService _fileSystemService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly System.Net.Http.HttpClient _httpClient;

    private bool _systemWinGetAvailable;

    private const int ComInitTimeoutSeconds = 5;

    public event EventHandler? WinGetInstalled;

    public bool IsSystemWinGetAvailable => _systemWinGetAvailable;

    public WinGetBootstrapper(
        WinGetComSession comSession,
        ILogService logService,
        ILocalizationService localization,
        IInteractiveUserService interactiveUserService,
        IPowerShellRunner powerShellRunner,
        IFileSystemService fileSystemService,
        ITaskProgressService taskProgressService,
        System.Net.Http.HttpClient httpClient)
    {
        _comSession = comSession;
        _logService = logService;
        _localization = localization;
        _interactiveUserService = interactiveUserService;
        _powerShellRunner = powerShellRunner;
        _fileSystemService = fileSystemService;
        _taskProgressService = taskProgressService;
        _httpClient = httpClient;
    }

    public async Task<bool> InstallWinGetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logService?.LogInformation("Starting AppInstaller installation...");

            var installer = new WinGetInstaller(_powerShellRunner, _httpClient, _logService, _localization, _taskProgressService, _fileSystemService);
            var (success, message) = await installer.InstallAsync(cancellationToken).ConfigureAwait(false);

            if (!success)
            {
                _logService?.LogError($"AppInstaller installation failed: {message}");
                return false;
            }

            _logService?.LogInformation("AppInstaller installed, waiting for COM API readiness...");

            // Retry COM init with loop (10 retries, 3s delay)
            for (int i = 0; i < 10; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(3000, cancellationToken).ConfigureAwait(false);

                _comSession.ResetFactory();
                if (await Task.Run(() => _comSession.EnsureComInitialized(), cancellationToken).ConfigureAwait(false))
                {
                    _logService?.LogInformation($"COM API ready after {i + 1} attempt(s)");
                    _systemWinGetAvailable = true;
                    WinGetInstalled?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                _logService?.LogInformation($"COM init attempt {i + 1}/10 failed, retrying...");
            }

            _logService?.LogWarning("COM API did not become ready after AppInstaller installation");
            // Still consider it a partial success — system winget may be available
            _systemWinGetAvailable = WinGetCliRunner.IsSystemWinGetAvailable(_interactiveUserService);
            if (_systemWinGetAvailable)
            {
                WinGetInstalled?.Invoke(this, EventArgs.Empty);
                return true;
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            _logService?.LogWarning("AppInstaller installation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Error installing AppInstaller: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> EnsureWinGetReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logService?.LogInformation("Checking WinGet availability...");

            _systemWinGetAvailable = WinGetCliRunner.IsSystemWinGetAvailable(_interactiveUserService);
            _logService?.LogInformation($"System winget available: {_systemWinGetAvailable}");

            if (_systemWinGetAvailable)
            {
                // OTS: COM activation uses the admin's MSIX registration but the
                // desktop session belongs to the standard user — the out-of-process
                // COM server cannot start in this mismatched state. Skip straight
                // to CLI fallback.
                if (_interactiveUserService.IsOtsElevation)
                {
                    _logService?.LogInformation("OTS elevation detected — skipping COM init (CLI fallback will be used)");
                    _comSession.ComInitTimedOut = true;
                }
                else
                {
                    // System winget is present — try COM init (likely succeeds).
                    // Use Task.WhenAny to enforce a timeout without nesting Task.Run
                    // (nested Task.Run breaks COM activation context).
                    try
                    {
                        var initTask = Task.Run(() => _comSession.EnsureComInitialized(), cancellationToken);
                        var completed = await Task.WhenAny(
                            initTask, Task.Delay(TimeSpan.FromSeconds(ComInitTimeoutSeconds), cancellationToken)).ConfigureAwait(false);

                        if (completed != initTask)
                        {
                            _logService?.LogWarning("COM init timed out in EnsureWinGetReadyAsync — using CLI fallback");
                            _comSession.ComInitTimedOut = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService?.LogWarning($"COM init failed (detection may use CLI fallback): {ex.Message}");
                    }
                }
            }
            else
            {
                // Only bundled winget available — skip COM attempt (will fail anyway)
                _logService?.LogInformation("No system winget — bundled CLI will be used for detection");
            }

            // Return true as long as bundled or system winget exists
            var exePath = WinGetCliRunner.GetWinGetExePath(_interactiveUserService);
            if (exePath == null || !_fileSystemService.FileExists(exePath))
            {
                _logService?.LogWarning("WinGet CLI not found — install/uninstall will fail");
                return false;
            }

            _logService?.LogInformation($"WinGet CLI found at: {exePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Error checking WinGet availability: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpgradeAppInstallerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var bundledPath = WinGetCliRunner.GetBundledWinGetExePath();
            if (bundledPath == null)
            {
                _logService?.LogWarning("Cannot upgrade AppInstaller — bundled winget not found");
                return false;
            }

            _logService?.LogInformation("Attempting to upgrade AppInstaller via bundled winget...");

            var arguments = "upgrade --id Microsoft.AppInstaller --exact --silent --accept-source-agreements --accept-package-agreements --force --disable-interactivity";

            var result = await WinGetCliRunner.RunAsync(
                arguments,
                onOutputLine: line =>
                {
                    if (!IsWinGetOutputNoise(line))
                        _logService?.LogInformation($"[winget-upgrade] {line}");
                },
                onErrorLine: line => _logService?.LogWarning($"[winget-upgrade-err] {line}"),
                cancellationToken: cancellationToken,
                timeoutMs: 120_000,
                exePathOverride: bundledPath).ConfigureAwait(false);

            if (WinGetExitCodes.IsSuccess(result.ExitCode))
            {
                _logService?.LogInformation($"AppInstaller upgrade succeeded (exit code: 0x{result.ExitCode:X8})");
                return true;
            }

            _logService?.LogWarning($"AppInstaller upgrade returned exit code: 0x{result.ExitCode:X8}");
            return false;
        }
        catch (Exception ex)
        {
            _logService?.LogWarning($"AppInstaller upgrade failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Returns true if the winget output line is visual noise (progress bars, spinners, blank lines)
    /// that should not be written to the log file.
    /// </summary>
    private static bool IsWinGetOutputNoise(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return true;

        var trimmed = line.Trim();

        // Progress bar characters
        if (trimmed.Contains('█') || trimmed.Contains('▒'))
            return true;

        // Spinner characters (single char lines like " - ", " \ ", " | ", " / ")
        if (trimmed.Length <= 2 && (trimmed == "-" || trimmed == "\\" || trimmed == "|" || trimmed == "/"))
            return true;

        return false;
    }
}
