using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Autounattend.ViewModels;

public partial class AutounattendViewModel : BaseSettingsFeatureViewModel
{
    // Punctuation rather than a word, so it needs no locale key.
    private const string Loading = "...";

    // Sidebar order; FeatureDefinitions.All declares Customize first.
    private static readonly (string Category, string LabelKey, string DefaultLabel)[] IncludedGroupLayout =
    [
        ("SoftwareApps", "Nav_SoftwareAndApps", "Software & Apps"),
        ("Optimize", "Nav_Optimize", "Optimize"),
        ("Customize", "Nav_Customize", "Customize"),
    ];

    // Each feature page's own header key; they do not all follow the feature id. The English sits here because
    // FeatureDefinitions.DefaultName says "Windows Apps" where the tab says "Windows Apps & Features".
    private static readonly Dictionary<string, (string Key, string DefaultLabel)> IncludedLabels = new(StringComparer.Ordinal)
    {
        [FeatureIds.WindowsApps] = ("SoftwareApps_Tab_WindowsApps", "Windows Apps & Features"),
        [FeatureIds.ExternalApps] = ("SoftwareApps_Tab_ExternalApps", "External Software"),
        [FeatureIds.Privacy] = ("Feature_Privacy_Name", "Privacy & Security"),
        [FeatureIds.Power] = ("Feature_Power_Name", "Power"),
        [FeatureIds.GamingPerformance] = ("Feature_GamingPerformance_Name", "Gaming & Performance"),
        [FeatureIds.Update] = ("Feature_Update_Name", "Update"),
        [FeatureIds.Notifications] = ("Feature_Notifications_Name", "Notifications"),
        [FeatureIds.Sound] = ("Feature_Sound_Name", "Sound"),
        [FeatureIds.WindowsTheme] = ("Feature_WindowsTheme_Name", "Windows Theme"),
        [FeatureIds.Taskbar] = ("Feature_Taskbar_Name", "Taskbar"),
        [FeatureIds.StartMenu] = ("Feature_StartMenu_Name", "Start Menu"),
        [FeatureIds.ExplorerCustomization] = ("Feature_Explorer_Name", "Explorer"),
        [FeatureIds.TimeRegionLanguage] = ("Feature_TimeRegionLanguage_Name", "Time, region and language"),
    };

    private readonly ICatalogSettingsRegistry _registry;
    private readonly ICatalogScopeProvider _scope;
    private readonly IAppSelectionSource _apps;
    private readonly IWindowsAppsService _windowsApps;
    private readonly IExternalAppsService _externalApps;

    private ISubscriptionToken? _filterStateChangedSubscription;
    private IncludedSummary? _summary;
    private int _loadVersion;
    private int _includedHosts;
    private bool _isIncludedExpanded = true;
    private string _includedCountText = Loading;

    public AutounattendViewModel(
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus,
        IApplicationModeService applicationModeService,
        ICatalogSettingsRegistry registry,
        ICatalogScopeProvider scope,
        IAppSelectionSource apps,
        IWindowsAppsService windowsApps,
        IExternalAppsService externalApps)
        : base(settingsLoadingService, logService, localizationService, dispatcherService, eventBus, applicationModeService)
    {
        _registry = registry;
        _scope = scope;
        _apps = apps;
        _windowsApps = windowsApps;
        _externalApps = externalApps;
        _localizationService.LanguageChanged += OnPageTextLanguageChanged;
    }

    public override string ModuleId => FeatureIds.Autounattend;

    protected override string GetDisplayNameKey() => "Feature_Autounattend_Name";

    public string PageTitle => _localizationService.GetStringOrDefault(
        "Autounattend_Page_Title",
        "Autounattend.xml Settings");

    public string PageDescription => _localizationService.GetStringOrDefault(
        "Autounattend_Page_Description",
        "Automate the Windows Setup Process");

    public string IncludedTitle => _localizationService.GetStringOrDefault(
        "Autounattend_Included_Name",
        "Included from other features");

    public string IncludedDescription => _localizationService.GetStringOrDefault(
        "Autounattend_Included_Description",
        "The choices on this page are written into the answer file together with your selections in the features below.");

    public string IncludedIcon => MaterialIcons.FormatListBulleted.Glyph;

    public ObservableCollection<IncludedRowViewModel> IncludedGroups { get; } = [];

    public string IncludedCountText
    {
        get => _includedCountText;
        private set => SetProperty(ref _includedCountText, value);
    }

    public bool IsIncludedExpanded
    {
        get => _isIncludedExpanded;
        private set
        {
            if (SetProperty(ref _isIncludedExpanded, value))
            {
                OnPropertyChanged(nameof(IncludedCornerRadius));
            }
        }
    }

