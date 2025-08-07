using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Domain service for coordinating search operations across multiple features.
    /// Follows Domain Driven Design principles for search coordination.
    /// </summary>
    public interface ISearchCoordinationService
    {
        /// <summary>
        /// Event fired when search text changes.
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

        /// <summary>
        /// Registers a feature for search coordination.
        /// </summary>
        /// <param name="featureId">Unique identifier for the feature.</param>
        /// <param name="searchHandler">Handler that will be called when search text changes.</param>
        void RegisterFeature(string featureId, Action<string> searchHandler);

        /// <summary>
        /// Unregisters a feature from search coordination.
        /// </summary>
        /// <param name="featureId">Unique identifier for the feature.</param>
        void UnregisterFeature(string featureId);

        /// <summary>
        /// Gets the visibility state for a feature based on current search results.
        /// </summary>
        /// <param name="featureId">Unique identifier for the feature.</param>
        /// <returns>True if the feature should be visible, false otherwise.</returns>
        bool GetFeatureVisibility(string featureId);

        /// <summary>
        /// Updates the visibility state for a feature.
        /// </summary>
        /// <param name="featureId">Unique identifier for the feature.</param>
        /// <param name="hasVisibleItems">Whether the feature has any visible items.</param>
        void UpdateFeatureVisibility(string featureId, bool hasVisibleItems);
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
