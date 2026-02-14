using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.UI.Features.SoftwareApps.Services;

public class ChocolateyConsentService : IChocolateyConsentService
{
    private const string PreferenceKey = "ChocolateyFallbackConsented";

    private readonly IDialogService _dialogService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ILocalizationService _localization;
    private readonly ILogService _logService;

    public ChocolateyConsentService(
        IDialogService dialogService,
        IUserPreferencesService userPreferencesService,
        ILocalizationService localization,
        ILogService logService)
    {
        _dialogService = dialogService;
        _userPreferencesService = userPreferencesService;
        _localization = localization;
        _logService = logService;
    }

    public async Task<bool> RequestConsentAsync()
    {
        try
        {
            var alreadyConsented = await _userPreferencesService.GetPreferenceAsync(PreferenceKey, false);
            if (alreadyConsented)
                return true;

            var (confirmed, dontAskAgain) = await _dialogService.ShowConfirmationWithCheckboxAsync(
                message: _localization.GetString("Dialog_Choco_ConsentMessage"),
                checkboxText: _localization.GetString("Dialog_Choco_DontAskAgain"),
                title: _localization.GetString("Dialog_Choco_ConsentTitle"),
                continueButtonText: _localization.GetString("Button_Yes"),
                cancelButtonText: _localization.GetString("Button_No"));

            if (confirmed && dontAskAgain)
            {
                await _userPreferencesService.SetPreferenceAsync(PreferenceKey, true);
                _logService.LogInformation("User consented to Chocolatey fallback (remembered)");
            }
            else if (confirmed)
            {
                _logService.LogInformation("User consented to Chocolatey fallback (one-time)");
            }
            else
            {
                _logService.LogInformation("User declined Chocolatey fallback");
            }

            return confirmed;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error requesting Chocolatey consent: {ex.Message}");
            return false;
        }
    }
}
