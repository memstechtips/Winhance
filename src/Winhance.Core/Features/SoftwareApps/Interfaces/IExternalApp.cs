namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Interface for external applications that can be installed via package managers.
    /// </summary>
    public interface IExternalApp : IInstallableApp
    {
        /// <summary>
        /// Gets the package name used by the package manager.
        /// </summary>
        string PackageName { get; }

        /// <summary>
        /// Gets the version of the app.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets a value indicating whether the app can be reinstalled after removal.
        /// </summary>
        bool CanBeReinstalled { get; }
    }
}