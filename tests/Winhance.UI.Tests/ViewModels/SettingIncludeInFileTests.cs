using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.TestSupport;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SettingIncludeInFileTests
{
    private readonly Mock<ISettingApplicationService> _applyService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IApplicationModeService> _modeService = new();

    private readonly HashSet<string> _excluded = new();

    private static readonly string[] TheParentAndBothChildren = ["parent", "first", "second"];

    public SettingIncludeInFileTests()
    {
        _localizationService.Setup(l => l.GetString(It.IsAny<string>())).Returns((string k) => k);
        _localizationService.MirrorTryGetString();

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

    private SettingItemViewModel CreateSut(string id = "card") => CreateSut(Config(id));

    private SettingItemViewModel CreateSut(SettingItemViewModelConfig config) =>
        new(
            config,
            SettingWriteStrategies.Selector(
                _applyService.Object, _dialogService.Object, _localizationService.Object,
                _logService.Object, _modeService.Object),
            _logService.Object,
            _dispatcherService.Object,
            _dialogService.Object,
            _localizationService.Object,
            null,
            null,
            null,
            _modeService.Object);

    private static SettingItemViewModelConfig Config(string id) => new()
    {
        Setting = new Setting { Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of("") } },
        SettingId = id,
        Name = id,
        Description = "",
        InputType = InputType.Toggle,
        Icon = "Cog",
        IconPack = "Material",
    };

    private static SettingStateResult LiveState(bool isEnabled = false) => new()
    {
        Success = true,
        IsEnabled = isEnabled,
        Outcome = SettingDetectionOutcome.Resolved,
    };

    private SettingItemViewModel Parent(params SettingItemViewModel[] children)
    {
        var parent = CreateSut("parent");
        parent.Children = new ObservableCollection<SettingItemViewModel>(children);
        return parent;
    }

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
    public void ACardNobodyTouched_IsInTheFile()
    {
        var sut = CreateSut();

        sut.IsIncluded.Should().BeTrue();
        sut.IncludeState.Should().BeTrue();
        sut.EffectiveIsEnabled.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(AuthoringModes))]
    public void WhileAModeAuthorsIntent_TheCheckboxIsShown(WinhanceMode mode)
    {
        _modeService.SetupGet(m => m.CurrentMode).Returns(mode);

        CreateSut().ShowIncludeCheckbox.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(NonAuthoringModes))]
    public void OutsideAnAuthoringMode_TheCheckboxIsHidden(WinhanceMode mode)
    {
        _modeService.SetupGet(m => m.CurrentMode).Returns(mode);

        var sut = CreateSut();

        sut.ShowIncludeCheckbox.Should().BeFalse();
    }

    [Fact]
    public void AnAnswerFileSetting_OffersNoIncludeCheckbox()
    {
        var config = Config("autounattend-computer-name") with
        {
            Setting = new Setting
            {
                Id = "autounattend-computer-name",
                Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("") },
                TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything."))),
                Targets = [new AutounattendElement("name", "specialize", "Microsoft-Windows-Shell-Setup", "ComputerName")],
            },
            InputType = InputType.TextBox,
        };

        CreateSut(config).ShowIncludeCheckbox.Should().BeFalse("an answer file has no way to leave a setting out");
    }

    [Fact]
    public void WhileAuthoring_TheBoxTooltipSaysWhetherTheCardIsInTheFile()
    {
        _localizationService.PresentKey("Setting_Include_Tooltip_In", "In the file");
        _localizationService.PresentKey("Setting_Include_Tooltip_Out", "Out of the file");
        var sut = CreateSut();
        sut.IncludeToolTip.Should().Be("In the file");

        sut.IncludeState = false;

        sut.IncludeToolTip.Should().Be("Out of the file");
    }

    [Fact]
    public void WhileAuthoring_ACardWidensItsGutterForTheBox()
    {
        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        var sut = CreateSut();
        sut.CardPadding.Left.Should().Be(16);
        sut.ChildCardPadding.Left.Should().Be(54);
        sut.RowPadding.Left.Should().Be(56, "a row under a card starts where the header's text does");

        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _modeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        sut.CardPadding.Left.Should().Be(72, "the box, its glyph and their gaps take 56px");
        sut.ChildCardPadding.Left.Should().Be(110, "a child keeps its indentation under the box");
        sut.RowPadding.Left.Should().Be(112, "a row under a card keeps its indentation under the box");
    }

    [Fact]
    public void EnteringAnAuthoringMode_ShowsTheCheckboxWithoutRebuildingTheCard()
    {
        // Entering Builder rebuilds no card: BuilderSeededEvent fires only for a seed that is not this machine.
        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        var sut = CreateSut();
        sut.ShowIncludeCheckbox.Should().BeFalse();

        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _modeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        sut.ShowIncludeCheckbox.Should().BeTrue();
    }

    [Fact]
    public void UntickingTheBox_RecordsTheExclusionOnTheSession()
    {
        var sut = CreateSut("dropped");

        sut.IncludeState = false;

        _modeService.Verify(m => m.SetIncluded("dropped", false), Times.Once);
        _excluded.Should().Contain("dropped");
    }

    [Fact]
    public void UntickingTheBox_DisablesTheValueControl()
    {
        var sut = CreateSut();

        sut.IncludeState = false;

        sut.EffectiveIsEnabled.Should().BeFalse(
            because: "the card has to say the setting is out without a second word on it");
    }

    [Fact]
    public void UntickingTheBox_KeepsTheAuthoredValue()
    {
        var sut = CreateSut();
        sut.IsSelected = true;

        sut.IncludeState = false;

        sut.IsSelected.Should().BeTrue(because: "re-ticking has to restore what the user chose");
    }

    [Fact]
    public void ARebuildFromLiveState_ReReadsTheExclusion_EvenWithNoAuthoredEdit()
    {
        // The authored-edit lookup returns early for a card with no edit, so the re-read cannot sit behind it.
        _excluded.Add("dropped");
        var sut = CreateSut("dropped");
        sut.IsIncluded.Should().BeTrue();

        sut.UpdateStateFromSystemState(LiveState(isEnabled: true));

        sut.IsIncluded.Should().BeFalse();
    }

    [Fact]
    public void ReReadingTheExclusion_DoesNotRecordItAgain()
    {
        _excluded.Add("dropped");
        var sut = CreateSut("dropped");

        sut.UpdateStateFromSystemState(LiveState());

        _modeService.Verify(m => m.SetIncluded(It.IsAny<string>(), It.IsAny<bool>()), Times.Never,
            failMessage: "restoring the user's own tick must not re-enter the recording path");
    }

    [Fact]
    public void LeavingTheAuthoringMode_PutsTheCardBackInTheFile()
    {
        var sut = CreateSut("dropped");
        sut.IncludeState = false;

        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        _modeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        sut.IsIncluded.Should().BeTrue();
        sut.EffectiveIsEnabled.Should().BeTrue();
    }

    [Fact]
    public void AParentWhoseChildrenDisagree_ReadsIndeterminate()
    {
        var included = CreateSut("in");
        var excluded = CreateSut("out");
        var parent = Parent(included, excluded);

        excluded.IncludeState = false;

        parent.IncludeState.Should().BeNull();
    }

    [Fact]
    public void AnExcludedParentWithIncludedChildren_AlsoReadsIndeterminate()
    {
        var parent = Parent(CreateSut("in"));

        parent.IsIncluded = false;

        parent.IncludeState.Should().BeNull();
    }

    [Fact]
    public void UntickingAParent_UnticksItAndEveryChild()
    {
        var first = CreateSut("first");
        var second = CreateSut("second");
        var parent = Parent(first, second);

        parent.IncludeState = false;

        parent.IsIncluded.Should().BeFalse();
        first.IsIncluded.Should().BeFalse();
        second.IsIncluded.Should().BeFalse();
        _excluded.Should().BeEquivalentTo(TheParentAndBothChildren);
    }

    [Fact]
    public void TickingAParentBack_TicksItAndEveryChild()
    {
        var first = CreateSut("first");
        var second = CreateSut("second");
        var parent = Parent(first, second);
        parent.IncludeState = false;

        parent.IncludeState = true;

        parent.IncludeState.Should().BeTrue();
        _excluded.Should().BeEmpty();
    }

    [Fact]
    public void SettingIsIncludedOnAParent_LeavesItsChildrenAlone()
    {
        // Only the checkbox cascades. Quick Actions walks the flat list and reaches every child directly.
        var child = CreateSut("child");
        var parent = Parent(child);

        parent.IsIncluded = false;

        child.IsIncluded.Should().BeTrue();
    }

    [Fact]
    public void AChildChanging_ReDerivesTheParentsBox()
    {
        var child = CreateSut("child");
        var parent = Parent(child);
        var raised = new List<string?>();
        parent.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        child.IncludeState = false;

        raised.Should().Contain(nameof(SettingItemViewModel.IncludeState));
    }

    [Fact]
    public void TheAutomationNameNamesTheSetting()
    {
        _localizationService.Setup(l => l.GetString("Setting_Include_AutomationName"))
            .Returns("Include {0} in the file");
        var sut = CreateSut("telemetry");

        sut.IncludeAutomationName.Should().Be("Include telemetry in the file");
    }

    [Fact]
    public void Dispose_StopsListeningToTheModeService()
    {
        // ShowIncludeCheckbox is computed live, so only the absence of a notification proves the handler is gone.
        var sut = CreateSut();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.Dispose();
        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        _modeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        raised.Should().BeEmpty();
    }
}
