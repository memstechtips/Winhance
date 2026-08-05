using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.ViewModels;

/// <summary>
/// One card on an Optimize/Customize overview: the section's review badge, its Recommended/Default
/// pills and its NEW count, as bound properties.
///
/// <para><b>Why this exists.</b> These values used to be pushed into named XAML elements by
/// <c>UpdateOverviewBadges</c> / <c>UpdateOverviewBadgePills</c> / <c>UpdateOverviewNewBadges</c>,
/// which meant every code path that could change a setting also had to remember to call them. Two
/// comments in the old page code-behind marked places where someone had not: returning from a
/// sub-page (no <c>OnNavigatedTo</c>) and Builder-mode bulk actions (no <c>SettingAppliedEvent</c>,
/// because nothing is applied). Those were fixed one report at a time. Recomputing from observed
/// state removes the category rather than adding a third call site.</para>
///
/// <para>The observe/detach shape mirrors <c>FeatureOutcomeBanner</c>, which already does this for
/// the banner beneath the same card — same feature, same settings, same invalidation triggers.</para>
/// </summary>
public sealed partial class SectionOverviewItemViewModel : ObservableObject, IDisposable
{
    private readonly IConfigReviewBadgeService _badgeService;
    private readonly IConfigReviewModeService _reviewModeService;
    private readonly ILocalizationService _localizationService;

    private ObservableCollection<SettingItemViewModel>? _observedSettings;
    private readonly List<SettingItemViewModel> _observedItems = new();
    private bool _disposed;

    /// <summary>The key this page's navigation uses ("Gaming", "Taskbar", …).</summary>
    public string SectionKey { get; }

    /// <summary>The feature id the review badge counts against.</summary>
    public string FeatureId { get; }

    /// <summary>
    /// Resource key for the card's header icon. Carries the "…Path" / "…Symbol" suffix convention
    /// that decides whether it resolves to a <c>PathIcon</c> or a <c>FluentIcon</c>.
    /// </summary>
    public string IconResourceKey { get; }

    /// <summary>The feature behind this card; the template binds its name and description.</summary>
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

    /// <summary>Review badge showing a checkmark only: the feature is in the config with nothing
    /// left to review.</summary>
    [ObservableProperty]
    public partial bool IsReviewSuccessBadgeVisible { get; set; }

    /// <summary>Review badge showing a count: that many diffs are still unreviewed.</summary>
    [ObservableProperty]
    public partial bool IsReviewPendingBadgeVisible { get; set; }

    [ObservableProperty]
    public partial int ReviewPendingCount { get; set; }

    /// <summary>Whether the Recommended/Default pill pair shows at all.</summary>
    [ObservableProperty]
    public partial bool ArePillsVisible { get; set; }

    [ObservableProperty]
    public partial string RecommendedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DefaultText { get; set; } = string.Empty;

    /// <summary>Dimmed to 0.4 when the count is zero — the pill stays visible so the denominator
    /// remains readable, which is why this is an opacity and not a visibility.</summary>
    [ObservableProperty]
    public partial double RecommendedOpacity { get; set; } = 1.0;

    [ObservableProperty]
    public partial double DefaultOpacity { get; set; } = 1.0;

    [ObservableProperty]
    public partial bool IsNewBadgeVisible { get; set; }

    [ObservableProperty]
    public partial string NewBadgeText { get; set; } = string.Empty;

    /// <summary>
    /// Re-derives every bound value from the feature's settings and the review services. Cheap and
    /// idempotent, so it is safe to call from any invalidation trigger.
    /// </summary>
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

    /// <summary>
    /// Mirrors the old <c>UpdateFeatureBadge</c>: a count while diffs are unreviewed, a checkmark
    /// once the feature is fully reviewed or is in the config with no diffs, nothing otherwise.
    /// Collapsed entirely outside review mode.
    /// </summary>
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
