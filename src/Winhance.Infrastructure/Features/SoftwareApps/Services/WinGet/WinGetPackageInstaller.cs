using System.Diagnostics;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;

internal class WinGetPackageInstaller : IWinGetPackageInstaller
{
    private readonly WinGetComSession _comSession;
    private readonly ITaskProgressService _taskProgressService;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localization;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IFileSystemService _fileSystemService;

    public WinGetPackageInstaller(
        WinGetComSession comSession,
        ITaskProgressService taskProgressService,
        ILogService logService,
        ILocalizationService localization,
        IInteractiveUserService interactiveUserService,
        IFileSystemService fileSystemService)
    {
        _comSession = comSession;
        _taskProgressService = taskProgressService;
        _logService = logService;
        _localization = localization;
        _interactiveUserService = interactiveUserService;
        _fileSystemService = fileSystemService;
    }

    public async Task<bool> IsWinGetInstalledAsync(CancellationToken cancellationToken = default)
    {
        // Bundled CLI is always available if the app is correctly installed
        var exePath = WinGetCliRunner.GetWinGetExePath(_interactiveUserService);
        if (exePath != null && _fileSystemService.FileExists(exePath))
            return true;

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            return await Task.Run(() => _comSession.EnsureComInitialized(), linkedCts.Token).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public Task<PackageInstallResult> InstallPackageAsync(string packageId, string? source, string? displayName, string? installerOverride, InstallOptions options, CancellationToken cancellationToken)
        => InstallCoreAsync(packageId, InstallArguments(packageId, source, installerOverride, options), displayName, WinGetOperation.Install, cancellationToken);

    public async Task<PackageInstallResult> UpgradePackageAsync(string packageId, string? source, string? displayName, CancellationToken cancellationToken)
    {
        if (IsAppInstaller(packageId))
            await ReleaseWinGetServerAsync(cancellationToken).ConfigureAwait(false);
        return await InstallCoreAsync(packageId, UpgradeArguments(packageId, source), displayName, WinGetOperation.Upgrade, cancellationToken).ConfigureAwait(false);
    }

    internal static bool IsAppInstaller(string packageId)
        => packageId.Equals("Microsoft.AppInstaller", StringComparison.OrdinalIgnoreCase)
            || packageId.Equals("9NBLGGH4NNS1", StringComparison.OrdinalIgnoreCase);

    // The next winget lookup reconnects on its own once the update has registered.
    private async Task ReleaseWinGetServerAsync(CancellationToken cancellationToken)
    {
        _comSession.Release();
        for (int i = 0; i < 20 && IsWinGetServerRunning(); i++)
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        _logService.LogInformation(IsWinGetServerRunning()
            ? "WinGet COM server still running; App Installer may only stage its update"
            : "Released the WinGet COM server before updating App Installer");
    }

    private static bool IsWinGetServerRunning()
    {
        var processes = Process.GetProcessesByName("WindowsPackageManagerServer");
        foreach (var process in processes)
            process.Dispose();
        return processes.Length > 0;
    }

    // --exact so an id like Git.Git never upgrades Git.GitLFS instead.
    internal static string UpgradeArguments(string packageId, string? source)
    {
        var arguments = $"upgrade --id {packageId} --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity";
        return string.IsNullOrEmpty(source) ? arguments : arguments + $" --source {source}";
    }

    // --interactive shows the installer's own screens; --disable-interactivity only stops winget itself prompting.
    internal static string InstallArguments(string packageId, string? source, string? installerOverride, InstallOptions options)
    {
        var mode = options.Interactive ? "--interactive" : "--silent";
        var arguments = $"install --id {packageId} --exact {mode} --accept-package-agreements --accept-source-agreements --force --disable-interactivity";
        if (!string.IsNullOrEmpty(source))
            arguments += $" --source {source}";
        if (!string.IsNullOrWhiteSpace(installerOverride))
            arguments += $" --override \"{installerOverride}\"";
        if (options.Scope != InstallScope.Default)
            arguments += options.Scope == InstallScope.Machine ? " --scope machine" : " --scope user";
        arguments += options.Architecture switch
        {
            InstallArchitecture.X64 => " --architecture x64",
            InstallArchitecture.X86 => " --architecture x86",
            InstallArchitecture.Arm64 => " --architecture arm64",
            _ => "",
        };
        return arguments;
    }

    // --skip-dependencies: the staged installer runs on its own on the new PC, so dependency installers would never
    // run. An app that needs them fails its staged install and the online route installs it with them.
    internal static string DownloadArguments(string packageId, string architecture, bool forAllUsers, string destinationFolder)
    {
        var arguments = $"download --id {packageId} --exact --source winget --architecture {architecture} --download-directory \"{destinationFolder}\" --skip-dependencies --accept-package-agreements --accept-source-agreements --disable-interactivity";
        return forAllUsers ? arguments + " --scope machine" : arguments;
    }

    public async Task<bool> DownloadPackageAsync(string packageId, string architecture, bool forAllUsers, string destinationFolder, string? displayName, CancellationToken cancellationToken)
    {
        var arguments = DownloadArguments(packageId, architecture, forAllUsers, destinationFolder);
        var exePath = WinGetCliRunner.GetBundledWinGetExePath();
        var output = new WinGetOutputStream(
            WinGetOperation.Download, displayName ?? packageId, _taskProgressService, _localization, _logService);
        output.WriteHeader(exePath ?? "winget", arguments);

        try
        {
            var result = await WinGetCliRunner.RunAsync(
                arguments,
                onOutputLine: output.HandleOutputLine,
                onErrorLine: output.HandleErrorLine,
                cancellationToken: cancellationToken,
                timeoutMs: WinGetCliRunner.InstallTimeoutMs,
                exePathOverride: exePath,
                // null keeps winget in this elevated process; under OTS it would run as the signed-in standard user.
                interactiveUserService: null,
                onProgressLine: output.ForwardProgressLine).ConfigureAwait(false);

            output.WriteFooter(result, WinGetCliRunner.InstallTimeoutMs, idleTimeoutMs: 0);

            cancellationToken.ThrowIfCancellationRequested();

            if (result.ExitCode == 0)
                return true;

            var text = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            var tail = text.Length > 500 ? text[^500..] : text;
            _logService?.LogWarning($"winget download of {packageId} ({architecture}) failed with exit code 0x{result.ExitCode:X8}: {tail.Trim()}");
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logService?.LogWarning($"winget download of {packageId} ({architecture}) failed: {ex.Message}");
            return false;
        }
    }

    private async Task<PackageInstallResult> InstallCoreAsync(string packageId, string arguments, string? displayName, WinGetOperation operation, CancellationToken cancellationToken)
    {
        var isUpgrade = operation == WinGetOperation.Upgrade;
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentException("Package ID cannot be null or empty", nameof(packageId));

        displayName ??= packageId;

        _taskProgressService?.UpdateProgress(10, _localization.GetString(isUpgrade ? "Progress_Updating" : "Progress_WinGet_CheckingPrerequisites", displayName));

        if (!await IsWinGetInstalledAsync(cancellationToken).ConfigureAwait(false))
        {
            _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_FailedInstallManager", displayName));
            return PackageInstallResult.Failed(InstallFailureReason.WinGetNotAvailable, "WinGet CLI not found");
        }

        try
        {
            _taskProgressService?.UpdateProgress(20, _localization.GetString(isUpgrade ? "Progress_Updating" : "Progress_WinGet_StartingInstallation", displayName));

            var wingetExe = WinGetCliRunner.GetWinGetExePath(_interactiveUserService) ?? "winget";
            var output = new WinGetOutputStream(
                operation, displayName, _taskProgressService, _localization, _logService);
            output.WriteHeader(wingetExe, arguments);

            var result = await WinGetCliRunner.RunAsync(
                arguments,
                onOutputLine: output.HandleOutputLine,
                onErrorLine: output.HandleErrorLine,
                cancellationToken: cancellationToken,
                timeoutMs: WinGetCliRunner.InstallTimeoutMs,
                idleTimeoutMs: WinGetCliRunner.InstallIdleTimeoutMs,
                interactiveUserService: _interactiveUserService,
                onProgressLine: output.ForwardProgressLine).ConfigureAwait(false);

            output.WriteFooter(result, WinGetCliRunner.InstallTimeoutMs, WinGetCliRunner.InstallIdleTimeoutMs);

            // If the user cancelled while the process was running, throw before
            // checking the exit code so the OperationCanceledException handler fires
            // and the Chocolatey fallback prompt is never reached.
            cancellationToken.ThrowIfCancellationRequested();

            if (WinGetExitCodes.IsSuccess(result.ExitCode))
            {
                _taskProgressService?.UpdateProgress(100, _localization.GetString(isUpgrade ? "Progress_UpdatedSuccess" : "Progress_WinGet_InstalledSuccess", displayName));
                return PackageInstallResult.Succeeded();
            }

            var failureReason = WinGetExitCodes.MapExitCode(result.ExitCode);
            var errorMessage = GetInstallErrorMessageCli(packageId, failureReason, result.ExitCode);
            _logService?.LogError($"Installation failed for {packageId}: {errorMessage} (exit code: {result.ExitCode})");
            _taskProgressService?.UpdateProgress(0, errorMessage);
            return PackageInstallResult.Failed(failureReason, errorMessage);
        }
        catch (OperationCanceledException)
        {
            _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_InstallationCancelled", displayName));
            throw;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Error installing {packageId}: {ex.Message}");

            var errorMessage = _localization.GetString("Progress_WinGet_InstallationError", displayName, ex.Message);
            _taskProgressService?.UpdateProgress(0, errorMessage);
            return PackageInstallResult.Failed(InstallFailureReason.Other, errorMessage);
        }
    }

    public async Task<bool> UninstallPackageAsync(string packageId, string? source = null, string? displayName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentException("Package ID cannot be null or empty", nameof(packageId));

        displayName ??= packageId;

        _taskProgressService?.UpdateProgress(10, _localization.GetString("Progress_WinGet_CheckingPrerequisitesUninstall", displayName));

        if (!await IsWinGetInstalledAsync(cancellationToken).ConfigureAwait(false))
        {
            _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_NotInstalled"));
            return false;
        }

        try
        {
            _taskProgressService?.UpdateProgress(20, _localization.GetString("Progress_WinGet_StartingUninstallation", displayName));

            var arguments = $"uninstall --id {packageId} --silent --accept-source-agreements --force --disable-interactivity";
            if (!string.IsNullOrEmpty(source))
                arguments += $" --source {source}";

            var wingetExe = WinGetCliRunner.GetWinGetExePath(_interactiveUserService) ?? "winget";
            var output = new WinGetOutputStream(
                WinGetOperation.Uninstall, displayName, _taskProgressService, _localization, _logService);

            async Task<WinGetCliRunner.WinGetCliResult> RunUninstallAsync(bool asShellUser)
            {
                output.WriteHeader(wingetExe, arguments);
                var run = await WinGetCliRunner.RunAsync(
                    arguments,
                    onOutputLine: output.HandleOutputLine,
                    onErrorLine: output.HandleErrorLine,
                    cancellationToken: cancellationToken,
                    timeoutMs: WinGetCliRunner.InstallTimeoutMs,
                    idleTimeoutMs: WinGetCliRunner.InstallIdleTimeoutMs,
                    interactiveUserService: _interactiveUserService,
                    onProgressLine: output.ForwardProgressLine,
                    asShellUser: asShellUser).ConfigureAwait(false);
                output.WriteFooter(run, WinGetCliRunner.InstallTimeoutMs, WinGetCliRunner.InstallIdleTimeoutMs);
                return run;
            }

            var result = await RunUninstallAsync(asShellUser: false).ConfigureAwait(false);
            if (result.ExitCode == WinGetExitCodes.NoAdminUninstallForUserScope)
            {
                _logService?.LogInformation($"{packageId} is installed for the signed-in user only - retrying the uninstall unelevated");
                result = await RunUninstallAsync(asShellUser: true).ConfigureAwait(false);
            }

            if (WinGetExitCodes.IsSuccess(result.ExitCode))
            {
                // Verify the package is actually gone — some uninstallers are interactive
                // and WinGet reports success as soon as it launches them.
                _taskProgressService?.UpdateProgress(95, _localization.GetString("Progress_WinGet_VerifyingUninstall", displayName));

                bool stillInstalled = await IsPackageStillInstalledAsync(packageId, source, cancellationToken).ConfigureAwait(false);

                if (stillInstalled)
                {
                    _logService?.LogInformation($"{packageId} still detected after uninstall — waiting for interactive uninstaller");
                    _taskProgressService?.UpdateProgress(95, _localization.GetString("Progress_WinGet_WaitingForUninstaller", displayName));

                    const int pollIntervalMs = 3000;
                    const int maxWaitMs = 60_000;
                    int elapsed = 0;

                    while (elapsed < maxWaitMs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
                        elapsed += pollIntervalMs;

                        if (!await IsPackageStillInstalledAsync(packageId, source, cancellationToken).ConfigureAwait(false))
                        {
                            stillInstalled = false;
                            _logService?.LogInformation($"{packageId} confirmed uninstalled after {elapsed / 1000}s");
                            break;
                        }
                    }
                }

                if (!stillInstalled)
                {
                    _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_UninstalledSuccess", displayName));
                    return true;
                }

                _logService?.LogWarning($"{packageId} still detected after 60s wait — uninstaller may require user interaction");
                _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_UninstalledSuccess", displayName));
                return true; // WinGet did report success; don't block the UI
            }

            // WinGet wraps any non-zero exit code from the underlying uninstaller
            // into EXEC_UNINSTALL_COMMAND_FAILED (0x8A150030), even when the uninstall
            // actually succeeded (e.g. Chromium-based apps always return exit code 19).
            // Before declaring failure, verify whether the package is actually still installed.
            if (WinGetExitCodes.IsUninstallVerifiable(result.ExitCode))
            {
                _logService?.LogInformation($"{packageId} returned 0x{result.ExitCode:X8} — verifying whether package was actually removed");
                _taskProgressService?.UpdateProgress(95, _localization.GetString("Progress_WinGet_VerifyingUninstall", displayName));

                bool stillInstalled = await IsPackageStillInstalledAsync(packageId, source, cancellationToken).ConfigureAwait(false);

                if (stillInstalled)
                {
                    _logService?.LogInformation($"{packageId} still detected after failed uninstall — waiting for interactive uninstaller");
                    _taskProgressService?.UpdateProgress(95, _localization.GetString("Progress_WinGet_WaitingForUninstaller", displayName));

                    const int pollIntervalMs = 3000;
                    const int maxWaitMs = 60_000;
                    int elapsed = 0;

                    while (elapsed < maxWaitMs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
                        elapsed += pollIntervalMs;

                        if (!await IsPackageStillInstalledAsync(packageId, source, cancellationToken).ConfigureAwait(false))
                        {
                            stillInstalled = false;
                            _logService?.LogInformation($"{packageId} confirmed uninstalled after {elapsed / 1000}s (despite exit code 0x{result.ExitCode:X8})");
                            break;
                        }
                    }
                }

                if (!stillInstalled)
                {
                    _logService?.LogInformation($"{packageId} verified as uninstalled despite WinGet exit code 0x{result.ExitCode:X8}");
                    _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_UninstalledSuccess", displayName));
                    return true;
                }

                _logService?.LogWarning($"{packageId} is still installed after verification — uninstall truly failed");
            }

            var failureReason = WinGetExitCodes.MapExitCode(result.ExitCode);
            var errorMessage = GetUninstallErrorMessageCli(packageId, failureReason, result.ExitCode);
            _logService?.LogError($"Uninstallation failed for {packageId}: {errorMessage} (exit code: {result.ExitCode})");
            _taskProgressService?.UpdateProgress(0, errorMessage);
            return false;
        }
        catch (OperationCanceledException)
        {
            _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_UninstallationCancelled", displayName));
            throw;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Error uninstalling {packageId}: {ex.Message}");
            _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_UninstallationError", displayName, ex.Message));
            return false;
        }
    }

    private async Task<bool> IsPackageStillInstalledAsync(string packageId, string? source, CancellationToken cancellationToken)
    {
        try
        {
            var arguments = $"list --id {packageId} --accept-source-agreements --disable-interactivity";
            if (!string.IsNullOrEmpty(source))
                arguments += $" --source {source}";

            var result = await WinGetCliRunner.RunAsync(
                arguments,
                cancellationToken: cancellationToken,
                timeoutMs: 10_000,
                interactiveUserService: _interactiveUserService).ConfigureAwait(false);

            // Exit code 0 = found (still installed), non-zero = not found
            return result.ExitCode == 0;
        }
        catch
        {
            // If the check fails, assume it's gone to avoid blocking
            return false;
        }
    }

    private string GetInstallErrorMessageCli(string packageId, InstallFailureReason reason, int exitCode)
    {
        return reason switch
        {
            InstallFailureReason.PackageNotFound => _localization.GetString("Progress_WinGet_PackageNotFound", packageId),
            InstallFailureReason.BlockedByPolicy => _localization.GetString("Progress_WinGet_Error_BlockedByPolicy", packageId),
            InstallFailureReason.DownloadError => _localization.GetString("Progress_WinGet_Error_DownloadError", packageId),
            InstallFailureReason.HashMismatchOrInstallError => _localization.GetString("Progress_WinGet_Error_InstallError", packageId, exitCode),
            InstallFailureReason.NoApplicableInstallers => _localization.GetString("Progress_WinGet_Error_NoApplicableInstallers", packageId),
            InstallFailureReason.AgreementsNotAccepted => _localization.GetString("Progress_WinGet_Error_AgreementsNotAccepted", packageId),
            InstallFailureReason.NetworkError => _localization.GetString("Progress_WinGet_NetworkError", packageId),
            _ => _localization.GetString("Progress_WinGet_Error_InstallFailed", packageId, exitCode)
        };
    }

    private string GetUninstallErrorMessageCli(string packageId, InstallFailureReason reason, int exitCode)
    {
        return reason switch
        {
            InstallFailureReason.PackageNotFound => _localization.GetString("Progress_WinGet_PackageNotInstalled", packageId),
            InstallFailureReason.BlockedByPolicy => _localization.GetString("Progress_WinGet_Error_UninstallBlockedByPolicy", packageId),
            _ => _localization.GetString("Progress_WinGet_Error_UninstallFailed", packageId, exitCode)
        };
    }
}
