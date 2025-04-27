namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Base interface for all view models.
    /// </summary>
    public interface IViewModel
    {
        /// <summary>
        /// Called when navigation to the view model has occurred.
        /// </summary>
        /// <param name="parameter">The navigation parameter.</param>
        void OnNavigatedTo(object? parameter = null);
    }
}