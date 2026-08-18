using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Common;

public class DismProcessRunnerTests
{
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly DismProcessRunner _runner;

    public DismProcessRunnerTests()
    {
        _runner = new DismProcessRunner(
            _mockProcessExecutor.Object,
            _mockLogService.Object,
            _mockFileSystem.Object);
    }

    [Fact]
    public async Task RunProcessWithProgressAsync_SuccessfulExecution_ReturnsExitCodeAndOutput()
    {
        _mockProcessExecutor
            .Setup(pe => pe.ExecuteWithStreamingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, Action<string>?, Action<string>?, CancellationToken>(
                (_, _, onOutput, _, _) =>
                {
                    onOutput?.Invoke("Processing...");
                    onOutput?.Invoke("50.0% complete");
                    onOutput?.Invoke("100.0% complete");
                })
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0, StandardOutput = "" });

        var reportedProgress = new List<TaskProgressDetail>();
        var progress = new Progress<TaskProgressDetail>(detail => reportedProgress.Add(detail));

        var (exitCode, output) = await _runner.RunProcessWithProgressAsync(
            "dism.exe", "/some-args", progress, CancellationToken.None);

        exitCode.Should().Be(0);
        output.Should().Contain("50.0% complete");
        output.Should().Contain("100.0% complete");
    }

    [Fact]
    public async Task RunProcessWithProgressAsync_ProgressPercentagesParsed_ReportsProgress()
    {
        var reportedDetails = new List<TaskProgressDetail>();

        _mockProcessExecutor
            .Setup(pe => pe.ExecuteWithStreamingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, Action<string>?, Action<string>?, CancellationToken>(
                (_, _, onOutput, _, _) =>
                {
                    onOutput?.Invoke("[==                ] 25.5%");
                })
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0 });

        var syncProgress = new SynchronousProgress<TaskProgressDetail>(d => reportedDetails.Add(d));

        await _runner.RunProcessWithProgressAsync(
            "dism.exe", "/args", syncProgress, CancellationToken.None);

        reportedDetails.Should().ContainSingle(d => d.Progress.HasValue && Math.Abs(d.Progress.Value - 25.5) < 0.01);
    }

    [Fact]
    public async Task RunProcessWithProgressAsync_StderrOutput_IncludedInResult()
    {
        _mockProcessExecutor
            .Setup(pe => pe.ExecuteWithStreamingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, Action<string>?, Action<string>?, CancellationToken>(
                (_, _, _, onError, _) =>
                {
                    onError?.Invoke("Error: something went wrong");
                })
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 1 });

        var (exitCode, output) = await _runner.RunProcessWithProgressAsync(
            "dism.exe", "/args", null, CancellationToken.None);

        exitCode.Should().Be(1);
        output.Should().Contain("Error: something went wrong");
    }

    [Fact]
    public async Task RunProcessWithProgressAsync_NonProgressLines_ReportedWithoutProgressValue()
    {
        var reportedDetails = new List<TaskProgressDetail>();
        _mockProcessExecutor
            .Setup(pe => pe.ExecuteWithStreamingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, Action<string>?, Action<string>?, CancellationToken>(
                (_, _, onOutput, _, _) =>
                {
                    onOutput?.Invoke("Deployment Image Servicing and Management tool");
                })
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0 });

        var syncProgress = new SynchronousProgress<TaskProgressDetail>(d => reportedDetails.Add(d));

        await _runner.RunProcessWithProgressAsync(
            "dism.exe", "/args", syncProgress, CancellationToken.None);

        reportedDetails.Should().ContainSingle();
        reportedDetails[0].Progress.Should().BeNull();
        reportedDetails[0].TerminalOutput.Should().Contain("Deployment Image");
    }

    // CheckDiskSpaceAsync gets limited coverage: it depends on DriveInfo.
    [Fact]
    public async Task CheckDiskSpaceAsync_InvalidPath_ReturnsTrueGracefully()
    {
        // An invalid path root makes DriveInfo throw.
        _mockFileSystem.Setup(fs => fs.GetPathRoot(It.IsAny<string>())).Returns("??:");

        var result = await _runner.CheckDiskSpaceAsync("??:\\invalid", 100, "test operation");

        // The catch block returns true on unexpected errors.
        result.Should().BeTrue();
    }

    // Unlike Progress<T>, invokes the callback immediately on the calling thread.
    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SynchronousProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }
}
