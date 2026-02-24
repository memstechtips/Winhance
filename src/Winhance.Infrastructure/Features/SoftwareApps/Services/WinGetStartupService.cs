using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Handles WinGet/AppInstaller readiness on application startup.
/// Extracted from MainWindowViewModel for testability.
/// </summary>
public class WinGetStartupService : IWinGetStartupService
{
    private readonly IWinGetBootstrapper _winGetBootstrapper;
    private readonly IInternetConnectivityService _internetConnectivityService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;

    public WinGetStartupService(
        IWinGetBootstrapper winGetBootstrapper,
        IInternetConnectivityService internetConnectivityService,
        ITaskProgressService taskProgressService,
        ILocalizationService localizationService,
        ILogService logService)
    {
        _winGetBootstrapper = winGetBootstrapper;
        _internetConnectivityService = internetConnectivityService;
        _taskProgressService = taskProgressService;
        _localizationService = localizationService;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task EnsureWinGetReadyOnStartupAsync()
    {
        try
        {
            if (_winGetBootstrapper.IsSystemWinGetAvailable)
            {
                // System winget already present -- silently attempt upgrade
                _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                    "Startup: System winget available, attempting silent AppInstaller upgrade...");

                bool upgraded = await _winGetBootstrapper.UpgradeAppInstallerAsync();
                if (upgraded)
                {
                    _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                        "Startup: AppInstaller upgraded successfully");

                    // Re-init COM in case the upgrade changed the COM server
                    await _winGetBootstrapper.EnsureWinGetReadyAsync();
                }
                else
                {
                    _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                        "Startup: AppInstaller upgrade not needed or not applicable");
                }
            }
            else
            {
                // Only bundled winget -- need to install AppInstaller
                // Check internet FIRST -- all install paths require connectivity
                var hasInternet = await _internetConnectivityService.IsInternetConnectedAsync(forceCheck: true);
                if (!hasInternet)
                {
                    _logService.Log(Core.Features.Common.Enums.LogLevel.Warning,
                        "Startup: No internet connection -- skipping AppInstaller installation");
                    return;
                }

                _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                    "Startup: No system winget, attempting to install AppInstaller...");

                _taskProgressService.StartTask(
                    _localizationService.GetString("Progress_WinGet_Installing") ?? "Installing WinGet...",
                    isIndeterminate: false);

                try
                {
                    bool installed = await _winGetBootstrapper.InstallWinGetAsync();

                    if (installed)
                    {
                        _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                            "Startup: AppInstaller installed successfully");
                        _taskProgressService.CompleteTask();
                    }
                    else
                    {
                        _logService.Log(Core.Features.Common.Enums.LogLevel.Warning,
                            "Startup: AppInstaller installation failed -- continuing with bundled CLI");
                        _taskProgressService.UpdateProgress(0,
                            _localizationService.GetString("Error_WinGetInstallFailed")
                            ?? "Failed to install WinGet. Please check your internet connection.");
                        await Task.Delay(5000).ConfigureAwait(false);
                        _taskProgressService.CompleteTask();
                    }
                }
                catch
                {
                    _taskProgressService.CompleteTask();
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error,
                $"Startup: Error in WinGet readiness flow: {ex.Message}");
        }
    }
}
