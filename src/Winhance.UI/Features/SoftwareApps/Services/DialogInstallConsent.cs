using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.UI.Features.SoftwareApps.Services;

public sealed class DialogInstallConsent(
    IDialogService dialogService,
    ILocalizationService localizationService,
    IUserPreferencesService userPreferencesService) : IInstallConsent
{
    private const string FallbackConfirmationPreferenceKey = "StoreDownloadFallback_DontShowAgain";

    public async Task<bool> AllowUpdatePolicyChangeAsync(string appName)
    {
        var response = await dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Title = localizationService.GetString("Dialog_UpdatePolicyBlocking_Title"),
            Message = localizationService.GetString("Dialog_UpdatePolicyBlocking_Message", appName),
            ConfirmButtonText = localizationService.GetString("Button_Yes"),
            CancelButtonText = localizationService.GetString("Button_No"),
        });
        return response.Confirmed;
    }

    public async Task<bool> AllowFallbackDownloadAsync(string appName)
    {
        if (await userPreferencesService.GetPreferenceAsync(FallbackConfirmationPreferenceKey, false))
            return true;

        var response = await dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Title = localizationService.GetString("Dialog_FallbackDownload"),
            Message = localizationService.GetString("WindowsApps_Msg_FallbackDownload", appName),
            CheckboxText = localizationService.GetString("WindowsApps_Checkbox_DontAskAgain"),
            ConfirmButtonText = localizationService.GetString("Button_Download"),
            CancelButtonText = localizationService.GetString("Button_Cancel"),
        });

        if (response.CheckboxChecked)
            await userPreferencesService.SetPreferenceAsync(FallbackConfirmationPreferenceKey, true);

        return response.Confirmed;
    }
}
