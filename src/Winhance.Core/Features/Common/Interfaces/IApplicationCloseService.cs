using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for handling application close functionality
    /// </summary>
    public interface IApplicationCloseService
    {
        /// <summary>
        /// Shows the support dialog if needed and handles the application close process
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task CloseApplicationWithSupportDialogAsync();
        
        /// <summary>
        /// Saves the "Don't show support dialog" preference
        /// </summary>
        /// <param name="dontShow">Whether to show the support dialog in the future</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SaveDontShowSupportPreferenceAsync(bool dontShow);
        
        /// <summary>
        /// Checks if the support dialog should be shown based on user preferences
        /// </summary>
        /// <returns>True if the dialog should be shown, false otherwise</returns>
        Task<bool> ShouldShowSupportDialogAsync();
    }
}
