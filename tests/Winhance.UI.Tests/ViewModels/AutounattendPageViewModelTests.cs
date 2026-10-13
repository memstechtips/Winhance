using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
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
            new Mock<IWindowsAppsService>().Object,
            new Mock<IExternalAppsService>().Object,
            _session);
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
    public void The_page_has_one_section_and_it_is_the_Autounattend_feature()
    {
        var sut = CreateSut();

        var section = sut.OverviewItems.Should().ContainSingle().Subject;
        section.FeatureId.Should().Be(FeatureIds.Autounattend);
        section.Feature.Should().BeSameAs(_feature);
    }

    // The breadcrumb's second level shows only on a detail page, so the root is all it ever reads.
    [Fact]
    public async Task The_page_never_opens_a_detail_page_of_its_own_section()
    {
        var sut = CreateSut();

        await sut.InitializeAsync();

        sut.IsInDetailPage.Should().BeFalse();
        sut.CurrentSectionItem.Should().BeNull();
    }

    [Fact]
    public void The_header_reads_the_keys_the_page_always_had()
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
    public async Task Initializing_the_page_does_not_load_the_cards_and_ends_its_own_loading_state()
    {
        var sut = CreateSut();
        sut.IsLoading.Should().BeTrue();

        await sut.InitializeAsync();

        sut.IsLoading.Should().BeFalse();
        sut.IsNotLoading.Should().BeTrue();
        _settingsLoading.Invocations.Should().BeEmpty();
    }

    [Fact]
    public void Typing_in_the_search_box_filters_the_Autounattend_cards()
    {
        var sut = CreateSut();

        sut.SearchText = "privacy";

        _feature.SearchText.Should().Be("privacy");
    }

    // On the other pages a suggestion jumps to a section that is not open; here the one section is always open.
    [Fact]
    public void The_search_box_suggests_nothing_because_the_cards_it_would_name_are_already_filtered_on_screen()
    {
        _feature.Settings = [Card("autounattend-privacy", InputType.Selection)];
        var sut = CreateSut();

        sut.SearchText = "privacy";

        sut.SearchSuggestions.Should().BeEmpty();
    }

    [Fact]
    public void Leaving_the_page_clears_the_filter_the_WIMUtil_host_would_otherwise_inherit()
    {
        var sut = CreateSut();
        sut.SearchText = "privacy";

        sut.OnNavigatedFrom();

        _feature.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void A_search_that_hides_every_card_reports_no_results_once_the_feature_has_filtered()
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
    public void Quick_actions_skip_a_card_the_page_does_not_show()
    {
        var sut = CreateSut();

        sut.TakesQuickActions(Card("autounattend-media-editions", InputType.Selection)).Should().BeFalse(
            "the edition cards show only in a session WIMUtil started");
        sut.TakesQuickActions(Card("autounattend-stage-apps", InputType.CheckBox)).Should().BeFalse(
            "the External Software row draws it");
        sut.TakesQuickActions(Card("autounattend-privacy", InputType.Selection)).Should().BeTrue();
    }

    [Fact]
    public void Quick_actions_take_the_edition_cards_in_a_WIMUtil_session()
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
    public void Quick_actions_leave_the_architecture_boxes_as_this_PC_set_them(string id)
    {
        CreateSut().TakesQuickActions(Card(id, InputType.CheckBox)).Should().BeFalse();
    }
}
