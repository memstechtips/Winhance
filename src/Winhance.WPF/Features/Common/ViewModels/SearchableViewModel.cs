using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.Common.ViewModels
{
    /// <summary>
    /// Base view model class that provides search functionality.
    /// </summary>
    /// <typeparam name="T">The type of items to search, must implement ISearchable.</typeparam>
    public abstract partial class SearchableViewModel<T> : AppListViewModel<T> where T : class, ISearchable
    {
        private readonly ISearchService _searchService;

        // Backing field for the search text
        private string _searchText = string.Empty;

        /// <summary>
        /// Gets or sets the search text.
        /// </summary>
        public virtual string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    IsSearchActive = !string.IsNullOrWhiteSpace(value);
                    ApplySearch();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether search is active.
        /// </summary>
        [ObservableProperty]
        private bool _isSearchActive;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchableViewModel{T}"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="searchService">The search service.</param>
        /// <param name="packageManager">The package manager.</param>
        protected SearchableViewModel(
            ITaskProgressService progressService,
            ISearchService searchService,
            IPackageManager? packageManager = null)
            : base(progressService, packageManager)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        }

        /// <summary>
        /// Applies the current search text to filter items.
        /// </summary>
        protected abstract void ApplySearch();

        /// <summary>
        /// Filters a collection of items based on the current search text.
        /// </summary>
        /// <param name="items">The collection of items to filter.</param>
        /// <returns>A filtered collection of items that match the search text.</returns>
        protected IEnumerable<T> FilterItems(IEnumerable<T> items)
        {
            // Log items before filtering
            foreach (var item in items)
            {
                var itemName = item.GetType().GetProperty("Name")?.GetValue(item)?.ToString();
                var itemGroupName = item.GetType().GetProperty("GroupName")?.GetValue(item)?.ToString();
            }

            var result = _searchService.FilterItems(items, SearchText);

            return result;
        }

        /// <summary>
        /// Asynchronously filters a collection of items based on the current search text.
        /// </summary>
        /// <param name="items">The collection of items to filter.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a filtered collection of items that match the search text.</returns>
        protected Task<IEnumerable<T>> FilterItemsAsync(IEnumerable<T> items)
        {
            return _searchService.FilterItemsAsync(items, SearchText);
        }

        /// <summary>
        /// Clears the search text.
        /// </summary>
        protected void ClearSearch()
        {
            SearchText = string.Empty;
        }
    }
}
