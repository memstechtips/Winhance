using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service interface for querying recommended settings across all domains.
    /// Provides a universal way to retrieve RecommendedValue settings for any domain.
    /// </summary>
    public interface IRecommendedSettingsService : IDomainService
    {
        /// <summary>
        /// Gets all recommended settings for the domain of the specified setting that are compatible with the current OS.
        /// </summary>
        /// <param name="settingId">A setting ID from the target domain (e.g., "start-menu-clean-11", "taskbar-alignment").</param>
        /// <returns>A collection of settings that have RecommendedValue defined and are OS-compatible.</returns>
        Task<IEnumerable<SettingDefinition>> GetRecommendedSettingsAsync(string settingId);
    }
}
