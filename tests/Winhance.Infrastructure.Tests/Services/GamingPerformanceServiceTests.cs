using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Optimize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class GamingPerformanceServiceTests
{
    private readonly Mock<ILogService> _logService;
    private readonly Mock<ICompatibleSettingsRegistry> _compatibleSettingsRegistry;
    private readonly GamingPerformanceService _sut;

    public GamingPerformanceServiceTests()
    {
        _logService = new Mock<ILogService>();
        _compatibleSettingsRegistry = new Mock<ICompatibleSettingsRegistry>();

        _sut = new GamingPerformanceService(
            _logService.Object,
            _compatibleSettingsRegistry.Object);
    }

    private static SettingDefinition MakeSetting(string id, string? name = null, string? description = null) =>
        new()
        {
            Id = id,
            Name = name ?? id,
            Description = description ?? $"Description for {id}",
        };

    [Fact]
    public void DomainName_ReturnsGamingPerformance()
    {
        _sut.DomainName.Should().Be(FeatureIds.GamingPerformance);
    }

    [Fact]
    public void DomainName_EqualsLiteralGamingPerformanceString()
    {
        _sut.DomainName.Should().Be("GamingPerformance");
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsFilteredSettings()
    {
        // Arrange
        var expectedSettings = new List<SettingDefinition>
        {
            MakeSetting("gaming-game-mode", "Game Mode"),
            MakeSetting("gaming-background-apps", "Background Apps"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.GamingPerformance))
            .Returns(expectedSettings);

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedSettings);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.GamingPerformance), Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsSettingsDirectly_NoCaching()
    {
        // Arrange - GamingPerformanceService has no caching; each call goes to the registry
        var settings = new List<SettingDefinition>
        {
            MakeSetting("gaming-game-mode"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.GamingPerformance))
            .Returns(settings);

        // Act
        var firstResult = await _sut.GetSettingsAsync();
        var secondResult = await _sut.GetSettingsAsync();

        // Assert - Both calls delegate to registry (no caching in this service)
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.GamingPerformance), Times.Exactly(2));
    }

    [Fact]
    public async Task GetSettingsAsync_WhenRegistryThrows_ReturnsEmptyAndLogs()
    {
        // Arrange
        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.GamingPerformance))
            .Throws(new InvalidOperationException("Registry not initialized"));

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        result.Should().BeEmpty();
        _logService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Error loading Gaming Performance settings"))),
            Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_SettingsIncludeExpectedIds()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            MakeSetting("gaming-game-mode"),
            MakeSetting("gaming-performance-explorer-mouse-precision"),
            MakeSetting("gaming-background-apps"),
            MakeSetting("gaming-storage-sense"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.GamingPerformance))
            .Returns(settings);

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().Contain(s => s.Id == "gaming-game-mode");
        resultList.Should().Contain(s => s.Id == "gaming-performance-explorer-mouse-precision");
        resultList.Should().Contain(s => s.Id == "gaming-background-apps");
        resultList.Should().Contain(s => s.Id == "gaming-storage-sense");
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsTaskFromResult_CompletedSynchronously()
    {
        // Arrange - GamingPerformanceService uses Task.FromResult, not async
        var expectedSettings = new List<SettingDefinition>
        {
            MakeSetting("gaming-game-mode"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.GamingPerformance))
            .Returns(expectedSettings);

        // Act
        var task = _sut.GetSettingsAsync();

        // Assert - Task should be completed synchronously
        task.IsCompleted.Should().BeTrue();
        var result = await task;
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenRegistryReturnsEmpty_ReturnsEmpty()
    {
        // Arrange
        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.GamingPerformance))
            .Returns(Enumerable.Empty<SettingDefinition>());

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSettingsAsync_WhenExceptionThrown_TaskCompletedSynchronously()
    {
        // Arrange
        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.GamingPerformance))
            .Throws(new Exception("Unexpected error"));

        // Act
        var task = _sut.GetSettingsAsync();

        // Assert - Even on error, Task.FromResult returns a completed task
        task.IsCompleted.Should().BeTrue();
        var result = await task;
        result.Should().BeEmpty();
    }
}
