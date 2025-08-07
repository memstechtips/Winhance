using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Represents a feature module that can be dynamically loaded and managed.
    /// Each feature module is responsible for one specific area of functionality.
    /// </summary>
    public interface IFeatureModule
    {
        /// <summary>
        /// Gets the unique identifier for this feature module.
        /// </summary>
        string ModuleId { get; }

        /// <summary>
        /// Gets the display name for this feature module.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the category this feature belongs to (e.g., "Optimization", "Customization").
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Gets the sort order for displaying this feature module.
        /// </summary>
        int SortOrder { get; }

        /// <summary>
        /// Determines if this feature module is available on the current system.
        /// </summary>
        /// <returns>True if the feature is available; otherwise, false.</returns>
        Task<bool> IsAvailableAsync();
    }
}
