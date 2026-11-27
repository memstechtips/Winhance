using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class TaskProgressViewModelTests : IDisposable
{
    private static readonly string[] ErrorOutput = ["error"];

    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IDebounceTimer> _hideTimer = new();
    private Action? _hideTick;

    private readonly TaskProgressViewModel _sut;

    public TaskProgressViewModelTests()
    {
        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());
        _mockDispatcherService
            .Setup(d => d.CreateDebounceTimer(It.IsAny<TimeSpan>(), It.IsAny<Action>()))
            .Callback<TimeSpan, Action>((_, tick) => _hideTick = tick)
            .Returns(_hideTimer.Object);

        // Default localization returns null so fallbacks are used
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => null!);
        _mockTaskProgressService.Setup(s => s.GetTerminalOutputLines()).Returns(Array.Empty<string>());

        _sut = new TaskProgressViewModel(
            _mockTaskProgressService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_InitializesDefaultProperties()
    {
        _sut.IsLoading.Should().BeFalse();
        _sut.IsTaskFailed.Should().BeFalse();
        _sut.AppName.Should().BeEmpty();
        _sut.LastTerminalLine.Should().BeEmpty();
        _sut.QueueStatusText.Should().BeEmpty();
        _sut.QueueNextItemName.Should().BeEmpty();
        _sut.IsQueueVisible.Should().BeFalse();
        _sut.ActiveScriptCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_SubscribesToProgressUpdated()
    {
        _mockTaskProgressService
            .Setup(s => s.IsTaskRunning)
            .Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { StatusText = "Installing...", TerminalOutput = "line1" });

        _sut.IsLoading.Should().BeTrue();
        _sut.AppName.Should().Be("Installing...");
    }

    [Fact]
    public void OnProgressUpdated_TaskRunning_SetsIsLoadingTrue()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { StatusText = "Working..." });

        _sut.IsLoading.Should().BeTrue();
    }

    [Fact]
    public void OnProgressUpdated_TaskRunning_UpdatesAppName()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { StatusText = "Installing App X" });

        _sut.AppName.Should().Be("Installing App X");
    }

    [Fact]
    public void OnProgressUpdated_TaskRunning_UpdatesLastTerminalLine()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { StatusText = "Work", TerminalOutput = "Downloading..." });

        _sut.LastTerminalLine.Should().Be("Downloading...");
    }

    [Fact]
    public void OnProgressUpdated_TaskRunning_NullTerminalOutput_SetsEmpty()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { StatusText = "Work", TerminalOutput = null });

        _sut.LastTerminalLine.Should().BeEmpty();
    }

    [Fact]
    public void OnProgressUpdated_TaskRunning_ProgressZero_DoesNotMarkTheTaskFailed()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { Progress = 0, StatusText = "Starting" });

        _sut.IsTaskFailed.Should().BeFalse();
    }

    [Fact]
    public void OnProgressUpdated_TaskRunning_ProgressNonZero_DoesNotSetTaskFailed()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { Progress = 50, StatusText = "Half done" });

        _sut.IsTaskFailed.Should().BeFalse();
    }

    [Fact]
    public void OnProgressUpdated_TaskRunning_EmptyStatusText_DoesNotUpdateAppName()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { StatusText = "Original" });

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { StatusText = "" });

        _sut.AppName.Should().Be("Original");
    }

    [Fact]
    public void OnProgressUpdated_TaskEndsFailedWithoutOutput_EndsLikeAnyOtherTask()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Raise(s => s.ProgressUpdated += null, this, new TaskProgressDetail { Progress = 0, StatusText = "Creating restore point" });

        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(false);
        _mockTaskProgressService.Raise(s => s.ProgressUpdated += null, this, new TaskProgressDetail { Progress = 0, StatusText = "Creating restore point" });

        _sut.IsTaskFailed.Should().BeFalse();
        _hideTimer.Verify(t => t.Restart(), Times.Once);

        _hideTick!();

        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void FailTask_OnTheRealService_LeavesNoTaskRunningAndHidesTheBar()
    {
        var service = new TaskProgressService(Mock.Of<ILogService>());
        using var sut = new TaskProgressViewModel(service, _mockDispatcherService.Object, _mockDialogService.Object, _mockLocalizationService.Object);

        service.StartTask("Creating restore point");
        service.FailTask();

        service.IsTaskRunning.Should().BeFalse();
        sut.IsTaskFailed.Should().BeFalse();
        _hideTick!();
        sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void OnProgressUpdated_TaskJustStopped_Cancelled_HidesImmediately()
    {
        var cts = new CancellationTokenSource();
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Setup(s => s.CurrentTaskCancellationSource).Returns(cts);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { Progress = 0, StatusText = "Working" });

        _sut.IsLoading.Should().BeTrue();

        cts.Cancel();

        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(false);
        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { Progress = 0, StatusText = "Working" });

        _sut.IsLoading.Should().BeFalse();
        _sut.IsTaskFailed.Should().BeFalse();
    }

    [Fact]
    public void OnProgressUpdated_TaskJustStopped_Succeeded_HidesWhenTheTimerFires()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Raise(s => s.ProgressUpdated += null, this, new TaskProgressDetail { StatusText = "Working" });

        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(false);
        _mockTaskProgressService.Raise(s => s.ProgressUpdated += null, this, new TaskProgressDetail { StatusText = "Done" });

        _sut.IsLoading.Should().BeTrue();
        _hideTimer.Verify(t => t.Restart(), Times.Once);

        _hideTick!();

        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void OnProgressUpdated_TaskEndsFailedWithOutput_OffersCloseAndTheDetails()
    {
        _mockLocalizationService.Setup(l => l.GetString("Progress_ClickToSeeDetails")).Returns("Click to see details");
        _mockTaskProgressService.Setup(s => s.GetTerminalOutputLines()).Returns(ErrorOutput);
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Raise(s => s.ProgressUpdated += null, this, new TaskProgressDetail { StatusText = "Installing" });

        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(false);
        _mockTaskProgressService.Raise(s => s.ProgressUpdated += null, this, new TaskProgressDetail { Progress = 0, StatusText = "Installing" });

        _sut.IsTaskFailed.Should().BeTrue();
        _sut.IsLoading.Should().BeTrue();
        _sut.LastTerminalLine.Should().Be("Click to see details");
        _hideTimer.Verify(t => t.Restart(), Times.Never);
    }

    [Fact]
    public void LanguageChanged_WhileFailed_RelocalizesTheClickToSeeDetailsLine()
    {
        _mockLocalizationService.Setup(l => l.GetString("Progress_ClickToSeeDetails")).Returns("Klicken Sie hier");
        _mockTaskProgressService.Setup(s => s.GetTerminalOutputLines()).Returns(ErrorOutput);
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Raise(s => s.ProgressUpdated += null, this, new TaskProgressDetail { StatusText = "Installing" });
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(false);
        _mockTaskProgressService.Raise(s => s.ProgressUpdated += null, this, new TaskProgressDetail { Progress = 0, StatusText = "Installing" });

        _mockLocalizationService.Setup(l => l.GetString("Progress_ClickToSeeDetails")).Returns("Click to see details");
        _mockLocalizationService.Raise(l => l.LanguageChanged += null, this, EventArgs.Empty);

        _sut.LastTerminalLine.Should().Be("Click to see details");
    }

    [Fact]
    public void OnProgressUpdated_QueueTotalGreaterThan1_ShowsQueue()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail
            {
                StatusText = "Installing",
                QueueTotal = 3,
                QueueCurrent = 1,
                QueueNextItemName = "App B"
            });

        _sut.IsQueueVisible.Should().BeTrue();
        _sut.QueueStatusText.Should().Be("1 / 3");
        _sut.QueueNextItemName.Should().Be("Next: App B");
    }

    [Fact]
    public void OnProgressUpdated_QueueTotalGreaterThan1_EmptyNextItemName_SetsEmptyQueueNextItemName()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail
            {
                StatusText = "Installing",
                QueueTotal = 2,
                QueueCurrent = 2,
                QueueNextItemName = ""
            });

        _sut.QueueNextItemName.Should().BeEmpty();
    }

    [Fact]
    public void OnProgressUpdated_QueueTotal1OrLess_HidesQueue()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { StatusText = "Installing", QueueTotal = 1 });

        _sut.IsQueueVisible.Should().BeFalse();
        _sut.QueueStatusText.Should().BeEmpty();
        _sut.QueueNextItemName.Should().BeEmpty();
    }

    [Fact]
    public void OnProgressUpdated_MultiScriptMode_UpdatesActiveScriptCount()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { ScriptSlotCount = 3, ScriptSlotIndex = 0 });

        _sut.ActiveScriptCount.Should().Be(3);
    }

    [Fact]
    public void OnProgressUpdated_MultiScriptMode_RaisesScriptProgressReceived()
    {
        int receivedSlotIndex = -1;
        TaskProgressDetail? receivedDetail = null;
        _sut.ScriptProgressReceived += (index, detail) =>
        {
            receivedSlotIndex = index;
            receivedDetail = detail;
        };

        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);
        var progressDetail = new TaskProgressDetail { ScriptSlotCount = 2, ScriptSlotIndex = 1, StatusText = "Script 2" };

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            progressDetail);

        receivedSlotIndex.Should().Be(1);
        receivedDetail.Should().BeSameAs(progressDetail);
    }

    [Fact]
    public void OnProgressUpdated_MultiScriptComplete_ResetsActiveScriptCount()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { ScriptSlotCount = 2, ScriptSlotIndex = 0 });
        _sut.ActiveScriptCount.Should().Be(2);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { ScriptSlotCount = 0, ScriptSlotIndex = -1 });

        _sut.ActiveScriptCount.Should().Be(0);
    }

    [Fact]
    public void CancelCommand_WhileRunning_CancelsAndShowsCancellingUntilTheTaskEnds()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Raise(s => s.ProgressUpdated += null, this, new TaskProgressDetail { StatusText = "Creating restore point" });

        _sut.CancelCommand.Execute(null);

        _mockTaskProgressService.Verify(s => s.CancelCurrentTask(), Times.Once);
        _sut.IsCancelling.Should().BeTrue();
        _sut.IsLoading.Should().BeTrue();

        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(false);
        _mockTaskProgressService.Raise(s => s.ProgressUpdated += null, this, new TaskProgressDetail { Progress = 0, StatusText = "Creating restore point" });

        _sut.IsCancelling.Should().BeFalse();
        _sut.IsLoading.Should().BeFalse();
        _sut.IsTaskFailed.Should().BeFalse();
    }

    [Fact]
    public void CancelCommand_WithNoTaskRunning_DoesNothing()
    {
        _sut.CancelCommand.Execute(null);

        _mockTaskProgressService.Verify(s => s.CancelCurrentTask(), Times.Never);
        _sut.IsCancelling.Should().BeFalse();
    }

    [Fact]
    public void CloseFailedTaskCommand_ResetsLoadingAndFailedState()
    {
        _mockTaskProgressService.Setup(s => s.GetTerminalOutputLines()).Returns(ErrorOutput);
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Raise(s => s.ProgressUpdated += null, this, new TaskProgressDetail { StatusText = "Installing" });
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(false);
        _mockTaskProgressService.Raise(s => s.ProgressUpdated += null, this, new TaskProgressDetail { Progress = 0, StatusText = "Installing" });

        _sut.IsTaskFailed.Should().BeTrue();
        _sut.IsLoading.Should().BeTrue();

        _sut.CloseFailedTaskCommand.Execute(null);

        _sut.IsTaskFailed.Should().BeFalse();
        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void CancelButtonLabel_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        _sut.CancelButtonLabel.Should().Be("Cancel");
    }

    [Fact]
    public void CloseButtonLabel_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        _sut.CloseButtonLabel.Should().Be("Close");
    }

    [Fact]
    public void CancellingLabel_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        _sut.CancellingLabel.Should().Be("Cancelling...");
    }

    [Fact]
    public void LanguageChanged_NotifiesButtonLabels()
    {
        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _mockLocalizationService.Raise(l => l.LanguageChanged += null, this, EventArgs.Empty);

        changedProperties.Should().Contain(nameof(_sut.CancelButtonLabel));
        changedProperties.Should().Contain(nameof(_sut.CloseButtonLabel));
        changedProperties.Should().Contain(nameof(_sut.CancellingLabel));
    }

    [Fact]
    public void Dispose_UnsubscribesFromProgressUpdated()
    {
        var sut = new TaskProgressViewModel(
            _mockTaskProgressService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object);

        sut.Dispose();

        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { StatusText = "Should not appear" });

        sut.IsLoading.Should().BeFalse();
        sut.AppName.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var sut = new TaskProgressViewModel(
            _mockTaskProgressService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object);

        var act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        act.Should().NotThrow();
    }
}
