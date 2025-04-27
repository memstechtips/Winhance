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
        public IEnumerable<T> FilterItems<T>(IEnumerable<T> items, string searchTerm) where T : ISearchable
        {
            // Add console logging for debugging
            Console.WriteLine($"SearchService.FilterItems: Filtering with search term '{searchTerm}'");
            
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                Console.WriteLine($"SearchService.FilterItems: Search term is empty, returning all {items.Count()} items");
                return items;
            }

            // Just trim the search term, but don't convert to lowercase
            // We'll handle case insensitivity in the MatchesSearch method
            searchTerm = searchTerm.Trim();
            Console.WriteLine($"SearchService.FilterItems: Normalized search term '{searchTerm}'");
            Console.WriteLine($"SearchService.FilterItems: Starting search with term '{searchTerm}'");
            
            // Log items before filtering
            int totalItems = items.Count();
            Console.WriteLine($"SearchService.FilterItems: Total items before filtering: {totalItems}");
            
            // Apply filtering and log results
            var filteredItems = items.Where(item => {
                bool matches = item.MatchesSearch(searchTerm);
                
                // Log details for all items to help diagnose search issues
                var itemName = item.GetType().GetProperty("Name")?.GetValue(item)?.ToString();
                var itemGroupName = item.GetType().GetProperty("GroupName")?.GetValue(item)?.ToString();
                
                // Log all items for better debugging
                Console.WriteLine($"SearchService.FilterItems: Checking item '{itemName}' (Group: '{itemGroupName}') - matches: {matches}");
                
                return matches;
            }).ToList();
            
            Console.WriteLine($"SearchService.FilterItems: Found {filteredItems.Count} matching items out of {totalItems}");
            
            return filteredItems;
        }

        /// <summary>
        /// Asynchronously filters a collection of items based on a search term.
        /// </summary>
        /// <typeparam name="T">The type of items to filter.</typeparam>
        /// <param name="items">The collection of items to filter.</param>
        /// <param name="searchTerm">The search term to filter by.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a filtered collection of items that match the search term.</returns>
        public Task<IEnumerable<T>> FilterItemsAsync<T>(IEnumerable<T> items, string searchTerm) where T : ISearchable
        {
            return Task.FromResult(FilterItems(items, searchTerm));
        }
    }
}
