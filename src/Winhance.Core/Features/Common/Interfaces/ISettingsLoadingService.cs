using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for loading and setting up feature settings across ViewModels.
    /// Centralizes the common loading logic to eliminate duplication.
    /// Returns fully configured SettingItemViewModels ready for use.
    /// </summary>
    public interface ISettingsLoadingService
    {
        /// <summary>
        /// Loads and configures settings for a feature using the provided domain service.
        /// Handles progress tracking, exception handling, event publishing, and SettingItemViewModel creation.
        /// Returns a collection of fully configured SettingItemViewModels ready for binding.
        /// </summary>
        /// <typeparam name="TDomainService">The type of domain service that provides settings.</typeparam>
        /// <param name="domainService">The domain service instance to get settings from.</param>
        /// <param name="featureModuleId">The module ID for the feature being loaded.</param>
        /// <param name="progressMessage">The progress message to display during loading.</param>
        /// <returns>A collection of fully configured SettingItemViewModels ready for use.</returns>
        Task<ObservableCollection<object>> LoadConfiguredSettingsAsync<TDomainService>(
            TDomainService domainService,
            string featureModuleId,
            string progressMessage)
            where TDomainService : class;
    }
}
