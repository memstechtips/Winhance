using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Domain interface for resolving ComboBox values from system state.
    /// Follows DIP principle by depending on abstractions, not concrete implementations.
    /// Each domain can implement its own resolution logic.
    /// </summary>
    public interface IComboBoxValueResolver
    {
        /// <summary>
        /// Gets the domain name this resolver handles.
        /// </summary>
        string DomainName { get; }

        /// <summary>
        /// Determines if this resolver can handle the given setting.
        /// </summary>
        /// <param name="setting">The setting to check.</param>
        /// <returns>True if this resolver can handle the setting.</returns>
        bool CanResolve(ApplicationSetting setting);

        /// <summary>
        /// Resolves the current ComboBox index from system state.
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
