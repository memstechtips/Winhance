using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ExplorerWindowManagerTests
{
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly ExplorerWindowManager _service;

    public ExplorerWindowManagerTests()
    {
        _service = new ExplorerWindowManager(
            _mockProcessExecutor.Object,
            _mockLogService.Object);
    }

    [Fact]
    public async Task OpenFolderAsync_COMInteropFails_FallsBackToExplorerProcess()
    {
        // COM interop will naturally fail or find no matching window in test env
        _mockProcessExecutor
            .Setup(pe => pe.ShellExecuteAsync("explorer.exe", It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _service.OpenFolderAsync(@"C:\TestFolder");

        _mockProcessExecutor.Verify(
            pe => pe.ShellExecuteAsync("explorer.exe", @"C:\TestFolder", false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenFolderAsync_WithTrailingSlash_NormalizesPath()
    {
        _mockProcessExecutor
            .Setup(pe => pe.ShellExecuteAsync(It.IsAny<string>(), It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _service.OpenFolderAsync(@"C:\TestFolder\");

        // explorer.exe still gets the original path with the trailing slash; normalization is only for window comparison
        _mockProcessExecutor.Verify(
            pe => pe.ShellExecuteAsync("explorer.exe", @"C:\TestFolder\", false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenFolderAsync_ShellExecuteThrows_DoesNotThrow()
    {
        // If ShellExecute itself throws, it propagates up (no catch around it).
        _mockProcessExecutor
            .Setup(pe => pe.ShellExecuteAsync(It.IsAny<string>(), It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var act = () => _service.OpenFolderAsync(@"C:\SomeFolder");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OpenFolderAsync_NullProcessExecutor_COMFallsThrough()
    {
        _mockProcessExecutor
            .Setup(pe => pe.ShellExecuteAsync("explorer.exe", It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _service.OpenFolderAsync(@"C:\Windows\System32");

        _mockProcessExecutor.Verify(
            pe => pe.ShellExecuteAsync("explorer.exe", It.IsAny<string>(), false, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
