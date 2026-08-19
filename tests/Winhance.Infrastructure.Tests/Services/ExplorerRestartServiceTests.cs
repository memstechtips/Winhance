using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ExplorerRestartServiceTests
{
    private readonly Mock<IWindowsUIManagementService> _mockUi = new();
    private readonly Mock<IInteractiveUserService> _mockInteractive = new();
    private readonly Mock<IPendingRestartService> _mockPending = new();
    private readonly Mock<ILogService> _mockLog = new();

    // Every poll delay the service asked for, in order. Recording them is the only way to see from
    // outside that a wait was SKIPPED, which is the whole point of the graceful-exit path. Writes are
    // serialised by the service's own restart gate, so no locking is needed here.
    private readonly List<int> _delays = new();

    // The injected delay is a no-op so the poll loops run instantly.
    private ExplorerRestartService Create() => new(
        _mockUi.Object,
        _mockInteractive.Object,
        _mockPending.Object,
        _mockLog.Object,
        delay: ms =>
        {
            _delays.Add(ms);
            return Task.CompletedTask;
        });

    [Fact]
    public async Task RestartAsync_WhenExplorerComesBackOnItsOwn_DoesNotLaunchManually()
    {
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(true);

        var result = await Create().RestartAsync();

        result.Success.Should().BeTrue();
        _mockUi.Verify(u => u.KillProcessAndWait("explorer", It.IsAny<int>()), Times.Once);
        _mockInteractive.Verify(
            i => i.LaunchProcessAsInteractiveUser(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockPending.Verify(p => p.Clear(), Times.Once);
    }

    // ---------------------------------------------------------------------------------------------
    // Regressions from the 2026-07-28 report: Marco's Explorer was killed and never came back, and
    // the bar disappeared as though the restart had worked.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task RestartAsync_NeverUsesTheNonWaitingKill()
    {
        // Process.Kill only REQUESTS termination, so a poll straight after it sees the dying process
        // and reads as "Explorer is already back" - the restart then reports success and the fallback
        // relaunch never runs. Only the waiting variant is safe here.
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(true);

        await Create().RestartAsync();

        _mockUi.Verify(u => u.KillProcess(It.IsAny<string>()), Times.Never,
            "KillProcess does not wait for exit; KillProcessAndWait must be used instead");
    }

    [Fact]
    public async Task RestartAsync_CapturesTheShellTokenBeforeKillingExplorer()
    {
        // The token is harvested from the LIVE explorer.exe. Capture it after the kill and there is
        // nothing left to harvest from - exactly when it is needed.
        var sequence = new List<string>();
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(true);
        _mockInteractive.Setup(i => i.CaptureShellRelaunchToken())
            .Callback(() => sequence.Add("capture"))
            .Returns(Mock.Of<IShellRelaunchToken>());
        _mockUi.Setup(u => u.KillProcessAndWait("explorer", It.IsAny<int>()))
            .Callback(() => sequence.Add("kill"))
            .Returns(true);

        await Create().RestartAsync();

        sequence.Should().Equal("capture", "kill");
    }

    [Fact]
    public async Task RestartAsync_WhenExplorerStaysDead_UsesTheCapturedTokenNotTheOtsPath()
    {
        // LaunchProcessAsInteractiveUser is gated on OTS elevation and silently degrades to
        // Process.Start otherwise. Winhance is always elevated, so that path cannot start the shell -
        // the captured token has to be the one used.
        var launched = false;
        var token = new Mock<IShellRelaunchToken>();
        token.Setup(t => t.TryLaunch("explorer.exe", It.IsAny<string>()))
            .Callback(() => launched = true)
            .Returns(true);
        _mockInteractive.Setup(i => i.CaptureShellRelaunchToken()).Returns(token.Object);

        var killed = false;
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(() => !killed || launched);
        _mockUi.Setup(u => u.KillProcessAndWait("explorer", It.IsAny<int>()))
            .Callback(() => killed = true)
            .Returns(true);

        var result = await Create().RestartAsync();

        result.Success.Should().BeTrue();
        token.Verify(t => t.TryLaunch("explorer.exe", It.IsAny<string>()), Times.Once);
        _mockInteractive.Verify(
            i => i.LaunchProcessAsInteractiveUser(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RestartAsync_DisposesTheCapturedToken()
    {
        var token = new Mock<IShellRelaunchToken>();
        _mockInteractive.Setup(i => i.CaptureShellRelaunchToken()).Returns(token.Object);
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(true);

        await Create().RestartAsync();

        token.Verify(t => t.Dispose(), Times.Once);
    }

    [Fact]
    public async Task RestartAsync_BroadcastsBeforeAndAfter()
    {
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(true);

        await Create().RestartAsync();

        _mockUi.Verify(u => u.BroadcastShellRefresh(), Times.Exactly(2));

        // The THEME half goes out once, after the relaunch only. A fresh shell needs the full
        // picture; the pre-kill broadcast does not, and it is the expensive one (a synchronous
        // send charged per top-level window), so sending it twice per restart buys nothing.
        _mockUi.Verify(u => u.BroadcastThemeRefresh(), Times.Once);
    }

    [Fact]
    public async Task RestartAsync_WhenAutoRestartFails_LaunchesAsInteractiveUser()
    {
        // Dead for every poll until the manual launch brings it back. This is the path that matters:
        // Winhance is always elevated, so the relaunch MUST go through the interactive-user token.
        var launched = false;
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(() => launched);
        _mockInteractive
            .Setup(i => i.LaunchProcessAsInteractiveUser("explorer.exe", It.IsAny<string>()))
            .Callback(() => launched = true);

        var result = await Create().RestartAsync();

        result.Success.Should().BeTrue();
        _mockInteractive.Verify(
            i => i.LaunchProcessAsInteractiveUser("explorer.exe", It.IsAny<string>()), Times.Once);
        _mockPending.Verify(p => p.Clear(), Times.Once);
    }

    [Fact]
    public async Task RestartAsync_WhenExplorerNeverReturns_FailsAndKeepsPendingState()
    {
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(false);

        var result = await Create().RestartAsync();

        result.Success.Should().BeFalse();
        _mockPending.Verify(p => p.Clear(), Times.Never,
            "a pending state that was not actually satisfied must survive so the user can retry");
    }

    [Fact]
    public async Task RestartAsync_WhenExplorerNotRunning_SkipsTheKill()
    {
        var started = false;
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(() => started);
        _mockInteractive
            .Setup(i => i.LaunchProcessAsInteractiveUser("explorer.exe", It.IsAny<string>()))
            .Callback(() => started = true);

        await Create().RestartAsync();

        _mockUi.Verify(u => u.KillProcessAndWait(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task RestartAsync_ConcurrentCallers_AreSerialised()
    {
        var concurrent = 0;
        var maxConcurrent = 0;
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(true);
        _mockUi.Setup(u => u.KillProcessAndWait("explorer", It.IsAny<int>())).Returns(true).Callback(() =>
        {
            var now = Interlocked.Increment(ref concurrent);
            maxConcurrent = Math.Max(maxConcurrent, now);
            Interlocked.Decrement(ref concurrent);
        });
        var sut = Create();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => sut.RestartAsync()));

        maxConcurrent.Should().Be(1, "overlapping restarts are what left users with no shell");
    }

    // ---------------------------------------------------------------------------------------------
    // The captured token can be harvested successfully and STILL fail to launch (CreateProcessWithTokenW
    // returns false). That is a different fault from "no token was captured", and until these tests
    // existed nothing exercised it - TryLaunch was only ever stubbed to succeed.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task RestartAsync_WhenTheCapturedTokenFailsToLaunch_StillTriesTheInteractiveUserFallback()
    {
        var token = new Mock<IShellRelaunchToken>();
        token.Setup(t => t.TryLaunch("explorer.exe", It.IsAny<string>())).Returns(false);
        _mockInteractive.Setup(i => i.CaptureShellRelaunchToken()).Returns(token.Object);

        var killed = false;
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(() => !killed);
        _mockUi.Setup(u => u.KillProcessAndWait("explorer", It.IsAny<int>()))
            .Callback(() => killed = true)
            .Returns(true);

        var result = await Create().RestartAsync();

        token.Verify(t => t.TryLaunch("explorer.exe", It.IsAny<string>()), Times.Once);
        _mockInteractive.Verify(
            i => i.LaunchProcessAsInteractiveUser("explorer.exe", It.IsAny<string>()), Times.Once,
            "a token that fails to launch must still fall through to the last-resort launch");
        result.Success.Should().BeFalse("Explorer never came back, so the poll is the verdict");
    }

    [Fact]
    public async Task RestartAsync_WhenTheFallbackAlsoFails_KeepsThePendingStateForRetry()
    {
        // The bar has to stay up. Clearing pending state on a restart that did not happen is what
        // left users with no shell and no way to retry.
        var token = new Mock<IShellRelaunchToken>();
        token.Setup(t => t.TryLaunch("explorer.exe", It.IsAny<string>())).Returns(false);
        _mockInteractive.Setup(i => i.CaptureShellRelaunchToken()).Returns(token.Object);

        var killed = false;
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(() => !killed);
        _mockUi.Setup(u => u.KillProcessAndWait("explorer", It.IsAny<int>()))
            .Callback(() => killed = true)
            .Returns(true);

        var result = await Create().RestartAsync();

        result.Success.Should().BeFalse();
        _mockPending.Verify(p => p.Clear(), Times.Never);
    }

    // ---------------------------------------------------------------------------------------------
    // Graceful exit (2026-07-31). Explorer is ASKED to leave first, so it flushes the desktop icon
    // layout and folder view preferences on the way out; terminating it throws all of that away. The
    // message behind it is undocumented, so the kill stays as the fallback and the fallback is exactly
    // the old behaviour.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task RestartAsync_WhenTheGracefulExitSucceeds_DoesNotKillExplorer()
    {
        var exited = false;
        var launched = false;
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(() => !exited || launched);
        _mockUi.Setup(u => u.TryGracefulShellExit(It.IsAny<int>()))
            .Callback(() => exited = true)
            .Returns(true);

        var token = new Mock<IShellRelaunchToken>();
        token.Setup(t => t.TryLaunch("explorer.exe", It.IsAny<string>()))
            .Callback(() => launched = true)
            .Returns(true);
        _mockInteractive.Setup(i => i.CaptureShellRelaunchToken()).Returns(token.Object);

        var result = await Create().RestartAsync();

        result.Success.Should().BeTrue();
        _mockUi.Verify(u => u.KillProcessAndWait(It.IsAny<string>(), It.IsAny<int>()), Times.Never,
            "Explorer left on its own, so terminating it would only discard the state it just saved");
        token.Verify(t => t.TryLaunch("explorer.exe", It.IsAny<string>()), Times.Once,
            "a graceful exit does not bring the shell back - the caller still owns the relaunch");
        _mockPending.Verify(p => p.Clear(), Times.Once);
    }

    [Fact]
    public async Task RestartAsync_WhenTheGracefulExitTimesOut_FallsBackToKillingExplorer()
    {
        // The message is undocumented, so "it did not work" has to be a normal outcome, and the fallback
        // has to be the behaviour that is known to work.
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(true);
        _mockUi.Setup(u => u.TryGracefulShellExit(It.IsAny<int>())).Returns(false);

        var result = await Create().RestartAsync();

        result.Success.Should().BeTrue();
        _mockUi.Verify(u => u.KillProcessAndWait("explorer", It.IsAny<int>()), Times.Once,
            "an unproven graceful exit must fall back to terminating the shell");
    }

    [Fact]
    public async Task RestartAsync_WhenAShellIsAlreadyBack_DoesNotLaunchASecondExplorer()
    {
        // Whether recent Windows 11 builds re-spawn the shell themselves after the graceful-exit message
        // is NOT established. If one is already up, a second Explorer opens a stray folder window on the
        // user's desktop - so the relaunch has to be idempotent.
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(true);
        _mockUi.Setup(u => u.TryGracefulShellExit(It.IsAny<int>())).Returns(true);
        _mockUi.Setup(u => u.IsShellWindowAlive()).Returns(true);

        var token = new Mock<IShellRelaunchToken>();
        _mockInteractive.Setup(i => i.CaptureShellRelaunchToken()).Returns(token.Object);

        var result = await Create().RestartAsync();

        result.Success.Should().BeTrue();
        token.Verify(t => t.TryLaunch(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockInteractive.Verify(
            i => i.LaunchProcessAsInteractiveUser(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockPending.Verify(p => p.Clear(), Times.Once,
            "a shell that is already back is a satisfied restart, not a failed one");
    }

    [Fact]
    public async Task RestartAsync_AfterAGracefulExit_DoesNotWaitForWinlogonToRestartTheShell()
    {
        // winlogon's AutoRestartShell only covers the shell stopping UNEXPECTEDLY, so after a graceful
        // exit that poll waits for something that by design will not happen - six of the twenty-two
        // seconds the user felt. Dead for every poll until the launch brings it back, so a wait that
        // still ran would show up as extra delays.
        var exited = false;
        var launched = false;
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(() => !exited || launched);
        _mockUi.Setup(u => u.TryGracefulShellExit(It.IsAny<int>()))
            .Callback(() => exited = true)
            .Returns(true);
        _mockUi.Setup(u => u.IsShellWindowAlive()).Returns(false);
        _mockInteractive.Setup(i => i.CaptureShellRelaunchToken()).Returns(Mock.Of<IShellRelaunchToken>());
        _mockInteractive
            .Setup(i => i.LaunchProcessAsInteractiveUser("explorer.exe", It.IsAny<string>()))
            .Callback(() => launched = true);

        await Create().RestartAsync();

        _delays.Should().HaveCount(1,
            "the only poll left after a graceful exit is the post-relaunch verification");
    }

    [Fact]
    public async Task RestartAsync_WhenExplorerHadToBeTerminated_StillGivesWinlogonItsShortWindow()
    {
        // AutoRestartShell DOES cover an unexpected termination, so on the fallback branch the wait is
        // real: if winlogon brings the shell back, launching a second one is the bug.
        _mockUi.Setup(u => u.IsProcessRunning("explorer")).Returns(true);
        _mockUi.Setup(u => u.TryGracefulShellExit(It.IsAny<int>())).Returns(false);
        _mockUi.Setup(u => u.IsShellWindowAlive()).Returns(false);

        var token = new Mock<IShellRelaunchToken>();
        _mockInteractive.Setup(i => i.CaptureShellRelaunchToken()).Returns(token.Object);

        await Create().RestartAsync();

        _delays.Should().HaveCount(1, "the auto-restart poll runs on the terminate branch, and Explorer answers it");
        token.Verify(t => t.TryLaunch(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockInteractive.Verify(
            i => i.LaunchProcessAsInteractiveUser(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
