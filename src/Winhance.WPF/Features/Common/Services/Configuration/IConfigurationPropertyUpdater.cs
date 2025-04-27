using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Services.Configuration
{
    /// <summary>
    /// Interface for a service that updates properties of configuration items.
    /// </summary>
    public interface IConfigurationPropertyUpdater
    {
        /// <summary>
        /// Updates properties of items based on the configuration.
        /// </summary>
        /// <param name="items">The items to update.</param>
        /// <param name="configFile">The configuration file containing the updates.</param>
        /// <returns>The number of items that were updated.</returns>
        Task<int> UpdateItemsAsync(IEnumerable<object> items, ConfigurationFile configFile);

        /// <summary>
        /// Updates additional properties of an item based on the configuration.
        /// </summary>
        /// <param name="item">The item to update.</param>
        /// <param name="configItem">The configuration item containing the updates.</param>
        /// <returns>True if any properties were updated, false otherwise.</returns>
        bool UpdateAdditionalProperties(object item, ConfigurationItem configItem);
    }
}