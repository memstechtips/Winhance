using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

// The OTS branch is the one part of RunAsync that runs without a real winget: the interactive-user
// helper is mocked, so the watchdog plumbing and termination classification can be exercised directly.
public class WinGetCliRunnerTests
{
    [Fact]
    public async Task RunAsync_Ots_SilenceRaisesTheStallNoticeButNeverKillsTheRun()
    {
        var helper = OtsHelper(async (_, _) =>
        {
            await Task.Delay(500, CancellationToken.None);
            return new InteractiveProcessResult(0, "", "");
        });
        var stalls = 0;

        var result = await WinGetCliRunner.RunAsync(
            "install Some.Package",
            exePathOverride: "winget.exe",
            interactiveUserService: helper.Object,
            timeoutMs: 0,
            onStall: () => Interlocked.Increment(ref stalls),
            stallAfterMs: 100);

        result.ExitCode.Should().Be(0);
        result.Termination.Should().Be(WinGetCliRunner.TerminationReason.None);
        stalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunAsync_Ots_OutputLinesKeepResettingTheStallTimer()
    {
        // 15 lines 100ms apart against a 1s window: only a working reset keeps the notice from firing.
        var helper = OtsHelper(async (onOutputLine, ct) =>
        {
            for (var i = 0; i < 15; i++)
            {
                await Task.Delay(100, CancellationToken.None);
                onOutputLine?.Invoke("still installing");
            }
            return new InteractiveProcessResult(0, "", "");
        });
        var stalls = 0;

        var result = await WinGetCliRunner.RunAsync(
            "install Some.Package",
            onOutputLine: _ => { },
            exePathOverride: "winget.exe",
            interactiveUserService: helper.Object,
            timeoutMs: 0,
            onStall: () => Interlocked.Increment(ref stalls),
            stallAfterMs: 1_000);

        result.ExitCode.Should().Be(0);
        stalls.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_Ots_RepeatedSpinnerFramesDoNotSuppressTheStallNotice()
    {
        var helper = new Mock<IInteractiveUserService>();
        helper.Setup(h => h.IsOtsElevation).Returns(true);
        helper.Setup(h => h.HasInteractiveUserToken).Returns(true);
        helper.Setup(h => h.RunProcessAsInteractiveUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<Action<string>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<Action<string>?>()))
            .Returns(async (string _, string _, Action<string>? _, Action<string>? _, CancellationToken _, int _, Action<string>? onProgressLine) =>
            {
                foreach (var frame in SpinnerFrames)
                {
                    await Task.Delay(50, CancellationToken.None);
                    onProgressLine?.Invoke(frame);
                }
                return new InteractiveProcessResult(0, "", "");
            });
        var stalls = 0;

        await WinGetCliRunner.RunAsync(
            "install Some.Package",
            onProgressLine: _ => { },
            exePathOverride: "winget.exe",
            interactiveUserService: helper.Object,
            timeoutMs: 0,
            onStall: () => Interlocked.Increment(ref stalls),
            stallAfterMs: 120);

        stalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ProgressKey_IgnoresSpinnerFramesAndFollowsThePercent()
    {
        WinGetCliRunner.ProgressKey("  \\ ").Should().BeEmpty();
        WinGetCliRunner.ProgressKey("40%").Should().NotBe(WinGetCliRunner.ProgressKey("41%"));
        WinGetCliRunner.ProgressKey("40%").Should().Be(WinGetCliRunner.ProgressKey("  40 %"));
    }

    [Fact]
    public async Task RunAsync_Ots_CallerCancellationIsReportedAsCancelled()
    {
        var helper = OtsHelper((_, ct) => ExitMinusOneWhenCancelledAsync(ct));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var result = await WinGetCliRunner.RunAsync(
            "install Some.Package",
            cancellationToken: cancelled.Token,
            exePathOverride: "winget.exe",
            interactiveUserService: helper.Object,
            timeoutMs: 0);

        result.ExitCode.Should().Be(-1);
        result.Termination.Should().Be(WinGetCliRunner.TerminationReason.Cancelled);
    }

    [Fact]
    public async Task RunAsync_Ots_WallClockTimeoutEndsTheRunEvenWhileOutputKeepsArriving()
    {
        var helper = OtsHelper(async (onOutputLine, ct) =>
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(20, CancellationToken.None);
                onOutputLine?.Invoke("spinner frame");
            }
            return new InteractiveProcessResult(-1, "", "");
        });

        var result = await WinGetCliRunner.RunAsync(
            "query Some.Package",
            onOutputLine: _ => { },
            exePathOverride: "winget.exe",
            interactiveUserService: helper.Object,
            timeoutMs: 300);

        result.ExitCode.Should().Be(-1);
        result.Termination.Should().Be(WinGetCliRunner.TerminationReason.WallClockTimeout);
    }

