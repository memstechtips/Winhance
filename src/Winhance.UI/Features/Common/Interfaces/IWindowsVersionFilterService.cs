using System;
using System.Threading.Tasks;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Manages the Windows version filter state, persistence, and review mode interactions.
/// </summary>
public interface IWindowsVersionFilterService
{
    /// <summary>Whether the filter is currently enabled.</summary>
    bool IsFilterEnabled { get; }

    /// <summary>Loads the filter preference from user preferences storage.</summary>
    Task LoadFilterPreferenceAsync();

    /// <summary>
    /// Toggles the filter state, optionally showing an explanation dialog.
    /// </summary>
    /// <param name="isInReviewMode">If true, toggling is blocked.</param>
    /// <returns>True if the filter was toggled; false if cancelled or blocked.</returns>
    Task<bool> ToggleFilterAsync(bool isInReviewMode);

    /// <summary>
    /// Forces the filter on (e.g., when entering review mode). Not persisted.
    /// </summary>
    void ForceFilterOn();

    /// <summary>
    /// Restores the persisted filter preference (e.g., when exiting review mode).
    /// </summary>
    Task RestoreFilterPreferenceAsync();

    /// <summary>Raised when the filter state changes (enabled/disabled).</summary>
    event EventHandler<bool>? FilterStateChanged;
}
