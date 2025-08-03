using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service responsible for initializing the ApplicationSettingsService with all settings definitions.
    /// </summary>
    public interface ISettingsInitializationService
    {
        /// <summary>
        /// Initializes all settings by loading them from model classes and registering them with the ApplicationSettingsService.
        /// </summary>
        Task InitializeAllSettingsAsync();

        /// <summary>
        /// Gets whether the settings have been initialized.
        /// </summary>
        bool IsInitialized { get; }
    }
}
