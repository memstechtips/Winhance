using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ScheduledTaskServiceTests
{
    private static readonly string[] AnyTaskPath = [@"\Any\Task\Path"];

    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly ScheduledTaskService _service;

    public ScheduledTaskServiceTests()
    {
        _service = new ScheduledTaskService(_mockLog.Object, _mockFileSystem.Object);
    }

    // --- RegisterScheduledTaskAsync ---

    [Fact]
    public async Task RegisterScheduledTaskAsync_NullScript_ReturnsFailure()
    {
        var result = await _service.RegisterScheduledTaskAsync(null!);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegisterScheduledTaskAsync_NullScriptPath_ReturnsFailure()
    {
        var script = new RemovalScript
        {
            Name = "TestTask",
            Content = "# script content",
            TargetScheduledTaskName = "TestTask",
            ActualScriptPath = null,
        };

        var result = await _service.RegisterScheduledTaskAsync(script);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Script or script path is null");
    }

    [Fact]
    public async Task RegisterScheduledTaskAsync_EnsuresScriptFileExists_WhenMissing()
    {
        var script = new RemovalScript
        {
            Name = "TestTask",
            Content = "# test content",
            TargetScheduledTaskName = "TestTask",
            ActualScriptPath = @"C:\ProgramData\Winhance\Scripts\TestTask.ps1",
        };

        _mockFileSystem.Setup(f => f.FileExists(script.ActualScriptPath)).Returns(false);
        _mockFileSystem.Setup(f => f.GetDirectoryName(script.ActualScriptPath))
            .Returns(@"C:\ProgramData\Winhance\Scripts");
        _mockFileSystem.Setup(f => f.DirectoryExists(@"C:\ProgramData\Winhance\Scripts"))
            .Returns(false);

        // RegisterScheduledTaskAsync will try to create the COM task service, which will fail
        // in a test environment, but we can verify the pre-COM setup logic works
        var result = await _service.RegisterScheduledTaskAsync(script);

        // The method will fail at COM interop (CreateTaskService), but we can verify
        // EnsureScriptFileExists was called
        _mockFileSystem.Verify(f => f.CreateDirectory(@"C:\ProgramData\Winhance\Scripts"), Times.Once);
        _mockFileSystem.Verify(f => f.WriteAllText(script.ActualScriptPath, script.Content), Times.Once);
    }

    [Fact]
    public async Task RegisterScheduledTaskAsync_DoesNotRewriteScript_WhenFileAlreadyExists()
    {
        var script = new RemovalScript
        {
            Name = "TestTask",
            Content = "# test content",
            TargetScheduledTaskName = "TestTask",
            ActualScriptPath = @"C:\ProgramData\Winhance\Scripts\TestTask.ps1",
        };

        _mockFileSystem.Setup(f => f.FileExists(script.ActualScriptPath)).Returns(true);

        // Will fail at COM interop but we can verify the file is not rewritten
        var result = await _service.RegisterScheduledTaskAsync(script);

        _mockFileSystem.Verify(f => f.WriteAllText(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterScheduledTaskAsync_DoesNotWriteFile_WhenContentIsEmpty()
    {
        var script = new RemovalScript
        {
            Name = "TestTask",
            Content = "",
            TargetScheduledTaskName = "TestTask",
            ActualScriptPath = @"C:\ProgramData\Winhance\Scripts\TestTask.ps1",
        };

        _mockFileSystem.Setup(f => f.FileExists(script.ActualScriptPath)).Returns(false);

        // EnsureScriptFileExists has guard: !string.IsNullOrEmpty(script.Content)
        var result = await _service.RegisterScheduledTaskAsync(script);

        _mockFileSystem.Verify(f => f.WriteAllText(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // Skipped: environment-dependent. The service talks to the real Task Scheduler
    // COM API ("Schedule.Service") with no mockable seam. The test assumes COM is
    // unavailable in the test host, but on a real Windows machine the registration
    // succeeds (Success == true) and creates an actual scheduled task as a side effect.
    // Re-enable once ScheduledTaskService exposes an injectable COM wrapper to mock.
    [Fact(Skip = "Environment-dependent: hits real Task Scheduler COM (no mockable seam) and has side effects; needs an injectable COM wrapper to run deterministically.")]
    public async Task RegisterScheduledTaskAsync_ComFailure_ReturnsFailedResult()
    {
        var script = new RemovalScript
        {
            Name = "TestTask",
            Content = "# content",
            TargetScheduledTaskName = "TestTask",
            ActualScriptPath = @"C:\ProgramData\Winhance\Scripts\TestTask.ps1",
        };

        _mockFileSystem.Setup(f => f.FileExists(script.ActualScriptPath)).Returns(true);

        // In a test environment, COM will fail. The method should handle this gracefully.
        var result = await _service.RegisterScheduledTaskAsync(script);

        // Should return failed (COM not available in test environment)
        result.Success.Should().BeFalse();
    }

    // --- UnregisterScheduledTaskAsync ---

    [Fact]
    public async Task UnregisterScheduledTaskAsync_ComFailure_ReturnsResult()
    {
        // In a unit test environment, COM interop calls will fail.
        // The method wraps everything in try/catch so it should not throw.
        var result = await _service.UnregisterScheduledTaskAsync("SomeTask");

        // Will either succeed (Winhance folder not found => returns Succeeded)
        // or fail (COM connection error), but should not throw
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task UnregisterScheduledTaskAsync_ReturnsResult_WithoutThrowing()
    {
        // Verify that the method is robust against all types of failures
        var action = () => _service.UnregisterScheduledTaskAsync("NonExistentTask");

        await action.Should().NotThrowAsync();
    }

    // --- IsTaskRegisteredAsync ---

    [Fact]
    public async Task IsTaskRegisteredAsync_ComFailure_ReturnsFalse()
    {
        // In a test environment, COM fails. The catch block returns false.
        var result = await _service.IsTaskRegisteredAsync("SomeTask");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTaskRegisteredAsync_DoesNotThrow_ForAnyInput()
    {
        var action = () => _service.IsTaskRegisteredAsync("AnyTaskName");

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IsTaskRegisteredAsync_EmptyTaskName_ReturnsFalse()
    {
        var result = await _service.IsTaskRegisteredAsync("");

        result.Should().BeFalse();
    }

    // --- SetTaskEnabled (synchronous: the Task Scheduler COM call blocks) ---

    [Fact]
    public void SetTaskEnabled_Enable_ComFailure_ReturnsFailedResult()
    {
        // The task does not exist on the test machine, so the COM lookup fails.
        var result = _service.SetTaskEnabled(@"\Microsoft\Windows\Test\SomeTask", enabled: true);

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void SetTaskEnabled_Enable_DoesNotThrow()
    {
        var action = () => _service.SetTaskEnabled(@"\Test\Task", enabled: true);

        action.Should().NotThrow();
    }

    [Fact]
    public void SetTaskEnabled_Disable_ComFailure_ReturnsFailedResult()
    {
        var result = _service.SetTaskEnabled(@"\Microsoft\Windows\Test\SomeTask", enabled: false);

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void SetTaskEnabled_Disable_DoesNotThrow()
    {
        var action = () => _service.SetTaskEnabled(@"\Test\Task", enabled: false);

        action.Should().NotThrow();
    }

    // --- GetTasksEnabled (batched over one connection) ---

    [Fact]
    public void GetTasksEnabled_NoPaths_ReturnsEmptyWithoutConnecting()
    {
        var result = _service.GetTasksEnabled(Array.Empty<string>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetTasksEnabled_ReturnsAnEntryForEveryRequestedPath()
    {
        // The contract the detection context relies on: every requested path is present in the result,
        // absent tasks as null. A missing KEY would be read as "never asked" and silently re-queried.
        var paths = new[] { @"\Microsoft\Windows\Test\One", @"\Microsoft\Windows\Test\Two" };

        var result = _service.GetTasksEnabled(paths);

        result.Should().HaveCount(2);
        result.Keys.Should().BeEquivalentTo(paths);
        result.Values.Should().AllSatisfy(v => v.Should().BeNull());
    }

    [Fact]
    public void GetTasksEnabled_DoesNotThrow()
    {
        var action = () => _service.GetTasksEnabled(AnyTaskPath);

        action.Should().NotThrow();
    }

    // --- RunScheduledTaskAsync ---

    [Fact]
    public async Task RunScheduledTaskAsync_ComFailure_ReturnsFailedResult()
    {
        var result = await _service.RunScheduledTaskAsync("SomeTask");

        result.Should().NotBeNull();
        // Will fail due to COM not being available
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RunScheduledTaskAsync_DoesNotThrow()
    {
        var action = () => _service.RunScheduledTaskAsync("AnyTask");

        await action.Should().NotThrowAsync();
    }

    // --- SplitTaskPath (tested indirectly via public methods) ---
    // SplitTaskPath is private static, but its logic is exercised through SetTaskEnabled/GetTasksEnabled.

    [Fact]
    public void SetTaskEnabled_WithFullPath_ParsesFolderAndName()
    {
        // The path "\Microsoft\Windows\Test\TaskName" should split to folder="\Microsoft\Windows\Test" name="TaskName"
        // COM will fail in test env, but we verify no exception
        var result = _service.SetTaskEnabled(@"\Microsoft\Windows\Test\TaskName", enabled: true);

        result.Should().NotBeNull();
    }

    [Fact]
    public void SetTaskEnabled_WithRootPath_ParsesCorrectly()
    {
        // The path "\TaskName" should split to folder="\" name="TaskName"
        var result = _service.SetTaskEnabled(@"\TaskName", enabled: true);

        result.Should().NotBeNull();
    }

    [Fact]
    public void SetTaskEnabled_WithBareTaskName_ParsesCorrectly()
    {
        // A bare name "TaskName" (lastSep <= 0) should split to folder="\" name="TaskName"
        var result = _service.SetTaskEnabled("TaskName", enabled: true);

        result.Should().NotBeNull();
    }
}
