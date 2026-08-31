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
    public async Task RunAsync_Ots_IdleTimeoutCancelsTheHelperAndIsReportedAsIdleTimeout()
    {
        var helper = OtsHelper((_, ct) => ExitMinusOneWhenCancelledAsync(ct));

        var result = await WinGetCliRunner.RunAsync(
            "install Some.Package",
            exePathOverride: "winget.exe",
            interactiveUserService: helper.Object,
            timeoutMs: 0,
            idleTimeoutMs: 100);

        result.ExitCode.Should().Be(-1);
        result.Termination.Should().Be(WinGetCliRunner.TerminationReason.IdleTimeout);
        helper.Verify(h => h.RunProcessAsInteractiveUserAsync(
            "winget.exe", "install Some.Package", It.IsAny<Action<string>?>(), It.IsAny<Action<string>?>(),
            It.IsAny<CancellationToken>(), 0, It.IsAny<Action<string>?>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_Ots_OutputLinesKeepResettingTheIdleTimer()
    {
        // 15 lines 100ms apart against a 1s idle window: only a working reset lets the run finish on its own.
        var helper = OtsHelper(async (onOutputLine, ct) =>
        {
            for (var i = 0; i < 15; i++)
            {
                await Task.Delay(100, CancellationToken.None);
                if (ct.IsCancellationRequested)
                    return new InteractiveProcessResult(-1, "", "");
                onOutputLine?.Invoke("still installing");
            }
            return new InteractiveProcessResult(0, "", "");
        });

        var result = await WinGetCliRunner.RunAsync(
            "install Some.Package",
            onOutputLine: _ => { },
            exePathOverride: "winget.exe",
            interactiveUserService: helper.Object,
            timeoutMs: 0,
            idleTimeoutMs: 1_000);

        result.ExitCode.Should().Be(0);
        result.Termination.Should().Be(WinGetCliRunner.TerminationReason.None);
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
            timeoutMs: 0,
            idleTimeoutMs: 180_000);

        result.ExitCode.Should().Be(-1);
        result.Termination.Should().Be(WinGetCliRunner.TerminationReason.Cancelled);
    }

    // The whole reason the wall clock exists: winget's spinner re-arms the idle timer forever, so output
    // that never stops must still end at the time limit.
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
            "install Some.Package",
            onOutputLine: _ => { },
            exePathOverride: "winget.exe",
            interactiveUserService: helper.Object,
            timeoutMs: 300,
            idleTimeoutMs: 60_000);

        result.ExitCode.Should().Be(-1);
        result.Termination.Should().Be(WinGetCliRunner.TerminationReason.WallClockTimeout);
    }

    // The run call and DescribeTermination take the two budgets separately, so a caller that updates one and
    // not the other ships "the 0 minute time limit" to the user. Both sides read the same constants now.
    [Fact]
    public void DescribeTermination_WallClockTimeout_ReportsTheInstallLimitNotZero()
    {
        var note = WinGetCliRunner.DescribeTermination(
            KilledBy(WinGetCliRunner.TerminationReason.WallClockTimeout),
            timeoutMs: WinGetCliRunner.InstallTimeoutMs,
            idleTimeoutMs: WinGetCliRunner.InstallIdleTimeoutMs);

        note.Should().Contain("120 minute").And.NotContain("the 0 minute");
    }

    [Fact]
    public void DescribeTermination_IdleTimeout_ReportsTheSilenceWithoutBlamingASource()
    {
        var note = WinGetCliRunner.DescribeTermination(
            KilledBy(WinGetCliRunner.TerminationReason.IdleTimeout),
            timeoutMs: WinGetCliRunner.InstallTimeoutMs,
            idleTimeoutMs: WinGetCliRunner.InstallIdleTimeoutMs);

        note.Should().Contain("no output for 3 minutes")
            .And.NotContain("package source")
            .And.NotContain("app deployment");
    }

    // These notes are the only explanation a user gets when Winhance kills winget itself, and 28 of the
    // 29 languages used to read them in English. The minute count is a placeholder so a language can put
    // it wherever its grammar needs it rather than where English does.
    [Fact]
    public void DescribeTermination_UsesTheLocalizedTemplateAndSubstitutesTheMinutes()
    {
        var localization = new Mock<ILocalizationService>();
        var template = "keine Ausgabe seit {0} Minuten";
        localization.Setup(l => l.TryGetString("WinGet_TerminatedIdle", out template)).Returns(true);

        var note = WinGetCliRunner.DescribeTermination(
            KilledBy(WinGetCliRunner.TerminationReason.IdleTimeout),
            timeoutMs: WinGetCliRunner.InstallTimeoutMs,
            idleTimeoutMs: WinGetCliRunner.InstallIdleTimeoutMs,
            localization: localization.Object);

        note.Should().Be("keine Ausgabe seit 3 Minuten");
    }

    // WinGetInstaller holds a nullable ILocalizationService, so the null path has to still say something.
    [Fact]
    public void DescribeTermination_NoLocalizationService_StillExplainsInEnglish()
    {
        var note = WinGetCliRunner.DescribeTermination(
            KilledBy(WinGetCliRunner.TerminationReason.Cancelled),
            timeoutMs: WinGetCliRunner.InstallTimeoutMs,
            idleTimeoutMs: WinGetCliRunner.InstallIdleTimeoutMs,
            localization: null);

        note.Should().Contain("cancelled");
    }

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
