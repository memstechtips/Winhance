using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Manages the app-wide Config Review Mode state.
    /// When active, the app displays per-setting diffs and allows users
    /// to approve/reject individual changes before applying a config.
    /// </summary>
    public interface IConfigReviewService
    {
        /// <summary>
        /// Whether the app is currently in Config Review Mode.
        /// </summary>
        bool IsInReviewMode { get; }

        /// <summary>
        /// The config file being reviewed, or null if not in review mode.
        /// </summary>
        UnifiedConfigurationFile? ActiveConfig { get; }

        /// <summary>
        /// Enters review mode with the given config. Eagerly computes diffs for all
        /// Optimize/Customize settings against current system state, so badge counts
        /// reflect actual changes (not total config items).
        /// </summary>
        Task EnterReviewModeAsync(UnifiedConfigurationFile config);

        /// <summary>
        /// Exits review mode, clearing all diffs and resetting state.
        /// </summary>
        void ExitReviewMode();

        /// <summary>
        /// Gets the diff for a specific setting, or null if the setting
        /// matches the current system value or is not in the config.
        /// </summary>
        ConfigReviewDiff? GetDiffForSetting(string settingId);

        /// <summary>
        /// Sets whether a specific setting change is approved for application.
        /// </summary>
        void SetSettingApproval(string settingId, bool approved);

        /// <summary>
        /// Gets all diffs that are currently approved.
        /// </summary>
        IReadOnlyList<ConfigReviewDiff> GetApprovedDiffs();

        /// <summary>
        /// Registers a diff for a setting (called during lazy loading when settings are loaded).
        /// </summary>
        void RegisterDiff(ConfigReviewDiff diff);

        /// <summary>
        /// Total number of settings that differ from the config.
        /// </summary>
        int TotalChanges { get; }

        /// <summary>
        /// Number of changes currently approved by the user.
        /// </summary>
        int ApprovedChanges { get; }

        /// <summary>
        /// Number of changes that have been explicitly reviewed (accept or reject).
        /// </summary>
        int ReviewedChanges { get; }

        /// <summary>
        /// Total number of config items across all sections, computed when entering review mode.
        /// Used for the initial status message before pages are visited.
        /// </summary>
        int TotalConfigItems { get; }

        /// <summary>
        /// Raised when review mode is entered or exited.
        /// </summary>
        event EventHandler? ReviewModeChanged;

        /// <summary>
        /// Raised when the approval count changes (user checks/unchecks a setting).
        /// </summary>
        event EventHandler? ApprovalCountChanged;

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
}
