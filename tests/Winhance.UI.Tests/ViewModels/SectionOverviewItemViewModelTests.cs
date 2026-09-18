using System.Collections.ObjectModel;
using System.ComponentModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.TestSupport;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SectionOverviewItemViewModelTests
{
    private const string SectionKey = "Privacy";
    private const string FeatureId = "privacy";

    private readonly Mock<IConfigReviewBadgeService> _badges = new();
    private readonly Mock<IConfigReviewModeService> _reviewMode = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<ISettingsFeatureViewModel> _feature = new();
    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<ICatalogScopeProvider> _scope = new();
    private readonly Mock<IApplicationModeService> _modeService = new();

    private readonly Mock<ISettingApplicationService> _applyService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDispatcherService> _dispatcher = new();
    private readonly Mock<IDialogService> _dialogService = new();

    private readonly List<Setting> _catalogSettings = new();
    private readonly HashSet<string> _excluded = new();
    private ObservableCollection<SettingItemViewModel> _settings = new();

    private static readonly string[] BothSettings = ["first", "second"];

    public SectionOverviewItemViewModelTests()
    {
        _feature.Setup(f => f.Settings).Returns(() => _settings);
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string k) => k);
        // Unstubbed TryGetString reports "missing" for every key, which is the fallback path.
        // Tests that care about a specific string stub it explicitly.

        _registry.Setup(r => r.GetByFeature(FeatureId, It.IsAny<CatalogScope>())).Returns(() => _catalogSettings);

        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _modeService.Setup(m => m.IsIncluded(It.IsAny<string>()))
            .Returns<string>(id => !_excluded.Contains(id));
        _modeService.Setup(m => m.SetIncluded(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((id, included) =>
            {
                if (included) _excluded.Remove(id);
                else _excluded.Add(id);
            });
    }

    private SectionOverviewItemViewModel CreateSut() => new(
        SectionKey,
        FeatureId,
        "PrivacyIconPath",
        _feature.Object,
        _badges.Object,
        _reviewMode.Object,
        _localization.Object,
        _registry.Object,
        _scope.Object,
        _modeService.Object);

    private void InScope(params string[] ids)
    {
        foreach (var id in ids)
            _catalogSettings.Add(CatalogSetting(id));
    }

    private static Setting CatalogSetting(string id) => new()
    {
        Id = id,
        Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of("") },
    };

    private static Setting AnswerFileSetting(string id) => new()
    {
        Id = id,
        Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of("") },
        TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything."))),
        Targets = [new AutounattendElement("name", "specialize", "Microsoft-Windows-Shell-Setup", "ComputerName")],
    };

    private SettingItemViewModel CreateCard(string id) => new(
        new SettingItemViewModelConfig
        {
            Setting = CatalogSetting(id),
            SettingId = id,
            Name = id,
            Description = "",
            InputType = InputType.Toggle,
        },
        SettingWriteStrategies.Selector(
            _applyService.Object, _dialogService.Object, _localization.Object,
            _logService.Object, _modeService.Object),
        _logService.Object,
        _dispatcher.Object,
        _dialogService.Object,
        _localization.Object,
        null,
        null,
        null,
        _modeService.Object);

    public static TheoryData<WinhanceMode> AuthoringModes() => Modes(authoring: true);

    public static TheoryData<WinhanceMode> NonAuthoringModes() => Modes(authoring: false);

    private static TheoryData<WinhanceMode> Modes(bool authoring)
    {
        var data = new TheoryData<WinhanceMode>();
        foreach (var mode in Enum.GetValues<WinhanceMode>())
        {
            if (ModeCapabilities.For(mode).AuthorsIntent == authoring)
                data.Add(mode);
        }
        return data;
    }

    [Fact]
    public void OutsideReviewMode_NoReviewBadgeShows_EvenWhenTheFeatureHasDiffs()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(false);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(5);

        var sut = CreateSut();

        sut.IsReviewSuccessBadgeVisible.Should().BeFalse();
        sut.IsReviewPendingBadgeVisible.Should().BeFalse();
    }

    [Fact]
    public void WithUnreviewedDiffs_ShowsThePendingCount_NotTheTotal()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(5);
        _badges.Setup(b => b.IsFeatureFullyReviewed(FeatureId)).Returns(false);
        _badges.Setup(b => b.GetFeaturePendingDiffCount(FeatureId)).Returns(2);

        var sut = CreateSut();

        sut.IsReviewPendingBadgeVisible.Should().BeTrue();
        sut.IsReviewSuccessBadgeVisible.Should().BeFalse();
        sut.ReviewPendingCount.Should().Be(2);
    }

    [Fact]
    public void WhenFullyReviewed_ShowsTheCheckmarkInsteadOfACount()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(5);
        _badges.Setup(b => b.IsFeatureFullyReviewed(FeatureId)).Returns(true);

        var sut = CreateSut();

        sut.IsReviewSuccessBadgeVisible.Should().BeTrue();
        sut.IsReviewPendingBadgeVisible.Should().BeFalse();
    }

    [Fact]
    public void InTheConfigWithNoDiffs_ShowsTheCheckmark()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(0);
        _badges.Setup(b => b.IsFeatureInConfig(FeatureId)).Returns(true);

        var sut = CreateSut();

        sut.IsReviewSuccessBadgeVisible.Should().BeTrue();
        sut.IsReviewPendingBadgeVisible.Should().BeFalse();
    }

    [Fact]
    public void AbsentFromTheConfig_ShowsNoBadgeAtAll()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(0);
        _badges.Setup(b => b.IsFeatureInConfig(FeatureId)).Returns(false);

        var sut = CreateSut();

        sut.IsReviewSuccessBadgeVisible.Should().BeFalse();
        sut.IsReviewPendingBadgeVisible.Should().BeFalse();
    }

    [Fact]
    public void EnteringReviewMode_UpdatesTheBadgeWithoutAnyoneCallingRefresh()
    {
        // The card must react to the service itself, rather than wait for a page to remember to push new
        // values into a named element.
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(false);
        var sut = CreateSut();
        sut.IsReviewPendingBadgeVisible.Should().BeFalse();

        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(3);
        _badges.Setup(b => b.IsFeatureFullyReviewed(FeatureId)).Returns(false);
        _badges.Setup(b => b.GetFeaturePendingDiffCount(FeatureId)).Returns(3);
        _reviewMode.Raise(r => r.ReviewModeChanged += null, EventArgs.Empty);

        sut.IsReviewPendingBadgeVisible.Should().BeTrue();
        sut.ReviewPendingCount.Should().Be(3);
    }

    [Fact]
    public void BadgeStateChanged_RecomputesTheBadge()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(4);
        _badges.Setup(b => b.IsFeatureFullyReviewed(FeatureId)).Returns(false);
        _badges.Setup(b => b.GetFeaturePendingDiffCount(FeatureId)).Returns(4);
        var sut = CreateSut();

        _badges.Setup(b => b.IsFeatureFullyReviewed(FeatureId)).Returns(true);
        _badges.Raise(b => b.BadgeStateChanged += null, EventArgs.Empty);

        sut.IsReviewSuccessBadgeVisible.Should().BeTrue();
        sut.IsReviewPendingBadgeVisible.Should().BeFalse();
    }

    [Fact]
    public void AFeatureWithNoBadgeData_ShowsNoPills()
    {
        var sut = CreateSut();

        sut.ArePillsVisible.Should().BeFalse();
        sut.IsNewBadgeVisible.Should().BeFalse();
    }

    [Fact]
    public void TurningInfoBadgesOff_HidesThePills_WithoutTouchingTheReviewBadge()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(0);
        _badges.Setup(b => b.IsFeatureInConfig(FeatureId)).Returns(true);
        var sut = CreateSut();

        sut.AreInfoBadgesVisible = false;

        sut.ArePillsVisible.Should().BeFalse();
        // A review decision is not optional detail — View > InfoBadges must not hide it.
        sut.IsReviewSuccessBadgeVisible.Should().BeTrue();
    }

    [Fact]
    public void Dispose_UnsubscribesFromTheServices()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(0);
        _badges.Setup(b => b.IsFeatureInConfig(FeatureId)).Returns(false);
        var sut = CreateSut();

        sut.Dispose();

        // Would flip the badge on if it were still listening.
        _badges.Setup(b => b.IsFeatureInConfig(FeatureId)).Returns(true);
        _badges.Raise(b => b.BadgeStateChanged += null, EventArgs.Empty);

        sut.IsReviewSuccessBadgeVisible.Should().BeFalse();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var sut = CreateSut();

        var act = () => { sut.Dispose(); sut.Dispose(); };

        act.Should().NotThrow();
    }

    [Fact]
    public void IdentityIsCarriedForTheTemplateToBindAndNavigateWith()
    {
        var sut = CreateSut();

        sut.SectionKey.Should().Be(SectionKey);
        sut.FeatureId.Should().Be(FeatureId);
        sut.IconResourceKey.Should().Be("PrivacyIconPath");
        sut.Feature.Should().BeSameAs(_feature.Object);
    }

    [Theory]
    [MemberData(nameof(AuthoringModes))]
    public void WhileAModeAuthorsIntent_TheBoxIsShown(WinhanceMode mode)
    {
        _modeService.SetupGet(m => m.CurrentMode).Returns(mode);

        CreateSut().ShowIncludeBox.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(NonAuthoringModes))]
    public void OutsideAnAuthoringMode_TheBoxIsHidden(WinhanceMode mode)
    {
        _modeService.SetupGet(m => m.CurrentMode).Returns(mode);

        CreateSut().ShowIncludeBox.Should().BeFalse();
    }

    [Fact]
    public void EnteringAnAuthoringMode_ShowsTheBoxWithoutRebuildingTheOverview()
    {
        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        var sut = CreateSut();
        sut.ShowIncludeBox.Should().BeFalse();
        sut.CardPadding.Left.Should().Be(16);

        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _modeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        sut.ShowIncludeBox.Should().BeTrue();
        sut.CardPadding.Left.Should().Be(72, "the box, its glyph and their gaps take 56px");
    }

    [Fact]
    public void TheConstructor_LeavesTheRegistryAlone()
    {
        // The items are built before startup initializes the registry, and an uninitialized registry throws.
        CreateSut();

        _registry.Verify(r => r.GetByFeature(It.IsAny<string>(), It.IsAny<CatalogScope>()), Times.Never);
    }

    [Fact]
    public void WithEverySettingInTheFile_TheBoxIsTicked()
    {
        InScope("first", "second");

        CreateSut().IncludeState.Should().BeTrue();
    }

    [Fact]
    public void WithNoSettingInTheFile_TheBoxIsUnticked()
    {
        InScope("first", "second");
        _excluded.Add("first");
        _excluded.Add("second");

        CreateSut().IncludeState.Should().BeFalse();
    }

    [Fact]
    public void WithTheSettingsDisagreeing_TheBoxIsIndeterminate()
    {
        InScope("first", "second");
        _excluded.Add("second");

        CreateSut().IncludeState.Should().BeNull();
    }

    [Fact]
    public void AnAnswerFileSetting_IsNoPartOfTheBox()
    {
        // The session refuses to exclude one, so counting it would pin the box to indeterminate forever.
        InScope("first");
        _catalogSettings.Add(AnswerFileSetting("autounattend-computer-name"));
        _excluded.Add("first");

        CreateSut().IncludeState.Should().BeFalse();
    }

    [Fact]
    public void AFeatureWithNothingToOfferTheFile_ReadsIndeterminate()
    {
        CreateSut().IncludeState.Should().BeNull();
    }

    [Fact]
    public void TickingTheBox_PutsEveryInScopeSettingBackInTheFile()
    {
        InScope("first", "second");
        _excluded.Add("first");
        _excluded.Add("second");
        var sut = CreateSut();

        sut.IncludeState = true;

        _excluded.Should().BeEmpty();
        sut.IncludeState.Should().BeTrue();
    }

    [Fact]
    public void UntickingTheBox_TakesEveryInScopeSettingOut_ButNeverAnAnswerFileOne()
    {
        InScope("first", "second");
        _catalogSettings.Add(AnswerFileSetting("autounattend-computer-name"));
        var sut = CreateSut();

        sut.IncludeState = false;

        _excluded.Should().BeEquivalentTo(BothSettings);
        _modeService.Verify(m => m.SetIncluded("autounattend-computer-name", It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void UntickingTheBox_TakesEveryBuiltCardWithIt()
    {
        InScope("first", "second");
        _settings = new ObservableCollection<SettingItemViewModel> { CreateCard("first"), CreateCard("second") };
        var sut = CreateSut();

        sut.IncludeState = false;

        _settings.Should().OnlyContain(card => !card.IsIncluded);
        _modeService.Verify(m => m.SetIncluded("first", false), Times.Once,
            failMessage: "the card reads the session back; recording it again would be a second write");
        _modeService.Verify(m => m.SetIncluded("second", false), Times.Once);
    }

    [Fact]
    public void ACardsOwnChange_ReDerivesTheBox_EvenAfterTheFeatureReplacedItsSettings()
    {
        // The first load replaces Settings rather than filling the collection the constructor attached to.
        InScope("first");
        var sut = CreateSut();
        var card = CreateCard("first");
        _settings = new ObservableCollection<SettingItemViewModel> { card };
        _feature.Raise(f => f.PropertyChanged += null,
            new PropertyChangedEventArgs(nameof(ISettingsFeatureViewModel.Settings)));
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        card.IsIncluded = false;

        raised.Should().Contain(nameof(SectionOverviewItemViewModel.IncludeState));
        sut.IncludeState.Should().BeFalse();
    }

    [Fact]
    public void TheAutomationNameNamesTheFeature()
    {
        _feature.Setup(f => f.DisplayName).Returns("Privacy");
        _localization.PresentKey("Setting_Include_AutomationName", "Include {0} in the file");

        CreateSut().IncludeAutomationName.Should().Be("Include Privacy in the file");
    }

    [Fact]
    public void Dispose_StopsListeningToTheModeService()
    {
        // ShowIncludeBox is computed live, so only the absence of a notification proves the handler is gone.
        var sut = CreateSut();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.Dispose();
        _modeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        raised.Should().BeEmpty();
    }
}
