using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.Helpers;

/// <summary>
/// The banner's row rules: which outcomes appear, in what order, and how many names before "+N more".
/// These ran only through the UserControl before, so they needed a XAML application to reach.
/// </summary>
public class FeatureOutcomeRowBuilderTests
{
    private readonly Mock<ISettingApplicationService> _settingAppService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IEventBus> _eventBus = new();

    public FeatureOutcomeRowBuilderTests()
    {
        // Mirrors the per-key GetString stubs onto TryGetString - an unstubbed Moq answers
        // "missing" for every key.
        _localizationService.MirrorTryGetString();
    }

    [Fact]
    public void Build_WithNullFeature_ReturnsNoRows()
    {
        FeatureOutcomeRowBuilder.Build(null).Should().BeEmpty();
    }

    [Fact]
    public void Build_WithNoSettings_ReturnsNoRows()
    {
        FeatureOutcomeRowBuilder.Build(Feature()).Should().BeEmpty();
    }

    [Fact]
    public void Build_WhenEverySettingResolved_ReturnsNoRows()
    {
        var feature = Feature(
            Setting("a", "Alpha", SettingDetectionOutcome.Resolved),
            Setting("b", "Bravo", SettingDetectionOutcome.Resolved));

        FeatureOutcomeRowBuilder.Build(feature).Should().BeEmpty();
    }

    [Fact]
    public void Build_OrdersRowsWorstFirst()
    {
        var feature = Feature(
            Setting("c", "Charlie", SettingDetectionOutcome.Custom),
            Setting("u", "Uniform", SettingDetectionOutcome.Undetermined),
            Setting("m", "Mike", SettingDetectionOutcome.Malformed));

        var rows = FeatureOutcomeRowBuilder.Build(feature);

        rows.Select(r => r.Outcome).Should().Equal(
            SettingDetectionOutcome.Undetermined,
            SettingDetectionOutcome.Malformed,
            SettingDetectionOutcome.Custom);
    }

    [Fact]
    public void Build_OmitsOutcomesWithNoSettings()
    {
        var feature = Feature(
            Setting("m", "Mike", SettingDetectionOutcome.Malformed),
            Setting("r", "Romeo", SettingDetectionOutcome.Resolved));

        var rows = FeatureOutcomeRowBuilder.Build(feature);

        rows.Should().ContainSingle();
        rows[0].Outcome.Should().Be(SettingDetectionOutcome.Malformed);
        rows[0].Names.Should().Equal("Mike");
        rows[0].Remaining.Should().Be(0);
    }

    [Fact]
    public void Build_ListsEveryNameWhenAtTheLimit()
    {
        var feature = Feature(
            Setting("1", "One", SettingDetectionOutcome.Custom),
            Setting("2", "Two", SettingDetectionOutcome.Custom),
            Setting("3", "Three", SettingDetectionOutcome.Custom));

        var row = FeatureOutcomeRowBuilder.Build(feature).Single();

        row.Names.Should().Equal("One", "Two", "Three");
        row.Remaining.Should().Be(0);
    }

    [Fact]
    public void Build_TruncatesPastTheLimitAndReportsTheRemainder()
    {
        var feature = Feature(
            Setting("1", "One", SettingDetectionOutcome.Custom),
            Setting("2", "Two", SettingDetectionOutcome.Custom),
            Setting("3", "Three", SettingDetectionOutcome.Custom),
            Setting("4", "Four", SettingDetectionOutcome.Custom),
            Setting("5", "Five", SettingDetectionOutcome.Custom));

        var row = FeatureOutcomeRowBuilder.Build(feature).Single();

        row.Names.Should().Equal("One", "Two", "Three");
        row.Remaining.Should().Be(2);
    }

    [Fact]
    public void Build_CountsRemainderPerOutcomeNotAcrossTheBanner()
    {
        var feature = Feature(
            Setting("m1", "M1", SettingDetectionOutcome.Malformed),
            Setting("m2", "M2", SettingDetectionOutcome.Malformed),
            Setting("m3", "M3", SettingDetectionOutcome.Malformed),
            Setting("m4", "M4", SettingDetectionOutcome.Malformed),
            Setting("c1", "C1", SettingDetectionOutcome.Custom));

        var rows = FeatureOutcomeRowBuilder.Build(feature);

        rows.Should().HaveCount(2);
        rows[0].Remaining.Should().Be(1);
        rows[1].Names.Should().Equal("C1");
        rows[1].Remaining.Should().Be(0);
    }

    [Theory]
    [InlineData(SettingDetectionOutcome.Undetermined, FluentIcons.Common.Icon.DismissCircle)]
    [InlineData(SettingDetectionOutcome.Malformed, FluentIcons.Common.Icon.ErrorCircle)]
    [InlineData(SettingDetectionOutcome.Custom, FluentIcons.Common.Icon.QuestionCircle)]
    public void Build_TakesTheIconFromTheSettingItself(
        SettingDetectionOutcome outcome, FluentIcons.Common.Icon expected)
    {
        var setting = Setting("x", "Xray", outcome);
        var row = FeatureOutcomeRowBuilder.Build(Feature(setting)).Single();

        // The banner must show the same glyph the setting's own control shows.
        row.Icon.Should().Be(expected);
        row.Icon.Should().Be(setting.OverlayIconFor(outcome));
    }

    [Fact]
    public void Build_TakesTheLabelFromTheSettingItself()
    {
        // A distinctive translation, so this cannot pass by both paths landing on the same English
        // fallback. It fails if the builder reverts to its own label map or the old InfoBadge_* keys.
        _localizationService.Setup(l => l.GetString("Common_MalformedState")).Returns("TRANSLATED");

        var setting = Setting("x", "Xray", SettingDetectionOutcome.Malformed);
        var row = FeatureOutcomeRowBuilder.Build(Feature(setting)).Single();

        row.Label.Should().Be("TRANSLATED");
        row.Label.Should().Be(setting.OverlayStateTextFor(SettingDetectionOutcome.Malformed));
    }

    // ── helpers ──

    private ISettingsFeatureViewModel Feature(params SettingItemViewModel[] settings)
    {
        var mock = new Mock<ISettingsFeatureViewModel>();
        mock.Setup(f => f.Settings)
            .Returns(new ObservableCollection<SettingItemViewModel>(settings));
        return mock.Object;
    }

    private SettingItemViewModel Setting(string id, string name, SettingDetectionOutcome outcome)
    {
        var config = new SettingItemViewModelConfig
        {
            Setting = new Setting { Id = id, Display = new() { Name = name, Description = "d" } },
            SettingId = id,
            Name = name,
            Description = "d",
            InputType = InputType.Toggle,
            Outcome = outcome,
        };

        return new SettingItemViewModel(
            config,
            SettingWriteStrategies.Selector(
                _settingAppService.Object, _dialogService.Object, _localizationService.Object, _logService.Object),
            _logService.Object,
            _dispatcherService.Object,
            _dialogService.Object,
            _localizationService.Object,
            _eventBus.Object);
    }
}
