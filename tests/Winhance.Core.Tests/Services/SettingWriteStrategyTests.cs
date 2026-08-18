using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Xunit;

namespace Winhance.Core.Tests.Services;

// The branches these replace sat behind five async UI handlers and were effectively unreachable from a unit
// test, which is how two of them could stop recording edits without anything going red.
public class SettingWriteStrategyTests
{
    private readonly Mock<ISettingApplicationService> _applyService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IApplicationModeService> _modeService = new();

    private sealed class ProgressSpy : ISettingWriteProgress
    {
        private bool _isApplying;

        public List<bool> Transitions { get; } = new();

        public bool IsApplying
        {
            get => _isApplying;
            set { _isApplying = value; Transitions.Add(value); }
        }
    }

    private static SettingWriteRequest Request(
        string settingId = "some-setting",
        BuilderEdit? authoredEdit = null,
        bool requiresConfirmation = false,
        bool checkboxAlsoAppliesRecommended = false,
        object? value = null) =>
        new()
        {
            Description = "test edit",
            SystemRequest = new ApplySettingRequest { SettingId = settingId, Enable = true, Value = value },
            AuthoredEdit = authoredEdit ?? new BuilderEdit { SettingId = settingId, InputType = InputType.Toggle, IsSelected = true },
            RequiresConfirmation = requiresConfirmation,
            CheckboxAlsoAppliesRecommended = checkboxAlsoAppliesRecommended,
        };

    private LiveSettingWriteStrategy Live() => new(
        _applyService.Object, _dialogService.Object, _localizationService.Object, _logService.Object);

    private BuilderSettingWriteStrategy Builder() => new(_modeService.Object, _logService.Object);

    private ReadOnlySettingWriteStrategy ReadOnly() => new(_logService.Object);

    private void ApplySucceeds() =>
        _applyService.Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

