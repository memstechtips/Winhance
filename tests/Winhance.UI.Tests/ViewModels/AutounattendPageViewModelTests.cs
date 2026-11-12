using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.TestSupport;
using Winhance.UI.Features.Autounattend.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.WimUtil.Models;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class AutounattendPageViewModelTests
{
    private readonly Mock<ISettingsLoadingService> _settingsLoading = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<IDispatcherService> _dispatcher = new();
    private readonly Mock<IApplicationModeService> _mode = new();
    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<ICatalogScopeProvider> _scope = new();
    private readonly WimUtilSession _session = new();
    private readonly AutounattendViewModel _feature;

    public AutounattendPageViewModelTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => $"[{key}]");
        _localization.MirrorTryGetString();
        _dispatcher.Setup(d => d.RunOnUIThread(It.IsAny<Action>())).Callback<Action>(action => action());

        _feature = new AutounattendViewModel(
            _settingsLoading.Object,
            _log.Object,
            _localization.Object,
            _dispatcher.Object,
            new Mock<IEventBus>().Object,
            _mode.Object,
            _registry.Object,
            _scope.Object,
            new Mock<IAppSelectionSource>().Object,
            _session,
            new Mock<ISystemInfoProvider>().Object,
            new Mock<IBuilderModeEntry>().Object);
    }

    private AutounattendPageViewModel CreateSut() => new(
        _log.Object,
        _localization.Object,
        _feature,
        new Mock<IConfigReviewBadgeService>().Object,
        new Mock<IConfigReviewModeService>().Object,
        _registry.Object,
        _scope.Object,
        _mode.Object);

    private SettingItemViewModel Card(string id, InputType inputType)
    {
        var setting = SettingCatalog.Find(id)!;
        var dialogs = new Mock<IDialogService>();
        return new SettingItemViewModel(
            new SettingItemViewModelConfig
            {
                Setting = setting,
                SettingId = setting.Id,
                Name = id,
                Description = string.Empty,
                InputType = inputType,
            },
            SettingWriteStrategies.Selector(
                new Mock<ISettingApplicationService>().Object, dialogs.Object, _localization.Object, _log.Object),
            _log.Object,
            _dispatcher.Object,
            dialogs.Object,
            _localization.Object);
    }

    [Fact]
    public void OverviewItems_Always_IsOneSectionForTheAutounattendFeature()
    {
        var sut = CreateSut();

        var section = sut.OverviewItems.Should().ContainSingle().Subject;
        section.FeatureId.Should().Be(FeatureIds.Autounattend);
        section.Feature.Should().BeSameAs(_feature);
    }

    // The breadcrumb's second level shows only on a detail page, so the root is all it ever reads.
    [Fact]
    public async Task InitializeAsync_Always_NeverOpensADetailPage()
    {
        var sut = CreateSut();

        await sut.InitializeAsync();

        sut.IsInDetailPage.Should().BeFalse();
        sut.CurrentSectionItem.Should().BeNull();
    }

    [Fact]
    public void PageTitle_Always_ReadsTheKeysThePageAlwaysHad()
    {
        var english = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(RepoPaths.LocalizationDir(), "en.json")))!;
        var sut = CreateSut();

        sut.PageTitle.Should().Be("[Autounattend_Page_Title]");
        sut.PageDescription.Should().Be("[Autounattend_Page_Description]");
        sut.BreadcrumbRootText.Should().Be(english["Autounattend_Page_Title"]);
        english.Should().ContainKey("Autounattend_Page_Description");
    }

    // The view loads the cards in both of its hosts; a second load from here would race it for the load lock.
    [Fact]
    public async Task InitializeAsync_Always_DoesNotLoadTheCardsAndEndsItsOwnLoadingState()
    {
        var sut = CreateSut();
        sut.IsLoading.Should().BeTrue();

        await sut.InitializeAsync();

        sut.IsLoading.Should().BeFalse();
        sut.IsNotLoading.Should().BeTrue();
        _settingsLoading.Invocations.Should().BeEmpty();
    }

    [Fact]
    public void SearchText_Typed_FiltersTheAutounattendCards()
    {
        var sut = CreateSut();

        sut.SearchText = "privacy";

        _feature.SearchText.Should().Be("privacy");
    }

    // On the other pages a suggestion jumps to a section that is not open; here the one section is always open.
    [Fact]
    public void SearchSuggestions_Always_AreEmptyBecauseTheCardsAreAlreadyFilteredOnScreen()
    {
        _feature.Settings = [Card("autounattend-privacy", InputType.Selection)];
        var sut = CreateSut();

        sut.SearchText = "privacy";

        sut.SearchSuggestions.Should().BeEmpty();
    }

    [Fact]
    public void OnNavigatedFrom_AfterSearching_ClearsTheFilterTheWimUtilHostWouldInherit()
    {
        var sut = CreateSut();
        sut.SearchText = "privacy";

        sut.OnNavigatedFrom();

        _feature.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void HasNoSearchResults_SearchHidingEveryCard_IsTrueOnceTheFeatureHasFiltered()
    {
        var card = Card("autounattend-privacy", InputType.Selection);
        card.IsVisible = false;
        _feature.Settings = [card];
        var sut = CreateSut();
        // The feature filters on a pool thread here, with no UI thread.
        var raised = new ConcurrentQueue<string?>();
        sut.SearchText = "zzz";
        sut.PropertyChanged += (_, e) => raised.Enqueue(e.PropertyName);

        _feature.GroupedSettings.Add(new SettingsGroup("General", [card]));

        sut.HasNoSearchResults.Should().BeTrue();
        raised.Should().Contain(nameof(sut.HasNoSearchResults));
    }

    [Fact]
    public void TakesQuickActions_CardThePageDoesNotShow_ReturnsFalse()
    {
        var sut = CreateSut();

        sut.TakesQuickActions(Card("autounattend-media-editions", InputType.Selection)).Should().BeFalse(
            "the edition cards show only in a session WIMUtil started");
        sut.TakesQuickActions(Card("autounattend-stage-apps", InputType.CheckBox)).Should().BeFalse(
            "the External Software row draws it");
        sut.TakesQuickActions(Card("autounattend-privacy", InputType.Selection)).Should().BeTrue();
    }

    [Fact]
    public void TakesQuickActions_EditionCardInWimUtilSession_ReturnsTrue()
    {
        _session.Editions = [new(1, "Windows 11 Home", "Core")];
        var sut = CreateSut();

        sut.TakesQuickActions(Card("autounattend-media-editions", InputType.Selection)).Should().BeTrue();
    }

    // Their one role says Checked, but each box starts from this PC's processor: a reset would tick all three.
    [Theory]
    [InlineData("autounattend-architecture-x64")]
    [InlineData("autounattend-architecture-arm64")]
    [InlineData("autounattend-architecture-x86")]
    public void TakesQuickActions_ArchitectureBoxes_ReturnsFalse(string id)
    {
        CreateSut().TakesQuickActions(Card(id, InputType.CheckBox)).Should().BeFalse();
    }
}
