using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ServicingSessionTests
{
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IProcessExecutor> _processExecutor = new();
    private string? _arguments;

    public ServicingSessionTests()
    {
        _processExecutor
            .Setup(x => x.ShellExecuteAsync("powershell.exe", It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
            .Callback<string, string?, bool, CancellationToken>((_, args, _, _) => _arguments = args)
            .ReturnsAsync(0);
    }

    private ServicingSession CreateSut() => new(_logService.Object, _processExecutor.Object);

    [Fact]
    public async Task RunAsync_TwoStatements_LaunchesOneWindowRunningBoth()
    {
        var sut = CreateSut();

        var launched = await sut.RunAsync(["Enable-Something", "Add-SomethingElse"], "Two things");

        launched.Should().BeTrue();
        _processExecutor.Verify(
            x => x.ShellExecuteAsync("powershell.exe", It.IsAny<string?>(), false, It.IsAny<CancellationToken>()),
            Times.Once);
        _arguments.Should().Be(
            "-NoProfile -Command \"& { Enable-Something; Add-SomethingElse; pause }\"");
    }

    [Fact]
    public async Task RunAsync_ProgressLineNamesTheLabel()
    {
        var sut = CreateSut();
        var progress = new Mock<IProgress<TaskProgressDetail>>();

        await sut.RunAsync(["Enable-Something"], "Legacy .NET, Recall", progress.Object);

        progress.Verify(
            p => p.Report(It.Is<TaskProgressDetail>(d => d.StatusText == "Enabling Legacy .NET, Recall...")),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_NoStatements_LaunchesNothing()
    {
        var sut = CreateSut();

        var launched = await sut.RunAsync([], "Nothing");

        launched.Should().BeFalse();
        _processExecutor.Verify(
            x => x.ShellExecuteAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_ProcessDoesNotStart_ReturnsFalse()
    {
        _processExecutor
            .Setup(x => x.ShellExecuteAsync("powershell.exe", It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        var sut = CreateSut();

        var launched = await sut.RunAsync(["Enable-Something"], "One thing");

        launched.Should().BeFalse();
    }
}