    [Fact]
    public void DescribeTermination_UsesTheLocalizedCancelledText()
    {
        var localization = new Mock<ILocalizationService>();
        var template = "abgebrochen";
        localization.Setup(l => l.TryGetString("WinGet_TerminatedCancelled", out template)).Returns(true);

        var note = WinGetCliRunner.DescribeTermination(
            KilledBy(WinGetCliRunner.TerminationReason.Cancelled), localization.Object);

        note.Should().Be("abgebrochen");
    }

    [Fact]
    public void DescribeTermination_NoLocalizationService_StillExplainsInEnglish()
    {
        var note = WinGetCliRunner.DescribeTermination(
            KilledBy(WinGetCliRunner.TerminationReason.Cancelled), localization: null);

        note.Should().Contain("cancelled");
    }

    [Fact]
    public async Task RunAsync_AsShellUser_RunsThroughTheShellTokenEvenWithoutOts()
    {
        var helper = new Mock<IInteractiveUserService>();
        helper.Setup(h => h.RunProcessAsShellUserAsync(
                "winget.exe", "uninstall --id Some.Package", It.IsAny<Action<string>?>(), It.IsAny<Action<string>?>(),
                It.IsAny<CancellationToken>(), 0, It.IsAny<Action<string>?>()))
            .ReturnsAsync(new InteractiveProcessResult(0, "Successfully uninstalled", ""));

        var result = await WinGetCliRunner.RunAsync(
            "uninstall --id Some.Package",
            exePathOverride: "winget.exe",
            interactiveUserService: helper.Object,
            asShellUser: true);

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be("Successfully uninstalled");
        helper.Verify(h => h.RunProcessAsInteractiveUserAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<Action<string>?>(),
            It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<Action<string>?>()), Times.Never);
    }

    [Fact]
    public void NoAdminUninstallForUserScope_IsTheCodeWingetPrints()
    {
        WinGetExitCodes.NoAdminUninstallForUserScope.Should().Be(-1978335107);
    }

    private static readonly string[] SpinnerFrames = ["-", "\\", "|", "/", "-", "\\", "|", "/"];

    private static WinGetCliRunner.WinGetCliResult KilledBy(WinGetCliRunner.TerminationReason reason)
        => new(-1, "", "", reason);

    private static Mock<IInteractiveUserService> OtsHelper(
        Func<Action<string>?, CancellationToken, Task<InteractiveProcessResult>> run)
    {
        var helper = new Mock<IInteractiveUserService>();
        helper.Setup(h => h.IsOtsElevation).Returns(true);
        helper.Setup(h => h.HasInteractiveUserToken).Returns(true);
        helper.Setup(h => h.RunProcessAsInteractiveUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<Action<string>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<Action<string>?>()))
            .Returns((string _, string _, Action<string>? onOutputLine, Action<string>? _, CancellationToken ct, int _, Action<string>? _)
                => run(onOutputLine, ct));
        return helper;
    }

    // Stands in for the helper's kill-on-cancel: a killed process reports -1.
    private static async Task<InteractiveProcessResult> ExitMinusOneWhenCancelledAsync(CancellationToken ct)
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ct.Register(() => cancelled.TrySetResult());
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        return new InteractiveProcessResult(-1, "", "");
    }
}
