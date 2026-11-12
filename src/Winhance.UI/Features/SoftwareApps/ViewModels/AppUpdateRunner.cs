using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

// Updates the given rows one by one under the shared progress bar. True when the user cancelled it.
// With a dialog service the user is asked once, before anything runs, to let Winhance close the apps that are open;
// without one (the tray's unattended run) an open app is left alone and its update lands when it next starts.
internal static class AppUpdateRunner
{
    internal static async Task<bool> RunAsync(
        IReadOnlyList<AppItemViewModel> apps,
        IInstalledAppsService installedAppsService,
        ITaskProgressService progressService,
        ILocalizationService localization,
        ILogService logService,
        IDialogService? dialogService = null)
    {
        if (dialogService is not null)
            await CloseOpenAppsAsync(apps, installedAppsService, dialogService, localization, logService);

        CancellationTokenSource? cancellation = null;
        try
        {
            cancellation = progressService.StartTask(localization.GetString("Progress_Task_UpdatingApps"), false);
            var progress = progressService.CreateDetailedProgress();

            for (int i = 0; i < apps.Count; i++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (progressService.ConsumeSkipNextRequest())
                    continue;

                var app = apps[i];
                progress.Report(new TaskProgressDetail
                {
                    StatusText = localization.GetString("Progress_Updating", app.Name),
                    QueueTotal = apps.Count,
                    QueueCurrent = i + 1,
                    QueueNextItemName = i + 1 < apps.Count ? apps[i + 1].Name : null,
                });

                var result = await installedAppsService.UpdateAsync(app.Installed!, cancellation.Token);
                if (!result.Success)
                    logService.LogWarning($"Update of {app.Name} failed: {result.ErrorMessage}");
            }
        }
        catch (OperationCanceledException)
        {
            progressService.CancelTask();
        }
        catch (Exception ex)
        {
            progressService.FailTask();
            logService.LogError("Error updating apps", ex);
        }
        finally
        {
            if (progressService.IsTaskRunning)
                progressService.CompleteTask();
        }

        return cancellation?.IsCancellationRequested == true;
    }

    private static async Task CloseOpenAppsAsync(
        IReadOnlyList<AppItemViewModel> apps,
        IInstalledAppsService installedAppsService,
        IDialogService dialogService,
        ILocalizationService localization,
        ILogService logService)
    {
        try
        {
            var open = await installedAppsService.FindOpenAppsAsync(apps.Select(a => a.Installed!).ToList());
            if (open.Length == 0)
                return;

            var names = string.Join(Environment.NewLine, open.Select(a => $"\u2022 {a.Name}"));
            var choice = await dialogService.ShowConfirmationAsync(new ConfirmationRequest
            {
                Title = localization.GetString("AppUpdate_CloseOpenApps_Title"),
                Message = localization.GetString("AppUpdate_CloseOpenApps_Message", names),
                ConfirmButtonText = localization.GetString("AppUpdate_CloseOpenApps_Confirm"),
                CancelButtonText = localization.GetString("AppUpdate_CloseOpenApps_Cancel"),
            });
            if (choice.Confirmed)
                await installedAppsService.CloseAsync(open);
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Could not check for open apps before updating: {ex.Message}");
        }
    }
}
