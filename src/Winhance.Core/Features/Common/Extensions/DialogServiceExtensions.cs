using System.Runtime.CompilerServices;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Extensions;

// A command that throws lands in App.UnhandledException, which only logs: the user clicks and nothing happens. This is
// the one log-and-tell policy for a whole operation; a catch that turns one specific failure into its own message
// stays a catch. Not ConfigureAwait(false): the dialog has to open on the caller's UI thread.
public static class DialogServiceExtensions
{
    // False when the action failed or was cancelled, so a caller can stop before its follow-up steps.
    public static async Task<bool> RunOrShowErrorAsync(this IDialogService dialogService, ILogService logService,
        Func<Task> action, Func<Exception, string> errorMessage,
        [CallerMemberName] string callerName = "", [CallerFilePath] string callerFilePath = "")
    {
        try
        {
            await action();
            return true;
        }
        catch (OperationCanceledException)
        {
            // Cancelled by the user or by the view model's teardown, neither of which is an error to report.
            return false;
        }
        catch (Exception ex)
        {
            logService.LogError($"{callerName} failed: {ex.Message}", ex, callerFilePath);
            await dialogService.ShowErrorAsync(errorMessage(ex));
            return false;
        }
    }

    public static Task<bool> RunOrShowErrorAsync(this IDialogService dialogService, ILogService logService,
        Action action, Func<Exception, string> errorMessage,
        [CallerMemberName] string callerName = "", [CallerFilePath] string callerFilePath = "") =>
        dialogService.RunOrShowErrorAsync(logService, () =>
        {
            action();
            return Task.CompletedTask;
        }, errorMessage, callerName, callerFilePath);
}
