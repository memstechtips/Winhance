using System;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Pure domain service for coordinating search text across application features.
    /// Follows clean architecture principles - only handles search text coordination, not UI concerns.
    /// </summary>
    public interface ISearchTextCoordinationService
    {
        /// <summary>
        /// Event fired when search text changes across the application.
        /// </summary>
        event EventHandler<SearchTextChangedEventArgs> SearchTextChanged;

        /// <summary>
        /// Gets the current search text.
        /// </summary>
        string CurrentSearchText { get; }

        /// <summary>
        /// Updates the search text and notifies all subscribers.
        /// </summary>
        /// <param name="searchText">The new search text.</param>
        void UpdateSearchText(string searchText);
    }

    /// <summary>
    /// Event arguments for search text changes.
    /// </summary>
    public class SearchTextChangedEventArgs : EventArgs
    {
        public string SearchText { get; }
        public string PreviousSearchText { get; }

        public SearchTextChangedEventArgs(string searchText, string previousSearchText)
        {
            SearchText = searchText;
            PreviousSearchText = previousSearchText;
        }
    }
}