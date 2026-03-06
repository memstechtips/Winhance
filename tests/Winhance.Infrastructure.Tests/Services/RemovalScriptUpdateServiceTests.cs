using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class RemovalScriptUpdateServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IScheduledTaskService> _mockScheduledTask = new();
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly RemovalScriptUpdateService _service;

    private static readonly string ScriptsDir = ScriptPaths.ScriptsDirectory;

    public RemovalScriptUpdateServiceTests()
    {
        _service = new RemovalScriptUpdateService(
            _mockLog.Object,
            _mockScheduledTask.Object,
            _mockFileSystem.Object);

        // Default: CombinePath delegates to Path.Combine
        _mockFileSystem
            .Setup(x => x.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => Path.Combine(paths));
    }

    private string ScriptPath(string name) => Path.Combine(ScriptsDir, $"{name}.ps1");

    private static string MakeScriptContent(string version) =>
        $"<#\n  .SYNOPSIS\n      Script Version: {version}\n#>";

    [Fact]
    public async Task CheckAndUpdateScriptsAsync_ScriptsUpToDate_NoChanges()
    {
        // Arrange - all script files exist with current versions
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("EdgeRemoval")))
            .Returns(true);
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("EdgeRemoval")))
            .Returns(MakeScriptContent(EdgeRemovalScript.ScriptVersion));

        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("OneDriveRemoval")))
            .Returns(true);
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("OneDriveRemoval")))
            .Returns(MakeScriptContent(OneDriveRemovalScript.ScriptVersion));

        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("BloatRemoval")))
            .Returns(true);
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("BloatRemoval")))
            .Returns(MakeScriptContent(BloatRemovalScriptGenerator.ScriptVersion));

        // Act
        await _service.CheckAndUpdateScriptsAsync();

        // Assert - no writes should occur
        _mockFileSystem.Verify(
            x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockScheduledTask.Verify(
            x => x.RunScheduledTaskAsync(It.IsAny<string>()), Times.Never);
        _mockLog.Verify(
            x => x.LogInformation(It.Is<string>(s => s.Contains("is up to date"))),
            Times.Exactly(3));
    }

    [Fact]
    public async Task CheckAndUpdateScriptsAsync_ScriptsOutdated_UpdatesContent()
    {
        // Arrange - EdgeRemoval exists but has an old version
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("EdgeRemoval")))
            .Returns(true);
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("EdgeRemoval")))
            .Returns(MakeScriptContent("0.1"));

        // OneDriveRemoval exists but has old version
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("OneDriveRemoval")))
            .Returns(true);
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("OneDriveRemoval")))
            .Returns(MakeScriptContent("0.1"));

        // BloatRemoval exists but has old version
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("BloatRemoval")))
            .Returns(true);
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("BloatRemoval")))
            .Returns(MakeScriptContent("0.1"));

        _mockScheduledTask
            .Setup(x => x.RunScheduledTaskAsync(It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _service.CheckAndUpdateScriptsAsync();

        // Assert
        // EdgeRemoval uses GetContent (full replacement), runs after update
        _mockFileSystem.Verify(
            x => x.WriteAllText(ScriptPath("EdgeRemoval"), It.IsAny<string>()), Times.Once);
        _mockScheduledTask.Verify(
            x => x.RunScheduledTaskAsync("EdgeRemoval"), Times.Once);

        // OneDriveRemoval uses GetContent (full replacement), runs after update
        _mockFileSystem.Verify(
            x => x.WriteAllText(ScriptPath("OneDriveRemoval"), It.IsAny<string>()), Times.Once);
        _mockScheduledTask.Verify(
            x => x.RunScheduledTaskAsync("OneDriveRemoval"), Times.Once);

        // BloatRemoval uses UpdateContent (template update), does NOT run after update
        _mockFileSystem.Verify(
            x => x.WriteAllText(ScriptPath("BloatRemoval"), It.IsAny<string>()), Times.Once);
        _mockScheduledTask.Verify(
            x => x.RunScheduledTaskAsync("BloatRemoval"), Times.Never);

        _mockLog.Verify(
            x => x.LogInformation(It.Is<string>(s => s.Contains("Updating"))),
            Times.Exactly(3));
    }

    [Fact]
    public async Task CheckAndUpdateScriptsAsync_ScriptFileDoesNotExist_SkipsIt()
    {
        // Arrange - no script files exist at all
        _mockFileSystem
            .Setup(x => x.FileExists(It.IsAny<string>()))
            .Returns(false);

        // Act
        await _service.CheckAndUpdateScriptsAsync();

        // Assert - nothing should be written or run
        _mockFileSystem.Verify(
            x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockFileSystem.Verify(
            x => x.ReadAllText(It.IsAny<string>()), Times.Never);
        _mockScheduledTask.Verify(
            x => x.RunScheduledTaskAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CheckAndUpdateScriptsAsync_VersionExtractionFails_TreatsAsOutdated()
    {
        // Arrange - EdgeRemoval exists but ReadAllText throws on version extraction
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("EdgeRemoval")))
            .Returns(true);

        // First call for version extraction throws; setup will be used for both calls
        // The service calls ReadAllText twice: once in ExtractVersionFromFile, once would fail
        // But since the version regex won't match on the actual content, we return content without a version
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("EdgeRemoval")))
            .Returns("Script with no version line");

        // Other scripts don't exist
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("OneDriveRemoval")))
            .Returns(false);
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("BloatRemoval")))
            .Returns(false);

        _mockScheduledTask
            .Setup(x => x.RunScheduledTaskAsync(It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _service.CheckAndUpdateScriptsAsync();

        // Assert - null version != current version, so it should update
        _mockFileSystem.Verify(
            x => x.WriteAllText(ScriptPath("EdgeRemoval"), It.IsAny<string>()), Times.Once);
        _mockLog.Verify(
            x => x.LogInformation(It.Is<string>(s => s.Contains("Updating") && s.Contains("unknown"))),
            Times.Once);
    }

    [Fact]
    public async Task CheckAndUpdateScriptsAsync_UpdateThrowsException_LogsError()
    {
        // Arrange - EdgeRemoval exists with old version but write fails
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("EdgeRemoval")))
            .Returns(true);
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("EdgeRemoval")))
            .Returns(MakeScriptContent("0.1"));
        _mockFileSystem
            .Setup(x => x.WriteAllText(ScriptPath("EdgeRemoval"), It.IsAny<string>()))
            .Throws(new IOException("Disk full"));

        // Other scripts don't exist
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("OneDriveRemoval")))
            .Returns(false);
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("BloatRemoval")))
            .Returns(false);

        // Act
        await _service.CheckAndUpdateScriptsAsync();

        // Assert
        _mockLog.Verify(
            x => x.LogError(It.Is<string>(s => s.Contains("Failed to update EdgeRemoval"))),
            Times.Once);
    }

    [Fact]
    public async Task CheckAndUpdateScriptsAsync_OnlyOneScriptOutdated_UpdatesOnlyThatOne()
    {
        // Arrange - EdgeRemoval is up to date, OneDriveRemoval is outdated, BloatRemoval doesn't exist
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("EdgeRemoval")))
            .Returns(true);
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("EdgeRemoval")))
            .Returns(MakeScriptContent(EdgeRemovalScript.ScriptVersion));

        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("OneDriveRemoval")))
            .Returns(true);
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("OneDriveRemoval")))
            .Returns(MakeScriptContent("0.5"));

        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("BloatRemoval")))
            .Returns(false);

        _mockScheduledTask
            .Setup(x => x.RunScheduledTaskAsync(It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _service.CheckAndUpdateScriptsAsync();

        // Assert
        _mockFileSystem.Verify(
            x => x.WriteAllText(ScriptPath("EdgeRemoval"), It.IsAny<string>()), Times.Never);
        _mockFileSystem.Verify(
            x => x.WriteAllText(ScriptPath("OneDriveRemoval"), It.IsAny<string>()), Times.Once);
        _mockScheduledTask.Verify(
            x => x.RunScheduledTaskAsync("OneDriveRemoval"), Times.Once);
    }
}
