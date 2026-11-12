using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Autounattend;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.WimUtil.Models;

namespace Winhance.UI.Features.Autounattend.ViewModels;

public partial class AutounattendViewModel : BaseSettingsFeatureViewModel
{
    // Punctuation rather than a word, so it needs no locale key.
    internal const string Loading = "...";

    private readonly ICatalogSettingsRegistry _registry;
    private readonly ICatalogScopeProvider _scope;
    private readonly IAppSelectionSource _apps;
    private readonly WimUtilSession _session;
    private readonly ISystemInfoProvider _systemInfo;
    private readonly IBuilderModeEntry _builderEntry;

    private static readonly BuilderSeed[] SeedOrder =
        [BuilderSeed.CurrentMachine, BuilderSeed.Recommended, BuilderSeed.WindowsDefaults, BuilderSeed.ConfigFile];

    private ISubscriptionToken? _filterStateChangedSubscription;
    private SettingItemViewModel? _ownInstallers;
    private SettingItemViewModel? _stageApps;
    private SettingItemViewModel? _computerName;
    private SettingItemViewModel? _uniqueName;
    private string? _uniqueNameDescription;
    private bool _reseeding;
    private string? _thisPcEnding;
    private IncludedSummary? _summary;
    private int _loadVersion;
    private int _includedHosts;
    private bool _installsApps;
    private bool _winhanceDecided;
    private bool _showsModeTags;
    private SettingItemViewModel? _netfx3;
    private SettingItemViewModel? _netfx3RemoveSource;
    private string? _netfx3RemoveSourceDescription;

