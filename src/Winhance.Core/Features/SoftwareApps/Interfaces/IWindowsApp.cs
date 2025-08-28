namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Interface for Windows built-in applications and components.
    /// </summary>
    public interface IWindowsApp : IInstallableApp
    {
        /// <summary>
        /// Gets the package identifier for the Windows app.
        /// </summary>
        string PackageId { get; }

        /// <summary>
        /// Gets a value indicating whether removing this app requires a restart.
        /// </summary>
        bool RequiresRestart { get; }

        /// <summary>
        /// Gets a value indicating whether the app can be reinstalled after removal.
        /// </summary>
        bool CanBeReinstalled { get; }
    }
}