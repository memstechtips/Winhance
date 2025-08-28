using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Domain interface for resolving ComboBox values from system state.
    /// Follows DIP principle by depending on abstractions, not concrete implementations.
    /// Each domain can implement its own resolution logic.
    /// </summary>
    public interface IComboBoxResolver
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
        bool CanResolve(SettingDefinition setting);

        /// <summary>
        /// Resolves the current ComboBox index from system state.
        /// </summary>
        /// <param name="setting">The ComboBox setting to resolve.</param>
        /// <returns>The current ComboBox index, or null if cannot be determined.</returns>
        Task<int?> ResolveCurrentIndexAsync(SettingDefinition setting);

        /// <summary>
        /// Applies a ComboBox index value to the system.
        /// </summary>
        /// <param name="setting">The ComboBox setting to apply.</param>
        /// <param name="index">The ComboBox index to apply.</param>
        Task ApplyIndexAsync(SettingDefinition setting, int index);

        /// <summary>
        /// Converts a ComboBox display value to its corresponding index.
        /// </summary>
        /// <param name="setting">The ComboBox setting.</param>
        /// <param name="displayValue">The display value to convert (e.g., "Show", "Hide").</param>
        /// <returns>The ComboBox index for the display value, or -1 if not found.</returns>
        int GetIndexForDisplayValue(SettingDefinition setting, string displayValue);
    }
}
