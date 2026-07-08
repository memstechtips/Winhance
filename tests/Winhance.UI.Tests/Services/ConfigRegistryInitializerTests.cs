using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Utilities;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ConfigRegistryInitializerTests
{
    private readonly Mock<ICompatibleSettingsRegistry> _mockRegistry = new();
    private readonly Mock<ILogService> _mockLogService = new();

    // -------------------------------------------------------
    // EnsureInitializedAsync - initializes the registry when needed
    // -------------------------------------------------------

    [Fact]
    public async Task EnsureInitializedAsync_WhenNotInitialized_InitializesRegistry()
    {
        _mockRegistry.Setup(r => r.IsInitialized).Returns(false);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockLogService.Object);

        _mockRegistry.Verify(r => r.InitializeAsync(), Times.Once);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenNotInitialized_LogsRegistryMessage()
    {
        _mockRegistry.Setup(r => r.IsInitialized).Returns(false);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockLogService.Object);

        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("registry")), null),
            Times.Once);
    }

    // -------------------------------------------------------
    // EnsureInitializedAsync - idempotent (already initialized)
    // -------------------------------------------------------

    [Fact]
    public async Task EnsureInitializedAsync_WhenAlreadyInitialized_SkipsInit()
    {
        _mockRegistry.Setup(r => r.IsInitialized).Returns(true);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockLogService.Object);

        _mockRegistry.Verify(r => r.InitializeAsync(), Times.Never);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenAlreadyInitialized_DoesNotLog()
    {
        _mockRegistry.Setup(r => r.IsInitialized).Returns(true);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockLogService.Object);

        _mockLogService.Verify(
            l => l.Log(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>()),
            Times.Never);
    }

    // -------------------------------------------------------
    // EnsureInitializedAsync - multiple sequential calls
    // -------------------------------------------------------

    [Fact]
    public async Task EnsureInitializedAsync_CalledTwice_SecondCallIsNoOp()
    {
        var callCount = 0;
        _mockRegistry.Setup(r => r.IsInitialized)
            .Returns(() => callCount > 0);
        _mockRegistry.Setup(r => r.InitializeAsync())
            .Callback(() => callCount++)
            .Returns(Task.CompletedTask);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockLogService.Object);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockLogService.Object);

        _mockRegistry.Verify(r => r.InitializeAsync(), Times.Once);
    }
}