    private const string Netfx3SettingId = "autounattend-netfx3";
    private const string Netfx3RemoveSourceId = "autounattend-netfx3-remove-source";

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
        WimUtilSession session,
        ISystemInfoProvider systemInfo,
        IBuilderModeEntry builderEntry)
        : base(settingsLoadingService, logService, localizationService, dispatcherService, eventBus, applicationModeService)
    {
        _registry = registry;
        _scope = scope;
        _apps = apps;
        _session = session;
        _systemInfo = systemInfo;
        _builderEntry = builderEntry;
        RemovedApps = new IncludedAppsCardViewModel(
            "SoftwareApps", FeatureIds.WindowsApps, FeatureDefinitions.Get(FeatureIds.WindowsApps)!.IconKey, RequestPage);
        InstalledApps = new InstalledAppsCardViewModel(
            "SoftwareApps", FeatureIds.ExternalApps, FeatureDefinitions.Get(FeatureIds.ExternalApps)!.IconKey, RequestPage);
        OptimizeSettings = new IncludedCardViewModel("Optimize", null, FeatureDefinitions.CategoryIconKey("Optimize"), RequestPage);
        CustomizeSettings = new IncludedCardViewModel("Customize", null, FeatureDefinitions.CategoryIconKey("Customize"), RequestPage);
        _session.EditionsChanged += OnSessionEditionsChanged;
        _session.BuiltIntoImageChanged += OnSessionBuiltIntoImageChanged;
        _localizationService.LanguageChanged += OnPageTextLanguageChanged;
        _builderEntry.StartedFromChanged += OnStartedFromChanged;
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

    public bool ShowsStartedFrom => _builderEntry.StartedFrom is not null;

    public string StartedFromHeader => Text("Builder_StartedFrom_Name", "Started from");

    public string StartedFromDescription => Text(
        "Builder_StartedFrom_Description",
        "Pick another start to begin again. Builder changes made so far are lost.");

    public IReadOnlyList<string> StartSeedOptions => SeedOrder.Select(SeedLabel).ToList();

    public int SelectedSeedIndex => _builderEntry.StartedFrom is { } seed ? Array.IndexOf(SeedOrder, seed) : -1;

    public async Task ChangeSeedAsync(int index)
    {
        // A second pick while the confirmation is open would open a second ContentDialog, which WinUI refuses.
        if (_reseeding || index < 0 || index >= SeedOrder.Length || index == SelectedSeedIndex)
            return;

        _reseeding = true;
        try
        {
            if (await _builderEntry.ReseedAsync(SeedOrder[index]))
                await RefreshIncludedAsync();
        }
        finally
        {
            _reseeding = false;

            // A declined change leaves the box showing the pick, so it is set back to the session's seed.
            OnPropertyChanged(nameof(SelectedSeedIndex));
        }
    }

    private string SeedLabel(BuilderSeed seed) => seed switch
    {
        BuilderSeed.CurrentMachine => Text("Dialog_BuilderSeed_CurrentMachine", "The current state of this machine"),
        BuilderSeed.Recommended => Text("Dialog_BuilderSeed_Recommended", "All Winhance recommended settings"),
        BuilderSeed.WindowsDefaults => Text("Dialog_BuilderSeed_WindowsDefaults", "All Windows default settings"),
        _ => Text("Dialog_BuilderSeed_ConfigFile", "A saved Winhance config or answer file"),
    };

    private void OnStartedFromChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ShowsStartedFrom));
        OnPropertyChanged(nameof(SelectedSeedIndex));
    }

    public IncludedAppsCardViewModel RemovedApps { get; }

    public InstalledAppsCardViewModel InstalledApps { get; }

    public IncludedCardViewModel OptimizeSettings { get; }

    public IncludedCardViewModel CustomizeSettings { get; }

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

    private void RequestPage(string pageTag, string? featureId) =>
        _eventBus.Publish(new PageRequestedEvent { PageTag = pageTag, FeatureId = featureId });

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

        ShowWinhanceOnNewPc();
        ShowModeTags();
    }

    // The WIMUtil page shows where each change lands; the Autounattend page has no image, so it shows none.
    public void ShowModeTags(bool show)
    {
        _showsModeTags = show;
        ShowModeTags();
    }

    private void ShowModeTags()
    {
        var inImage = _showsModeTags && _session.BuiltIntoImage ? Text("WIMUtil_Tag_InImage", "In the image") : string.Empty;
        var duringSetup = _showsModeTags ? Text("WIMUtil_Tag_DuringSetup", "During setup") : string.Empty;
        var imageOrSetup = _session.BuiltIntoImage ? inImage : duringSetup;

        RemovedApps.ShowTag(imageOrSetup);
        OptimizeSettings.ShowTag(imageOrSetup);
        CustomizeSettings.ShowTag(imageOrSetup);
        InstalledApps.ShowTag(duringSetup);
    }

    private void LogIncludedFailure(Exception ex) =>
        _logService.LogWarning($"Autounattend summary could not be built: {ex.Message}");

    private void ShowIncludedPlaceholder()
    {
        _summary = null;
        foreach (var card in new IncludedCardViewModel[] { RemovedApps, InstalledApps, OptimizeSettings, CustomizeSettings })
            card.Show(Loading, string.Empty, string.Empty);
        RemovedApps.ShowNames([], string.Empty, string.Empty);
        InstalledApps.ShowNames([], string.Empty, string.Empty);
    }

    private void RelabelIncluded()
    {
        OnPropertyChanged(nameof(IncludedTitle));
        OnPropertyChanged(nameof(IncludedDescription));
        OnPropertyChanged(nameof(StartedFromHeader));
        OnPropertyChanged(nameof(StartedFromDescription));
        OnPropertyChanged(nameof(StartSeedOptions));
        OnPropertyChanged(nameof(SelectedSeedIndex));

        if (_summary is not { } summary)
        {
            ShowIncludedPlaceholder();
            return;
        }

        // Every lookup first, so one that throws cannot leave half the cards relabelled.
        var softwareApps = Text("Nav_SoftwareAndApps", "Software & Apps");
        var optimize = Text("Nav_Optimize", "Optimize");
        var customize = Text("Nav_Customize", "Customize");
        var seeList = Text("Autounattend_Included_SeeList", "See list");
        var hideList = Text("Autounattend_Included_HideList", "Hide list");

        int installs = summary.InstalledApps.Count + (_ownInstallers?.Rows.Count ?? 0);
        var removedHeader = CountText(
            summary.RemovedApps.Count,
            ("Autounattend_Included_WindowsApps_None", "No Windows apps removed"),
            ("Autounattend_Included_WindowsApps_One", "1 Windows app removed"),
            ("Autounattend_Included_WindowsApps", "{0} Windows apps removed"));
        var installedHeader = CountText(
            installs,
            ("Autounattend_Included_ExternalApps_None", "No apps installed after setup"),
            ("Autounattend_Included_ExternalApps_One", "1 app installed after setup"),
            ("Autounattend_Included_ExternalApps", "{0} apps installed after setup"));
        var removedDescription = summary.RemovedApps.Count > 0
            ? Preview(summary.RemovedApps)
            : Text("Autounattend_Included_WindowsApps_Hint", "Tick apps on Windows Apps & Features to remove them during setup.");
        var installedDescription = summary.InstalledApps.Count > 0
            ? Preview(summary.InstalledApps)
            : installs > 0
                ? string.Empty
                : Text("Autounattend_Included_ExternalApps_Hint", "Tick apps on External Software, or add your own installers below.");
        var openSoftwareApps = Format("Autounattend_Included_Change", "Change in {0}", softwareApps);
        var openOptimize = Format("Autounattend_Included_Change", "Change in {0}", optimize);
        var openCustomize = Format("Autounattend_Included_Change", "Change in {0}", customize);
        var optimizeDescription = SettingsText(summary.Optimize);
        var customizeDescription = SettingsText(summary.Customize);

        RemovedApps.Show(removedHeader, removedDescription, openSoftwareApps);
        RemovedApps.ShowNames(summary.RemovedApps, seeList, hideList);
        InstalledApps.Show(installedHeader, installedDescription, openSoftwareApps);
        InstalledApps.ShowNames(summary.InstalledApps, seeList, hideList);
        OptimizeSettings.Show(optimize, optimizeDescription, openOptimize);
        CustomizeSettings.Show(customize, customizeDescription, openCustomize);
        _session.Changes = new(summary.RemovedApps.Count, installs, summary.Optimize.Included + summary.Customize.Included);
    }

    private string Text(string key, string english) => _localizationService.GetStringOrDefault(key, english);

    // The format comes from 29 translated files, so a broken one falls back to the English rather than throwing.
    private string Format(string key, string english, params object[] args)
    {
        try
        {
            return string.Format(Text(key, english), args);
        }
        catch (FormatException)
        {
            return string.Format(english, args);
        }
    }

    private string CountText(int count, (string Key, string English) none, (string Key, string English) one, (string Key, string English) many) =>
        count switch
        {
            0 => Text(none.Key, none.English),
            1 => Text(one.Key, one.English),
            _ => Format(many.Key, many.English, count),
        };

    private string Preview(IReadOnlyList<string> names) =>
        names.Count <= IncludedAppsCardViewModel.PreviewNames
            ? string.Join(", ", names)
            : Format("Autounattend_Included_More", "{0} and {1} more", string.Join(", ", names.Take(IncludedAppsCardViewModel.PreviewNames)), names.Count - IncludedAppsCardViewModel.PreviewNames);

    private string SettingsText(IncludedCount count) => (count.Included == count.Total, count.Included == 1) switch
    {
        (true, true) => Format("Autounattend_Included_Settings_One", "{0} setting set on the new PC", count.Included),
        (true, false) => Format("Autounattend_Included_Settings", "{0} settings set on the new PC", count.Included),
        (false, true) => Format("Autounattend_Included_Settings_One_LeftOut", "{0} setting set on the new PC, {1} left out", count.Included, count.Total - count.Included),
        (false, false) => Format("Autounattend_Included_Settings_LeftOut", "{0} settings set on the new PC, {1} left out", count.Included, count.Total - count.Included),
    };

    // Apps on the file need Winhance on the new PC to install them, so they decide the card that installs it.
    private void ShowWinhanceOnNewPc()
    {
        bool installs = _summary?.InstalledApps.Count > 0 || _ownInstallers?.HasRows == true;
        bool decided = installs || KeepsRemovedAppsWithWinhance();
        if (installs == _installsApps && decided == _winhanceDecided)
            return;

        _installsApps = installs;
        _winhanceDecided = decided;
        RefreshDeclaredGates();
    }

    // Mirrors WinhanceOnNewPc.KeepsRemovedAppsWithWinhance, read from the live cards instead of a selection set.
    private bool KeepsRemovedAppsWithWinhance() =>
        _summary?.RemovedApps.Count > 0
        && Settings?.FirstOrDefault(setting => setting.SettingId == WinhanceOnNewPc.KeepRemovedId)?.CurrentStateLabel
            == LocKey.Setting.AutounattendKeepRemoved.Option2.Value;

    // Setup installs .NET from the media's source files, so a file applied during setup cannot ask to delete them.
    private bool NeedsNetfx3Source() =>
        !_session.BuiltIntoImage
        && Settings?.FirstOrDefault(setting => setting.SettingId == Netfx3SettingId)?.CurrentStateLabel
            == LocKey.Common.Enabled.Value;

    protected override LocKey? DecidedByPage(SettingItemViewModel setting)
    {
        if (setting.SettingId == Netfx3RemoveSourceId && NeedsNetfx3Source())
            return LocKey.Common.Disabled;

        // The image build cleans up offline, so the file adds no second cleanup on the new PC.
        if (setting.SettingId == AutounattendCleanup.SettingId && _session.BuiltIntoImage)
            return LocKey.Common.Disabled;

        // The keep-removed card changes only refresh the gates, so it is read live here, never latched in _installsApps.
        return (_installsApps || KeepsRemovedAppsWithWinhance()) && setting.SettingId == WinhanceOnNewPc.SettingId ? WinhanceOnNewPc.InstallsAutomatically : null;
    }

    protected override string? DisabledReasonFor(SettingItemViewModel setting) => setting.SettingId switch
    {
        Netfx3RemoveSourceId => string.Format(
            _localizationService.GetString("Autounattend_Disabled_Netfx3Source"),
            Settings?.FirstOrDefault(card => card.SettingId == Netfx3SettingId)?.Name),
        AutounattendCleanup.SettingId => _localizationService.GetString("Autounattend_Disabled_CleanupInImage"),
        WinhanceOnNewPc.SettingId => _localizationService.GetString("Autounattend_Disabled_WinhanceInstalls"),
        _ => null,
    };

    // The page's cards are rebuilt on every load, so the installer cards are looked up again whenever the groups change.
    private void OnGroupedSettingsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ShowMediaEditions();
        WatchComputerName();
        WatchNetfx3();

        var cards = GroupedSettings.SelectMany(group => group).ToList();
        var ownInstallers = cards.FirstOrDefault(setting => setting.SettingId == OwnInstaller.SettingId);
        var stageApps = cards.FirstOrDefault(setting => setting.SettingId == OwnInstaller.StageAppsId);
        if (ReferenceEquals(ownInstallers, _ownInstallers) && ReferenceEquals(stageApps, _stageApps))
            return;

        WatchInstallerCards(ownInstallers, stageApps);
        TryRelabelIncluded();
    }

    private void WatchInstallerCards(SettingItemViewModel? ownInstallers, SettingItemViewModel? stageApps)
    {
        if (_ownInstallers is not null)
            _ownInstallers.PropertyChanged -= OnOwnInstallersChanged;

        _ownInstallers = ownInstallers;
        _stageApps = stageApps;

        if (ownInstallers is not null)
            ownInstallers.PropertyChanged += OnOwnInstallersChanged;

        InstalledApps.Attach(ownInstallers, stageApps);
    }

    // Raised on every add and remove, so the count follows each one.
    private void OnOwnInstallersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingItemViewModel.HasRows))
            TryRelabelIncluded();
    }

    // A child card is not in GroupedSettings, so both name cards come from Settings.
    private void WatchComputerName()
    {
        var name = Settings?.FirstOrDefault(setting => setting.SettingId == UniqueComputerName.NameSettingId);
        var unique = Settings?.FirstOrDefault(setting => setting.SettingId == UniqueComputerName.SettingId);
        if (ReferenceEquals(name, _computerName) && ReferenceEquals(unique, _uniqueName))
            return;

        if (_computerName is not null)
            _computerName.PropertyChanged -= OnComputerNameChanged;

        _computerName = name;
        _uniqueName = unique;
        _uniqueNameDescription = unique?.Description;

        if (name is not null)
            name.PropertyChanged += OnComputerNameChanged;

        _ = ShowNamePreviewAsync();
    }

    private void WatchNetfx3()
    {
        var netfx3 = Settings?.FirstOrDefault(setting => setting.SettingId == Netfx3SettingId);
        var removeSource = Settings?.FirstOrDefault(setting => setting.SettingId == Netfx3RemoveSourceId);
        if (ReferenceEquals(netfx3, _netfx3) && ReferenceEquals(removeSource, _netfx3RemoveSource))
            return;

        if (_netfx3 is not null)
            _netfx3.PropertyChanged -= OnNetfx3Changed;

        _netfx3 = netfx3;
        _netfx3RemoveSource = removeSource;
        _netfx3RemoveSourceDescription = removeSource?.Description;

        if (netfx3 is not null)
            netfx3.PropertyChanged += OnNetfx3Changed;

        ShowNetfx3SourceNote();
    }

    private void OnNetfx3Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingItemViewModel.IsSelected) or nameof(SettingItemViewModel.SelectedValue))
            ShowNetfx3SourceNote();
    }

    private void ShowNetfx3SourceNote()
    {
        if (_netfx3RemoveSource is not { } card || _netfx3RemoveSourceDescription is null)
            return;

        card.Description = NeedsNetfx3Source()
            ? _netfx3RemoveSourceDescription + "\n" + Text("Autounattend_Netfx3Source_Needed", "Setup needs these files to install .NET Framework.")
            : _netfx3RemoveSourceDescription;
    }

    private void OnComputerNameChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingItemViewModel.TextValue))
            _ = ShowNamePreviewAsync();
    }

    // This PC's ending is read once and kept, so the preview does not change while the user types.
    private async Task ShowNamePreviewAsync()
    {
        _thisPcEnding ??= await Task.Run(() =>
            UniqueComputerName.Ending(_systemInfo.BiosSerialNumber(), _systemInfo.NetworkCardAddress())
            ?? UniqueComputerName.RandomEnding());
        if (_uniqueName is not { } card || _uniqueNameDescription is null)
            return;

        var name = UniqueComputerName.Name(_computerName?.TextValue, _thisPcEnding);
        card.Description = _uniqueNameDescription + "\n" + Format("Autounattend_ComputerName_Preview", "On this PC it would be {0}.", name);
    }

    // The apps card draws the installer cards. The edition cards describe the image a WIMUtil session is building,
    // so they show only in a session started there.
    protected override bool IsShownOnPage(SettingItemViewModel setting) =>
        setting.SettingId is not (OwnInstaller.SettingId or OwnInstaller.StageAppsId)
        && (_session.Editions.Count > 0 || setting.Setting?.Targets.Any(target => target is AutounattendMedia) != true);

    // This feature is the whole page, so matching its name would show every card.
    protected override bool SearchMatchesFeatureName => false;

    public bool TakesQuickActions(SettingItemViewModel setting) =>
        IsShownOnPage(setting) && setting.Setting?.SeedsFromThisPc != true;

    private void OnSessionEditionsChanged(object? sender, EventArgs e) => ShowMediaEditions();

    private void OnSessionBuiltIntoImageChanged(object? sender, EventArgs e)
    {
        ShowModeTags();
        ShowWinhanceOnNewPc();
        ShowNetfx3SourceNote();
        RefreshDeclaredGates();
    }

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

        var removed = (await _apps.CheckedWindowsAppsAsync()).Select(app => app.Name).ToList();
        var installed = (await _apps.CheckedExternalAppsAsync()).Select(app => app.Name).ToList();
        return new IncludedSummary(
            removed,
            installed,
            CountSettings(FeatureDefinitions.OptimizeFeatures),
            CountSettings(FeatureDefinitions.CustomizeFeatures));
    }

    private IncludedCount CountSettings(HashSet<string> features)
    {
        var scope = _scope.Current;

        // The saved file can carry fewer: SettingSnapshotSource drops a setting whose live state could not be read.
        var inScope = SettingCatalog.ByFeature.Keys
            .Where(features.Contains)
            .SelectMany(featureId => _registry.GetByFeature(featureId, scope))
            .ToList();
        return new IncludedCount(inScope.Count(setting => _applicationModeService.IsIncluded(setting.Id)), inScope.Count);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localizationService.LanguageChanged -= OnPageTextLanguageChanged;
            _builderEntry.StartedFromChanged -= OnStartedFromChanged;
            GroupedSettings.CollectionChanged -= OnGroupedSettingsChanged;
            _session.EditionsChanged -= OnSessionEditionsChanged;
            _session.BuiltIntoImageChanged -= OnSessionBuiltIntoImageChanged;
            WatchInstallerCards(null, null);
            if (_netfx3 is not null)
                _netfx3.PropertyChanged -= OnNetfx3Changed;
            if (_computerName is not null)
                _computerName.PropertyChanged -= OnComputerNameChanged;
            ReleaseIncluded();
        }
        base.Dispose(disposing);
    }

    private sealed record IncludedCount(int Included, int Total);

    private sealed record IncludedSummary(
        IReadOnlyList<string> RemovedApps,
        IReadOnlyList<string> InstalledApps,
        IncludedCount Optimize,
        IncludedCount Customize);
}

