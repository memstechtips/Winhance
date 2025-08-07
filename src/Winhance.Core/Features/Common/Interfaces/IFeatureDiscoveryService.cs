using System.Collections.Generic;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for discovering and managing feature descriptors.
    /// This is a pure Core layer service that handles feature metadata discovery.
    /// </summary>
    public interface IFeatureDiscoveryService
    {
        /// <summary>
        /// Discovers all available features for a specific category.
        /// </summary>
        /// <param name="category">The category to filter by (e.g., "Optimization", "Customization").</param>
        /// <returns>A collection of feature descriptors for the specified category.</returns>
        Task<IEnumerable<IFeatureDescriptor>> DiscoverFeaturesAsync(string category);

        /// <summary>
        /// Gets a specific feature descriptor by its identifier.
        /// </summary>
        /// <param name="moduleId">The unique identifier of the feature.</param>
        /// <returns>The feature descriptor if found; otherwise, null.</returns>
        Task<IFeatureDescriptor> GetFeatureAsync(string moduleId);

        /// <summary>
        /// Registers a feature descriptor with the discovery service.
        /// </summary>
        /// <param name="descriptor">The feature descriptor to register.</param>
        void RegisterFeature(IFeatureDescriptor descriptor);

        /// <summary>
        /// Gets all registered feature descriptors.
        /// </summary>
        /// <returns>A collection of all registered feature descriptors.</returns>
        Task<IEnumerable<IFeatureDescriptor>> GetAllFeaturesAsync();

        /// <summary>
        /// Gets all available categories.
        /// </summary>
        /// <returns>A collection of category names.</returns>
        Task<IEnumerable<string>> GetCategoriesAsync();

        /// <summary>
        /// Checks if a feature with the specified module ID is registered.
        /// </summary>
        /// <param name="moduleId">The module ID to check.</param>
        /// <returns>True if the feature is registered; otherwise, false.</returns>
        bool IsFeatureRegistered(string moduleId);
    }
}
