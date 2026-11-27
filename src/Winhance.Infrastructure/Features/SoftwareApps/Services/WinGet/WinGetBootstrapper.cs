using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;

internal class WinGetBootstrapper : IWinGetBootstrapper
{
    private readonly WinGetComSession _comSession;
    private readonly ILogService _logService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IFileSystemService _fileSystemService;

    private bool _systemWinGetAvailable;

    private const int ComInitTimeoutSeconds = 5;

    public bool IsSystemWinGetAvailable => _systemWinGetAvailable;

    public WinGetBootstrapper(
        WinGetComSession comSession,
        ILogService logService,
        IInteractiveUserService interactiveUserService,
        IFileSystemService fileSystemService)
    {
        _comSession = comSession;
        _logService = logService;
        _interactiveUserService = interactiveUserService;
        _fileSystemService = fileSystemService;
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

}
