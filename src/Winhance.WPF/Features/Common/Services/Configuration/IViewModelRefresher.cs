using System.Threading.Tasks;

namespace Winhance.WPF.Features.Common.Services.Configuration
{
    /// <summary>
    /// Interface for a service that refreshes view models after configuration changes.
    /// </summary>
    public interface IViewModelRefresher
    {
        /// <summary>
        /// Refreshes a view model after configuration changes.
        /// </summary>
        /// <param name="viewModel">The view model to refresh.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RefreshViewModelAsync(object viewModel);

        /// <summary>
        /// Refreshes a child view model after configuration changes.
        /// </summary>
        /// <param name="childViewModel">The child view model to refresh.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RefreshChildViewModelAsync(object childViewModel);
    }
}