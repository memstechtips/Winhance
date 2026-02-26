using System;
using System.Collections.Generic;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Represents a computed badge state for a navigation section.
/// </summary>
public record NavBadgeUpdate(string Tag, int Count, string Style);

/// <summary>
/// Computes navigation badge state during Config Review Mode.
/// Separates badge logic from MainWindow code-behind for testability.
/// </summary>
public interface INavBadgeService
{
    /// <summary>
    /// Computes badge updates for all nav sections (SoftwareApps, Optimize, Customize).
    /// Returns a list of badge updates to apply to the NavSidebar.
    /// </summary>
    IReadOnlyList<NavBadgeUpdate> ComputeNavBadges();

    /// <summary>
    /// Gets the total selected item count across WindowsApps and ExternalApps ViewModels.
    /// </summary>
    int GetSoftwareAppsSelectedCount();

    /// <summary>
    /// Subscribes to SoftwareApps VM property changes that affect badge counts.
    /// Calls the provided callback when changes are detected.
    /// </summary>
    void SubscribeToSoftwareAppsChanges(Action onChanged);

    /// <summary>
    /// Unsubscribes from SoftwareApps VM property change events.
    /// </summary>
    void UnsubscribeFromSoftwareAppsChanges();

    /// <summary>
    /// Whether SoftwareApps change subscriptions are active.
    /// </summary>
    bool IsSoftwareAppsBadgeSubscribed { get; }
}
