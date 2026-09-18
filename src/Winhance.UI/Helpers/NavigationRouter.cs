using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.Autounattend;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Customize;
using Winhance.UI.Features.Optimize;
using Winhance.UI.Features.Settings;
using Winhance.UI.Features.SoftwareApps;
using Winhance.UI.Features.WimUtil;
using Microsoft.UI.Dispatching;

namespace Winhance.UI.Helpers;

internal sealed class NavigationRouter
{
    private readonly IConfigReviewService? _configReviewService;
    private readonly INavBadgeService? _navBadgeService;
    private readonly DispatcherQueue _dispatcherQueue;

    internal static readonly Dictionary<string, Type> TagToPageType = new()
    {
        ["Settings"] = typeof(SettingsPage),
        ["Optimize"] = typeof(OptimizePage),
        ["Customize"] = typeof(CustomizePage),
        ["WimUtil"] = typeof(WimUtilPage),
        ["Autounattend"] = typeof(AutounattendPage),
        ["SoftwareApps"] = typeof(SoftwareAppsPage),
    };

    internal static readonly Dictionary<string, string> PageTypeNameToTag = new()
    {
        [nameof(SettingsPage)] = "Settings",
        [nameof(OptimizePage)] = "Optimize",
        [nameof(CustomizePage)] = "Customize",
        [nameof(WimUtilPage)] = "WimUtil",
        [nameof(AutounattendPage)] = "Autounattend",
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
        StartupLogger.Log($"NavigateToPage called with tag: {tag}");

        if (tag == null || !TagToPageType.TryGetValue(tag, out var pageType))
        {
            StartupLogger.Log($"Skipping navigation - unknown tag: {tag}");
            return;
        }

        StartupLogger.Log($"Resolved page type: {pageType.Name}");

        if (frame.CurrentSourcePageType != pageType)
        {
            try
            {
                StartupLogger.Log($"Navigating to {pageType.Name}...");
                var result = parameter != null
                    ? frame.Navigate(pageType, parameter)
                    : frame.Navigate(pageType);
                StartupLogger.Log($"Navigate result: {result}");

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
                StartupLogger.Log($"Navigation EXCEPTION: {ex}");
            }
        }
        else
        {
            StartupLogger.Log($"Skipping navigation - already on page");
        }
    }

    public string? GetTagForCurrentPage(Type? pageType)
    {
        if (pageType == null) return null;
        return PageTypeNameToTag.GetValueOrDefault(pageType.Name);
    }
}
