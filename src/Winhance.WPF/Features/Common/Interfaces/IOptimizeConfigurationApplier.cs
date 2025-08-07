using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for applying configuration to the Optimize section.
    /// </summary>
    public interface IOptimizeConfigurationApplier
    {
        /// <summary>
        /// Applies configuration to the Optimize view model.
        /// </summary>
        /// <param name="viewModelObj">The view model object to apply configuration to.</param>
        /// <param name="configFile">The configuration file containing settings to apply.</param>
        /// <returns>True if configuration was applied successfully, false otherwise.</returns>
        Task<bool> ApplyConfigAsync(object viewModelObj, ConfigurationFile configFile);
    }
}
