using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Optimize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class PrivacyAndSecurityServiceTests
{
    private readonly Mock<ILogService> _logService;
    private readonly Mock<ICompatibleSettingsRegistry> _compatibleSettingsRegistry;
    private readonly PrivacyAndSecurityService _sut;

    public PrivacyAndSecurityServiceTests()
    {
        _logService = new Mock<ILogService>();
        _compatibleSettingsRegistry = new Mock<ICompatibleSettingsRegistry>();

        _sut = new PrivacyAndSecurityService(
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
    public void DomainName_ReturnsPrivacy()
    {
        _sut.DomainName.Should().Be(FeatureIds.Privacy);
    }

    [Fact]
    public void DomainName_EqualsLiteralPrivacyString()
    {
        _sut.DomainName.Should().Be("Privacy");
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsFilteredSettings()
    {
        // Arrange
        var expectedSettings = new List<SettingDefinition>
        {
            MakeSetting("security-uac-level", "User Account Control Level"),
            MakeSetting("security-wifi-sense", "Wi-Fi Sense"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.Privacy))
            .Returns(expectedSettings);

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedSettings);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.Privacy), Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsSettingsDirectly_NoCaching()
    {
        // Arrange - PrivacyAndSecurityService has no caching; each call goes to the registry
        var settings = new List<SettingDefinition>
        {
            MakeSetting("security-uac-level"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.Privacy))
            .Returns(settings);

        // Act
        var firstResult = await _sut.GetSettingsAsync();
        var secondResult = await _sut.GetSettingsAsync();

        // Assert - Both calls delegate to registry (no caching in this service)
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.Privacy), Times.Exactly(2));
    }

    [Fact]
    public async Task GetSettingsAsync_WhenRegistryThrows_ReturnsEmptyAndLogs()
    {
        // Arrange
        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.Privacy))
            .Throws(new InvalidOperationException("Registry not initialized"));

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        result.Should().BeEmpty();
        _logService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Error loading Privacy & Security settings"))),
            Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_SettingsIncludeExpectedIds()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            MakeSetting("security-uac-level"),
            MakeSetting("security-wifi-sense"),
            MakeSetting("security-error-reporting"),
            MakeSetting("security-automatic-maintenance"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.Privacy))
            .Returns(settings);

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().Contain(s => s.Id == "security-uac-level");
        resultList.Should().Contain(s => s.Id == "security-wifi-sense");
        resultList.Should().Contain(s => s.Id == "security-error-reporting");
        resultList.Should().Contain(s => s.Id == "security-automatic-maintenance");
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsTaskFromResult_CompletedSynchronously()
    {
        // Arrange - PrivacyAndSecurityService uses Task.FromResult, not async
        var expectedSettings = new List<SettingDefinition>
        {
            MakeSetting("security-uac-level"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.Privacy))
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
            .Setup(r => r.GetFilteredSettings(FeatureIds.Privacy))
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
            .Setup(r => r.GetFilteredSettings(FeatureIds.Privacy))
            .Throws(new Exception("Unexpected error"));

        // Act
        var task = _sut.GetSettingsAsync();

        // Assert - Even on error, Task.FromResult returns a completed task
        task.IsCompleted.Should().BeTrue();
        var result = await task;
        result.Should().BeEmpty();
    }
}
