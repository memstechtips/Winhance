using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service for handling search operations across different types of items.
    /// </summary>
    public class SearchService : ISearchService
    {
        /// <summary>
        /// Filters a collection of items based on a search term.
        /// </summary>
        /// <typeparam name="T">The type of items to filter.</typeparam>
        /// <param name="items">The collection of items to filter.</param>
        /// <param name="searchTerm">The search term to filter by.</param>
        /// <returns>A filtered collection of items that match the search term.</returns>
        public IEnumerable<T> FilterItems<T>(IEnumerable<T> items, string searchTerm)
            where T : ISearchable
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return items;
            }

            // Just trim the search term, but don't convert to lowercase
            // We'll handle case insensitivity in the MatchesSearch method
            searchTerm = searchTerm.Trim();

            // Log items before filtering
            int totalItems = items.Count();

            // Apply filtering and log results
            var filteredItems = items
                .Where(item =>
                {
                    bool matches = item.MatchesSearch(searchTerm);

                    // Log details for all items to help diagnose search issues
                    var itemName = item.GetType().GetProperty("Name")?.GetValue(item)?.ToString();
                    var itemGroupName = item.GetType()
                        .GetProperty("GroupName")
                        ?.GetValue(item)
                        ?.ToString();

                    return matches;
                })
                .ToList();

            return filteredItems;
        }

        /// <summary>
        /// Asynchronously filters a collection of items based on a search term.
        /// </summary>
        /// <typeparam name="T">The type of items to filter.</typeparam>
        /// <param name="items">The collection of items to filter.</param>
        /// <param name="searchTerm">The search term to filter by.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a filtered collection of items that match the search term.</returns>
        public Task<IEnumerable<T>> FilterItemsAsync<T>(IEnumerable<T> items, string searchTerm)
            where T : ISearchable
        {
            return Task.FromResult(FilterItems(items, searchTerm));
        }
    }
}
