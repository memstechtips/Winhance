using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.UI.Features.SoftwareApps.Services;

public sealed class DialogInstallConsent(
    IDialogService dialogService,
    ILocalizationService localizationService) : IInstallConsent
{
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
}
