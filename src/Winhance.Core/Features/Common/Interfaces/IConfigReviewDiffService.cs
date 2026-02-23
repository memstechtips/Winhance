using System;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Manages per-setting diffs and approval state during Config Review Mode.
    /// </summary>
    public interface IConfigReviewDiffService
    {
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
        /// Raised when the approval count changes (user checks/unchecks a setting).
        /// </summary>
        event EventHandler? ApprovalCountChanged;
    }
}
