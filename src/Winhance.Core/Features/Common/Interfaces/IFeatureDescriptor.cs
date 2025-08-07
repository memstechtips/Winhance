using System;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Represents a feature descriptor that contains metadata about a feature module.
    /// This is a pure Core layer interface with no UI dependencies.
    /// </summary>
    public interface IFeatureDescriptor
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
        /// Gets the type of the domain service that handles this feature's business logic.
        /// </summary>
        Type DomainServiceType { get; }

        /// <summary>
        /// Gets an optional description of what this feature does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Determines if this feature module is available on the current system.
        /// </summary>
        /// <returns>True if the feature is available; otherwise, false.</returns>
        Task<bool> IsAvailableAsync();
    }
}
