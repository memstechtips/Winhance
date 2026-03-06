using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class TaskProgressServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly TaskProgressService _sut;

    public TaskProgressServiceTests()
    {
        _sut = new TaskProgressService(_mockLog.Object);
    }

    // ── Constructor ──

    [Fact]
    public void Constructor_NullLogService_ThrowsArgumentNullException()
    {
        var act = () => new TaskProgressService(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logService");
    }

    [Fact]
    public void Constructor_InitializesWithDefaultState()
    {
        _sut.IsTaskRunning.Should().BeFalse();
        _sut.CurrentProgress.Should().Be(0);
        _sut.CurrentStatusText.Should().BeEmpty();
        _sut.IsIndeterminate.Should().BeFalse();
        _sut.CurrentTaskCancellationSource.Should().BeNull();
    }

    // ── StartTask ──

    [Fact]
    public void StartTask_ValidName_SetsRunningStateAndReturnsCancellationSource()
    {
        var cts = _sut.StartTask("Apply Settings");

        _sut.IsTaskRunning.Should().BeTrue();
        _sut.CurrentStatusText.Should().Be("Apply Settings");
        _sut.CurrentProgress.Should().Be(0);
        cts.Should().NotBeNull();
        cts.IsCancellationRequested.Should().BeFalse();
        _sut.CurrentTaskCancellationSource.Should().BeSameAs(cts);
    }

    [Fact]
    public void StartTask_WithIndeterminate_SetsIndeterminateFlag()
    {
        _sut.StartTask("Loading", isIndeterminate: true);

        _sut.IsIndeterminate.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void StartTask_NullOrEmptyName_ThrowsArgumentException(string? taskName)
    {
        var act = () => _sut.StartTask(taskName!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("taskName");
    }

    [Fact]
    public void StartTask_RaisesProgressUpdatedEvent()
    {
        TaskProgressDetail? received = null;
        _sut.ProgressUpdated += (_, detail) => received = detail;

        _sut.StartTask("Test Task");

        received.Should().NotBeNull();
        received!.Progress.Should().Be(0);
        received.StatusText.Should().Be("Test Task");
    }

    // ── UpdateProgress ──

    [Fact]
    public void UpdateProgress_WithinRunningTask_UpdatesProgressAndStatus()
    {
        _sut.StartTask("Running");

        _sut.UpdateProgress(50, "Halfway");

        _sut.CurrentProgress.Should().Be(50);
        _sut.CurrentStatusText.Should().Be("Halfway");
    }

    [Fact]
    public void UpdateProgress_WhenNoTaskRunning_DoesNothing()
    {
        // No task started, so the method should silently return
        var act = () => _sut.UpdateProgress(50, "Status");

        act.Should().NotThrow();
        _sut.CurrentProgress.Should().Be(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void UpdateProgress_OutOfRange_ThrowsArgumentOutOfRangeException(int progress)
    {
        _sut.StartTask("Running");

        var act = () => _sut.UpdateProgress(progress);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateProgress_WithoutStatusText_KeepsPreviousStatusText()
    {
        _sut.StartTask("Initial");

        _sut.UpdateProgress(25);

        _sut.CurrentStatusText.Should().Be("Initial");
        _sut.CurrentProgress.Should().Be(25);
    }

    // ── CompleteTask ──

    [Fact]
    public void CompleteTask_SetsProgressTo100AndClearsRunningState()
    {
        _sut.StartTask("Work");
        _sut.UpdateProgress(50, "Working...");

        _sut.CompleteTask();

        _sut.IsTaskRunning.Should().BeFalse();
        _sut.CurrentProgress.Should().Be(100);
        _sut.IsIndeterminate.Should().BeFalse();
        _sut.CurrentTaskCancellationSource.Should().BeNull();
    }

    [Fact]
    public void CompleteTask_WhenNoTaskRunning_DoesNothing()
    {
        var act = () => _sut.CompleteTask();

        act.Should().NotThrow();
    }

    [Fact]
    public void CompleteTask_RaisesProgressUpdatedWithCompletion()
    {
        _sut.StartTask("Work");
        TaskProgressDetail? received = null;
        _sut.ProgressUpdated += (_, detail) => received = detail;

        _sut.CompleteTask();

        received.Should().NotBeNull();
        received!.Progress.Should().Be(100);
        received.DetailedMessage.Should().Be("Task completed");
    }

    // ── CancelCurrentTask ──

    [Fact]
    public void CancelCurrentTask_WithRunningTask_RequestsCancellation()
    {
        var cts = _sut.StartTask("Cancellable");

        _sut.CancelCurrentTask();

        cts.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void CancelCurrentTask_WhenNoTask_DoesNotThrow()
    {
        var act = () => _sut.CancelCurrentTask();

        act.Should().NotThrow();
    }

    // ── StartMultiScriptTask ──

    [Fact]
    public void StartMultiScriptTask_ValidScripts_SetsTaskRunning()
    {
        var scripts = new[] { "Script1", "Script2" };

        var cts = _sut.StartMultiScriptTask(scripts);

        _sut.IsTaskRunning.Should().BeTrue();
        cts.Should().NotBeNull();
    }

    [Fact]
    public void StartMultiScriptTask_NullOrEmpty_ThrowsArgumentException()
    {
        var act = () => _sut.StartMultiScriptTask(Array.Empty<string>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StartMultiScriptTask_FiresProgressUpdatedForEachSlot()
    {
        var received = new List<TaskProgressDetail>();
        _sut.ProgressUpdated += (_, detail) => received.Add(detail);

        _sut.StartMultiScriptTask(new[] { "A", "B" });

        received.Should().HaveCount(2);
        received[0].ScriptSlotIndex.Should().Be(0);
        received[0].ScriptSlotCount.Should().Be(2);
        received[1].ScriptSlotIndex.Should().Be(1);
        received[1].ScriptSlotCount.Should().Be(2);
    }

    // ── CompleteMultiScriptTask ──

    [Fact]
    public void CompleteMultiScriptTask_ResetsStateAndFiresCompletionEvent()
    {
        _sut.StartMultiScriptTask(new[] { "Script1" });
        TaskProgressDetail? received = null;
        _sut.ProgressUpdated += (_, detail) => received = detail;

        _sut.CompleteMultiScriptTask();

        _sut.IsTaskRunning.Should().BeFalse();
        received.Should().NotBeNull();
        received!.ScriptSlotCount.Should().Be(0);
        received.Progress.Should().Be(100);
    }

    // ── ConsumeSkipNextRequest ──

    [Fact]
    public void ConsumeSkipNextRequest_WhenNotRequested_ReturnsFalse()
    {
        _sut.StartTask("Test");

        _sut.ConsumeSkipNextRequest().Should().BeFalse();
    }

    // ── UpdateDetailedProgress ──

    [Fact]
    public void UpdateDetailedProgress_WhenNoTaskRunning_DoesNothing()
    {
        var detail = new TaskProgressDetail { Progress = 50, StatusText = "Status" };

        var act = () => _sut.UpdateDetailedProgress(detail);

        act.Should().NotThrow();
        _sut.CurrentProgress.Should().Be(0);
    }

    [Fact]
    public void UpdateDetailedProgress_InvalidProgress_ThrowsOutOfRange()
    {
        _sut.StartTask("Work");

        var act = () => _sut.UpdateDetailedProgress(new TaskProgressDetail { Progress = 150 });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateDetailedProgress_WithTerminalOutput_AccumulatesLines()
    {
        _sut.StartTask("Work");

        _sut.UpdateDetailedProgress(new TaskProgressDetail
        {
            Progress = 10,
            TerminalOutput = "Line 1"
        });
        _sut.UpdateDetailedProgress(new TaskProgressDetail
        {
            Progress = 20,
            TerminalOutput = "Line 2"
        });

        var lines = _sut.GetTerminalOutputLines();
        lines.Should().HaveCount(2);
        lines[0].Should().Be("Line 1");
        lines[1].Should().Be("Line 2");
    }

    [Fact]
    public void UpdateDetailedProgress_BlankTerminalOutput_IsFilteredAsNoise()
    {
        _sut.StartTask("Work");

        _sut.UpdateDetailedProgress(new TaskProgressDetail
        {
            Progress = 10,
            TerminalOutput = "   "
        });

        _sut.GetTerminalOutputLines().Should().BeEmpty();
    }

    // ── CreateDetailedProgress / CreatePowerShellProgress ──

    [Fact]
    public void CreateDetailedProgress_ReturnsNonNullProgressReporter()
    {
        _sut.StartTask("Work");

        var progress = _sut.CreateDetailedProgress();

        progress.Should().NotBeNull();
    }

    [Fact]
    public void CreatePowerShellProgress_ReturnsNonNullProgressReporter()
    {
        _sut.StartTask("Work");

        var progress = _sut.CreatePowerShellProgress();

        progress.Should().NotBeNull();
    }

    // ── Queue sticky state ──

    [Fact]
    public void UpdateDetailedProgress_WithQueueInfo_PersistsStickily()
    {
        _sut.StartTask("Queue Task");
        var receivedDetails = new List<TaskProgressDetail>();
        _sut.ProgressUpdated += (_, detail) => receivedDetails.Add(detail);

        // First update with queue info
        _sut.UpdateDetailedProgress(new TaskProgressDetail
        {
            Progress = 10,
            StatusText = "Item 1",
            QueueTotal = 5,
            QueueCurrent = 1,
            QueueNextItemName = "Item 2"
        });

        // Second update without queue info should still carry it
        _sut.UpdateDetailedProgress(new TaskProgressDetail
        {
            Progress = 20,
            StatusText = "Still Item 1"
        });

        receivedDetails.Should().HaveCountGreaterOrEqualTo(2);
        var lastDetail = receivedDetails[^1];
        lastDetail.QueueTotal.Should().Be(5);
        lastDetail.QueueCurrent.Should().Be(1);
    }
}
