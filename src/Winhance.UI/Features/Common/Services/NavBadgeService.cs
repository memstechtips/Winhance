using System.ComponentModel;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Services;

public class NavBadgeService(
    IConfigReviewModeService modeService,
    IConfigReviewBadgeService badgeService,
    WindowsAppsViewModel windowsAppsVm,
    ExternalAppsViewModel externalAppsVm) : INavBadgeService
{
    private Action? _onChanged;

    private bool _isSoftwareAppsBadgeSubscribed;

    public IReadOnlyList<NavBadgeUpdate> ComputeNavBadges()
    {
        var results = new List<NavBadgeUpdate>();

        if (!modeService.IsInReviewMode)
            return results;

        foreach (var tag in new[] { "SoftwareApps", "Optimize", "Customize" })
        {
            var count = tag == "SoftwareApps" && _isSoftwareAppsBadgeSubscribed
                ? GetSoftwareAppsSelectedCount()
                : badgeService.GetNavBadgeCount(tag);

            if (count > 0)
            {
                if (badgeService.IsSectionFullyReviewed(tag))
                    results.Add(new NavBadgeUpdate(tag, -1, "SuccessIcon"));
                else
                    results.Add(new NavBadgeUpdate(tag, count, "Attention"));
            }
            else
            {
                bool sectionInConfig = tag switch
                {
                    "SoftwareApps" => badgeService.IsFeatureInConfig(FeatureIds.WindowsApps)
                                   || badgeService.IsFeatureInConfig(FeatureIds.ExternalApps),
                    "Optimize" => FeatureDefinitions.OptimizeFeatures.Any(f => badgeService.IsFeatureInConfig(f)),
                    "Customize" => FeatureDefinitions.CustomizeFeatures.Any(f => badgeService.IsFeatureInConfig(f)),
                    _ => false
                };

                results.Add(sectionInConfig
                    ? new NavBadgeUpdate(tag, -1, "SuccessIcon")
                    : new NavBadgeUpdate(tag, -1, string.Empty));
            }
        }

        return results;
    }

    private int GetSoftwareAppsSelectedCount()
    {
        int count = 0;
        if (windowsAppsVm.Items != null)
            count += windowsAppsVm.Items.Count(a => a.IsSelected);
        if (externalAppsVm.Items != null)
            count += externalAppsVm.Items.Count(a => a.IsSelected);
        return count;
    }

    public void SubscribeToSoftwareAppsChanges(Action onChanged)
    {
        if (_isSoftwareAppsBadgeSubscribed) return;

        _onChanged = onChanged;
        windowsAppsVm.PropertyChanged += OnSoftwareAppsPropertyChanged;
        externalAppsVm.PropertyChanged += OnSoftwareAppsPropertyChanged;
        _isSoftwareAppsBadgeSubscribed = true;
    }

    public void UnsubscribeFromSoftwareAppsChanges()
    {
        if (!_isSoftwareAppsBadgeSubscribed) return;

        windowsAppsVm.PropertyChanged -= OnSoftwareAppsPropertyChanged;
        externalAppsVm.PropertyChanged -= OnSoftwareAppsPropertyChanged;
        _isSoftwareAppsBadgeSubscribed = false;
        _onChanged = null;
    }

    private void OnSoftwareAppsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if ((e.PropertyName == "HasSelectedItems" || e.PropertyName == "IsSelected")
            && modeService.IsInReviewMode)
        {
            _onChanged?.Invoke();
        }
    }
}
