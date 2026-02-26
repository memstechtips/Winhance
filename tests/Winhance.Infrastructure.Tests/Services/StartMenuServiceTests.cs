using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Customize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class StartMenuServiceTests
{
    private readonly Mock<IScheduledTaskService> _scheduledTaskService;
    private readonly Mock<ILogService> _logService;
    private readonly Mock<ICompatibleSettingsRegistry> _compatibleSettingsRegistry;
    private readonly Mock<IInteractiveUserService> _interactiveUserService;
    private readonly Mock<IProcessExecutor> _processExecutor;
    private readonly Mock<IFileSystemService> _fileSystemService;
    private readonly StartMenuService _sut;

    public StartMenuServiceTests()
    {
        _scheduledTaskService = new Mock<IScheduledTaskService>();
        _logService = new Mock<ILogService>();
        _compatibleSettingsRegistry = new Mock<ICompatibleSettingsRegistry>();
        _interactiveUserService = new Mock<IInteractiveUserService>();
        _processExecutor = new Mock<IProcessExecutor>();
        _fileSystemService = new Mock<IFileSystemService>();

        _sut = new StartMenuService(
            _scheduledTaskService.Object,
            _logService.Object,
            _compatibleSettingsRegistry.Object,
            _interactiveUserService.Object,
            _processExecutor.Object,
            _fileSystemService.Object);
    }

    private static SettingDefinition MakeSetting(string id, string? name = null, string? description = null) =>
        new()
        {
            Id = id,
            Name = name ?? id,
            Description = description ?? $"Description for {id}",
        };

    [Fact]
    public void DomainName_ReturnsStartMenu()
    {
        _sut.DomainName.Should().Be(FeatureIds.StartMenu);
    }

    [Fact]
    public void DomainName_EqualsLiteralStartMenuString()
    {
        _sut.DomainName.Should().Be("StartMenu");
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsFilteredSettings()
    {
        // Arrange
        var expectedSettings = new List<SettingDefinition>
        {
            MakeSetting("start-menu-clean-10", "Clean Start Menu"),
            MakeSetting("start-menu-clean-11", "Clean Start Menu"),
            MakeSetting("start-menu-layout", "Start Menu Layout"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.StartMenu))
            .Returns(expectedSettings);

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedSettings);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.StartMenu), Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_CachesResults_SecondCallReturnsSameReference()
    {
        // Arrange
        var expectedSettings = new List<SettingDefinition>
        {
            MakeSetting("start-menu-clean-10"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.StartMenu))
            .Returns(expectedSettings);

        // Act
        var firstResult = await _sut.GetSettingsAsync();
        var secondResult = await _sut.GetSettingsAsync();

        // Assert
        firstResult.Should().BeSameAs(secondResult);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.StartMenu), Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenRegistryThrows_ReturnsEmptyAndLogs()
    {
        // Arrange
        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.StartMenu))
            .Throws(new InvalidOperationException("Registry not initialized"));

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        result.Should().BeEmpty();
        _logService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Error loading Start Menu settings"))),
            Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_SettingsIncludeExpectedIds()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            MakeSetting("start-menu-clean-10"),
            MakeSetting("start-menu-clean-11"),
            MakeSetting("start-menu-layout"),
            MakeSetting("start-recommended-section"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.StartMenu))
            .Returns(settings);

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().Contain(s => s.Id == "start-menu-clean-10");
        resultList.Should().Contain(s => s.Id == "start-menu-clean-11");
        resultList.Should().Contain(s => s.Id == "start-menu-layout");
        resultList.Should().Contain(s => s.Id == "start-recommended-section");
    }

    [Fact]
    public async Task InvalidateCache_ClearsCache_NextCallReloads()
    {
        // Arrange
        var firstSettings = new List<SettingDefinition>
        {
            MakeSetting("start-menu-clean-10"),
        };
        var secondSettings = new List<SettingDefinition>
        {
            MakeSetting("start-menu-clean-10"),
            MakeSetting("start-menu-clean-11"),
        };

        _compatibleSettingsRegistry
            .SetupSequence(r => r.GetFilteredSettings(FeatureIds.StartMenu))
            .Returns(firstSettings)
            .Returns(secondSettings);

        // Act
        var first = await _sut.GetSettingsAsync();
        _sut.InvalidateCache();
        var afterInvalidation = await _sut.GetSettingsAsync();

        // Assert
        first.Should().HaveCount(1);
        afterInvalidation.Should().HaveCount(2);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.StartMenu), Times.Exactly(2));
    }

    [Fact]
    public void SupportedCommands_ContainsExpectedCommands()
    {
        _sut.SupportedCommands.Should().Contain("CleanWindows10StartMenuAsync");
        _sut.SupportedCommands.Should().Contain("CleanWindows11StartMenuAsync");
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
}
