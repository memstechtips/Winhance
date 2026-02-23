using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Computes navigation badge state during Config Review Mode.
/// </summary>
public class NavBadgeService : INavBadgeService
{
    private readonly IConfigReviewModeService _modeService;
    private readonly IConfigReviewBadgeService _badgeService;
    private readonly WindowsAppsViewModel _windowsAppsVm;
    private readonly ExternalAppsViewModel _externalAppsVm;

    private Action? _onChanged;

    public NavBadgeService(
        IConfigReviewModeService modeService,
        IConfigReviewBadgeService badgeService,
        WindowsAppsViewModel windowsAppsVm,
        ExternalAppsViewModel externalAppsVm)
    {
        _modeService = modeService;
        _badgeService = badgeService;
        _windowsAppsVm = windowsAppsVm;
        _externalAppsVm = externalAppsVm;
    }

    public bool IsSoftwareAppsBadgeSubscribed { get; private set; }

    public IReadOnlyList<NavBadgeUpdate> ComputeNavBadges()
    {
        var results = new List<NavBadgeUpdate>();

        if (!_modeService.IsInReviewMode)
            return results;

        foreach (var tag in new[] { "SoftwareApps", "Optimize", "Customize" })
        {
            var count = tag == "SoftwareApps" && IsSoftwareAppsBadgeSubscribed
                ? GetSoftwareAppsSelectedCount()
                : _badgeService.GetNavBadgeCount(tag);

            if (count > 0)
            {
                if (_badgeService.IsSectionFullyReviewed(tag))
                    results.Add(new NavBadgeUpdate(tag, -1, "SuccessIcon"));
                else
                    results.Add(new NavBadgeUpdate(tag, count, "Attention"));
            }
            else
            {
                bool sectionInConfig = tag switch
                {
                    "SoftwareApps" => _badgeService.IsFeatureInConfig(FeatureIds.WindowsApps)
                                   || _badgeService.IsFeatureInConfig(FeatureIds.ExternalApps),
                    "Optimize" => FeatureDefinitions.OptimizeFeatures.Any(f => _badgeService.IsFeatureInConfig(f)),
                    "Customize" => FeatureDefinitions.CustomizeFeatures.Any(f => _badgeService.IsFeatureInConfig(f)),
                    _ => false
                };

                results.Add(sectionInConfig
                    ? new NavBadgeUpdate(tag, -1, "SuccessIcon")
                    : new NavBadgeUpdate(tag, -1, string.Empty));
            }
        }

        return results;
    }

    public int GetSoftwareAppsSelectedCount()
    {
        int count = 0;
        if (_windowsAppsVm.Items != null)
            count += _windowsAppsVm.Items.Count(a => a.IsSelected);
        if (_externalAppsVm.Items != null)
            count += _externalAppsVm.Items.Count(a => a.IsSelected);
        return count;
    }

    public void SubscribeToSoftwareAppsChanges(Action onChanged)
    {
        if (IsSoftwareAppsBadgeSubscribed) return;

        _onChanged = onChanged;
        _windowsAppsVm.PropertyChanged += OnSoftwareAppsPropertyChanged;
        _externalAppsVm.PropertyChanged += OnSoftwareAppsPropertyChanged;
        IsSoftwareAppsBadgeSubscribed = true;
    }

    public void UnsubscribeFromSoftwareAppsChanges()
    {
        if (!IsSoftwareAppsBadgeSubscribed) return;

        _windowsAppsVm.PropertyChanged -= OnSoftwareAppsPropertyChanged;
        _externalAppsVm.PropertyChanged -= OnSoftwareAppsPropertyChanged;
        IsSoftwareAppsBadgeSubscribed = false;
        _onChanged = null;
    }

    private void OnSoftwareAppsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if ((e.PropertyName == "HasSelectedItems" || e.PropertyName == "IsSelected")
            && _modeService.IsInReviewMode)
        {
            _onChanged?.Invoke();
        }
    }
}
