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

        await _service.CheckAndUpdateScriptsAsync();

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
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("EdgeRemoval")))
            .Returns(true);
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("EdgeRemoval")))
            .Returns(MakeScriptContent("0.1"));

        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("OneDriveRemoval")))
            .Returns(true);
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("OneDriveRemoval")))
            .Returns(MakeScriptContent("0.1"));

        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("BloatRemoval")))
            .Returns(true);
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("BloatRemoval")))
            .Returns(MakeScriptContent("0.1"));

        _mockScheduledTask
            .Setup(x => x.RunScheduledTaskAsync(It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());

        await _service.CheckAndUpdateScriptsAsync();

        // EdgeRemoval uses GetContent (full replacement), runs after update
        _mockFileSystem.Verify(
            x => x.WriteAllText(ScriptPath("EdgeRemoval"), It.IsAny<string>()), Times.Once);
        _mockScheduledTask.Verify(
            x => x.RunScheduledTaskAsync("EdgeRemoval"), Times.Once);

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
        _mockFileSystem
            .Setup(x => x.FileExists(It.IsAny<string>()))
            .Returns(false);

        await _service.CheckAndUpdateScriptsAsync();

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
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("EdgeRemoval")))
            .Returns(true);

        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("EdgeRemoval")))
            .Returns("Script with no version line");

        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("OneDriveRemoval")))
            .Returns(false);
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("BloatRemoval")))
            .Returns(false);

        _mockScheduledTask
            .Setup(x => x.RunScheduledTaskAsync(It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());

        await _service.CheckAndUpdateScriptsAsync();

        _mockFileSystem.Verify(
            x => x.WriteAllText(ScriptPath("EdgeRemoval"), It.IsAny<string>()), Times.Once);
        _mockLog.Verify(
            x => x.LogInformation(It.Is<string>(s => s.Contains("Updating") && s.Contains("unknown"))),
            Times.Once);
    }

    [Fact]
    public async Task CheckAndUpdateScriptsAsync_UpdateThrowsException_LogsError()
    {
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("EdgeRemoval")))
            .Returns(true);
        _mockFileSystem
            .Setup(x => x.ReadAllText(ScriptPath("EdgeRemoval")))
            .Returns(MakeScriptContent("0.1"));
        _mockFileSystem
            .Setup(x => x.WriteAllText(ScriptPath("EdgeRemoval"), It.IsAny<string>()))
            .Throws(new IOException("Disk full"));

        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("OneDriveRemoval")))
            .Returns(false);
        _mockFileSystem
            .Setup(x => x.FileExists(ScriptPath("BloatRemoval")))
            .Returns(false);

        await _service.CheckAndUpdateScriptsAsync();

        _mockLog.Verify(
            x => x.LogError(It.Is<string>(s => s.Contains("Failed to update EdgeRemoval"))),
            Times.Once);
    }

    [Fact]
    public async Task CheckAndUpdateScriptsAsync_OnlyOneScriptOutdated_UpdatesOnlyThatOne()
    {
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

        await _service.CheckAndUpdateScriptsAsync();

        _mockFileSystem.Verify(
            x => x.WriteAllText(ScriptPath("EdgeRemoval"), It.IsAny<string>()), Times.Never);
        _mockFileSystem.Verify(
            x => x.WriteAllText(ScriptPath("OneDriveRemoval"), It.IsAny<string>()), Times.Once);
        _mockScheduledTask.Verify(
            x => x.RunScheduledTaskAsync("OneDriveRemoval"), Times.Once);
    }
}
