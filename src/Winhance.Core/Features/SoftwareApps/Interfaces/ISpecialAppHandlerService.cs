using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Provides functionality for handling special applications that require custom removal procedures.
    /// </summary>
    public interface ISpecialAppHandlerService
    {
        /// <summary>
        /// Gets all registered special app handlers.
        /// </summary>
        /// <returns>A collection of special app handlers.</returns>
        IEnumerable<SpecialAppHandler> GetAllHandlers();

        /// <summary>
        /// Gets a special app handler by its type.
        /// </summary>
        /// <param name="handlerType">The type of handler to retrieve.</param>
        /// <returns>The requested special app handler, or null if not found.</returns>
        SpecialAppHandler? GetHandler(string handlerType);

        /// <summary>
        /// Removes Microsoft Edge.
        /// </summary>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        Task<bool> RemoveEdgeAsync();

        /// <summary>
        /// Removes OneDrive.
        /// </summary>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        Task<bool> RemoveOneDriveAsync();

        /// <summary>
        /// Removes a special application using its registered handler.
        /// </summary>
        /// <param name="handlerType">The type of handler to use.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        Task<bool> RemoveSpecialAppAsync(string handlerType);
    }
}