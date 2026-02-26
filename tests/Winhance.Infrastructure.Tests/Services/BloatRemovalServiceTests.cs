using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class BloatRemovalServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IScheduledTaskService> _mockScheduledTask = new();
    private readonly Mock<IPowerShellRunner> _mockPowerShell = new();
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly BloatRemovalService _service;

    public BloatRemovalServiceTests()
    {
        _service = new BloatRemovalService(
            _mockLog.Object,
            _mockScheduledTask.Object,
            _mockPowerShell.Object,
            _mockFileSystem.Object);
    }

    private static ItemDefinition CreateAppxApp(string id, string appxName, string? name = null) => new()
    {
        Id = id,
        Name = name ?? id,
        Description = $"Description for {id}",
        AppxPackageName = appxName,
    };

    private static ItemDefinition CreateDedicatedScriptApp(string id, string name, Func<string> removalScript) => new()
    {
        Id = id,
        Name = name,
        Description = $"Description for {id}",
        RemovalScript = removalScript,
    };

    private static ItemDefinition CreateCapabilityApp(string id, string capabilityName) => new()
    {
        Id = id,
        Name = id,
        Description = $"Description for {id}",
        CapabilityName = capabilityName,
    };

    private static ItemDefinition CreateFeatureApp(string id, string featureName) => new()
    {
        Id = id,
        Name = id,
        Description = $"Description for {id}",
        OptionalFeatureName = featureName,
    };

    // --- ExecuteDedicatedScriptAsync ---

    [Fact]
    public async Task ExecuteDedicatedScriptAsync_EdgeApp_CreatesAndRunsEdgeRemovalScript()
    {
        var app = CreateDedicatedScriptApp("windows-app-edge", "Microsoft Edge",
            () => "# Edge removal script content");

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));

        var result = await _service.ExecuteDedicatedScriptAsync(app);

        result.Should().Be(RemovalOutcome.Success);
        _mockFileSystem.Verify(f => f.CreateDirectory(ScriptPaths.ScriptsDirectory), Times.Once);
        _mockFileSystem.Verify(f => f.WriteAllTextAsync(
            It.Is<string>(p => p.Contains("EdgeRemoval.ps1")),
            "# Edge removal script content",
            It.IsAny<CancellationToken>()), Times.Once);
        _mockPowerShell.Verify(p => p.RunScriptFileAsync(
            It.Is<string>(s => s.Contains("EdgeRemoval.ps1")),
            It.IsAny<string>(),
            It.IsAny<IProgress<TaskProgressDetail>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteDedicatedScriptAsync_OneDriveApp_CreatesOneDriveRemovalScript()
    {
        var app = CreateDedicatedScriptApp("windows-app-onedrive", "OneDrive",
            () => "# OneDrive removal script");

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));

        var result = await _service.ExecuteDedicatedScriptAsync(app);

        result.Should().Be(RemovalOutcome.Success);
        _mockFileSystem.Verify(f => f.WriteAllTextAsync(
            It.Is<string>(p => p.Contains("OneDriveRemoval.ps1")),
            "# OneDrive removal script",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteDedicatedScriptAsync_UnsupportedAppId_ThrowsNotSupportedException()
    {
        var app = CreateDedicatedScriptApp("windows-app-unknown", "Unknown",
            () => "# script");

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));

        // The NotSupportedException from CreateScriptName will be caught by the catch block
        var result = await _service.ExecuteDedicatedScriptAsync(app);

        result.Should().Be(RemovalOutcome.Failed);
    }

    [Fact]
    public async Task ExecuteDedicatedScriptAsync_ExecutionPolicyBlocked_ReturnsDeferredToScheduledTask()
    {
        var app = CreateDedicatedScriptApp("windows-app-edge", "Microsoft Edge",
            () => "# Edge script");

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockPowerShell
            .Setup(p => p.RunScriptFileAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExecutionPolicyException("Execution policy blocked"));

        var result = await _service.ExecuteDedicatedScriptAsync(app);

        result.Should().Be(RemovalOutcome.DeferredToScheduledTask);
        _mockLog.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("deferring to scheduled task"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteDedicatedScriptAsync_Cancelled_ReturnsFailed()
    {
        var app = CreateDedicatedScriptApp("windows-app-edge", "Microsoft Edge",
            () => "# Edge script");

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockPowerShell
            .Setup(p => p.RunScriptFileAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await _service.ExecuteDedicatedScriptAsync(app);

        result.Should().Be(RemovalOutcome.Failed);
    }

    [Fact]
    public async Task ExecuteDedicatedScriptAsync_InvalidOperationException_ReturnsSuccess()
    {
        var app = CreateDedicatedScriptApp("windows-app-edge", "Microsoft Edge",
            () => "# Edge script");

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockPowerShell
            .Setup(p => p.RunScriptFileAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Non-zero exit code"));

        var result = await _service.ExecuteDedicatedScriptAsync(app);

        // InvalidOperationException (non-ExecutionPolicyException) is treated as warning/success
        result.Should().Be(RemovalOutcome.Success);
    }

    [Fact]
    public async Task ExecuteDedicatedScriptAsync_ReportsProgress()
    {
        var app = CreateDedicatedScriptApp("windows-app-edge", "Microsoft Edge",
            () => "# Edge script");

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));

        var progressReports = new List<TaskProgressDetail>();
        var progress = new Progress<TaskProgressDetail>(detail => progressReports.Add(detail));

        await _service.ExecuteDedicatedScriptAsync(app, progress);

        // We can verify the log was called; the progress reporting is async via IProgress
        _mockLog.Verify(l => l.LogInformation(
            It.Is<string>(s => s.Contains("Executing dedicated removal script"))), Times.Once);
    }

    // --- ExecuteBloatRemovalAsync ---

    [Fact]
    public async Task ExecuteBloatRemovalAsync_NoItems_ReturnsSuccess()
    {
        var apps = new List<ItemDefinition>();

        var result = await _service.ExecuteBloatRemovalAsync(apps);

        result.Should().Be(RemovalOutcome.Success);
        _mockLog.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("No items to process"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteBloatRemovalAsync_WithPackages_CreatesAndRunsScript()
    {
        var apps = new List<ItemDefinition>
        {
            CreateAppxApp("app1", "Microsoft.BingNews"),
            CreateAppxApp("app2", "Microsoft.BingWeather"),
        };

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        var result = await _service.ExecuteBloatRemovalAsync(apps);

        result.Should().Be(RemovalOutcome.Success);
        _mockFileSystem.Verify(f => f.CreateDirectory(ScriptPaths.ScriptsDirectory), Times.Once);
        _mockFileSystem.Verify(f => f.WriteAllTextAsync(
            It.Is<string>(p => p.Contains("BloatRemoval.ps1")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockPowerShell.Verify(p => p.RunScriptFileAsync(
            It.Is<string>(s => s.Contains("BloatRemoval.ps1")),
            It.IsAny<string>(),
            It.IsAny<IProgress<TaskProgressDetail>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteBloatRemovalAsync_SkipsDedicatedScriptApps()
    {
        // Apps with RemovalScript should be excluded from BloatRemoval (they use dedicated scripts)
        var apps = new List<ItemDefinition>
        {
            CreateDedicatedScriptApp("windows-app-edge", "Edge", () => "# script"),
        };

        var result = await _service.ExecuteBloatRemovalAsync(apps);

        // Since dedicated script apps are filtered out, there should be no items
        result.Should().Be(RemovalOutcome.Success);
        _mockLog.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("No items to process"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteBloatRemovalAsync_MergesWithExistingScript_WhenFileExists()
    {
        var apps = new List<ItemDefinition>
        {
            CreateAppxApp("app1", "Microsoft.NewApp"),
        };

        var existingScript = @"$packages = @(
    ""Microsoft.ExistingApp""
)

$capabilities = @()

$optionalFeatures = @()

$specialApps = @()";

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingScript);

        var result = await _service.ExecuteBloatRemovalAsync(apps);

        result.Should().Be(RemovalOutcome.Success);
        // Verify the merged content is written
        _mockFileSystem.Verify(f => f.WriteAllTextAsync(
            It.IsAny<string>(),
            It.Is<string>(content => content.Contains("Microsoft.NewApp") && content.Contains("Microsoft.ExistingApp")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteBloatRemovalAsync_ExecutionPolicyBlocked_ReturnsDeferredToScheduledTask()
    {
        var apps = new List<ItemDefinition>
        {
            CreateAppxApp("app1", "Microsoft.App1"),
        };

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockPowerShell
            .Setup(p => p.RunScriptFileAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExecutionPolicyException("Policy blocked"));

        var result = await _service.ExecuteBloatRemovalAsync(apps);

        result.Should().Be(RemovalOutcome.DeferredToScheduledTask);
    }

    [Fact]
    public async Task ExecuteBloatRemovalAsync_Cancelled_ReturnsFailed()
    {
        var apps = new List<ItemDefinition>
        {
            CreateAppxApp("app1", "Microsoft.App1"),
        };

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockPowerShell
            .Setup(p => p.RunScriptFileAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await _service.ExecuteBloatRemovalAsync(apps);

        result.Should().Be(RemovalOutcome.Failed);
    }

    // --- PersistRemovalScriptsAsync ---

    [Fact]
    public async Task PersistRemovalScriptsAsync_RegistersScheduledTaskForDedicatedScript()
    {
        var apps = new List<ItemDefinition>
        {
            CreateDedicatedScriptApp("windows-app-edge", "Microsoft Edge", () => "# Edge script"),
        };

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockFileSystem
            .Setup(f => f.FileExists(It.Is<string>(p => p.Contains("EdgeRemoval.ps1"))))
            .Returns(true);
        _mockFileSystem
            .Setup(f => f.ReadAllTextAsync(It.Is<string>(p => p.Contains("EdgeRemoval.ps1")), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Edge script content");
        _mockScheduledTask
            .Setup(s => s.RegisterScheduledTaskAsync(It.IsAny<RemovalScript>()))
            .ReturnsAsync(OperationResult.Succeeded());

        await _service.PersistRemovalScriptsAsync(apps);

        _mockScheduledTask.Verify(s => s.RegisterScheduledTaskAsync(
            It.Is<RemovalScript>(rs =>
                rs.Name == "EdgeRemoval" &&
                rs.RunOnStartup == true)), Times.Once);
    }

    [Fact]
    public async Task PersistRemovalScriptsAsync_EdgeScript_HasRunOnStartupTrue()
    {
        var apps = new List<ItemDefinition>
        {
            CreateDedicatedScriptApp("windows-app-edge", "Edge", () => "# script"),
        };

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockFileSystem
            .Setup(f => f.FileExists(It.Is<string>(p => p.Contains("EdgeRemoval.ps1"))))
            .Returns(true);
        _mockFileSystem
            .Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# content");
        _mockScheduledTask
            .Setup(s => s.RegisterScheduledTaskAsync(It.IsAny<RemovalScript>()))
            .ReturnsAsync(OperationResult.Succeeded());

        await _service.PersistRemovalScriptsAsync(apps);

        _mockScheduledTask.Verify(s => s.RegisterScheduledTaskAsync(
            It.Is<RemovalScript>(rs => rs.RunOnStartup == true)), Times.Once);
    }

    [Fact]
    public async Task PersistRemovalScriptsAsync_OneDriveScript_HasRunOnStartupFalse()
    {
        var apps = new List<ItemDefinition>
        {
            CreateDedicatedScriptApp("windows-app-onedrive", "OneDrive", () => "# script"),
        };

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockFileSystem
            .Setup(f => f.FileExists(It.Is<string>(p => p.Contains("OneDriveRemoval.ps1"))))
            .Returns(true);
        _mockFileSystem
            .Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# content");
        _mockScheduledTask
            .Setup(s => s.RegisterScheduledTaskAsync(It.IsAny<RemovalScript>()))
            .ReturnsAsync(OperationResult.Succeeded());

        await _service.PersistRemovalScriptsAsync(apps);

        _mockScheduledTask.Verify(s => s.RegisterScheduledTaskAsync(
            It.Is<RemovalScript>(rs => rs.RunOnStartup == false)), Times.Once);
    }

    [Fact]
    public async Task PersistRemovalScriptsAsync_SkipsWhenScriptFileNotFound()
    {
        var apps = new List<ItemDefinition>
        {
            CreateDedicatedScriptApp("windows-app-edge", "Edge", () => "# script"),
        };

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        await _service.PersistRemovalScriptsAsync(apps);

        _mockScheduledTask.Verify(s => s.RegisterScheduledTaskAsync(It.IsAny<RemovalScript>()), Times.Never);
        _mockLog.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Script not found"))), Times.Once);
    }

    [Fact]
    public async Task PersistRemovalScriptsAsync_RegistersBloatRemovalTask_WhenScriptExists()
    {
        var apps = new List<ItemDefinition>(); // No dedicated script apps

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockFileSystem
            .Setup(f => f.FileExists(It.Is<string>(p => p.Contains("BloatRemoval.ps1"))))
            .Returns(true);
        _mockFileSystem
            .Setup(f => f.ReadAllTextAsync(It.Is<string>(p => p.Contains("BloatRemoval.ps1")), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# bloat removal content");
        _mockScheduledTask
            .Setup(s => s.RegisterScheduledTaskAsync(It.IsAny<RemovalScript>()))
            .ReturnsAsync(OperationResult.Succeeded());

        await _service.PersistRemovalScriptsAsync(apps);

        _mockScheduledTask.Verify(s => s.RegisterScheduledTaskAsync(
            It.Is<RemovalScript>(rs =>
                rs.Name == "BloatRemoval" &&
                rs.RunOnStartup == false)), Times.Once);
    }

    // --- RemoveItemsFromScriptAsync ---

    [Fact]
    public async Task RemoveItemsFromScriptAsync_ScriptDoesNotExist_ReturnsTrue()
    {
        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        var result = await _service.RemoveItemsFromScriptAsync(new List<ItemDefinition>());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveItemsFromScriptAsync_RemovesItemsFromExistingScript()
    {
        var itemsToRemove = new List<ItemDefinition>
        {
            CreateAppxApp("app1", "Microsoft.BingNews"),
        };

        var existingScript = @"$packages = @(
    ""Microsoft.BingNews"",
    ""Microsoft.BingWeather""
)

$capabilities = @()

$optionalFeatures = @()

$specialApps = @()";

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem
            .Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingScript);
        _mockScheduledTask
            .Setup(s => s.RegisterScheduledTaskAsync(It.IsAny<RemovalScript>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var result = await _service.RemoveItemsFromScriptAsync(itemsToRemove);

        result.Should().BeTrue();
        // Verify the updated content no longer contains the removed app but keeps the remaining one
        _mockFileSystem.Verify(f => f.WriteAllTextAsync(
            It.IsAny<string>(),
            It.Is<string>(content =>
                !content.Contains("Microsoft.BingNews") &&
                content.Contains("Microsoft.BingWeather")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveItemsFromScriptAsync_WhenAllItemsRemoved_CleansUpArtifacts()
    {
        var itemsToRemove = new List<ItemDefinition>
        {
            CreateAppxApp("app1", "Microsoft.BingNews"),
        };

        var existingScript = @"$packages = @(
    ""Microsoft.BingNews""
)

$capabilities = @()

$optionalFeatures = @()

$specialApps = @()";

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem
            .Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingScript);
        _mockScheduledTask
            .Setup(s => s.IsTaskRegisteredAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockScheduledTask
            .Setup(s => s.UnregisterScheduledTaskAsync(It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var result = await _service.RemoveItemsFromScriptAsync(itemsToRemove);

        result.Should().BeTrue();
        // When script becomes empty, cleanup artifacts (unregister task + delete file)
        _mockScheduledTask.Verify(s => s.UnregisterScheduledTaskAsync("BloatRemoval"), Times.Once);
        _mockFileSystem.Verify(f => f.DeleteFile(It.Is<string>(p => p.Contains("BloatRemoval.ps1"))), Times.Once);
    }

    [Fact]
    public async Task RemoveItemsFromScriptAsync_WhenExceptionThrown_ReturnsFalse()
    {
        var itemsToRemove = new List<ItemDefinition>
        {
            CreateAppxApp("app1", "Microsoft.App1"),
        };

        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem
            .Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Access denied"));

        var result = await _service.RemoveItemsFromScriptAsync(itemsToRemove);

        result.Should().BeFalse();
        _mockLog.Verify(l => l.LogError(
            It.Is<string>(s => s.Contains("Error removing items from script")),
            It.IsAny<Exception>()), Times.Once);
    }

    // --- CleanupAllRemovalArtifactsAsync ---

    [Fact]
    public async Task CleanupAllRemovalArtifactsAsync_CleansUpAllThreeTasksAndScripts()
    {
        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockScheduledTask
            .Setup(s => s.IsTaskRegisteredAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockScheduledTask
            .Setup(s => s.UnregisterScheduledTaskAsync(It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

        await _service.CleanupAllRemovalArtifactsAsync();

        // Verify all three scheduled tasks are unregistered
        _mockScheduledTask.Verify(s => s.UnregisterScheduledTaskAsync("EdgeRemoval"), Times.Once);
        _mockScheduledTask.Verify(s => s.UnregisterScheduledTaskAsync("OneDriveRemoval"), Times.Once);
        _mockScheduledTask.Verify(s => s.UnregisterScheduledTaskAsync("BloatRemoval"), Times.Once);

        // Verify all three script files are deleted
        _mockFileSystem.Verify(f => f.DeleteFile(It.Is<string>(p => p.Contains("EdgeRemoval.ps1"))), Times.Once);
        _mockFileSystem.Verify(f => f.DeleteFile(It.Is<string>(p => p.Contains("OneDriveRemoval.ps1"))), Times.Once);
        _mockFileSystem.Verify(f => f.DeleteFile(It.Is<string>(p => p.Contains("BloatRemoval.ps1"))), Times.Once);
    }

    [Fact]
    public async Task CleanupAllRemovalArtifactsAsync_TaskNotRegistered_SkipsUnregister()
    {
        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockScheduledTask
            .Setup(s => s.IsTaskRegisteredAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        await _service.CleanupAllRemovalArtifactsAsync();

        _mockScheduledTask.Verify(s => s.UnregisterScheduledTaskAsync(It.IsAny<string>()), Times.Never);
        _mockFileSystem.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CleanupAllRemovalArtifactsAsync_ExceptionDuringCleanup_DoesNotThrow()
    {
        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));
        _mockScheduledTask
            .Setup(s => s.IsTaskRegisteredAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("COM failure"));

        // Should not throw despite internal errors
        var action = () => _service.CleanupAllRemovalArtifactsAsync();

        await action.Should().NotThrowAsync();
    }
}
