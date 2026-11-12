using System.Text.Json;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Autounattend;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.TestSupport;
using Winhance.UI.Features.Autounattend.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.WimUtil.Models;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class AutounattendViewModelTests
{
    private readonly Mock<ISettingsLoadingService> _settingsLoading = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<IDispatcherService> _dispatcher = new();
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly Mock<IApplicationModeService> _mode = new();
    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<ICatalogScopeProvider> _scope = new();
    private readonly Mock<IAppSelectionSource> _apps = new();
    private readonly Mock<ISubscriptionToken> _subscription = new();
    private readonly Mock<IFilePickerService> _filePicker = new();
    private readonly Mock<IApplicationModeService> _builder = new();
    private readonly Dictionary<string, SettingChoice> _authored = new();
    private readonly WimUtilSession _session = new();
    private readonly Mock<ISystemInfoProvider> _systemInfo = new();
    private readonly Mock<IBuilderModeEntry> _builderEntry = new();
    private Func<FilterStateChangedEvent, Task>? _filterStateChangedHandler;

    private static readonly int OptimizeSettings = SettingCatalog.ByFeature
        .Where(kv => FeatureDefinitions.OptimizeFeatures.Contains(kv.Key))
        .Sum(kv => kv.Value.Count);

    private static readonly int CustomizeSettings = SettingCatalog.ByFeature
        .Where(kv => FeatureDefinitions.CustomizeFeatures.Contains(kv.Key))
        .Sum(kv => kv.Value.Count);

    public AutounattendViewModelTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => $"[{key}]");
        _localization.MirrorTryGetString();

        _dispatcher.Setup(d => d.RunOnUIThread(It.IsAny<Action>())).Callback<Action>(action => action());
        _dispatcher.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(action => action());

        _scope.SetupGet(s => s.Current).Returns(CatalogScope.CurrentMachine);
        _registry.Setup(r => r.InitializeAsync()).Returns(Task.CompletedTask);
        _mode.Setup(m => m.IsIncluded(It.IsAny<string>())).Returns(true);
        _registry.Setup(r => r.GetByFeature(It.IsAny<string>(), It.IsAny<CatalogScope>()))
            .Returns((string featureId, CatalogScope _) => SettingCatalog.ByFeature[featureId]);

        _apps.Setup(a => a.CheckedWindowsAppsAsync())
            .ReturnsAsync((IReadOnlyList<AppChoice>)[App("a"), App("b")]);
        _apps.Setup(a => a.CheckedExternalAppsAsync())
            .ReturnsAsync((IReadOnlyList<AppChoice>)[App("c")]);

        _eventBus
            .Setup(e => e.SubscribeAsync(It.IsAny<Func<FilterStateChangedEvent, Task>>()))
            .Callback<Func<FilterStateChangedEvent, Task>>(handler => _filterStateChangedHandler = handler)
            .Returns(_subscription.Object);
    }

    private Task RaiseFilterStateChanged() =>
        _filterStateChangedHandler!(new FilterStateChangedEvent(false));

    private void RaiseLanguageChanged() =>
        _localization.Raise(l => l.LanguageChanged += null, this, EventArgs.Empty);

    private static AppChoice App(string id) => new(id, id, null, null, null, null);

    private static Dictionary<string, string> EnglishStrings() =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(RepoPaths.LocalizationDir(), "en.json")))!;

    private AutounattendViewModel CreateSut() => new(
        _settingsLoading.Object,
        _log.Object,
        _localization.Object,
        _dispatcher.Object,
        _eventBus.Object,
        _mode.Object,
        _registry.Object,
        _scope.Object,
        _apps.Object,
        _session,
        _systemInfo.Object,
        _builderEntry.Object);

    [Fact]
    public void StartedFromCard_ShowsOnlyWhileASeedIsKnown_AndSelectsItsOption()
    {
        var sut = CreateSut();
        sut.ShowsStartedFrom.Should().BeFalse();
        sut.SelectedSeedIndex.Should().Be(-1);

        _builderEntry.SetupGet(b => b.StartedFrom).Returns(BuilderSeed.WindowsDefaults);
        _builderEntry.Raise(b => b.StartedFromChanged += null, EventArgs.Empty);

        sut.ShowsStartedFrom.Should().BeTrue();
        sut.StartSeedOptions[sut.SelectedSeedIndex].Should().Be("All Windows default settings");
    }

    [Fact]
    public async Task PickingAnotherSeed_ReseedsOnce_AndRefreshesTheSummary()
    {
        _builderEntry.SetupGet(b => b.StartedFrom).Returns(BuilderSeed.CurrentMachine);
        _builderEntry.Setup(b => b.ReseedAsync(BuilderSeed.Recommended)).ReturnsAsync(true);

        await CreateSut().ChangeSeedAsync(1);

        _builderEntry.Verify(b => b.ReseedAsync(BuilderSeed.Recommended), Times.Once);
        _apps.Verify(a => a.CheckedWindowsAppsAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PickingTheSeedItStartedFrom_AsksNothing()
    {
        _builderEntry.SetupGet(b => b.StartedFrom).Returns(BuilderSeed.Recommended);

        await CreateSut().ChangeSeedAsync(1);

        _builderEntry.Verify(b => b.ReseedAsync(It.IsAny<BuilderSeed>()), Times.Never);
    }

    [Fact]
    public async Task ADeclinedReseed_PutsTheBoxBackOnTheSessionsSeed()
    {
        _builderEntry.SetupGet(b => b.StartedFrom).Returns(BuilderSeed.CurrentMachine);
        _builderEntry.Setup(b => b.ReseedAsync(It.IsAny<BuilderSeed>())).ReturnsAsync(false);
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await sut.ChangeSeedAsync(2);

        changed.Should().Contain(nameof(AutounattendViewModel.SelectedSeedIndex));
        sut.SelectedSeedIndex.Should().Be(0);
        _apps.Verify(a => a.CheckedWindowsAppsAsync(), Times.Never);
    }

    [Fact]
    public async Task ASecondPickWhileTheConfirmationIsOpen_IsIgnored()
    {
        _builderEntry.SetupGet(b => b.StartedFrom).Returns(BuilderSeed.CurrentMachine);
        var confirmation = new TaskCompletionSource<bool>();
        _builderEntry.Setup(b => b.ReseedAsync(It.IsAny<BuilderSeed>())).Returns(confirmation.Task);
        var sut = CreateSut();

        var first = sut.ChangeSeedAsync(1);
        await sut.ChangeSeedAsync(2);
        confirmation.SetResult(false);
        await first;

        _builderEntry.Verify(b => b.ReseedAsync(It.IsAny<BuilderSeed>()), Times.Once);
    }

    // The page's own card, driven the way the page reads it: a Selection's SelectedValue is its state index.
    private SettingItemViewModel WinhanceOnNewPcCard(int state)
    {
        var setting = SettingCatalog.Find("autounattend-desktop-shortcut")!;
        var dialogs = new Mock<IDialogService>();
        return new SettingItemViewModel(
            new SettingItemViewModelConfig
            {
                Setting = setting,
                SettingId = setting.Id,
                Name = "Winhance on the new PC",
                Description = string.Empty,
                InputType = InputType.Selection,
            },
            SettingWriteStrategies.Selector(
                new Mock<ISettingApplicationService>().Object, dialogs.Object, _localization.Object, _log.Object),
            _log.Object,
            _dispatcher.Object,
            dialogs.Object,
            _localization.Object)
        {
            SelectedValue = state,
        };
    }

    [Fact]
    public void ModuleId_Always_IsTheAutounattendFeature()
    {
        CreateSut().ModuleId.Should().Be(FeatureIds.Autounattend);
    }

    [Fact]
    public void DisplayName_Always_ComesFromFeatureNameKey()
    {
        _localization.PresentKey("Feature_Autounattend_Name", "Unattend");

        CreateSut().DisplayName.Should().Be("Unattend");
    }

    [Fact]
    public async Task SearchText_InsidePageName_StillFiltersCards()
    {
        _localization.PresentKey("Feature_Autounattend_Name", "Autounattend");
        var sut = CreateSut();
        var card = WinhanceOnNewPcCard(0);
        sut.Settings.Add(card);
        var filtered = new TaskCompletionSource();
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(sut.HasVisibleSettings))
                filtered.TrySetResult();
        };

        sut.SearchText = "auto";
        await filtered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        card.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void Constructor_Always_StartsWithNoCardsAndNotLoading()
    {
        var sut = CreateSut();

        sut.Settings.Should().BeEmpty();
        sut.GroupedSettings.Should().BeEmpty();
        sut.IsLoading.Should().BeFalse();
        sut.SettingsCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        act.Should().NotThrow();
    }

    private IncludedCardViewModel[] Cards(AutounattendViewModel sut) =>
        [sut.RemovedApps, sut.InstalledApps, sut.OptimizeSettings, sut.CustomizeSettings];

    [Fact]
    public void Header_BeforeTheLoad_IsAPlaceholderOnEveryCard()
    {
        var sut = CreateSut();

        Cards(sut).Select(card => card.Header).Should().AllBe("...");
        sut.RemovedApps.HasList.Should().BeFalse();
    }

    [Fact]
    public async Task LoadIncludedAsync_AppCards_LeadWithWhatChangesAndNameTheApps()
    {
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.RemovedApps.Header.Should().Be("2 Windows apps removed");
        sut.RemovedApps.Description.Should().Be("a, b");
        sut.InstalledApps.Header.Should().Be("1 app installed after setup");
        sut.InstalledApps.Description.Should().Be("c");
        sut.RemovedApps.HasList.Should().BeFalse(because: "the description already names every app");
    }

    [Fact]
    public async Task LoadIncludedAsync_WithNoApps_CardsSaySoAndHowToAddSome()
    {
        _apps.Setup(a => a.CheckedWindowsAppsAsync()).ReturnsAsync((IReadOnlyList<AppChoice>)[]);
        _apps.Setup(a => a.CheckedExternalAppsAsync()).ReturnsAsync((IReadOnlyList<AppChoice>)[]);
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.RemovedApps.Header.Should().Be("No Windows apps removed");
        sut.RemovedApps.Description.Should().Be("Tick apps on Windows Apps & Features to remove them during setup.");
        sut.InstalledApps.Header.Should().Be("No apps installed after setup");
        sut.InstalledApps.Description.Should().Be("Tick apps on External Software, or add your own installers below.");
    }

    [Fact]
    public async Task LoadIncludedAsync_LongList_PreviewsThreeNamesAndOpensTheRestInAChildCard()
    {
        _apps.Setup(a => a.CheckedWindowsAppsAsync())
            .ReturnsAsync((IReadOnlyList<AppChoice>)[App("a"), App("b"), App("c"), App("d"), App("e")]);
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        var card = sut.RemovedApps;

        card.Header.Should().Be("5 Windows apps removed");
        card.Description.Should().Be("a, b, c and 2 more");
        card.HasList.Should().BeTrue();
        card.ShowsList.Should().BeFalse();
        card.ListToggleLabel.Should().Be("See list");
        card.CornerRadius.Should().Be(new Microsoft.UI.Xaml.CornerRadius(4));

        card.ToggleList();

        card.ShowsList.Should().BeTrue();
        card.Names.Should().Be("a, b, c, d, e");
        card.ListToggleLabel.Should().Be("Hide list");
        card.CornerRadius.Should().Be(new Microsoft.UI.Xaml.CornerRadius(4, 4, 0, 0));
        card.ListCornerRadius.Should().Be(new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4));
    }

    [Fact]
    public async Task LoadIncludedAsync_SettingsCards_SayHowManySettingsTheNewPcGets()
    {
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.OptimizeSettings.Header.Should().Be("Optimize");
        sut.OptimizeSettings.Description.Should().Be($"{OptimizeSettings} settings set on the new PC");
        sut.CustomizeSettings.Header.Should().Be("Customize");
        sut.CustomizeSettings.Description.Should().Be($"{CustomizeSettings} settings set on the new PC");
    }

    [Fact]
    public async Task LoadIncludedAsync_WhenUserLeftSettingsOut_CountNamesThem()
    {
        var excluded = SettingCatalog.ByFeature[FeatureIds.Privacy].Take(2).Select(setting => setting.Id)
            .ToHashSet(StringComparer.Ordinal);
        _mode.Setup(m => m.IsIncluded(It.IsAny<string>())).Returns<string>(id => !excluded.Contains(id));
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.OptimizeSettings.Description.Should().Be($"{OptimizeSettings - 2} settings set on the new PC, 2 left out");
    }

    [Fact]
    public async Task LoadIncludedAsync_SettingHiddenByFilters_IsNotCountedOnEitherSide()
    {
        var sound = SettingCatalog.ByFeature[FeatureIds.Sound].Count;
        _registry.Setup(r => r.GetByFeature(FeatureIds.Sound, It.IsAny<CatalogScope>()))
            .Returns(SettingCatalog.ByFeature[FeatureIds.Sound].Take(2).ToList());
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.OptimizeSettings.Description.Should().Be($"{OptimizeSettings - sound + 2} settings set on the new PC");
    }

    [Fact]
    public async Task OpenPage_EveryCard_LinksToThePageThatChangesIt()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();

        sut.RemovedApps.OpenPageLabel.Should().Be("Change in Software & Apps");
        sut.OptimizeSettings.OpenPageLabel.Should().Be("Change in Optimize");

        sut.RemovedApps.OpenPage();
        sut.InstalledApps.OpenPage();
        sut.CustomizeSettings.OpenPage();

        _eventBus.Verify(e => e.Publish(It.Is<PageRequestedEvent>(
            r => r.PageTag == "SoftwareApps" && r.FeatureId == FeatureIds.WindowsApps)), Times.Once);
        _eventBus.Verify(e => e.Publish(It.Is<PageRequestedEvent>(
            r => r.PageTag == "SoftwareApps" && r.FeatureId == FeatureIds.ExternalApps)), Times.Once);
        _eventBus.Verify(e => e.Publish(It.Is<PageRequestedEvent>(
            r => r.PageTag == "Customize" && r.FeatureId == null)), Times.Once);
    }

    [Fact]
    public void IconResourceKey_EveryCard_MatchesTheIconItsOwnPageDraws()
    {
        var sut = CreateSut();

        sut.RemovedApps.IconResourceKey.Should().Be(FeatureDefinitions.Get(FeatureIds.WindowsApps)!.IconKey);
        sut.InstalledApps.IconResourceKey.Should().Be(FeatureDefinitions.Get(FeatureIds.ExternalApps)!.IconKey);
        sut.OptimizeSettings.IconResourceKey.Should().Be("OptimizeIconSymbol");
        sut.CustomizeSettings.IconResourceKey.Should().Be("CustomizeIconSymbol");
    }

    [Theory]
    [InlineData("Autounattend_Included_WindowsApps", "{0} Windows apps removed")]
    [InlineData("Autounattend_Included_WindowsApps_One", "1 Windows app removed")]
    [InlineData("Autounattend_Included_WindowsApps_None", "No Windows apps removed")]
    [InlineData("Autounattend_Included_WindowsApps_Hint", "Tick apps on Windows Apps & Features to remove them during setup.")]
    [InlineData("Autounattend_Included_ExternalApps", "{0} apps installed after setup")]
    [InlineData("Autounattend_Included_ExternalApps_One", "1 app installed after setup")]
    [InlineData("Autounattend_Included_ExternalApps_None", "No apps installed after setup")]
    [InlineData("Autounattend_Included_ExternalApps_Hint", "Tick apps on External Software, or add your own installers below.")]
    [InlineData("Autounattend_Included_More", "{0} and {1} more")]
    [InlineData("Autounattend_Included_SeeList", "See list")]
    [InlineData("Autounattend_Included_HideList", "Hide list")]
    [InlineData("Autounattend_Included_Change", "Change in {0}")]
    [InlineData("Autounattend_Included_Settings", "{0} settings set on the new PC")]
    [InlineData("Autounattend_Included_Settings_LeftOut", "{0} settings set on the new PC, {1} left out")]
    [InlineData("Autounattend_Included_Settings_One", "{0} setting set on the new PC")]
    [InlineData("Autounattend_Included_Settings_One_LeftOut", "{0} setting set on the new PC, {1} left out")]
    [InlineData("Autounattend_ComputerName_Preview", "On this PC it would be {0}.")]
    public void EnglishFallbacks_EveryKey_MatchesTheEnglishInEnJson(string key, string english)
    {
        EnglishStrings().Should().ContainKey(key).WhoseValue.Should().Be(english);
    }

    [Fact]
    public async Task LoadIncludedAsync_WhenKeyIsPresent_WinsOverTheEnglishFallback()
    {
        _localization.PresentKey("Autounattend_Included_Name", "Aus anderen Bereichen");
        _localization.PresentKey("Autounattend_Included_WindowsApps", "{0} Windows-Apps entfernt");
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.IncludedTitle.Should().Be("Aus anderen Bereichen");
        sut.RemovedApps.Header.Should().Be("2 Windows-Apps entfernt");
    }

    [Fact]
    public async Task LoadIncludedAsync_WhenTranslatedFormatIsBroken_FallsBackToTheEnglish()
    {
        _localization.PresentKey("Autounattend_Included_WindowsApps", "{0 Windows-Apps entfernt");
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.RemovedApps.Header.Should().Be("2 Windows apps removed");
    }

    [Fact]
    public async Task LoadIncludedAsync_WhenSourceThrows_LeavesThePlaceholderAndLogsInsteadOfTakingThePageDown()
    {
        _apps.Setup(a => a.CheckedWindowsAppsAsync()).ThrowsAsync(new InvalidOperationException("no provider"));
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        Cards(sut).Select(card => card.Header).Should().AllBe("...");
        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("no provider")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task LoadIncludedAsync_WhenRelabelThrows_CatchesAndLogsIt()
    {
        _localization.Setup(l => l.GetString("Nav_Optimize")).Throws(new InvalidOperationException("bad label"));
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        Cards(sut).Select(card => card.Header).Should().AllBe("...");
        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("bad label")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseIncluded_SecondHost_KeepsFilterSubscriptionUntilBothHaveReleased()
    {
        // The Unattend page and WIMUtil's card host one view model, and the new host can load before the old unloads.
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        await sut.LoadIncludedAsync();

        sut.ReleaseIncluded();
        _subscription.Verify(s => s.Dispose(), Times.Never);

        sut.ReleaseIncluded();
        _subscription.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public async Task LoadIncludedAsync_CalledRepeatedly_TakesTheFilterSubscriptionOnce()
    {
        var sut = CreateSut();

        await sut.LoadIncludedAsync();
        await sut.LoadIncludedAsync();

        _eventBus.Verify(
            e => e.SubscribeAsync(It.IsAny<Func<FilterStateChangedEvent, Task>>()),
            Times.Once);
    }

    [Fact]
    public async Task ReleaseIncluded_AfterLoad_DropsTheFilterSubscriptionAndTheNextLoadTakesItAgain()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();

        sut.ReleaseIncluded();
        _subscription.Verify(token => token.Dispose(), Times.Once);

        await sut.LoadIncludedAsync();

        _eventBus.Verify(
            e => e.SubscribeAsync(It.IsAny<Func<FilterStateChangedEvent, Task>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task LoadIncludedAsync_WhenSupersededLoadThrows_DoesNotClearTheCards()
    {
        var firstLoad = new TaskCompletionSource<IReadOnlyList<AppChoice>>();
        var calls = 0;
        _apps.Setup(a => a.CheckedWindowsAppsAsync())
            .Returns(() => ++calls == 1
                ? firstLoad.Task
                : Task.FromResult((IReadOnlyList<AppChoice>)[App("a"), App("b")]));
        var sut = CreateSut();

        var superseded = sut.LoadIncludedAsync();
        await sut.LoadIncludedAsync();
        sut.RemovedApps.Header.Should().Be("2 Windows apps removed");

        firstLoad.SetException(new InvalidOperationException("too late"));
        await superseded;

        sut.RemovedApps.Header.Should().Be("2 Windows apps removed");
        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("too late")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task FilterStateChanged_WhenHardwareFilterFlips_CountsTheSettingsAgain()
    {
        var sound = SettingCatalog.ByFeature[FeatureIds.Sound].Count;
        var soundInScope = true;
        _registry.Setup(r => r.GetByFeature(FeatureIds.Sound, It.IsAny<CatalogScope>()))
            .Returns(() => soundInScope ? SettingCatalog.ByFeature[FeatureIds.Sound] : Array.Empty<Setting>());
        var sut = CreateSut();
        await sut.LoadIncludedAsync();

        soundInScope = false;
        await RaiseFilterStateChanged();

        sut.OptimizeSettings.Description.Should().Be($"{OptimizeSettings - sound} settings set on the new PC");
    }

    [Fact]
    public async Task LanguageChanged_Always_RelabelsTheCardsAndKeepsTheCounts()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        _localization.PresentKey("Nav_Optimize", "Optimieren");

        RaiseLanguageChanged();

        sut.OptimizeSettings.Header.Should().Be("Optimieren");
        sut.RemovedApps.Header.Should().Be("2 Windows apps removed");
    }

    private const string Reader = "C:\\Setup\\Reader.exe";
    private const string Vpn = "C:\\Setup\\Vpn.msi";

    // The two cards the row draws, built the way the page's own cards are and writing into a Builder session.
    private SettingItemViewModel InstallerCard(string id, InputType inputType, string name)
    {
        _builder.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _builder.SetupGet(m => m.CurrentBuilderTarget).Returns(BuilderTarget.Autounattend);
        _builder.Setup(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()))
            .Callback<SettingChoice>(edit => _authored[edit.SettingId] = edit);
        var setting = SettingCatalog.Find(id)!;
        var dialogs = new Mock<IDialogService>();
        return new SettingItemViewModel(
            new SettingItemViewModelConfig
            {
                Setting = setting,
                SettingId = setting.Id,
                Name = name,
                Description = name + ", described",
                InputType = inputType,
                IsSelected = inputType == InputType.CheckBox,
            },
            SettingWriteStrategies.Selector(
                new Mock<ISettingApplicationService>().Object, dialogs.Object, _localization.Object, _log.Object, _builder.Object),
            _log.Object,
            _dispatcher.Object,
            dialogs.Object,
            _localization.Object,
            applicationModeService: _builder.Object,
            filePickerService: _filePicker.Object)
        {
            ListAddLabel = "Add an installer",
            ListRemoveLabel = "Remove this installer",
        };
    }

    private (SettingItemViewModel Stage, SettingItemViewModel Installers) LoadInstallerCards(
        AutounattendViewModel sut, params SettingItemViewModel[] others)
    {
        var stage = InstallerCard(OwnInstaller.StageAppsId, InputType.CheckBox, "Put the installers on the ISO");
        var installers = InstallerCard(OwnInstaller.SettingId, InputType.List, "Your own installers");
        sut.Settings = [.. others, stage, installers];
        sut.GroupedSettings.Add(new SettingsGroup("General", [.. others, stage, installers]));
        return (stage, installers);
    }

    private IReadOnlyList<OwnInstaller> RecordedInstallers() =>
        OwnInstaller.From(new SelectionSet([_authored[OwnInstaller.SettingId]], [], []));

    private void ArrangePicks(params string[] paths)
    {
        var picks = _filePicker.SetupSequence(p => p.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()));
        foreach (var path in paths)
            picks = picks.Returns(path);
    }

    [Fact]
    public async Task LoadIncludedAsync_CardsLoaded_ShowInstallOptionsWithOrWithoutWimUtilFolder()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        sut.InstalledApps.HasInstallerCards.Should().BeFalse();

        LoadInstallerCards(sut);

        _session.WorkingDirectory.Should().BeNull();
        sut.InstalledApps.HasInstallerCards.Should().BeTrue();
    }

    [Fact]
    public async Task LoadIncludedAsync_ReloadedPage_HandsRowItsNewCards()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        var row = sut.InstalledApps;
        var (staleStage, _) = LoadInstallerCards(sut);

        sut.GroupedSettings.Clear();
        row.HasInstallerCards.Should().BeFalse();
        var (stage, _) = LoadInstallerCards(sut);
        staleStage.IsSelected = false;

        row.HasInstallerCards.Should().BeTrue();
        row.StageOnMedia.Should().BeTrue(because: "the row reads the card the page shows now");
        stage.IsSelected.Should().BeTrue();
    }

    // With the media's editions loaded every other media card shows on the grouped list; these two never do.
    [Fact]
    public void GroupedSettings_InstallerCards_AreDrawnByRowAndStayOffGroupedList()
    {
        _session.Editions = [new(1, "Windows 11 Home", "Core")];
        var sut = CreateSut();

        var (stage, installers) = LoadInstallerCards(sut);

        stage.ParentIsVisible.Should().BeFalse();
        installers.ParentIsVisible.Should().BeFalse();
    }

    [Fact]
    public async Task StageOnMedia_Unticked_RecordsChoiceAndRebuiltRowStillReadsIt()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        LoadInstallerCards(sut);
        var row = sut.InstalledApps;
        row.StageOnMedia.Should().BeTrue();

        row.StageOnMedia = false;

        _authored[OwnInstaller.StageAppsId].Value.Should().Be(new ChoiceValue.CheckBox(false));
        row.StageOnMedia.Should().BeFalse();
        await sut.LoadIncludedAsync();
        row.StageOnMedia.Should().BeFalse();
    }

    [Fact]
    public async Task AddOwnInstaller_PickedFile_AddsRowAndRecordsIt()
    {
        ArrangePicks(Reader);
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        LoadInstallerCards(sut);
        var row = sut.InstalledApps;

        row.AddOwnInstaller();

        RecordedInstallers().Should().Equal(new OwnInstaller(Reader, string.Empty));
        row.OwnInstallerRows.Should().ContainSingle()
            .Which.FieldFor("file")!.Text.Should().Be(Reader);
        row.HasOwnInstallers.Should().BeTrue();
        _filePicker.Verify(p => p.PickFile(It.Is<string[]>(f => f[1] == "*.exe;*.msi"), "Add an installer"), Times.Once);
    }

    [Fact]
    public async Task AddOwnInstaller_CancelledPick_AddsNothing()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        LoadInstallerCards(sut);
        var row = sut.InstalledApps;

        row.AddOwnInstaller();

        row.OwnInstallerRows.Should().BeEmpty();
        _authored.Should().NotContainKey(OwnInstaller.SettingId);
    }

    [Fact]
    public async Task OwnInstallerRows_TypedSwitches_AreRecordedOnThatInstaller()
    {
        ArrangePicks(Reader);
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        LoadInstallerCards(sut);
        var row = sut.InstalledApps;
        row.AddOwnInstaller();

        row.OwnInstallerRows!.Single().FieldFor("switches")!.Text = " /sAll ";

        RecordedInstallers().Should().Equal(new OwnInstaller(Reader, "/sAll"));
    }

    [Fact]
    public async Task Remove_EachRow_TakesRowAndRecordAwayDownToNone()
    {
        ArrangePicks(Reader, Vpn);
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        LoadInstallerCards(sut);
        var row = sut.InstalledApps;
        row.AddOwnInstaller();
        row.AddOwnInstaller();

        row.OwnInstallerRows![0].Remove();

        RecordedInstallers().Should().Equal(new OwnInstaller(Vpn, string.Empty));
        row.OwnInstallerRows.Should().ContainSingle()
            .Which.FieldFor("file")!.Text.Should().Be(Vpn);

        row.OwnInstallerRows[0].Remove();

        RecordedInstallers().Should().BeEmpty();
        row.HasOwnInstallers.Should().BeFalse();
    }

    [Fact]
    public async Task InstalledApps_Always_ShowsTheCardsOwnTextAndFollowsIt()
    {
        ArrangePicks(Reader);
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        var (stage, installers) = LoadInstallerCards(sut);
        var row = sut.InstalledApps;
        row.AddOwnInstaller();

        row.StageOnMediaLabel.Should().Be("Put the installers on the ISO");
        row.OwnInstallersName.Should().Be("Your own installers");
        row.OwnInstallersDescription.Should().Be("Your own installers, described");
        row.AddOwnInstallerLabel.Should().Be("Add an installer");
        row.OwnInstallerRows!.Single().RemoveLabel.Should().Be("Remove this installer");

        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        stage.Name = "Installer auf die ISO";
        installers.Name = "Eigene Installer";

        row.StageOnMediaLabel.Should().Be("Installer auf die ISO");
        row.OwnInstallersName.Should().Be("Eigene Installer");
        raised.Should().Contain(nameof(InstalledAppsCardViewModel.StageOnMediaLabel))
            .And.Contain(nameof(InstalledAppsCardViewModel.OwnInstallersName));
    }

    [Fact]
    public void Description_OfTheUniqueNameCard_PreviewsThisPcsNameAndFollowsTheTypedName()
    {
        _systemInfo.Setup(s => s.BiosSerialNumber()).Returns("PF3A7F3A");
        var sut = CreateSut();
        var name = InstallerCard(UniqueComputerName.NameSettingId, InputType.TextBox, "Computer name");
        var unique = InstallerCard(UniqueComputerName.SettingId, InputType.Toggle, "Unique name");
        sut.Settings = [name, unique];
        sut.GroupedSettings.Add(new SettingsGroup("Setup screens", [name]));

        SpinWait.SpinUntil(() => unique.Description.Contains("WINHANCE-7F3A"), TimeSpan.FromSeconds(5)).Should().BeTrue();
        unique.Description.Should().StartWith("Unique name, described\n");

        name.TextValue = "OFFICE";
        SpinWait.SpinUntil(() => unique.Description.Contains("OFFICE-7F3A"), TimeSpan.FromSeconds(5)).Should().BeTrue();
        unique.Description.Should().NotContain("WINHANCE");
    }

    // Built in a Builder session, so the card shows the decided state the way the page shows it.
    private SettingItemViewModel AuthoredWinhanceOnNewPcCard(AutounattendViewModel sut, int state)
    {
        var card = InstallerCard(WinhanceOnNewPc.SettingId, InputType.Selection, "Winhance on the new PC");
        card.SelectedValue = state;
        sut.Settings = [card];
        sut.GroupedSettings.Add(new SettingsGroup("General", [card]));
        return card;
    }

    [Fact]
    public async Task LoadIncludedAsync_WithAppsOnTheFile_InstallsWinhanceAutomaticallyAndGreysTheCard()
    {
        var sut = CreateSut();
        var card = AuthoredWinhanceOnNewPcCard(sut, 1);

        await sut.LoadIncludedAsync();

        card.DecidedState.Should().Be(WinhanceOnNewPc.InstallsAutomatically);
        card.SelectedValue.Should().Be(2);
        card.ParentIsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task LoadIncludedAsync_WhenKeepingRemovedAppsWithWinhance_CountsAsAnInstallAndDecidesTheCard()
    {
        _apps.Setup(a => a.CheckedExternalAppsAsync()).ReturnsAsync((IReadOnlyList<AppChoice>)[]);
        var sut = CreateSut();
        var card = InstallerCard(WinhanceOnNewPc.SettingId, InputType.Selection, "Winhance on the new PC");
        var keepRemoved = InstallerCard(WinhanceOnNewPc.KeepRemovedId, InputType.Selection, "Keep removed apps");
        keepRemoved.SelectedValue = 2;
        sut.Settings = [card, keepRemoved];
        sut.GroupedSettings.Add(new SettingsGroup("General", [card, keepRemoved]));

        await sut.LoadIncludedAsync();

        card.DecidedState.Should().Be(WinhanceOnNewPc.InstallsAutomatically);
    }

    [Fact]
    public async Task LoadIncludedAsync_WhenNotKeepingRemovedApps_LeavesTheCardToTheUser()
    {
        _apps.Setup(a => a.CheckedExternalAppsAsync()).ReturnsAsync((IReadOnlyList<AppChoice>)[]);
        var sut = CreateSut();
        var card = InstallerCard(WinhanceOnNewPc.SettingId, InputType.Selection, "Winhance on the new PC");
        var keepRemoved = InstallerCard(WinhanceOnNewPc.KeepRemovedId, InputType.Selection, "Keep removed apps");
        keepRemoved.SelectedValue = 1;
        sut.Settings = [card, keepRemoved];
        sut.GroupedSettings.Add(new SettingsGroup("General", [card, keepRemoved]));

        await sut.LoadIncludedAsync();

        card.DecidedState.Should().BeNull();
    }

    [Fact]
    public async Task DecidedState_WhenKeepRemovedAppsSwitchedOffAfterLoad_HandsTheCardBackToTheUser()
    {
        _apps.Setup(a => a.CheckedExternalAppsAsync()).ReturnsAsync((IReadOnlyList<AppChoice>)[]);
        var sut = CreateSut();
        var card = InstallerCard(WinhanceOnNewPc.SettingId, InputType.Selection, "Winhance on the new PC");
        var keepRemoved = InstallerCard(WinhanceOnNewPc.KeepRemovedId, InputType.Selection, "Keep removed apps");
        keepRemoved.SelectedValue = 2;
        sut.Settings = [card, keepRemoved];
        sut.GroupedSettings.Add(new SettingsGroup("General", [card, keepRemoved]));
        await sut.LoadIncludedAsync();
        card.DecidedState.Should().Be(WinhanceOnNewPc.InstallsAutomatically);

        keepRemoved.SelectedValue = 1;
        _session.Editions = [];

        card.DecidedState.Should().BeNull();
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    public async Task LoadIncludedAsync_RemoveSourceCard_IsDecidedOnlyDuringSetupWithNetFx3On(bool builtIntoImage, bool netfx3On, bool decided)
    {
        _session.BuiltIntoImage = builtIntoImage;
        var sut = CreateSut();
        var netfx3 = InstallerCard("autounattend-netfx3", InputType.Toggle, "NetFx3");
        netfx3.IsSelected = netfx3On;
        var removeSource = InstallerCard("autounattend-netfx3-remove-source", InputType.Toggle, "Remove the source");
        sut.Settings = [netfx3, removeSource];
        sut.GroupedSettings.Add(new SettingsGroup("General", [netfx3, removeSource]));

        await sut.LoadIncludedAsync();

        (removeSource.DecidedState == LocKey.Common.Disabled).Should().Be(decided);
        removeSource.ParentIsEnabled.Should().Be(!decided);
        removeSource.Description.Contains("Setup needs these files").Should().Be(decided);
    }

    [Fact]
    public async Task BuiltIntoImage_WhenSwitched_ReopensOrClosesTheRemoveSourceCard()
    {
        var sut = CreateSut();
        var netfx3 = InstallerCard("autounattend-netfx3", InputType.Toggle, "NetFx3");
        netfx3.IsSelected = true;
        var removeSource = InstallerCard("autounattend-netfx3-remove-source", InputType.Toggle, "Remove the source");
        sut.Settings = [netfx3, removeSource];
        sut.GroupedSettings.Add(new SettingsGroup("General", [netfx3, removeSource]));
        await sut.LoadIncludedAsync();
        removeSource.ParentIsEnabled.Should().BeFalse();

        _session.BuiltIntoImage = true;

        removeSource.ParentIsEnabled.Should().BeTrue();
        removeSource.Description.Should().NotContain("Setup needs these files");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task LoadIncludedAsync_CleanupCard_IsDecidedOffWhenChangesAreBuiltIntoTheImage(bool builtIntoImage, bool decided)
    {
        _session.BuiltIntoImage = builtIntoImage;
        var sut = CreateSut();
        var cleanup = InstallerCard("autounattend-cleanup", InputType.Toggle, "Clean up");
        sut.Settings = [cleanup];
        sut.GroupedSettings.Add(new SettingsGroup("General", [cleanup]));

        await sut.LoadIncludedAsync();

        (cleanup.DecidedState == LocKey.Common.Disabled).Should().Be(decided);
        cleanup.ParentIsEnabled.Should().Be(!decided);
    }

    [Fact]
    public async Task LoadIncludedAsync_WithoutShowModeTags_LeavesEveryCardUntagged()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();

        sut.RemovedApps.Tag.Should().BeEmpty();
        sut.InstalledApps.Tag.Should().BeEmpty();
        sut.OptimizeSettings.Tag.Should().BeEmpty();
        sut.CustomizeSettings.Tag.Should().BeEmpty();
    }

    [Fact]
    public async Task ShowModeTags_WhenBuiltIntoTheImage_TagsOnlyTheInstalledAppsDuringSetup()
    {
        _session.BuiltIntoImage = true;
        var sut = CreateSut();
        await sut.LoadIncludedAsync();

        sut.ShowModeTags(true);

        sut.RemovedApps.Tag.Should().Be("In the image");
        sut.OptimizeSettings.Tag.Should().Be("In the image");
        sut.CustomizeSettings.Tag.Should().Be("In the image");
        sut.InstalledApps.Tag.Should().Be("During setup");
    }

    [Fact]
    public async Task ShowModeTags_DuringSetup_TagsEveryCardDuringSetup()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();

        sut.ShowModeTags(true);

        new[] { sut.RemovedApps, sut.InstalledApps, sut.OptimizeSettings, sut.CustomizeSettings }
            .Should().OnlyContain(card => card.Tag == "During setup");
    }

    [Fact]
    public async Task ShowModeTags_WhenServicingModeChanges_TagsFollowAndGoAwayWhenHidden()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        sut.ShowModeTags(true);

        _session.BuiltIntoImage = true;
        sut.RemovedApps.Tag.Should().Be("In the image");

        sut.ShowModeTags(false);
        sut.RemovedApps.Tag.Should().BeEmpty();
        sut.InstalledApps.Tag.Should().BeEmpty();
    }

    [Fact]
    public async Task FilterStateChanged_WithNoAppsLeft_ReturnsTheCardToTheUser()
    {
        var sut = CreateSut();
        var card = AuthoredWinhanceOnNewPcCard(sut, 1);
        await sut.LoadIncludedAsync();

        _apps.Setup(a => a.CheckedExternalAppsAsync()).ReturnsAsync((IReadOnlyList<AppChoice>)[]);
        await RaiseFilterStateChanged();

        card.DecidedState.Should().BeNull();
        card.ParentIsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task AddOwnInstaller_Always_CountsAsAnAppAndDecidesTheCard()
    {
        _apps.Setup(a => a.CheckedExternalAppsAsync()).ReturnsAsync((IReadOnlyList<AppChoice>)[]);
        ArrangePicks(Reader);
        var sut = CreateSut();
        var card = InstallerCard(WinhanceOnNewPc.SettingId, InputType.Selection, "Winhance on the new PC");
        LoadInstallerCards(sut, card);
        await sut.LoadIncludedAsync();
        sut.InstalledApps.Header.Should().Be("No apps installed after setup");
        card.DecidedState.Should().BeNull();

        sut.InstalledApps.AddOwnInstaller();

        sut.InstalledApps.Header.Should().Be("1 app installed after setup");
        sut.InstalledApps.Description.Should().BeEmpty();
        card.DecidedState.Should().Be(WinhanceOnNewPc.InstallsAutomatically);
    }

    [Fact]
    public async Task AddOwnInstaller_WithTickedApps_AddsTheInstallerToTheInstalledCount()
    {
        ArrangePicks(Reader);
        var sut = CreateSut();
        LoadInstallerCards(sut);
        await sut.LoadIncludedAsync();

        sut.InstalledApps.AddOwnInstaller();

        sut.InstalledApps.Header.Should().Be("2 apps installed after setup");
        sut.InstalledApps.Description.Should().Be("c");
    }

    [Fact]
    public async Task CornerRadius_WhenInstallerCardsLoad_JoinsTheAppsCardUnderItsHeader()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        sut.InstalledApps.CornerRadius.Should().Be(new Microsoft.UI.Xaml.CornerRadius(4));

        LoadInstallerCards(sut);

        sut.InstalledApps.CornerRadius.Should().Be(new Microsoft.UI.Xaml.CornerRadius(4, 4, 0, 0));
        sut.InstalledApps.OwnInstallersCornerRadius.Should().Be(new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4));
    }

    private SettingItemViewModel MediaEditionsListCard()
    {
        var setting = SettingCatalog.Find("autounattend-media-editions-list")!;
        var dialogs = new Mock<IDialogService>();
        return new SettingItemViewModel(
            new SettingItemViewModelConfig
            {
                Setting = setting,
                SettingId = setting.Id,
                Name = "Editions to keep",
                Description = string.Empty,
                InputType = InputType.CheckList,
            },
            SettingWriteStrategies.Selector(
                new Mock<ISettingApplicationService>().Object, dialogs.Object, _localization.Object, _log.Object),
            _log.Object,
            _dispatcher.Object,
            dialogs.Object,
            _localization.Object);
    }

    [Fact]
    public void Settings_WithoutWimUtilSession_HidesMediaCardsAndShowsTheRest()
    {
        var sut = CreateSut();
        var checklist = MediaEditionsListCard();
        var other = WinhanceOnNewPcCard(0);
        sut.Settings = [checklist, other];

        sut.GroupedSettings.Add(new SettingsGroup("General", [other]));

        checklist.ParentIsVisible.Should().BeFalse();
        checklist.CheckItems.Should().BeEmpty();
        other.ParentIsVisible.Should().BeTrue();
    }

    [Fact]
    public void Settings_WithWimUtilSession_ShowsMediaCardsWithItsEditions()
    {
        _session.Editions = [new(1, "Windows 11 Home", "Core"), new(2, "Windows 11 Pro", "Professional")];
        var sut = CreateSut();
        var checklist = MediaEditionsListCard();
        sut.Settings = [checklist];

        sut.GroupedSettings.Add(new SettingsGroup("General", [checklist]));

        checklist.ParentIsVisible.Should().BeTrue();
        checklist.CheckItems.Select(item => item.Key).Should().Equal("Windows 11 Home", "Windows 11 Pro");
    }
}
