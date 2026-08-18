using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.AdvancedTools;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Customize;
using Winhance.UI.Features.Optimize;
using Winhance.UI.Features.Settings;
using Winhance.UI.Features.SoftwareApps;
using Microsoft.UI.Dispatching;

namespace Winhance.UI.Helpers;

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

    public string? GetTagForCurrentPage(Type? pageType)
    {
        if (pageType == null) return null;
        return PageTypeNameToTag.GetValueOrDefault(pageType.Name);
    }
}
