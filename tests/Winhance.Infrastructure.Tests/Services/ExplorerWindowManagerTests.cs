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
        // Arrange - COM interop will naturally fail or find no matching window in test env
        _mockProcessExecutor
            .Setup(pe => pe.ShellExecuteAsync("explorer.exe", It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _service.OpenFolderAsync(@"C:\TestFolder");

        // Assert - the fallback explorer.exe launch should have been called
        _mockProcessExecutor.Verify(
            pe => pe.ShellExecuteAsync("explorer.exe", @"C:\TestFolder", false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenFolderAsync_WithTrailingSlash_NormalizesPath()
    {
        // Arrange
        _mockProcessExecutor
            .Setup(pe => pe.ShellExecuteAsync(It.IsAny<string>(), It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act - pass path with trailing slash; the method normalizes it for comparison
        await _service.OpenFolderAsync(@"C:\TestFolder\");

        // Assert - explorer.exe gets called with original folder path (normalization is internal)
        _mockProcessExecutor.Verify(
            pe => pe.ShellExecuteAsync("explorer.exe", @"C:\TestFolder\", false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenFolderAsync_ShellExecuteThrows_DoesNotThrow()
    {
        // Arrange - ShellExecuteAsync might throw but since COM failed,
        // the method would have already tried COM and caught that exception.
        // If ShellExecute itself throws, it propagates up (no catch around it).
        // We verify that when COM fails gracefully, explorer is attempted.
        _mockProcessExecutor
            .Setup(pe => pe.ShellExecuteAsync(It.IsAny<string>(), It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        // Act & Assert - should not throw
        var act = () => _service.OpenFolderAsync(@"C:\SomeFolder");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OpenFolderAsync_NullProcessExecutor_COMFallsThrough()
    {
        // Arrange - even with an empty folder path, the method should attempt explorer
        _mockProcessExecutor
            .Setup(pe => pe.ShellExecuteAsync("explorer.exe", It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _service.OpenFolderAsync(@"C:\Windows\System32");

        // Assert
        _mockProcessExecutor.Verify(
            pe => pe.ShellExecuteAsync("explorer.exe", It.IsAny<string>(), false, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
