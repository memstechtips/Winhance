namespace Winhance.Core.Features.SoftwareApps.Interfaces;

// The two answers an app install can only ask for halfway through, once WinGet has failed. Deliberately not
// IDialogService: how the question is put (dialog, wording, "don't ask again") is the host's business.
public interface IInstallConsent
{
    Task<bool> AllowUpdatePolicyChangeAsync(string appName);

    Task<bool> AllowFallbackDownloadAsync(string appName);
}
