using System;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Tracks badge counts, feature visit state, and section review status
/// for nav items during Config Review Mode.
/// </summary>
public interface IConfigReviewBadgeService
{
    /// <summary>
    /// Raised when badge state changes (feature visited, approval changed, review mode change).
    /// </summary>
    event EventHandler? BadgeStateChanged;

    /// <summary>
    /// Marks a feature as visited (its settings page has been loaded).
    /// </summary>
    void MarkFeatureVisited(string featureId);

    /// <summary>
    /// Gets the number of config items for a nav section (SoftwareApps, Optimize, Customize).
    /// </summary>
    int GetNavBadgeCount(string sectionTag);

    /// <summary>
    /// Gets the number of actual diffs (changes from current state) for a specific feature.
    /// </summary>
    int GetFeatureDiffCount(string featureId);

    /// <summary>
    /// Gets the number of unreviewed diffs for a specific feature.
    /// This decreases as the user reviews (approves/rejects) individual settings.
    /// </summary>
    int GetFeaturePendingDiffCount(string featureId);

    /// <summary>
    /// Returns true if the given feature ID is present in the active config.
    /// </summary>
    bool IsFeatureInConfig(string featureId);

    /// <summary>
    /// Returns true if all features in the section that are in the config have been visited
    /// and all registered diffs for those features are reviewed.
    /// </summary>
    bool IsSectionFullyReviewed(string sectionTag);

    /// <summary>
    /// Returns true if the feature has been visited and all its diffs are reviewed.
    /// </summary>
    bool IsFeatureFullyReviewed(string featureId);

    /// <summary>
    /// Sets whether the SoftwareApps section has been fully reviewed
    /// (action choices made for all sub-sections with config items).
    /// </summary>
    bool IsSoftwareAppsReviewed { get; set; }

    /// <summary>
    /// Notifies that badge state has changed externally (e.g., from action choice changes).
    /// </summary>
    void NotifyBadgeStateChanged();
}
