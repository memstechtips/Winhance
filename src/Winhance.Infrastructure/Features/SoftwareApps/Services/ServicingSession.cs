using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal class ServicingSession(
    ILogService logService,
    IProcessExecutor processExecutor) : IServicingSession
{
    public async Task<bool> RunAsync(
        IReadOnlyList<string> statements,
        string label,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (statements is null || statements.Count == 0)
            return false;

        try
        {
            logService.LogInformation($"Starting servicing session: {label}");

            progress?.Report(new TaskProgressDetail
            {
                StatusText = $"Enabling {label}...",
                IsIndeterminate = true
            });

            // CBS is transactional and does not support concurrent servicing commands, so everything
            // selected runs as statements of one session rather than a window per kind. The window is
            // deliberately not awaited: the enable outlives the app, and Winhance stays usable.
            var script = string.Join("; ", statements);

            var started = await processExecutor.ShellExecuteAsync(
                "powershell.exe",
                $"-NoProfile -Command \"& {{ {script}; pause }}\"",
                waitForExit: false,
                cancellationToken).ConfigureAwait(false);

            if (started is null)
            {
                logService.LogError($"PowerShell did not start for: {label}");
                return false;
            }

            logService.LogInformation($"PowerShell launched for: {label}");

            progress?.Report(new TaskProgressDetail
            {
                StatusText = $"PowerShell launched for {label}",
                IsIndeterminate = false
            });

            return true;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error enabling {label}: {ex.Message}");
            progress?.Report(new TaskProgressDetail
            {
                StatusText = $"Failed to enable {label}: {ex.Message}",
                IsIndeterminate = false,
                LogLevel = Core.Features.Common.Enums.LogLevel.Error
            });
            return false;
        }
    }
}
