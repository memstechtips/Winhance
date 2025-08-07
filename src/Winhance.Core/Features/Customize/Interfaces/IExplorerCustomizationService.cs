using System.Threading.Tasks;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Customize.Interfaces
{
    /// <summary>
    /// Service interface for managing Windows Explorer customization settings.
    /// Handles file explorer appearance, layout, visual preferences, and user interface customizations.
    /// </summary>
    public interface IExplorerCustomizationService : IDomainService
    {
        /// <summary>
        /// Executes explorer customization action asynchronously.
        /// </summary>
        /// <param name="actionId">The action identifier.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ExecuteExplorerActionAsync(string actionId);
        
        /// <summary>
        /// Gets all explorer customization settings asynchronously.
        /// </summary>
        /// <returns>A collection of explorer customization settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetSettingsAsync();
    }
}
