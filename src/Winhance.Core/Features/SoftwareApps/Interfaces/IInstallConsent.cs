namespace Winhance.Core.Features.SoftwareApps.Interfaces;

// The answer an app install can only ask for halfway through, once WinGet has failed. Deliberately not
// IDialogService: how the question is put (dialog, wording) is the host's business.
public interface IInstallConsent
{
    Task<bool> AllowUpdatePolicyChangeAsync(string appName);
}
