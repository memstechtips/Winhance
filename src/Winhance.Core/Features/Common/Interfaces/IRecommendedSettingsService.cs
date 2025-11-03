using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service interface for applying recommended settings across all domains.
    /// Provides a universal way to apply RecommendedValue settings for any domain.
    /// </summary>
    public interface IRecommendedSettingsService : IDomainService
    {
        /// <summary>
        /// Applies all recommended settings for the domain of the specified setting that are compatible with the current OS.
        /// </summary>
        /// <param name="settingId">A setting ID from the target domain (e.g., "start-menu-clean-11", "taskbar-alignment").</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ApplyRecommendedSettingsAsync(string settingId);

        /// <summary>
        /// Gets all recommended settings for the domain of the specified setting that are compatible with the current OS.
        /// </summary>
        /// <param name="settingId">A setting ID from the target domain (e.g., "start-menu-clean-11", "taskbar-alignment").</param>
        /// <returns>A collection of settings that have RecommendedValue defined and are OS-compatible.</returns>
        Task<IEnumerable<SettingDefinition>> GetRecommendedSettingsAsync(string settingId);
    }
}
