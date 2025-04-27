using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for managing unified configuration operations across the application.
    /// </summary>
    public interface IUnifiedConfigurationService
    {
        /// <summary>
        /// Creates a unified configuration file containing settings from all view models.
        /// </summary>
        /// <returns>A task representing the asynchronous operation. Returns the unified configuration file.</returns>
        Task<UnifiedConfigurationFile> CreateUnifiedConfigurationAsync();

        /// <summary>
        /// Saves a unified configuration file.
        /// </summary>
        /// <param name="config">The unified configuration to save.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if successful, false otherwise.</returns>
        Task<bool> SaveUnifiedConfigurationAsync(UnifiedConfigurationFile config);

        /// <summary>
        /// Loads a unified configuration file.
        /// </summary>
        /// <returns>A task representing the asynchronous operation. Returns the unified configuration file if successful, null otherwise.</returns>
        Task<UnifiedConfigurationFile> LoadUnifiedConfigurationAsync();

        /// <summary>
        /// Shows the unified configuration dialog to let the user select which sections to include.
        /// </summary>
        /// <param name="config">The unified configuration file.</param>
        /// <param name="isSaveDialog">Whether this is a save dialog (true) or an import dialog (false).</param>
        /// <returns>A dictionary of section names and their selection state.</returns>
        Task<Dictionary<string, bool>> ShowUnifiedConfigurationDialogAsync(UnifiedConfigurationFile config, bool isSaveDialog);

        /// <summary>
        /// Applies a unified configuration to the selected sections.
        /// </summary>
        /// <param name="config">The unified configuration file.</param>
        /// <param name="selectedSections">The sections to apply.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if successful, false otherwise.</returns>
        Task<bool> ApplyUnifiedConfigurationAsync(UnifiedConfigurationFile config, IEnumerable<string> selectedSections);
    }
}