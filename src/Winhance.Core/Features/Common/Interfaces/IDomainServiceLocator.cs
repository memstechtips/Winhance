using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service locator interface for finding the appropriate domain service for a given setting.
    /// Follows Clean Architecture by providing abstraction for cross-domain setting resolution.
    /// </summary>
    public interface IDomainServiceLocator
    {
        /// <summary>
        /// Finds the domain service that contains the specified setting.
        /// </summary>
        /// <param name="settingId">The ID of the setting to locate.</param>
        /// <returns>The domain service that handles the setting, or null if not found.</returns>
        Task<IDomainService?> FindServiceForSettingAsync(string settingId);

        /// <summary>
        /// Gets the setting model from the appropriate domain service.
        /// </summary>
        /// <param name="settingId">The ID of the setting to retrieve.</param>
        /// <returns>The application setting model, or null if not found.</returns>
        Task<ApplicationSetting?> GetSettingAsync(string settingId);
    }
}