    public Microsoft.UI.Xaml.CornerRadius IncludedCornerRadius => IsIncludedExpanded && IncludedGroups.Count > 0
        ? new Microsoft.UI.Xaml.CornerRadius(4, 4, 0, 0)
        : new Microsoft.UI.Xaml.CornerRadius(4);

    public void ToggleIncludedExpander() => IsIncludedExpanded = !IsIncludedExpanded;

    public Task LoadIncludedAsync()
    {
        _includedHosts++;

        // Not from the constructor: the container resolves this view model long before the page is first opened.
        _filterStateChangedSubscription ??= _eventBus.SubscribeAsync<FilterStateChangedEvent>(OnIncludedFilterStateChangedAsync);

        return RefreshIncludedAsync();
    }

    private async Task RefreshIncludedAsync()
    {
        // A filter change can start a second count while the first is still reading; only the newest lands.
        var version = ++_loadVersion;

        try
        {
            var summary = await CountIncludedAsync();
            if (version != _loadVersion)
                return;

            _summary = summary;
        }
        catch (Exception ex)
        {
            LogIncludedFailure(ex);

            if (version == _loadVersion)
                ShowIncludedPlaceholder();

            return;
        }

        TryRelabelIncluded();
    }

    // Counted: navigating between the two hosts can load the new one before the old one unloads.
    public void ReleaseIncluded()
    {
        _includedHosts = Math.Max(0, _includedHosts - 1);
        if (_includedHosts > 0)
            return;

        _filterStateChangedSubscription?.Dispose();
        _filterStateChangedSubscription = null;
    }

    // FilterStateChangedEvent is published from the UI thread, so this needs no dispatcher.
    private Task OnIncludedFilterStateChangedAsync(FilterStateChangedEvent e) => RefreshIncludedAsync();