public partial class IncludedCardViewModel(string pageTag, string? featureId, string iconResourceKey, Action<string, string?> openPage)
    : ObservableObject
{
    public string PageTag => pageTag;

    public string? FeatureId => featureId;

    public string IconResourceKey => iconResourceKey;

    [ObservableProperty]
    public partial string Header { get; private set; } = AutounattendViewModel.Loading;

    [ObservableProperty]
    public partial string Description { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string OpenPageLabel { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Tag { get; private set; } = string.Empty;

    public void OpenPage() => openPage(pageTag, featureId);

    internal void ShowTag(string tag) => Tag = tag;

    internal void Show(string header, string description, string openPageLabel)
    {
        Header = header;
        Description = description;
        OpenPageLabel = openPageLabel;
    }
}

// The full list sits in a child card, so the description only has to name the first few.
public partial class IncludedAppsCardViewModel(string pageTag, string? featureId, string iconResourceKey, Action<string, string?> openPage)
    : IncludedCardViewModel(pageTag, featureId, iconResourceKey, openPage)
{
    // Past this many names the description points at the full list instead.
    internal const int PreviewNames = 3;

    private string _seeList = string.Empty;
    private string _hideList = string.Empty;

    [ObservableProperty]
    public partial string Names { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasList { get; private set; }

    [ObservableProperty]
    public partial bool IsListOpen { get; private set; }

    public string ListToggleLabel => IsListOpen ? _hideList : _seeList;

    public bool ShowsList => HasList && IsListOpen;

    public Microsoft.UI.Xaml.CornerRadius CornerRadius => HasChildren
        ? new Microsoft.UI.Xaml.CornerRadius(4, 4, 0, 0)
        : new Microsoft.UI.Xaml.CornerRadius(4);

    public Microsoft.UI.Xaml.CornerRadius ListCornerRadius => ListIsLast
        ? new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4)
        : new Microsoft.UI.Xaml.CornerRadius(0);

    protected virtual bool HasChildren => ShowsList;

    protected virtual bool ListIsLast => true;

    public void ToggleList() => IsListOpen = !IsListOpen;

    internal void ShowNames(IReadOnlyList<string> names, string seeList, string hideList)
    {
        _seeList = seeList;
        _hideList = hideList;
        Names = string.Join(", ", names);
        HasList = names.Count > PreviewNames;
        OnPropertyChanged(nameof(ListToggleLabel));
    }

    partial void OnHasListChanged(bool value) => ShowCorners();

    partial void OnIsListOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(ListToggleLabel));
        ShowCorners();
    }

    protected void ShowCorners()
    {
        OnPropertyChanged(nameof(ShowsList));
        OnPropertyChanged(nameof(CornerRadius));
        OnPropertyChanged(nameof(ListCornerRadius));
    }
}

// The staging tick and the installer list are two catalog cards the page keeps off its grouped list; this card draws
// them under the apps, so they show without expanding anything.
public sealed partial class InstalledAppsCardViewModel(string pageTag, string? featureId, string iconResourceKey, Action<string, string?> openPage)
    : IncludedAppsCardViewModel(pageTag, featureId, iconResourceKey, openPage)
{
    private SettingItemViewModel? _ownInstallers;
    private SettingItemViewModel? _stageApps;

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

    public Microsoft.UI.Xaml.CornerRadius OwnInstallersCornerRadius => HasOwnInstallers
        ? new Microsoft.UI.Xaml.CornerRadius(0)
        : new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4);

    protected override bool HasChildren => ShowsList || HasInstallerCards;

    protected override bool ListIsLast => !HasInstallerCards;

    public void AddOwnInstaller() => _ownInstallers?.AddRow();

    // The page's cards are rebuilt on every load, so either one can be replaced or not loaded yet.
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
        OnPropertyChanged(nameof(OwnInstallersCornerRadius));
        ShowCorners();
    }
}
