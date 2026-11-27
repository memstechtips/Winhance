using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IInstallOptionsService
{
    InstallOptions Defaults { get; }

    // Null when the app follows the defaults.
    InstallOptions? ForApp(string appId);

    InstallOptions EffectiveFor(string appId);

    Task SetDefaultsAsync(InstallOptions options);

    // Null removes the app's own options, so it follows the defaults again.
    Task SetForAppAsync(string appId, InstallOptions? options);

    // This PC's saved options, or the session's while one is open.
    InstallOptionsStore Current { get; }

    // A config being built, reviewed or applied brings its own options. Until EndSession they replace this PC's saved
    // ones, and changes made meanwhile go into the session's copy, never into the saved options. Sessions do not nest.
    void BeginSession(InstallOptionsStore options);

    void EndSession();
}
