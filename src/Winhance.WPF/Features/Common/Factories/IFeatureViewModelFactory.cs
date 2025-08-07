using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Factories
{
    /// <summary>
    /// Factory for creating feature ViewModels from feature descriptors.
    /// This factory handles the mapping between Core layer descriptors and WPF layer ViewModels.
    /// </summary>
    public interface IFeatureViewModelFactory
    {
        /// <summary>
        /// Creates a new feature ViewModel instance from a feature descriptor.
        /// Each call returns a fresh instance to avoid cross-contamination.
        /// </summary>
        /// <param name="descriptor">The feature descriptor containing metadata about the feature.</param>
        /// <returns>A new feature ViewModel instance, or null if the factory cannot create a ViewModel for this descriptor.</returns>
        Task<IFeatureViewModel> CreateAsync(IFeatureDescriptor descriptor);

        /// <summary>
        /// Determines if this factory can create a ViewModel for the given feature descriptor.
        /// </summary>
        /// <param name="descriptor">The feature descriptor to check.</param>
        /// <returns>True if the factory can create a ViewModel for this descriptor; otherwise, false.</returns>
        bool CanCreate(IFeatureDescriptor descriptor);

        /// <summary>
        /// Gets the supported categories for this factory.
        /// </summary>
        /// <returns>An array of category names that this factory supports.</returns>
        string[] GetSupportedCategories();
    }
}
