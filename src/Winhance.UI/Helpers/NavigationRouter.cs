using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.AdvancedTools;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Customize;
using Winhance.UI.Features.Optimize;
using Winhance.UI.Features.Settings;
using Winhance.UI.Features.SoftwareApps;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;

namespace Winhance.UI.Helpers;

/// <summary>
/// Handles page navigation routing, tag-to-Type mapping, and review mode feature tracking.
/// Extracted from MainWindow to reduce code-behind complexity.
/// </summary>
internal sealed class NavigationRouter
{
    private readonly IConfigReviewService? _configReviewService;
    private readonly INavBadgeService? _navBadgeService;
    private readonly DispatcherQueue _dispatcherQueue;

    private static readonly Dictionary<string, Type> TagToPageType = new()
    {
        ["Settings"] = typeof(SettingsPage),
        ["Optimize"] = typeof(OptimizePage),
        ["Customize"] = typeof(CustomizePage),
        ["AdvancedTools"] = typeof(AdvancedToolsPage),
        ["SoftwareApps"] = typeof(SoftwareAppsPage),
    };

    private static readonly Dictionary<string, string> PageTypeNameToTag = new()
    {
        [nameof(SettingsPage)] = "Settings",
        [nameof(OptimizePage)] = "Optimize",
        [nameof(CustomizePage)] = "Customize",
        [nameof(AdvancedToolsPage)] = "AdvancedTools",
        [nameof(SoftwareAppsPage)] = "SoftwareApps",
    };

    public NavigationRouter(
        IConfigReviewService? configReviewService,
        INavBadgeService? navBadgeService,
        DispatcherQueue dispatcherQueue)
    {
        _configReviewService = configReviewService;
        _navBadgeService = navBadgeService;
        _dispatcherQueue = dispatcherQueue;
    }

    /// <summary>
    /// Navigates the content frame to the page corresponding to the given tag.
    /// Also handles review mode feature-visited tracking for SoftwareApps.
    /// </summary>
    /// <param name="frame">The content frame to navigate.</param>
    /// <param name="tag">The navigation tag (e.g., "SoftwareApps", "Optimize").</param>
    /// <param name="parameter">Optional navigation parameter.</param>
    /// <param name="applyNavBadges">Callback to refresh nav badges after subscription.</param>
    public void NavigateToPage(Frame frame, string? tag, object? parameter = null, Action? applyNavBadges = null)
    {
        StartupLogger.Log("NavigationRouter", $"NavigateToPage called with tag: {tag}");

        if (tag == null || !TagToPageType.TryGetValue(tag, out var pageType))
        {
            StartupLogger.Log("NavigationRouter", $"Skipping navigation - unknown tag: {tag}");
            return;
        }

        StartupLogger.Log("NavigationRouter", $"Resolved page type: {pageType.Name}");

        if (frame.CurrentSourcePageType != pageType)
        {
            try
            {
                StartupLogger.Log("NavigationRouter", $"Navigating to {pageType.Name}...");
                var result = parameter != null
                    ? frame.Navigate(pageType, parameter)
                    : frame.Navigate(pageType);
                StartupLogger.Log("NavigationRouter", $"Navigate result: {result}");

                // Mark SoftwareApps features as visited when navigating to that page
                if (tag == "SoftwareApps" && _configReviewService?.IsInReviewMode == true)
                {
                    _configReviewService.MarkFeatureVisited(FeatureIds.WindowsApps);
                    _configReviewService.MarkFeatureVisited(FeatureIds.ExternalApps);
                    _navBadgeService?.SubscribeToSoftwareAppsChanges(() =>
                        _dispatcherQueue.TryEnqueue(() => applyNavBadges?.Invoke()));
                }
            }
            catch (Exception ex)
            {
                StartupLogger.Log("NavigationRouter", $"Navigation EXCEPTION: {ex}");
            }
        }
        else
        {
            StartupLogger.Log("NavigationRouter", $"Skipping navigation - already on page");
        }
    }

    /// <summary>
    /// Gets the navigation tag for the currently displayed page type.
    /// </summary>
    public string? GetTagForCurrentPage(Type? pageType)
    {
        if (pageType == null) return null;
        return PageTypeNameToTag.GetValueOrDefault(pageType.Name);
    }
}
