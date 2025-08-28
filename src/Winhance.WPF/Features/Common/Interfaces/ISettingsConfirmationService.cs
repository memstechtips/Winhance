using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Interfaces
{
    /// <summary>
    /// Service responsible for handling setting confirmation dialogs.
    /// Follows SRP by handling only confirmation UI logic.
    /// </summary>
    public interface ISettingsConfirmationService
    {
        /// <summary>
        /// Handles confirmation dialog for a setting change if required.
        /// </summary>
        /// <param name="settingId">The ID of the setting being changed</param>
        /// <param name="value">The new value for the setting</param>
        /// <param name="setting">The application setting model containing confirmation metadata</param>
        /// <returns>A task that returns (confirmed, checkboxChecked) or (true, true) if no confirmation needed</returns>
        Task<(bool confirmed, bool checkboxChecked)> HandleConfirmationAsync(
            string settingId, 
            object? value, 
            SettingDefinition setting);

        /// <summary>
        /// Replaces placeholders in confirmation dialog text with runtime values.
        /// </summary>
        /// <param name="text">The text containing placeholders</param>
        /// <param name="settingId">The setting ID for context-specific replacements</param>
        /// <param name="value">The value for placeholder replacement</param>
        /// <returns>Text with placeholders replaced</returns>
        string ReplacePlaceholders(string text, string settingId, object? value);
    }
}
