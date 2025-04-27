using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for coordinating configuration operations across multiple view models.
    /// </summary>
    public interface IConfigurationCoordinatorService
    {
        /// <summary>
        /// Creates a unified configuration file containing settings from all view models.
        /// </summary>
        /// <returns>A task representing the asynchronous operation. Returns the unified configuration file.</returns>
        Task<UnifiedConfigurationFile> CreateUnifiedConfigurationAsync();

        /// <summary>
        /// Applies a unified configuration to the selected sections.
        /// </summary>
        /// <param name="config">The unified configuration file.</param>
        /// <param name="selectedSections">The sections to apply.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if successful, false otherwise.</returns>
        Task<bool> ApplyUnifiedConfigurationAsync(UnifiedConfigurationFile config, IEnumerable<string> selectedSections);
    }
}