using System;

namespace Winhance.WPF.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for feature ViewModels that participate in search coordination.
    /// Extends IFeatureViewModel with search-specific capabilities.
    /// </summary>
    public interface ISearchableFeatureViewModel : IFeatureViewModel
    {
        /// <summary>
        /// Event fired when the feature's visibility state changes due to search filtering.
        /// </summary>
        event EventHandler<FeatureVisibilityChangedEventArgs> VisibilityChanged;

        /// <summary>
        /// Applies search filtering to this feature's content.
        /// </summary>
        /// <param name="searchText">The search text to filter by.</param>
        void ApplySearchFilter(string searchText);

        /// <summary>
        /// Gets whether this feature should be visible based on current search results.
        /// </summary>
        bool IsVisibleInSearch { get; }
    }

    /// <summary>
    /// Event arguments for feature visibility changes.
    /// </summary>
    public class FeatureVisibilityChangedEventArgs : EventArgs
    {
        public string FeatureId { get; }
        public bool IsVisible { get; }
        public string SearchText { get; }

        public FeatureVisibilityChangedEventArgs(string featureId, bool isVisible, string searchText)
        {
            FeatureId = featureId;
            IsVisible = isVisible;
            SearchText = searchText;
        }
    }
}
