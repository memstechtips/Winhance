using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Optimize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SoundServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ICompatibleSettingsRegistry> _mockRegistry = new();
    private readonly SoundService _service;

    public SoundServiceTests()
    {
        _service = new SoundService(
            _mockLogService.Object,
            _mockRegistry.Object);
    }

    #region DomainName

    [Fact]
    public void DomainName_ReturnsSound()
    {
        _service.DomainName.Should().Be("Sound");
    }

    #endregion

    #region GetSettingsAsync

    [Fact]
    public async Task GetSettingsAsync_ReturnsSettingsFromRegistry()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            new SettingDefinition
            {
                Id = "sound-1",
                Name = "Sound Setting One",
                Description = "First sound setting"
            },
            new SettingDefinition
            {
                Id = "sound-2",
                Name = "Sound Setting Two",
                Description = "Second sound setting"
            }
        };

        _mockRegistry
            .Setup(r => r.GetFilteredSettings("Sound"))
            .Returns(settings);

        // Act
        var result = await _service.GetSettingsAsync();

        // Assert
        result.Should().BeSameAs(settings);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenRegistryThrows_ReturnsEmptyAndLogs()
    {
        // Arrange
        _mockRegistry
            .Setup(r => r.GetFilteredSettings("Sound"))
            .Throws(new InvalidOperationException("Sound registry failure"));

        // Act
        var result = await _service.GetSettingsAsync();

        // Assert
        result.Should().BeEmpty();
        _mockLogService.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Error,
                It.Is<string>(s => s.Contains("Sound") && s.Contains("Sound registry failure"))),
            Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_CalledTwice_ReturnsSameReference()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            new SettingDefinition
            {
                Id = "sound-1",
                Name = "Sound Setting",
                Description = "Test"
            }
        };

        _mockRegistry
            .Setup(r => r.GetFilteredSettings("Sound"))
            .Returns(settings);

        // Act
        var result1 = await _service.GetSettingsAsync();
        var result2 = await _service.GetSettingsAsync();

        // Assert
        result1.Should().BeSameAs(result2);
    }

    [Fact]
    public async Task GetSettingsAsync_EmptyRegistry_ReturnsEmptyCollection()
    {
        // Arrange
        _mockRegistry
            .Setup(r => r.GetFilteredSettings("Sound"))
            .Returns(Enumerable.Empty<SettingDefinition>());

        // Act
        var result = await _service.GetSettingsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion
}
