using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.ViewModels;

// Recomputed from observed state rather than pushed into named XAML elements: every code path that could change
// a setting used to have to remember to call the updaters, and two paths didn't (returning from a sub-page,
// Builder bulk actions). Mirrors FeatureOutcomeBanner's observe/detach shape - same feature, same settings,
// same invalidation triggers.
public sealed partial class SectionOverviewItemViewModel : ObservableObject, IDisposable
{
    private readonly IConfigReviewBadgeService _badgeService;
    private readonly IConfigReviewModeService _reviewModeService;
    private readonly ILocalizationService _localizationService;

    private ObservableCollection<SettingItemViewModel>? _observedSettings;
    private readonly List<SettingItemViewModel> _observedItems = new();
    private bool _disposed;

    public string SectionKey { get; }

    public string FeatureId { get; }

    public string IconResourceKey { get; }

    public ISettingsFeatureViewModel Feature { get; }

    public SectionOverviewItemViewModel(
        string sectionKey,
        string featureId,
        string iconResourceKey,
        ISettingsFeatureViewModel feature,
        IConfigReviewBadgeService badgeService,
        IConfigReviewModeService reviewModeService,
        ILocalizationService localizationService)
    {
        SectionKey = sectionKey;
        FeatureId = featureId;
        IconResourceKey = iconResourceKey;
        Feature = feature;
        _badgeService = badgeService;
        _reviewModeService = reviewModeService;
        _localizationService = localizationService;

        _badgeService.BadgeStateChanged += OnBadgeStateChanged;
        _reviewModeService.ReviewModeChanged += OnReviewModeChanged;
        _localizationService.LanguageChanged += OnLanguageChanged;

        Attach();
        Refresh();
    }

    // ── Global view toggles (View menu). Set by the page; each one re-derives the card. ──

    [ObservableProperty]
    public partial bool AreInfoBadgesVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool AreNewBadgesVisible { get; set; } = true;

    partial void OnAreInfoBadgesVisibleChanged(bool value) => Refresh();

    partial void OnAreNewBadgesVisibleChanged(bool value) => Refresh();

    // ── Derived, bound state ──

    [ObservableProperty]
    public partial bool IsReviewSuccessBadgeVisible { get; set; }

    [ObservableProperty]
    public partial bool IsReviewPendingBadgeVisible { get; set; }

    [ObservableProperty]
    public partial int ReviewPendingCount { get; set; }

    [ObservableProperty]
    public partial bool ArePillsVisible { get; set; }

    [ObservableProperty]
    public partial string RecommendedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DefaultText { get; set; } = string.Empty;

    // Dimmed to 0.4 when the count is zero - the pill stays visible so the denominator remains readable, which is
    // why this is an opacity and not a visibility.
    [ObservableProperty]
    public partial double RecommendedOpacity { get; set; } = 1.0;

    [ObservableProperty]
    public partial double DefaultOpacity { get; set; } = 1.0;

    [ObservableProperty]
    public partial bool IsNewBadgeVisible { get; set; }

    [ObservableProperty]
    public partial string NewBadgeText { get; set; } = string.Empty;

    // Cheap and idempotent, so it is safe to call from any invalidation trigger.
    public void Refresh()
    {
        if (_disposed) return;

        UpdateReviewBadge();

        var summary = FeatureBadgeAggregator.Aggregate(Feature);
        int total = summary.TotalWithBadgeData;

        ArePillsVisible = AreInfoBadgesVisible && total > 0;
        if (ArePillsVisible)
        {
            RecommendedText =
                $"{Localized("InfoBadge_Recommended", "Recommended")} {summary.RecommendedCount}/{total}";
            RecommendedOpacity = summary.RecommendedCount > 0 ? 1.0 : 0.4;

            DefaultText = $"{Localized("InfoBadge_Default", "Default")} {summary.DefaultCount}/{total}";
            DefaultOpacity = summary.DefaultCount > 0 ? 1.0 : 0.4;
        }

        IsNewBadgeVisible = AreNewBadgesVisible && summary.NewCount > 0;
        if (IsNewBadgeVisible)
        {
            NewBadgeText = $"{Localized("Badge_New", "NEW")} {summary.NewCount}";
        }
    }

    // A count while diffs are unreviewed, a checkmark once fully reviewed or in the config with no diffs, nothing
    // otherwise; collapsed outside review mode.
    private void UpdateReviewBadge()
    {
        if (!_reviewModeService.IsInReviewMode)
        {
            IsReviewSuccessBadgeVisible = false;
            IsReviewPendingBadgeVisible = false;
            return;
        }

        if (_badgeService.GetFeatureDiffCount(FeatureId) > 0)
        {
            bool fullyReviewed = _badgeService.IsFeatureFullyReviewed(FeatureId);
            IsReviewSuccessBadgeVisible = fullyReviewed;
            IsReviewPendingBadgeVisible = !fullyReviewed;
            ReviewPendingCount = fullyReviewed
                ? 0
                : _badgeService.GetFeaturePendingDiffCount(FeatureId);
            return;
        }

        // In the config but with no diffs: nothing to review, so the checkmark stands alone.
        IsReviewSuccessBadgeVisible = _badgeService.IsFeatureInConfig(FeatureId);
        IsReviewPendingBadgeVisible = false;
    }

    private string Localized(string key, string fallback) =>
        _localizationService.TryGetString(key, out var value) ? value : fallback;

    // ── Observation ──

    private void Attach()
    {
        Detach();

        _observedSettings = Feature.Settings;
        if (_observedSettings is not { } settings) return;

        settings.CollectionChanged += OnSettingsCollectionChanged;
        foreach (var setting in settings)
        {
            setting.PropertyChanged += OnSettingPropertyChanged;
            _observedItems.Add(setting);
        }
    }

    private void Detach()
    {
        if (_observedSettings is { } settings)
        {
            settings.CollectionChanged -= OnSettingsCollectionChanged;
            _observedSettings = null;
        }

        foreach (var setting in _observedItems)
        {
            setting.PropertyChanged -= OnSettingPropertyChanged;
        }
        _observedItems.Clear();
    }

    private void OnSettingsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // A reload replaces every SettingItemViewModel, so re-subscribe rather than patching the
        // delta — the collection is small and this cannot drift out of sync with the contents.
        Attach();
        Refresh();
    }

    private void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // BadgeRow is rebuilt in place by ComputeBadgeState, so its own PropertyChanged does not
        // fire for content edits; the properties that accompany a recompute are watched instead.
        if (e.PropertyName is null
            or nameof(SettingItemViewModel.BadgeRow)
            or nameof(SettingItemViewModel.Outcome)
            or nameof(SettingItemViewModel.IsSelected)
            or nameof(SettingItemViewModel.SelectedValue)
            or nameof(SettingItemViewModel.IsNew))
        {
            Refresh();
        }
    }

    private void OnBadgeStateChanged(object? sender, EventArgs e) => UpdateReviewBadge();

    private void OnReviewModeChanged(object? sender, EventArgs e) => UpdateReviewBadge();

    private void OnLanguageChanged(object? sender, EventArgs e) => Refresh();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Detach();
        _badgeService.BadgeStateChanged -= OnBadgeStateChanged;
        _reviewModeService.ReviewModeChanged -= OnReviewModeChanged;
        _localizationService.LanguageChanged -= OnLanguageChanged;
    }
}
