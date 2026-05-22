using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Customize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class TaskbarServiceTests
{
    private readonly Mock<ILogService> _logService;
    private readonly Mock<IWindowsRegistryService> _windowsRegistryService;
    private readonly TaskbarService _sut;

    public TaskbarServiceTests()
    {
        _logService = new Mock<ILogService>();
        _windowsRegistryService = new Mock<IWindowsRegistryService>();

        _sut = new TaskbarService(
            _logService.Object,
            _windowsRegistryService.Object);
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
