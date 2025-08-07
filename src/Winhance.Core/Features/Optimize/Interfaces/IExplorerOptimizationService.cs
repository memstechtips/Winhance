using System.Threading.Tasks;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Interfaces
{
    /// <summary>
    /// Service interface for managing Windows Explorer optimization settings.
    /// Handles file explorer performance, indexing, search optimization, and system efficiency tweaks.
    /// </summary>
    public interface IExplorerOptimizationService : IDomainService
    {
        /// <summary>
        /// Executes explorer optimization action asynchronously.
        /// </summary>
        /// <param name="actionId">The action identifier.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ExecuteExplorerActionAsync(string actionId);
        
        /// <summary>
        /// Gets all explorer optimization settings asynchronously.
        /// </summary>
        /// <returns>A collection of explorer optimization settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetSettingsAsync();
    }
}
