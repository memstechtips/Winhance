using System.Collections.Generic;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for services that handle search operations.
    /// </summary>
    public interface ISearchService
    {
        /// <summary>
        /// Filters a collection of items based on a search term.
        /// </summary>
        /// <typeparam name="T">The type of items to filter.</typeparam>
        /// <param name="items">The collection of items to filter.</param>
        /// <param name="searchTerm">The search term to filter by.</param>
        /// <returns>A filtered collection of items that match the search term.</returns>
        IEnumerable<T> FilterItems<T>(IEnumerable<T> items, string searchTerm) where T : ISearchable;

        /// <summary>
        /// Asynchronously filters a collection of items based on a search term.
        /// </summary>
        /// <typeparam name="T">The type of items to filter.</typeparam>
        /// <param name="items">The collection of items to filter.</param>
        /// <param name="searchTerm">The search term to filter by.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a filtered collection of items that match the search term.</returns>
        Task<IEnumerable<T>> FilterItemsAsync<T>(IEnumerable<T> items, string searchTerm) where T : ISearchable;
    }
}
