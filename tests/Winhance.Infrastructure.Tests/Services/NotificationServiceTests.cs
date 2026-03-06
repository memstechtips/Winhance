using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Optimize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class NotificationServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ICompatibleSettingsRegistry> _mockRegistry = new();
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _service = new NotificationService(
            _mockLogService.Object,
            _mockRegistry.Object);
    }

    #region DomainName

    [Fact]
    public void DomainName_ReturnsNotifications()
    {
        _service.DomainName.Should().Be("Notifications");
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
                Id = "notif-1",
                Name = "Test Notification Setting",
                Description = "A test notification setting"
            },
            new SettingDefinition
            {
                Id = "notif-2",
                Name = "Another Notification Setting",
                Description = "Another test notification setting"
            }
        };

        _mockRegistry
            .Setup(r => r.GetFilteredSettings("Notifications"))
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
            .Setup(r => r.GetFilteredSettings("Notifications"))
            .Throws(new InvalidOperationException("Registry failure"));

        // Act
        var result = await _service.GetSettingsAsync();

        // Assert
        result.Should().BeEmpty();
        _mockLogService.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Error,
                It.Is<string>(s => s.Contains("Notifications") && s.Contains("Registry failure"))),
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
                Id = "notif-1",
                Name = "Test Setting",
                Description = "Test"
            }
        };

        _mockRegistry
            .Setup(r => r.GetFilteredSettings("Notifications"))
            .Returns(settings);

        // Act
        var result1 = await _service.GetSettingsAsync();
        var result2 = await _service.GetSettingsAsync();

        // Assert — the registry is called each time, but returns the same collection reference
        result1.Should().BeSameAs(result2);
    }

    [Fact]
    public async Task GetSettingsAsync_EmptyRegistry_ReturnsEmptyCollection()
    {
        // Arrange
        _mockRegistry
            .Setup(r => r.GetFilteredSettings("Notifications"))
            .Returns(Enumerable.Empty<SettingDefinition>());

        // Act
        var result = await _service.GetSettingsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion
}
