using System.Threading.Tasks;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Models.Enums;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for a service that manages UAC settings persistence.
    /// </summary>
    public interface IUacSettingsService
    {
        /// <summary>
        /// Saves custom UAC settings.
        /// </summary>
        /// <param name="consentPromptValue">The ConsentPromptBehaviorAdmin registry value.</param>
        /// <param name="secureDesktopValue">The PromptOnSecureDesktop registry value.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveCustomUacSettingsAsync(int consentPromptValue, int secureDesktopValue);

        /// <summary>
        /// Loads custom UAC settings.
        /// </summary>
        /// <returns>A CustomUacSettings object if settings exist, null otherwise.</returns>
        Task<CustomUacSettings> LoadCustomUacSettingsAsync();

        /// <summary>
        /// Checks if custom UAC settings exist.
        /// </summary>
        /// <returns>True if custom settings exist, false otherwise.</returns>
        Task<bool> HasCustomUacSettingsAsync();

        /// <summary>
        /// Gets custom UAC settings if they exist.
        /// </summary>
        /// <param name="consentPromptValue">The ConsentPromptBehaviorAdmin registry value.</param>
        /// <param name="secureDesktopValue">The PromptOnSecureDesktop registry value.</param>
        /// <returns>True if custom settings were retrieved, false otherwise.</returns>
        bool TryGetCustomUacValues(out int consentPromptValue, out int secureDesktopValue);
    }
}
