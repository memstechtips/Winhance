using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Customize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ExplorerCustomizationServiceTests
{
    private readonly Mock<ILogService> _logService;
    private readonly Mock<ICompatibleSettingsRegistry> _compatibleSettingsRegistry;
    private readonly ExplorerCustomizationService _sut;

    public ExplorerCustomizationServiceTests()
    {
        _logService = new Mock<ILogService>();
        _compatibleSettingsRegistry = new Mock<ICompatibleSettingsRegistry>();

        _sut = new ExplorerCustomizationService(
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
    public void DomainName_ReturnsExplorerCustomization()
    {
        _sut.DomainName.Should().Be(FeatureIds.ExplorerCustomization);
    }

    [Fact]
    public void DomainName_EqualsLiteralExplorerCustomizationString()
    {
        _sut.DomainName.Should().Be("ExplorerCustomization");
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsFilteredSettings()
    {
        // Arrange
        var expectedSettings = new List<SettingDefinition>
        {
            MakeSetting("explorer-customization-shortcut-suffix", "Remove '- Shortcut' suffix"),
            MakeSetting("explorer-customization-context-menu", "Context Menu"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.ExplorerCustomization))
            .Returns(expectedSettings);

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedSettings);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.ExplorerCustomization), Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_CachesResults_SecondCallReturnsSameReference()
    {
        // Arrange
        var expectedSettings = new List<SettingDefinition>
        {
            MakeSetting("explorer-customization-shortcut-suffix"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.ExplorerCustomization))
            .Returns(expectedSettings);

        // Act
        var firstResult = await _sut.GetSettingsAsync();
        var secondResult = await _sut.GetSettingsAsync();

        // Assert
        firstResult.Should().BeSameAs(secondResult);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.ExplorerCustomization), Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenRegistryThrows_ReturnsEmptyAndLogs()
    {
        // Arrange
        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.ExplorerCustomization))
            .Throws(new InvalidOperationException("Registry not initialized"));

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        result.Should().BeEmpty();
        _logService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Error loading Explorer Customizations settings"))),
            Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_SettingsIncludeExpectedIds()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            MakeSetting("explorer-customization-shortcut-suffix"),
            MakeSetting("explorer-customization-context-menu"),
            MakeSetting("explorer-take-ownership"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.ExplorerCustomization))
            .Returns(settings);

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().Contain(s => s.Id == "explorer-customization-shortcut-suffix");
        resultList.Should().Contain(s => s.Id == "explorer-customization-context-menu");
        resultList.Should().Contain(s => s.Id == "explorer-take-ownership");
    }

    [Fact]
    public async Task InvalidateCache_ClearsCache_NextCallReloads()
    {
        // Arrange
        var firstSettings = new List<SettingDefinition>
        {
            MakeSetting("explorer-customization-shortcut-suffix"),
        };
        var secondSettings = new List<SettingDefinition>
        {
            MakeSetting("explorer-customization-shortcut-suffix"),
            MakeSetting("explorer-customization-context-menu"),
        };

        _compatibleSettingsRegistry
            .SetupSequence(r => r.GetFilteredSettings(FeatureIds.ExplorerCustomization))
            .Returns(firstSettings)
            .Returns(secondSettings);

        // Act
        var first = await _sut.GetSettingsAsync();
        _sut.InvalidateCache();
        var afterInvalidation = await _sut.GetSettingsAsync();

        // Assert
        first.Should().HaveCount(1);
        afterInvalidation.Should().HaveCount(2);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.ExplorerCustomization), Times.Exactly(2));
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsTaskFromResult_CompletedSynchronously()
    {
        // Arrange - ExplorerCustomizationService uses Task.FromResult, not async
        var expectedSettings = new List<SettingDefinition>
        {
            MakeSetting("explorer-customization-shortcut-suffix"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.ExplorerCustomization))
            .Returns(expectedSettings);

        // Act
        var task = _sut.GetSettingsAsync();

        // Assert - Task should be completed synchronously
        task.IsCompleted.Should().BeTrue();
        var result = await task;
        result.Should().HaveCount(1);
    }
}