    private void ApplyFails() =>
        _applyService.Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Failed("nope"));

    [Fact]
    public async Task Live_AppliesToTheMachineAndReportsApplied()
    {
        ApplySucceeds();

        var result = await Live().WriteAsync(Request(), new ProgressSpy());

        result.Outcome.Should().Be(SettingWriteOutcome.Applied);
        _applyService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "some-setting")), Times.Once);
    }

    [Fact]
    public async Task Live_RejectsWhenTheApplyFails()
    {
        ApplyFails();

        var result = await Live().WriteAsync(Request(), new ProgressSpy());

        result.Outcome.Should().Be(SettingWriteOutcome.Rejected);
    }

    [Fact]
    public async Task Live_RejectsWhenTheApplyThrows()
    {
        _applyService.Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await Live().WriteAsync(Request(), new ProgressSpy());

        result.Outcome.Should().Be(SettingWriteOutcome.Rejected,
            because: "a throwing apply and a failing apply must leave the caller with one revert path, not two");
    }

    [Fact]
    public async Task Live_RaisesAndLowersProgressAroundTheApply()
    {
        ApplySucceeds();
        var progress = new ProgressSpy();

        await Live().WriteAsync(Request(), progress);

        progress.Transitions.Should().Equal(true, false);
        progress.IsApplying.Should().BeFalse();
    }

    [Fact]
    public async Task Live_LowersProgressEvenWhenTheApplyThrows()
    {
        _applyService.Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var progress = new ProgressSpy();

        await Live().WriteAsync(Request(), progress);

        progress.IsApplying.Should().BeFalse(
            because: "a card stuck showing a progress ring after a failed apply is unusable");
    }

    [Fact]
    public async Task Live_DoesNotPromptWhenTheRequestDoesNotAskFor()
    {
        ApplySucceeds();

        await Live().WriteAsync(Request(requiresConfirmation: false), new ProgressSpy());

        _dialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    [Fact]
    public async Task Live_RejectsWithoutApplyingWhenTheUserCancels()
    {
        ApplySucceeds();
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        var result = await Live().WriteAsync(Request(requiresConfirmation: true), new ProgressSpy());

        result.Outcome.Should().Be(SettingWriteOutcome.Rejected);
        _applyService.Verify(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Never);
    }

    [Fact]
    public async Task Live_DoesNotShowProgressWhileTheConfirmationIsOpen()
    {
        ApplySucceeds();
        var progress = new ProgressSpy();
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true })
            .Callback(() => progress.IsApplying.Should().BeFalse(
                because: "the machine is not busy while a dialog waits on the user"));

        await Live().WriteAsync(Request(requiresConfirmation: true), progress);

        progress.Transitions.Should().Equal(true, false);
    }

    [Fact]
    public async Task Live_PassesTheCheckboxThroughAsTheCheckboxResult()
    {
        ApplySucceeds();
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true, CheckboxChecked = true });

        var result = await Live().WriteAsync(Request(requiresConfirmation: true), new ProgressSpy());

        result.ConfirmationCheckboxChecked.Should().BeTrue();
        _applyService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(
            r => r.CheckboxResult && !r.ApplyRecommended)), Times.Once);
    }

    [Fact]
    public async Task Live_AlsoAppliesRecommendedWhenTheRequestSaysTheCheckboxMeansThat()
    {
        ApplySucceeds();
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true, CheckboxChecked = true });

        await Live().WriteAsync(
            Request(requiresConfirmation: true, checkboxAlsoAppliesRecommended: true),
            new ProgressSpy());

        _applyService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(
            r => r.CheckboxResult && r.ApplyRecommended)), Times.Once);
    }

    [Fact]
    public async Task Live_RecordsNoBuilderEdit()
    {
        ApplySucceeds();

        await Live().WriteAsync(Request(), new ProgressSpy());

        _modeService.Verify(m => m.RecordBuilderEdit(It.IsAny<BuilderEdit>()), Times.Never);
    }

    [Fact]
    public async Task Builder_RecordsTheEditAndReportsRecorded()
    {
        var edit = new BuilderEdit { SettingId = "authored", InputType = InputType.NumericRange, NumericValue = 42 };

        var result = await Builder().WriteAsync(Request(authoredEdit: edit), new ProgressSpy());

        result.Outcome.Should().Be(SettingWriteOutcome.Recorded);
        _modeService.Verify(m => m.RecordBuilderEdit(edit), Times.Once);
    }

    [Fact]
    public async Task Builder_NeverTouchesTheMachine()
    {
        await Builder().WriteAsync(Request(requiresConfirmation: true), new ProgressSpy());

        _applyService.Verify(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Never);
        _dialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    [Fact]
    public async Task Builder_NeverFlagsProgress()
    {
        var progress = new ProgressSpy();

        await Builder().WriteAsync(Request(), progress);

        progress.Transitions.Should().BeEmpty(
            because: "authoring is instantaneous - a progress ring blinking on every edit is a visible defect");
    }

    [Fact]
    public async Task Builder_WarnsRatherThanSilentlyDroppingAnUnrepresentableEdit()
    {
        var result = await Builder().WriteAsync(
            Request() with { AuthoredEdit = null },
            new ProgressSpy());

        result.Outcome.Should().Be(SettingWriteOutcome.Recorded,
            because: "the value the user set still has to show on the card");
        _modeService.Verify(m => m.RecordBuilderEdit(It.IsAny<BuilderEdit>()), Times.Never);
        _logService.Verify(l => l.Log(LogLevel.Warning, It.IsAny<string>(), It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task ReadOnly_RefusesWithoutApplyingOrRecording()
    {
        var result = await ReadOnly().WriteAsync(Request(), new ProgressSpy());

        result.Outcome.Should().Be(SettingWriteOutcome.Rejected);
        _applyService.Verify(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Never);
        _modeService.Verify(m => m.RecordBuilderEdit(It.IsAny<BuilderEdit>()), Times.Never);
    }

    [Fact]
    public async Task ReadOnly_NeverFlagsProgress()
    {
        var progress = new ProgressSpy();

        await ReadOnly().WriteAsync(Request(), progress);

        progress.Transitions.Should().BeEmpty();
    }

    [Theory]
    [InlineData(WinhanceMode.Normal, typeof(LiveSettingWriteStrategy))]
    [InlineData(WinhanceMode.Builder, typeof(BuilderSettingWriteStrategy))]
    [InlineData(WinhanceMode.ConfigReview, typeof(ReadOnlySettingWriteStrategy))]
    public void Selector_PicksTheStrategyTheModesCapabilitiesCallFor(WinhanceMode mode, Type expected)
    {
        _modeService.SetupGet(m => m.CurrentMode).Returns(mode);

        var sut = new SettingWriteStrategySelector(_modeService.Object, Live(), Builder(), ReadOnly());

        sut.ForCurrentMode().Should().BeOfType(expected);
    }

    [Fact]
    public void Selector_ResolvesPerCallSoAModeSwitchTakesEffectImmediately()
    {
        var sut = new SettingWriteStrategySelector(_modeService.Object, Live(), Builder(), ReadOnly());

        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        sut.ForCurrentMode().Should().BeOfType<LiveSettingWriteStrategy>();

        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        sut.ForCurrentMode().Should().BeOfType<BuilderSettingWriteStrategy>(
            because: "ViewModels outlive the mode, so the strategy cannot be resolved once and held");
    }
}
