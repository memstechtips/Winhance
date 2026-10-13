using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.WimUtil.Models;

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
        [FeatureIds.Explorer] = ("Feature_Explorer_Name", "Explorer"),
        [FeatureIds.TimeRegionLanguage] = ("Feature_TimeRegionLanguage_Name", "Time, region and language"),
    };

    private readonly ICatalogSettingsRegistry _registry;
    private readonly ICatalogScopeProvider _scope;
    private readonly IAppSelectionSource _apps;
    private readonly IWindowsAppsService _windowsApps;
    private readonly IExternalAppsService _externalApps;
    private readonly WimUtilSession _session;

    private ISubscriptionToken? _filterStateChangedSubscription;
    private SettingItemViewModel? _winhanceOnNewPc;
    private SettingItemViewModel? _ownInstallers;
    private SettingItemViewModel? _stageApps;
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
        IExternalAppsService externalApps,
        WimUtilSession session)
        : base(settingsLoadingService, logService, localizationService, dispatcherService, eventBus, applicationModeService)
    {
        _registry = registry;
        _scope = scope;
        _apps = apps;
        _windowsApps = windowsApps;
        _externalApps = externalApps;
        _session = session;
        _session.EditionsChanged += OnSessionEditionsChanged;
        _localizationService.LanguageChanged += OnPageTextLanguageChanged;
        // The base constructor assigns it; the nullable analysis of this constructor cannot see that.
        GroupedSettings!.CollectionChanged += OnGroupedSettingsChanged;
    }

    public override string ModuleId => FeatureIds.Autounattend;

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

        ShowMediaEditions();
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

    private void OnPageTextLanguageChanged(object? sender, EventArgs e) => TryRelabelIncluded();

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
        ExternalAppsRow()?.Attach(null, null);
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
            ExternalAppsRow()?.Attach(null, null);
            IncludedGroups.Clear();
            IncludedCountText = Loading;
            OnPropertyChanged(nameof(IncludedCornerRadius));
            return;
        }

        var format = _localizationService.GetStringOrDefault("Autounattend_Included_Count", "{0} of {1} included");
        var windowsAppsFormat = _localizationService.GetStringOrDefault(
            "Autounattend_Included_WindowsApps_Count",
            "{0} will be removed from Windows");

        // Built whole first, so a label lookup that throws cannot leave half a list on the page.
        var rebuilt = new List<IncludedRowViewModel>();

        foreach (var group in summary.Groups)
        {
            var features = group.Features
                .Select(feature => feature.FeatureId == FeatureIds.ExternalApps
                    ? NewExternalAppsRow(feature)
                    : new IncludedRowViewModel(
                        feature.FeatureId,
                        _localizationService.GetStringOrDefault(feature.LabelKey, feature.DefaultLabel),
                        IncludedCountFormat(
                            feature.FeatureId == FeatureIds.WindowsApps ? windowsAppsFormat : format,
                            feature.Included,
                            feature.Total),
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

        ExternalAppsRow()?.Attach(null, null);
        IncludedGroups.Clear();
        foreach (var row in rebuilt)
        {
            IncludedGroups.Add(row);
        }

        ExternalAppsRow()?.Attach(_ownInstallers, _stageApps);

        foreach (var row in IncludedGroups.Where(built => expanded.Contains(built.Key)))
        {
            row.ToggleExpander();
        }

        IncludedCountText = countText;
        OnPropertyChanged(nameof(IncludedCornerRadius));
        RefreshIncludedCorners();
        ShowWinhanceOnNewPc();
    }

    private ExternalAppsRowViewModel NewExternalAppsRow(IncludedFeature feature) =>
        new(
            feature.FeatureId,
            _localizationService.GetStringOrDefault(feature.LabelKey, feature.DefaultLabel),
            IncludedCountFormat(
                _localizationService.GetStringOrDefault(
                    "Autounattend_Included_ExternalApps_Count",
                    "{0} will be installed by Winhance"),
                feature.Included,
                feature.Total),
            feature.IconResourceKey,
            RefreshIncludedCorners,
            feature.Included)
        {
            WarningText = _localizationService.GetStringOrDefault(
                "Autounattend_Included_ExternalApps_NotInstalled",
                "'Winhance on the new PC' is set to 'Don't add', so these apps will not be installed."),
        };

    private ExternalAppsRowViewModel? ExternalAppsRow() =>
        IncludedGroups.SelectMany(group => group.Children).OfType<ExternalAppsRowViewModel>().FirstOrDefault();

    // Until the card loads it reads as its seed state, the desktop shortcut.
    private void ShowWinhanceOnNewPc()
    {
        if (ExternalAppsRow() is not { } row)
            return;

        var state = _winhanceOnNewPc?.CurrentStateLabel;
        bool installs = row.AppCount > 0 || _ownInstallers?.HasRows == true;
        row.IsWarning = installs && state == LocKey.Setting.AutounattendDesktopShortcut.Option0.Value;
        row.Description = !installs || row.IsWarning
            ? string.Empty
            : state == LocKey.Setting.AutounattendDesktopShortcut.Option2.Value
                ? _localizationService.GetStringOrDefault(
                    "Autounattend_Included_ExternalApps_Automatic",
                    "Installed automatically after setup")
                : _localizationService.GetStringOrDefault(
                    "Autounattend_Included_ExternalApps_Shortcut",
                    "Installed from the 'Install Winhance' desktop shortcut");
    }

    // The page's cards are rebuilt on every load, so the cards the External Software row reads are looked up again
    // whenever the groups change.
    private void OnGroupedSettingsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ShowMediaEditions();

        var cards = GroupedSettings.SelectMany(group => group).ToList();
        var winhance = cards.FirstOrDefault(setting => setting.SettingId == "autounattend-desktop-shortcut");
        var ownInstallers = cards.FirstOrDefault(setting => setting.SettingId == OwnInstaller.SettingId);
        var stageApps = cards.FirstOrDefault(setting => setting.SettingId == OwnInstaller.StageAppsId);
        if (ReferenceEquals(winhance, _winhanceOnNewPc)
            && ReferenceEquals(ownInstallers, _ownInstallers)
            && ReferenceEquals(stageApps, _stageApps))
            return;

        WatchWinhanceOnNewPc(winhance);
        WatchInstallerCards(ownInstallers, stageApps);
        ShowWinhanceOnNewPc();
    }

    private void WatchWinhanceOnNewPc(SettingItemViewModel? card)
    {
        if (_winhanceOnNewPc is not null)
            _winhanceOnNewPc.PropertyChanged -= OnWinhanceOnNewPcChanged;

        _winhanceOnNewPc = card;

        if (card is not null)
            card.PropertyChanged += OnWinhanceOnNewPcChanged;
    }

    private void OnWinhanceOnNewPcChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingItemViewModel.SelectedValue))
            ShowWinhanceOnNewPc();
    }

    private void WatchInstallerCards(SettingItemViewModel? ownInstallers, SettingItemViewModel? stageApps)
    {
        if (_ownInstallers is not null)
            _ownInstallers.PropertyChanged -= OnOwnInstallersChanged;

        _ownInstallers = ownInstallers;
        _stageApps = stageApps;

        if (ownInstallers is not null)
            ownInstallers.PropertyChanged += OnOwnInstallersChanged;

        ExternalAppsRow()?.Attach(ownInstallers, stageApps);
    }

    private void OnOwnInstallersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingItemViewModel.HasRows))
            ShowWinhanceOnNewPc();
    }

    // The External Software row draws the installer cards. The edition cards describe the image a WIMUtil session
    // is building, so they show only in a session started there.
    protected override bool IsShownOnPage(SettingItemViewModel setting) =>
        setting.SettingId is not (OwnInstaller.SettingId or OwnInstaller.StageAppsId)
        && (_session.Editions.Count > 0 || setting.Setting?.Targets.Any(target => target is AutounattendMedia) != true);

    // This feature is the whole page, so matching its name would show every card.
    protected override bool SearchMatchesFeatureName => false;

    public bool TakesQuickActions(SettingItemViewModel setting) =>
        IsShownOnPage(setting) && setting.Setting?.SeedsFromThisPc != true;

    private void OnSessionEditionsChanged(object? sender, EventArgs e) => ShowMediaEditions();

    private void ShowMediaEditions()
    {
        if (Settings is null)
            return;

        var names = _session.Editions.Select(edition => edition.Name).ToList();
        foreach (var card in Settings.Where(setting => setting.Setting?.CheckList?.Source == CheckListSource.MediaEditions))
        {
            if (!card.CheckItems.Select(item => item.Key).SequenceEqual(names))
                card.SetCheckListEntries(names);
        }

        RefreshDeclaredGates();
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
            GroupedSettings.CollectionChanged -= OnGroupedSettingsChanged;
            _session.EditionsChanged -= OnSessionEditionsChanged;
            WatchWinhanceOnNewPc(null);
            WatchInstallerCards(null, null);
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

public partial class IncludedRowViewModel : ObservableObject
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

// The staging tick and the installer list are two catalog cards the page keeps off its grouped list; this row draws them.
public sealed partial class ExternalAppsRowViewModel : IncludedRowViewModel
{
    private SettingItemViewModel? _ownInstallers;
    private SettingItemViewModel? _stageApps;

    public ExternalAppsRowViewModel(
        string key,
        string name,
        string countText,
        string iconResourceKey,
        Action onExpansionChanged,
        int appCount)
        : base(key, name, countText, iconResourceKey, [], onExpansionChanged)
    {
        AppCount = appCount;
    }

    internal int AppCount { get; }

    [ObservableProperty]
    public partial string Description { get; internal set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsWarning { get; internal set; }

    public string WarningText { get; init; } = string.Empty;

    public bool HasInstallerCards => _ownInstallers is not null && _stageApps is not null;

    public bool StageOnMedia
    {
        get => _stageApps?.IsSelected ?? true;
        set => _stageApps?.OnCheckBoxChanged(value);
    }

    public string StageOnMediaLabel => _stageApps?.Name ?? string.Empty;

    public string OwnInstallersName => _ownInstallers?.Name ?? string.Empty;

    public string OwnInstallersDescription => _ownInstallers?.Description ?? string.Empty;

    public string AddOwnInstallerLabel => _ownInstallers?.ListAddLabel ?? string.Empty;

    public ObservableCollection<ListRowViewModel>? OwnInstallerRows => _ownInstallers?.Rows;

    public bool HasOwnInstallers => _ownInstallers?.HasRows == true;

    public void AddOwnInstaller() => _ownInstallers?.AddRow();

    // Rows and cards are rebuilt at different times, so either side can arrive first and either can be replaced.
    internal void Attach(SettingItemViewModel? ownInstallers, SettingItemViewModel? stageApps)
    {
        if (_ownInstallers is not null)
            _ownInstallers.PropertyChanged -= OnCardChanged;
        if (_stageApps is not null)
            _stageApps.PropertyChanged -= OnCardChanged;

        _ownInstallers = ownInstallers;
        _stageApps = stageApps;

        if (ownInstallers is not null)
            ownInstallers.PropertyChanged += OnCardChanged;
        if (stageApps is not null)
            stageApps.PropertyChanged += OnCardChanged;

        ShowCards();
    }

    private void OnCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingItemViewModel.IsSelected)
            or nameof(SettingItemViewModel.HasRows)
            or nameof(SettingItemViewModel.Name)
            or nameof(SettingItemViewModel.Description)
            or nameof(SettingItemViewModel.ListAddLabel))
            ShowCards();
    }

    private void ShowCards()
    {
        OnPropertyChanged(nameof(HasInstallerCards));
        OnPropertyChanged(nameof(StageOnMedia));
        OnPropertyChanged(nameof(StageOnMediaLabel));
        OnPropertyChanged(nameof(OwnInstallersName));
        OnPropertyChanged(nameof(OwnInstallersDescription));
        OnPropertyChanged(nameof(AddOwnInstallerLabel));
        OnPropertyChanged(nameof(OwnInstallerRows));
        OnPropertyChanged(nameof(HasOwnInstallers));
    }
}
