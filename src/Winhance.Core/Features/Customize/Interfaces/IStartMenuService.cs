using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Customize.Interfaces
{
    /// <summary>
    /// Service interface for managing Start Menu customization settings.
    /// Handles Start Menu layout, search, and behavior customizations.
    /// </summary>
    public interface IStartMenuService : IDomainService
    {
        // Inherits all base functionality from IDomainService
        // Domain-specific methods can be added here as needed
        
        /// <summary>
        /// Applies multiple settings asynchronously.
        /// </summary>
        /// <param name="settings">The settings to apply.</param>
        /// <param name="isEnabled">Whether the settings should be enabled.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ApplyMultipleSettingsAsync(IEnumerable<ApplicationSetting> settings, bool isEnabled);

        /// <summary>
        /// Executes Start Menu cleanup operation.
        /// Removes all pinned items and applies recommended settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CleanStartMenuAsync();
    }
}
