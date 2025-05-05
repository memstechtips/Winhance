using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Interface for services that handle verification of application installations.
/// </summary>
public interface IAppVerificationService
{
    /// <summary>
    /// Verifies if an app is installed.
    /// </summary>
    /// <param name="packageName">The package name to verify.</param>
    /// <returns>True if the app is installed; otherwise, false.</returns>
    Task<bool> VerifyAppInstallationAsync(string packageName);
}