    private void OnPageTextLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageDescription));
        TryRelabelIncluded();
    }

    // Neither caller catches: the view awaits LoadIncludedAsync from an async void Loaded handler, and the
    // language change arrives on an event handler.
    private void TryRelabelIncluded()
    {
        try
        {
            RelabelIncluded();
        }
        catch (Exception ex)
        {
            LogIncludedFailure(ex);
            ShowIncludedPlaceholder();
        }
    }

    private void LogIncludedFailure(Exception ex) =>
        _logService.LogWarning($"Autounattend summary could not be built: {ex.Message}");

    private void ShowIncludedPlaceholder()
    {
        _summary = null;
        IncludedGroups.Clear();
        IncludedCountText = Loading;
        OnPropertyChanged(nameof(IncludedCornerRadius));
    }

    private void RelabelIncluded()
    {
        OnPropertyChanged(nameof(IncludedTitle));
        OnPropertyChanged(nameof(IncludedDescription));

        var expanded = IncludedGroups
            .Where(existing => existing.IsExpanded)
            .Select(existing => existing.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (_summary is not { } summary)
        {
            IncludedGroups.Clear();
            IncludedCountText = Loading;
            OnPropertyChanged(nameof(IncludedCornerRadius));
            return;
        }

        var format = _localizationService.GetStringOrDefault("Autounattend_Included_Count", "{0} of {1} included");

        // Built whole first, so a label lookup that throws cannot leave half a list on the page.
        var rebuilt = new List<IncludedRowViewModel>();

        foreach (var group in summary.Groups)
        {
            var features = group.Features
                .Select(feature => new IncludedRowViewModel(
                    feature.FeatureId,
                    _localizationService.GetStringOrDefault(feature.LabelKey, feature.DefaultLabel),
                    IncludedCountFormat(format, feature.Included, feature.Total),
                    feature.IconResourceKey,
                    [],
                    RefreshIncludedCorners))
                .ToList();

            rebuilt.Add(new IncludedRowViewModel(
                group.LabelKey,
                _localizationService.GetStringOrDefault(group.LabelKey, group.DefaultLabel),
                IncludedCountFormat(format, group.Included, group.Total),
                group.IconResourceKey,
                features,
                RefreshIncludedCorners));
        }

        var countText = IncludedCountFormat(format, summary.Included, summary.Total);

        IncludedGroups.Clear();
        foreach (var row in rebuilt)
        {
            IncludedGroups.Add(row);
        }

        foreach (var row in IncludedGroups.Where(built => expanded.Contains(built.Key)))
        {
            row.ToggleExpander();
        }

        IncludedCountText = countText;
        OnPropertyChanged(nameof(IncludedCornerRadius));
        RefreshIncludedCorners();
    }

    private async Task<IncludedSummary> CountIncludedAsync()
    {
        await _registry.InitializeAsync();

        var scope = _scope.Current;
        var included = new Dictionary<string, int>(StringComparer.Ordinal);
        var total = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var featureId in SettingCatalog.ByFeature.Keys)
        {
            // The saved file can carry fewer: SettingSnapshotSource drops a setting whose live state could not be read.
            var inScope = _registry.GetByFeature(featureId, scope);
            included[featureId] = inScope.Count(setting => _applicationModeService.IsIncluded(setting.Id));
            total[featureId] = inScope.Count;
        }

        included[FeatureIds.WindowsApps] = (await _apps.CheckedWindowsAppsAsync()).Count;
        included[FeatureIds.ExternalApps] = (await _apps.CheckedExternalAppsAsync()).Count;
        total[FeatureIds.WindowsApps] = (await _windowsApps.GetAppsAsync()).Count();
        total[FeatureIds.ExternalApps] = (await _externalApps.GetAppsAsync()).Count();

        return new IncludedSummary(IncludedGroupLayout
            .Select(group => new IncludedGroup(
                group.LabelKey,
                group.DefaultLabel,
                FeatureDefinitions.CategoryIconKey(group.Category),
                FeatureDefinitions.All
                    .Where(feature => feature.Category == group.Category)
                    .Select(feature =>
                    {
                        var (key, defaultLabel) = IncludedLabel(feature.Id);
                        return new IncludedFeature(
                            feature.Id,
                            key,
                            defaultLabel,
                            feature.IconKey,
                            included.TryGetValue(feature.Id, out var n) ? n : 0,
                            total.TryGetValue(feature.Id, out var m) ? m : 0);
                    })
                    .ToList()))
            .ToList());
    }

    private void RefreshIncludedCorners()
    {
        var visible = new List<IncludedRowViewModel>();
        foreach (var group in IncludedGroups)
        {
            visible.Add(group);
            if (group.IsExpanded)
            {
                visible.AddRange(group.Children);
            }
        }

        for (int i = 0; i < visible.Count; i++)
        {
            visible[i].SetLastVisible(i == visible.Count - 1);
        }
    }

    // The format comes from 29 translated files, so a broken one must not throw.
    internal static string IncludedCountFormat(string format, int included, int total)
    {
        try
        {
            return string.Format(format, included, total);
        }
        catch (FormatException)
        {
            return $"{included}/{total}";
        }
    }

    internal static string IncludedLabelKey(string featureId) => IncludedLabel(featureId).Key;

    private static (string Key, string DefaultLabel) IncludedLabel(string featureId) =>
        IncludedLabels.TryGetValue(featureId, out var label)
            ? label
            : throw new ArgumentOutOfRangeException(
                nameof(featureId), featureId, "No summary row label is mapped for this feature.");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localizationService.LanguageChanged -= OnPageTextLanguageChanged;
            ReleaseIncluded();
        }
        base.Dispose(disposing);
    }

    private sealed record IncludedFeature(
        string FeatureId,
        string LabelKey,
        string DefaultLabel,
        string IconResourceKey,
        int Included,
        int Total);

    private sealed record IncludedGroup(
        string LabelKey,
        string DefaultLabel,
        string IconResourceKey,
        IReadOnlyList<IncludedFeature> Features)
    {
        public int Included => Features.Sum(f => f.Included);

        public int Total => Features.Sum(f => f.Total);
    }

    private sealed record IncludedSummary(IReadOnlyList<IncludedGroup> Groups)
    {
        public int Included => Groups.Sum(g => g.Included);

        public int Total => Groups.Sum(g => g.Total);
    }
}

public sealed partial class IncludedRowViewModel : ObservableObject
{
    private readonly Action _onExpansionChanged;
    private bool _isExpanded;
    private bool _isLastVisible;

    public IncludedRowViewModel(
        string key,
        string name,
        string countText,
        string iconResourceKey,
        IReadOnlyList<IncludedRowViewModel> children,
        Action onExpansionChanged)
    {
        Key = key;
        Name = name;
        CountText = countText;
        IconResourceKey = iconResourceKey;
        Children = children;
        _onExpansionChanged = onExpansionChanged;
    }

    public string Key { get; }

    public string Name { get; }

    public string CountText { get; }

    public string IconResourceKey { get; }

    public IReadOnlyList<IncludedRowViewModel> Children { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        private set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                _onExpansionChanged();
            }
        }
    }

    public Microsoft.UI.Xaml.CornerRadius CornerRadius => _isLastVisible
        ? new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4)
        : new Microsoft.UI.Xaml.CornerRadius(0);

    public void ToggleExpander() => IsExpanded = !IsExpanded;

    internal void SetLastVisible(bool isLastVisible)
    {
        if (_isLastVisible != isLastVisible)
        {
            _isLastVisible = isLastVisible;
            OnPropertyChanged(nameof(CornerRadius));
        }
    }
}
