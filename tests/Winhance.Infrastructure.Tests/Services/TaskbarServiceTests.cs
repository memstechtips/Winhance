using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Customize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class TaskbarServiceTests
{
    private readonly Mock<ILogService> _logService;
    private readonly Mock<IWindowsRegistryService> _windowsRegistryService;
    private readonly Mock<ICompatibleSettingsRegistry> _compatibleSettingsRegistry;
    private readonly TaskbarService _sut;

    public TaskbarServiceTests()
    {
        _logService = new Mock<ILogService>();
        _windowsRegistryService = new Mock<IWindowsRegistryService>();
        _compatibleSettingsRegistry = new Mock<ICompatibleSettingsRegistry>();

        _sut = new TaskbarService(
            _logService.Object,
            _windowsRegistryService.Object,
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
    public void DomainName_ReturnsTaskbar()
    {
        _sut.DomainName.Should().Be(FeatureIds.Taskbar);
    }

    [Fact]
    public void DomainName_EqualsLiteralTaskbarString()
    {
        _sut.DomainName.Should().Be("Taskbar");
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsFilteredSettings()
    {
        // Arrange
        var expectedSettings = new List<SettingDefinition>
        {
            MakeSetting("taskbar-clean", "Clean Taskbar"),
            MakeSetting("taskbar-search-box-11", "Search in taskbar"),
            MakeSetting("taskbar-alignment", "Taskbar Alignment"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.Taskbar))
            .Returns(expectedSettings);

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedSettings);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.Taskbar), Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_CachesResults_SecondCallReturnsSameReference()
    {
        // Arrange
        var expectedSettings = new List<SettingDefinition>
        {
            MakeSetting("taskbar-clean"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.Taskbar))
            .Returns(expectedSettings);

        // Act
        var firstResult = await _sut.GetSettingsAsync();
        var secondResult = await _sut.GetSettingsAsync();

        // Assert
        firstResult.Should().BeSameAs(secondResult);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.Taskbar), Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenRegistryThrows_ReturnsEmptyAndLogs()
    {
        // Arrange
        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.Taskbar))
            .Throws(new InvalidOperationException("Registry not initialized"));

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        result.Should().BeEmpty();
        _logService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Error loading Taskbar settings"))),
            Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_SettingsIncludeExpectedIds()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            MakeSetting("taskbar-clean"),
            MakeSetting("taskbar-search-box-11"),
            MakeSetting("taskbar-alignment"),
            MakeSetting("taskbar-meet-now"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.Taskbar))
            .Returns(settings);

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().Contain(s => s.Id == "taskbar-clean");
        resultList.Should().Contain(s => s.Id == "taskbar-search-box-11");
        resultList.Should().Contain(s => s.Id == "taskbar-alignment");
        resultList.Should().Contain(s => s.Id == "taskbar-meet-now");
    }

    [Fact]
    public async Task InvalidateCache_ClearsCache_NextCallReloads()
    {
        // Arrange
        var firstSettings = new List<SettingDefinition>
        {
            MakeSetting("taskbar-clean"),
        };
        var secondSettings = new List<SettingDefinition>
        {
            MakeSetting("taskbar-clean"),
            MakeSetting("taskbar-alignment"),
        };

        _compatibleSettingsRegistry
            .SetupSequence(r => r.GetFilteredSettings(FeatureIds.Taskbar))
            .Returns(firstSettings)
            .Returns(secondSettings);

        // Act
        var first = await _sut.GetSettingsAsync();
        _sut.InvalidateCache();
        var afterInvalidation = await _sut.GetSettingsAsync();

        // Assert
        first.Should().HaveCount(1);
        afterInvalidation.Should().HaveCount(2);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.Taskbar), Times.Exactly(2));
    }

    [Fact]
    public void SupportedCommands_ContainsCleanTaskbarAsync()
    {
        _sut.SupportedCommands.Should().Contain("CleanTaskbarAsync");
    }

    [Fact]
    public async Task ExecuteCommandAsync_UnsupportedCommand_ThrowsNotSupportedException()
    {
        // Act
        var action = () => _sut.ExecuteCommandAsync("NonExistentCommand");

        // Assert
        await action.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*NonExistentCommand*");
    }

    [Fact]
    public async Task CleanTaskbarAsync_WhenTaskbandKeyDoesNotExist_LogsWarning()
    {
        // Arrange
        _windowsRegistryService
            .Setup(r => r.KeyExists(It.IsAny<string>()))
            .Returns(false);

        // Act
        await _sut.CleanTaskbarAsync();

        // Assert
        _logService.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("Taskband key does not exist"))),
            Times.Once);
    }

    [Fact]
    public async Task CleanTaskbarAsync_WhenKeyExists_SetsFavoritesToEmptyBinary()
    {
        // Arrange
        _windowsRegistryService
            .Setup(r => r.KeyExists(It.IsAny<string>()))
            .Returns(true);

        _windowsRegistryService
            .Setup(r => r.SetValue(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Microsoft.Win32.RegistryValueKind>()))
            .Returns(true);

        // Act
        await _sut.CleanTaskbarAsync();

        // Assert
        _windowsRegistryService.Verify(
            r => r.SetValue(
                It.Is<string>(s => s.Contains("Taskband")),
                "Favorites",
                It.IsAny<byte[]>(),
                Microsoft.Win32.RegistryValueKind.Binary),
            Times.Once);

        _logService.Verify(
            l => l.Log(LogLevel.Success, It.Is<string>(s => s.Contains("Successfully cleared Favorites"))),
            Times.Once);
    }
}
