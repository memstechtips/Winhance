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
    private readonly Mock<IGlobalSettingsPreloader> _mockPreloader = new();
    private readonly Mock<ILogService> _mockLogService = new();

    // -------------------------------------------------------
    // EnsureInitializedAsync - first call initializes both
    // -------------------------------------------------------

    [Fact]
    public async Task EnsureInitializedAsync_WhenNeitherInitialized_InitializesBoth()
    {
        _mockRegistry.Setup(r => r.IsInitialized).Returns(false);
        _mockPreloader.Setup(p => p.IsPreloaded).Returns(false);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockPreloader.Object,
            _mockLogService.Object);

        _mockRegistry.Verify(r => r.InitializeAsync(), Times.Once);
        _mockPreloader.Verify(p => p.PreloadAllSettingsAsync(), Times.Once);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenNeitherInitialized_LogsBothMessages()
    {
        _mockRegistry.Setup(r => r.IsInitialized).Returns(false);
        _mockPreloader.Setup(p => p.IsPreloaded).Returns(false);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockPreloader.Object,
            _mockLogService.Object);

        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("registry")), null),
            Times.Once);
        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("Preloading")), null),
            Times.Once);
    }

    // -------------------------------------------------------
    // EnsureInitializedAsync - idempotent (second call skips)
    // -------------------------------------------------------

    [Fact]
    public async Task EnsureInitializedAsync_WhenAlreadyInitialized_SkipsBoth()
    {
        _mockRegistry.Setup(r => r.IsInitialized).Returns(true);
        _mockPreloader.Setup(p => p.IsPreloaded).Returns(true);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockPreloader.Object,
            _mockLogService.Object);

        _mockRegistry.Verify(r => r.InitializeAsync(), Times.Never);
        _mockPreloader.Verify(p => p.PreloadAllSettingsAsync(), Times.Never);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenAlreadyInitialized_DoesNotLog()
    {
        _mockRegistry.Setup(r => r.IsInitialized).Returns(true);
        _mockPreloader.Setup(p => p.IsPreloaded).Returns(true);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockPreloader.Object,
            _mockLogService.Object);

        _mockLogService.Verify(
            l => l.Log(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>()),
            Times.Never);
    }

    // -------------------------------------------------------
    // EnsureInitializedAsync - partial initialization
    // -------------------------------------------------------

    [Fact]
    public async Task EnsureInitializedAsync_WhenRegistryInitializedButNotPreloader_OnlyPreloads()
    {
        _mockRegistry.Setup(r => r.IsInitialized).Returns(true);
        _mockPreloader.Setup(p => p.IsPreloaded).Returns(false);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockPreloader.Object,
            _mockLogService.Object);

        _mockRegistry.Verify(r => r.InitializeAsync(), Times.Never);
        _mockPreloader.Verify(p => p.PreloadAllSettingsAsync(), Times.Once);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenPreloaderDoneButNotRegistry_OnlyInitializesRegistry()
    {
        _mockRegistry.Setup(r => r.IsInitialized).Returns(false);
        _mockPreloader.Setup(p => p.IsPreloaded).Returns(true);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockPreloader.Object,
            _mockLogService.Object);

        _mockRegistry.Verify(r => r.InitializeAsync(), Times.Once);
        _mockPreloader.Verify(p => p.PreloadAllSettingsAsync(), Times.Never);
    }

    // -------------------------------------------------------
    // EnsureInitializedAsync - multiple sequential calls
    // -------------------------------------------------------

    [Fact]
    public async Task EnsureInitializedAsync_CalledTwice_SecondCallIsNoOp()
    {
        // First call: not initialized
        var callCount = 0;
        _mockRegistry.Setup(r => r.IsInitialized)
            .Returns(() => callCount > 0);
        _mockRegistry.Setup(r => r.InitializeAsync())
            .Callback(() => callCount++)
            .Returns(Task.CompletedTask);

        var preloadCount = 0;
        _mockPreloader.Setup(p => p.IsPreloaded)
            .Returns(() => preloadCount > 0);
        _mockPreloader.Setup(p => p.PreloadAllSettingsAsync())
            .Callback(() => preloadCount++)
            .Returns(Task.CompletedTask);

        // First call initializes
        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockPreloader.Object,
            _mockLogService.Object);

        // Second call should be idempotent
        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockPreloader.Object,
            _mockLogService.Object);

        _mockRegistry.Verify(r => r.InitializeAsync(), Times.Once);
        _mockPreloader.Verify(p => p.PreloadAllSettingsAsync(), Times.Once);
    }

    // -------------------------------------------------------
    // EnsureInitializedAsync - logging for partial init
    // -------------------------------------------------------

    [Fact]
    public async Task EnsureInitializedAsync_WhenOnlyRegistryNeeded_LogsOnlyRegistryMessage()
    {
        _mockRegistry.Setup(r => r.IsInitialized).Returns(false);
        _mockPreloader.Setup(p => p.IsPreloaded).Returns(true);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockPreloader.Object,
            _mockLogService.Object);

        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("registry")), null),
            Times.Once);
        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("Preloading")), null),
            Times.Never);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenOnlyPreloaderNeeded_LogsOnlyPreloaderMessage()
    {
        _mockRegistry.Setup(r => r.IsInitialized).Returns(true);
        _mockPreloader.Setup(p => p.IsPreloaded).Returns(false);

        await ConfigRegistryInitializer.EnsureInitializedAsync(
            _mockRegistry.Object,
            _mockPreloader.Object,
            _mockLogService.Object);

        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("registry")), null),
            Times.Never);
        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("Preloading")), null),
            Times.Once);
    }
}
