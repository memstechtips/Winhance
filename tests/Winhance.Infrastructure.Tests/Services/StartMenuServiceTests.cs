using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Customize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class StartMenuServiceTests
{
    private readonly Mock<IScheduledTaskService> _scheduledTaskService;
    private readonly Mock<ILogService> _logService;
    private readonly Mock<IInteractiveUserService> _interactiveUserService;
    private readonly Mock<IProcessExecutor> _processExecutor;
    private readonly Mock<IFileSystemService> _fileSystemService;
    private readonly Mock<IWindowsRegistryService> _windowsRegistryService;
    private readonly StartMenuService _sut;

    public StartMenuServiceTests()
    {
        _scheduledTaskService = new Mock<IScheduledTaskService>();
        _logService = new Mock<ILogService>();
        _interactiveUserService = new Mock<IInteractiveUserService>();
        _processExecutor = new Mock<IProcessExecutor>();
        _fileSystemService = new Mock<IFileSystemService>();
        _windowsRegistryService = new Mock<IWindowsRegistryService>();

        _sut = new StartMenuService(
            _scheduledTaskService.Object,
            _logService.Object,
            _interactiveUserService.Object,
            _processExecutor.Object,
            _fileSystemService.Object,
            _windowsRegistryService.Object);
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
