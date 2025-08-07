using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for updating properties based on configuration settings.
    /// </summary>
    public interface IPropertyUpdater
    {
        /// <summary>
        /// Updates items in a collection based on configuration settings.
        /// </summary>
        /// <param name="items">The collection of items to update.</param>
        /// <param name="configFile">The configuration file containing settings to apply.</param>
        /// <returns>The number of items that were updated.</returns>
        Task<int> UpdateItemsAsync(IEnumerable<object> items, ConfigurationFile configFile);
    }
}
