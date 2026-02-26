using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class TaskProgressViewModelTests : IDisposable
{
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<ILogService> _mockLogService = new();

    private readonly TaskProgressViewModel _sut;

    public TaskProgressViewModelTests()
    {
        // Set up dispatcher to execute actions synchronously
        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());

        // Default localization returns null so fallbacks are used
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => null!);

        _sut = new TaskProgressViewModel(
            _mockTaskProgressService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ── Constructor ──

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
        // Verify by raising the event and checking that properties update
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

    // ── Progress Updates: Task Running ──

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
    public void OnProgressUpdated_TaskRunning_ProgressZeroWithStatusText_SetsTaskFailed()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { Progress = 0, StatusText = "Error occurred" });

        _sut.IsTaskFailed.Should().BeTrue();
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

        // First set a known AppName
        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { StatusText = "Original" });

        // Then send update with empty status text
        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { StatusText = "" });

        _sut.AppName.Should().Be("Original");
    }

    // ── Progress Updates: Task Completion ──

    [Fact]
    public void OnProgressUpdated_TaskJustStopped_Failed_KeepsLoadingTrue()
    {
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);

        // First, simulate a running task that fails
        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { Progress = 0, StatusText = "Error" });

        _sut.IsTaskFailed.Should().BeTrue();
        _sut.IsLoading.Should().BeTrue();

        // Now task stops
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(false);
        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { StatusText = "" });

        // Failed task should stay visible
        _sut.IsLoading.Should().BeTrue();
    }

    // ── Progress Updates: Queue Display ──

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

    // ── Progress Updates: Multi-Script Mode ──

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

        // First, enter multi-script mode
        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { ScriptSlotCount = 2, ScriptSlotIndex = 0 });
        _sut.ActiveScriptCount.Should().Be(2);

        // Then, receive completion (ScriptSlotCount = 0, ScriptSlotIndex = -1)
        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { ScriptSlotCount = 0, ScriptSlotIndex = -1 });

        _sut.ActiveScriptCount.Should().Be(0);
    }

    // ── Cancel Command ──

    [Fact]
    public void CancelCommand_DelegatesToTaskProgressService()
    {
        _sut.CancelCommand.Execute(null);

        _mockTaskProgressService.Verify(s => s.CancelCurrentTask(), Times.Once);
    }

    // ── CloseFailedTask Command ──

    [Fact]
    public void CloseFailedTaskCommand_ResetsLoadingAndFailedState()
    {
        // Set up failed state
        _mockTaskProgressService.Setup(s => s.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Raise(
            s => s.ProgressUpdated += null,
            this,
            new TaskProgressDetail { Progress = 0, StatusText = "Error" });

        _sut.IsTaskFailed.Should().BeTrue();
        _sut.IsLoading.Should().BeTrue();

        _sut.CloseFailedTaskCommand.Execute(null);

        _sut.IsTaskFailed.Should().BeFalse();
        _sut.IsLoading.Should().BeFalse();
    }

    // ── Localized Labels ──

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
    public void LanguageChanged_NotifiesButtonLabels()
    {
        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _mockLocalizationService.Raise(l => l.LanguageChanged += null, this, EventArgs.Empty);

        changedProperties.Should().Contain(nameof(_sut.CancelButtonLabel));
        changedProperties.Should().Contain(nameof(_sut.CloseButtonLabel));
    }

    // ── IDisposable ──

    [Fact]
    public void Dispose_UnsubscribesFromProgressUpdated()
    {
        var sut = new TaskProgressViewModel(
            _mockTaskProgressService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object);

        sut.Dispose();

        // After dispose, raising the event should not cause any changes
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
            _mockLocalizationService.Object,
            _mockLogService.Object);

        var act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        act.Should().NotThrow();
    }
}
