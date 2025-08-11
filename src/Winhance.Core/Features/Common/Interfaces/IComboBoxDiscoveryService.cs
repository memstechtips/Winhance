using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Infrastructure interface for ComboBox value discovery coordination.
    /// Follows DIP principle by providing abstraction for ComboBox resolution.
    /// </summary>
    public interface IComboBoxDiscoveryService
    {
        /// <summary>
        /// Resolves the current ComboBox index for a setting from system state.
        /// </summary>
        /// <param name="setting">The ComboBox setting to resolve.</param>
        /// <returns>The current ComboBox index, or null if cannot be determined.</returns>
        Task<int?> ResolveCurrentIndexAsync(ApplicationSetting setting);

        /// <summary>
        /// Applies a ComboBox index value to the system.
        /// </summary>
        /// <param name="setting">The ComboBox setting to apply.</param>
        /// <param name="index">The ComboBox index to apply.</param>
        Task ApplyIndexAsync(ApplicationSetting setting, int index);
    }
}
