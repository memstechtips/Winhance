namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Base interface for installable applications (both external and Windows apps).
    /// Represents the SoftwareApps domain contract separate from settings.
    /// </summary>
    public interface IInstallableApp
    {
        /// <summary>
        /// Gets the unique identifier for the app.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the name of the app.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the app.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets a value indicating whether the app is currently installed.
        /// </summary>
        bool IsInstalled { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the app is selected for installation/removal.
        /// This is UI selection state.
        /// </summary>
        bool IsSelected { get; set; }

        /// <summary>
        /// Gets the category of the app.
        /// </summary>
        string Category { get; }
    }
}