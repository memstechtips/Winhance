using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Extensions;

public static class RestorePointDialogExtensions
{
    // When this run turned System Protection on, the failure dialog itself asks whether to keep it. Closing the
    // dialog keeps it on, so only an explicit answer changes the machine again.
    public static async Task ShowRestorePointFailureAsync(this IDialogService dialogService,
        ILocalizationService localization, ISystemBackupService backupService, BackupResult result,
        string message, string title = "")
    {
        if (!string.IsNullOrEmpty(result.ErrorMessage))
            message += $"\n\n{result.ErrorMessage}";

        if (!result.TurnedProtectionOn)
        {
            await dialogService.ShowWarningAsync(message, title);
            return;
        }

        var answer = await dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Title = string.IsNullOrEmpty(title) ? localization.GetString("Dialog_Warning") : title,
            Message = message + "\n\n" + localization.GetString("RestorePoint_Fail_ProtectionTurnedOn"),
            ConfirmButtonText = localization.GetString("RestorePoint_Fail_Button_TurnOff"),
            CancelButtonText = localization.GetString("RestorePoint_Fail_Button_KeepOn"),
        });

        if (answer.Confirmed)
            await backupService.TurnOffSystemRestoreAsync();
    }
}
