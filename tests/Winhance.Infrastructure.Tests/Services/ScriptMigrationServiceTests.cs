using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ScriptMigrationServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IScheduledTaskService> _mockScheduledTask = new();
    private readonly Mock<IUserPreferencesService> _mockPrefs = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUser = new();
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly ScriptMigrationService _service;

    private const string FakeLocalAppData = @"C:\Users\TestUser\AppData\Local";
    private const string FakeOldScriptsPath = @"C:\Users\TestUser\AppData\Local\Winhance\Scripts";

    public ScriptMigrationServiceTests()
    {
        _mockInteractiveUser
            .Setup(x => x.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData))
            .Returns(FakeLocalAppData);

        _mockFileSystem
            .Setup(x => x.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => Path.Combine(paths));

        _service = new ScriptMigrationService(
            _mockLog.Object,
            _mockScheduledTask.Object,
            _mockPrefs.Object,
            _mockInteractiveUser.Object,
            _mockFileSystem.Object);
    }

    [Fact]
    public async Task MigrateFromOldPathsAsync_AlreadyMigrated_SkipsAndReturnsSuccess()
    {
        // Arrange
        _mockPrefs
            .Setup(x => x.GetPreferenceAsync("ScriptMigrationCompleted", false))
            .ReturnsAsync(true);

        // Act
        var result = await _service.MigrateFromOldPathsAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.MigrationPerformed.Should().BeFalse();
        _mockFileSystem.Verify(x => x.DirectoryExists(It.IsAny<string>()), Times.Never);
        _mockLog.Verify(x => x.Log(LogLevel.Info, It.Is<string>(s => s.Contains("already completed")), null), Times.Once);
    }

    [Fact]
    public async Task MigrateFromOldPathsAsync_OldFilesFound_MigratesAndRenames()
    {
        // Arrange
        _mockPrefs
            .Setup(x => x.GetPreferenceAsync("ScriptMigrationCompleted", false))
            .ReturnsAsync(false);

        _mockFileSystem
            .Setup(x => x.DirectoryExists(FakeOldScriptsPath))
            .Returns(true);

        // All three scripts exist
        _mockFileSystem
            .Setup(x => x.FileExists(It.Is<string>(p => p.EndsWith(".ps1"))))
            .Returns(true);

        // No .old files exist yet
        _mockFileSystem
            .Setup(x => x.FileExists(It.Is<string>(p => p.EndsWith(".ps1.old"))))
            .Returns(false);

        // All three scheduled tasks exist
        _mockScheduledTask
            .Setup(x => x.IsTaskRegisteredAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockScheduledTask
            .Setup(x => x.UnregisterScheduledTaskAsync(It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());

        _mockPrefs
            .Setup(x => x.SetPreferenceAsync("ScriptMigrationCompleted", true))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        var result = await _service.MigrateFromOldPathsAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.MigrationPerformed.Should().BeTrue();
        result.TasksDeleted.Should().Be(3);
        result.ScriptsRenamed.Should().Be(3);

        _mockScheduledTask.Verify(
            x => x.UnregisterScheduledTaskAsync(It.IsAny<string>()), Times.Exactly(3));
        _mockFileSystem.Verify(
            x => x.MoveFile(It.IsAny<string>(), It.Is<string>(p => p.EndsWith(".old"))), Times.Exactly(3));
        _mockPrefs.Verify(
            x => x.SetPreferenceAsync("ScriptMigrationCompleted", true), Times.Once);
    }

    [Fact]
    public async Task MigrateFromOldPathsAsync_OldDirectoryExistsButNoScripts_MarksComplete()
    {
        // Arrange
        _mockPrefs
            .Setup(x => x.GetPreferenceAsync("ScriptMigrationCompleted", false))
            .ReturnsAsync(false);

        _mockFileSystem
            .Setup(x => x.DirectoryExists(FakeOldScriptsPath))
            .Returns(true);

        // No scripts exist at old paths
        _mockFileSystem
            .Setup(x => x.FileExists(It.IsAny<string>()))
            .Returns(false);

        // No scheduled tasks exist
        _mockScheduledTask
            .Setup(x => x.IsTaskRegisteredAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockPrefs
            .Setup(x => x.SetPreferenceAsync("ScriptMigrationCompleted", true))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        var result = await _service.MigrateFromOldPathsAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.MigrationPerformed.Should().BeTrue();
        result.TasksDeleted.Should().Be(0);
        result.ScriptsRenamed.Should().Be(0);
        _mockPrefs.Verify(
            x => x.SetPreferenceAsync("ScriptMigrationCompleted", true), Times.Once);
    }

    [Fact]
    public async Task MigrateFromOldPathsAsync_NoOldDirectory_MarksCompleteWithoutMigration()
    {
        // Arrange
        _mockPrefs
            .Setup(x => x.GetPreferenceAsync("ScriptMigrationCompleted", false))
            .ReturnsAsync(false);

        _mockFileSystem
            .Setup(x => x.DirectoryExists(FakeOldScriptsPath))
            .Returns(false);

        _mockPrefs
            .Setup(x => x.SetPreferenceAsync("ScriptMigrationCompleted", true))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        var result = await _service.MigrateFromOldPathsAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.MigrationPerformed.Should().BeFalse();
        _mockPrefs.Verify(
            x => x.SetPreferenceAsync("ScriptMigrationCompleted", true), Times.Once);
        _mockScheduledTask.Verify(
            x => x.IsTaskRegisteredAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MigrateFromOldPathsAsync_ExistingOldRenameFile_DeletesBeforeRenaming()
    {
        // Arrange
        _mockPrefs
            .Setup(x => x.GetPreferenceAsync("ScriptMigrationCompleted", false))
            .ReturnsAsync(false);

        _mockFileSystem
            .Setup(x => x.DirectoryExists(FakeOldScriptsPath))
            .Returns(true);

        // Only one script exists and its .old counterpart also exists
        _mockFileSystem
            .Setup(x => x.FileExists(It.Is<string>(p =>
                p == Path.Combine(FakeOldScriptsPath, "EdgeRemoval.ps1"))))
            .Returns(true);

        _mockFileSystem
            .Setup(x => x.FileExists(It.Is<string>(p =>
                p == Path.Combine(FakeOldScriptsPath, "EdgeRemoval.ps1.old"))))
            .Returns(true);

        // Other scripts don't exist
        _mockFileSystem
            .Setup(x => x.FileExists(It.Is<string>(p =>
                p == Path.Combine(FakeOldScriptsPath, "BloatRemoval.ps1"))))
            .Returns(false);

        _mockFileSystem
            .Setup(x => x.FileExists(It.Is<string>(p =>
                p == Path.Combine(FakeOldScriptsPath, "OneDriveRemoval.ps1"))))
            .Returns(false);

        _mockScheduledTask
            .Setup(x => x.IsTaskRegisteredAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockPrefs
            .Setup(x => x.SetPreferenceAsync("ScriptMigrationCompleted", true))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        var result = await _service.MigrateFromOldPathsAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.ScriptsRenamed.Should().Be(1);

        _mockFileSystem.Verify(
            x => x.DeleteFile(It.Is<string>(p => p.EndsWith("EdgeRemoval.ps1.old"))),
            Times.Once);
        _mockFileSystem.Verify(
            x => x.MoveFile(
                It.Is<string>(p => p.EndsWith("EdgeRemoval.ps1")),
                It.Is<string>(p => p.EndsWith("EdgeRemoval.ps1.old"))),
            Times.Once);
    }

    [Fact]
    public async Task MigrateFromOldPathsAsync_ExceptionThrown_ReturnsFailure()
    {
        // Arrange
        _mockPrefs
            .Setup(x => x.GetPreferenceAsync("ScriptMigrationCompleted", false))
            .ThrowsAsync(new InvalidOperationException("Prefs unavailable"));

        // Act
        var result = await _service.MigrateFromOldPathsAsync();

        // Assert
        result.Success.Should().BeFalse();
        _mockLog.Verify(
            x => x.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Error during script migration")), null),
            Times.Once);
    }

    [Fact]
    public async Task MigrateFromOldPathsAsync_TaskDeletionFails_ContinuesWithOtherTasks()
    {
        // Arrange
        _mockPrefs
            .Setup(x => x.GetPreferenceAsync("ScriptMigrationCompleted", false))
            .ReturnsAsync(false);

        _mockFileSystem
            .Setup(x => x.DirectoryExists(FakeOldScriptsPath))
            .Returns(true);

        _mockFileSystem
            .Setup(x => x.FileExists(It.IsAny<string>()))
            .Returns(false);

        // First task throws, second is registered and succeeds, third not registered
        _mockScheduledTask
            .Setup(x => x.IsTaskRegisteredAsync("BloatRemoval"))
            .ThrowsAsync(new Exception("Access denied"));

        _mockScheduledTask
            .Setup(x => x.IsTaskRegisteredAsync("EdgeRemoval"))
            .ReturnsAsync(true);

        _mockScheduledTask
            .Setup(x => x.UnregisterScheduledTaskAsync("EdgeRemoval"))
            .ReturnsAsync(OperationResult.Succeeded());

        _mockScheduledTask
            .Setup(x => x.IsTaskRegisteredAsync("OneDriveRemoval"))
            .ReturnsAsync(false);

        _mockPrefs
            .Setup(x => x.SetPreferenceAsync("ScriptMigrationCompleted", true))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        var result = await _service.MigrateFromOldPathsAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.TasksDeleted.Should().Be(1);
        _mockLog.Verify(
            x => x.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("Could not delete task BloatRemoval")), null),
            Times.Once);
    }
}
