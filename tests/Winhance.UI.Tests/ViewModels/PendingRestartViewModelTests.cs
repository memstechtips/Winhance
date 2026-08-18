using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.ViewModels;

public class PendingRestartViewModelTests
{
    private static readonly string[] TwoFakeCatalogIds = ["not-a-real-catalog-id-a", "not-a-real-catalog-id-b"];

    private readonly Mock<IPendingRestartService> _mockPending = new();
    private readonly Mock<IExplorerRestartService> _mockRestart = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IConfigImportState> _mockImportState = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgress = new();
    private readonly Mock<IDispatcherService> _mockDispatcher = new();
    private readonly Mock<ILogService> _mockLog = new();

    public PendingRestartViewModelTests()
    {
        // The real service returns the "[Key]" miss-marker for unknown keys; echoing the key back is
        // enough for these tests and keeps assertions readable.
        _mockLocalization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string k) => k);
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        _mockLocalization.MirrorTryGetString();
        _mockPending.Setup(p => p.PendingSettingIds).Returns(Array.Empty<string>());

        // Run inline: the view-model marshals its own refresh through this service, so a mock
        // that swallowed the action would leave PendingRestartChanged_RefreshesOnTheUIThread
        // asserting nothing. Every other test here calls Refresh() directly and does not need it.
        _mockDispatcher.Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback((Action a) => a());
    }

    private PendingRestartViewModel Create() => new(
        _mockPending.Object,
        _mockRestart.Object,
        _mockEventBus.Object,
        _mockLocalization.Object,
        _mockImportState.Object,
        _mockTaskProgress.Object,
        _mockDispatcher.Object,
        _mockLog.Object);

    [Fact]
    public void IsBarVisible_WhenNothingPending_IsFalse()
    {
        _mockPending.Setup(p => p.IsPending).Returns(false);

        Create().IsBarVisible.Should().BeFalse();
    }

    [Fact]
    public void Refresh_WhenPending_ShowsBarAndEnablesTheButton()
    {
        _mockPending.Setup(p => p.IsPending).Returns(true);
        var vm = Create();

        vm.Refresh();

        vm.IsBarVisible.Should().BeTrue();
        vm.CanRestart.Should().BeTrue();
    }

    [Fact]
    public void CanRestart_DuringConfigImport_IsFalse()
    {
        _mockPending.Setup(p => p.IsPending).Returns(true);
        _mockImportState.Setup(i => i.IsActive).Returns(true);
        var vm = Create();

        vm.Refresh();

        vm.IsBarVisible.Should().BeTrue("the bar stays up; only the button greys out");
        vm.CanRestart.Should().BeFalse();
    }

    [Fact]
    public void CanRestart_WhileATaskIsRunning_IsFalse()
    {
        _mockPending.Setup(p => p.IsPending).Returns(true);
        _mockTaskProgress.Setup(t => t.IsTaskRunning).Returns(true);
        var vm = Create();

        vm.Refresh();

        vm.CanRestart.Should().BeFalse("a restart must not land in the middle of an apply");
    }

    [Fact]
    public async Task RestartCommand_OnSuccess_HidesBar()
    {
        // Pending until the service clears it, then not.
        var pending = true;
        _mockPending.Setup(p => p.IsPending).Returns(() => pending);
        _mockRestart.Setup(r => r.RestartAsync())
            .ReturnsAsync(OperationResult.Succeeded())
            .Callback(() => pending = false);
        var vm = Create();

        await vm.RestartCommand.ExecuteAsync(null);

        _mockRestart.Verify(r => r.RestartAsync(), Times.Once);
        vm.IsRestarting.Should().BeFalse();
        vm.IsBarVisible.Should().BeFalse();
    }

    [Fact]
    public async Task RestartCommand_OnFailure_KeepsBarVisible()
    {
        _mockPending.Setup(p => p.IsPending).Returns(true);
        _mockRestart.Setup(r => r.RestartAsync()).ReturnsAsync(OperationResult.Failed("nope"));
        var vm = Create();

        await vm.RestartCommand.ExecuteAsync(null);

        vm.IsRestarting.Should().BeFalse();
        vm.IsBarVisible.Should().BeTrue("a failed restart must leave the user a way to retry");
        vm.CanRestart.Should().BeTrue();
    }

    [Fact]
    public void TooltipText_ListsThePendingSettings()
    {
        _mockPending.Setup(p => p.IsPending).Returns(true);
        _mockPending.Setup(p => p.PendingSettingIds)
            .Returns(TwoFakeCatalogIds);
        var vm = Create();

        vm.Refresh();

        // Unknown IDs fall back to the raw ID rather than throwing.
        vm.TooltipText.Should().Contain("not-a-real-catalog-id-a");
        vm.TooltipText.Should().Contain("not-a-real-catalog-id-b");
    }

    [Fact]
    public void TooltipText_WhenNothingPending_IsEmpty()
    {
        _mockPending.Setup(p => p.IsPending).Returns(false);
        var vm = Create();

        vm.Refresh();

        vm.TooltipText.Should().BeEmpty();
    }

    [Fact]
    public void Message_ContainsNoDigits()
    {
        // There is no plural infrastructure in the app, so no user-facing string may carry a count.
        _mockLocalization.Setup(l => l.GetString("PendingRestart_Message"))
            .Returns("Some changes need Explorer restarted to take effect.");
        var vm = Create();

        vm.Refresh();

        vm.Message.Should().NotContainAny("0", "1", "2", "3", "4", "5", "6", "7", "8", "9");
    }

    [Fact]
    public void PendingRestartChanged_RefreshesOnTheUIThread()
    {
        // The event is raised from the apply pipeline's background thread and Refresh writes bound
        // properties. Capturing the handler the view-model registers is the only way to show the
        // marshalling happens INSIDE the view-model rather than in whichever host subscribed.
        Action<PendingRestartChangedEvent>? handler = null;
        _mockEventBus
            .Setup(b => b.Subscribe(It.IsAny<Action<PendingRestartChangedEvent>>()))
            .Callback((Action<PendingRestartChangedEvent> h) => handler = h)
            .Returns(Mock.Of<ISubscriptionToken>());
        _mockPending.Setup(p => p.IsPending).Returns(false);
        var vm = Create();
        vm.IsBarVisible.Should().BeFalse("nothing is pending when the view-model is built");

        _mockPending.Setup(p => p.IsPending).Returns(true);
        handler.Should().NotBeNull("the view-model has to subscribe to the event it refreshes on");
        handler!(new PendingRestartChangedEvent { IsPending = true });

        _mockDispatcher.Verify(d => d.RunOnUIThread(It.IsAny<Action>()), Times.Once);
        vm.IsBarVisible.Should().BeTrue("the handler re-reads the pending state");
    }

    [Fact]
    public async Task RestartCommand_WhenTheServiceThrows_LogsAndLeavesTheBarUsable()
    {
        _mockPending.Setup(p => p.IsPending).Returns(true);
        _mockRestart.Setup(r => r.RestartAsync()).ThrowsAsync(new InvalidOperationException("boom"));
        var vm = Create();

        // This is a [RelayCommand]: an unhandled throw comes back out on the UI thread and takes
        // the process down, while the user is staring at a shell that may already be gone.
        Func<Task> execute = async () => await vm.RestartCommand.ExecuteAsync(null);
        await execute.Should().NotThrowAsync();

        _mockLog.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
        vm.IsRestarting.Should().BeFalse("the finally block clears it or the button stays a spinner");
        vm.IsBarVisible.Should().BeTrue("a failed restart must leave the user a way to retry");
    }

    [Fact]
    public void Dispose_UnsubscribesFromTheEventBus()
    {
        var token = Mock.Of<ISubscriptionToken>();
        _mockEventBus
            .Setup(b => b.Subscribe(It.IsAny<Action<PendingRestartChangedEvent>>()))
            .Returns(token);
        var vm = Create();

        vm.Dispose();

        _mockEventBus.Verify(b => b.Unsubscribe(token), Times.Once);
    }
}
