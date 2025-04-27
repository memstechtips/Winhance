using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Services.Configuration
{
    /// <summary>
    /// Interface for the service that applies configuration settings to different view models.
    /// </summary>
    public interface IConfigurationApplierService
    {
        /// <summary>
        /// Applies configuration settings to the selected sections.
        /// </summary>
        /// <param name="config">The unified configuration file.</param>
        /// <param name="selectedSections">The sections to apply.</param>
        /// <returns>A dictionary of section names and their application result.</returns>
        Task<Dictionary<string, bool>> ApplySectionsAsync(UnifiedConfigurationFile config, IEnumerable<string> selectedSections);
    }
}