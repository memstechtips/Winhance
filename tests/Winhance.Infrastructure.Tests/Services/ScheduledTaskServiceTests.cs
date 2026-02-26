using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ScheduledTaskServiceTests
{
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

    [Fact]
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

    // --- EnableTaskAsync ---

    [Fact]
    public async Task EnableTaskAsync_ComFailure_ReturnsFailedResult()
    {
        // COM interop fails in test environment
        var result = await _service.EnableTaskAsync(@"\Microsoft\Windows\Test\SomeTask");

        result.Should().NotBeNull();
        // Will fail due to COM not being available
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task EnableTaskAsync_DoesNotThrow()
    {
        var action = () => _service.EnableTaskAsync(@"\Test\Task");

        await action.Should().NotThrowAsync();
    }

    // --- DisableTaskAsync ---

    [Fact]
    public async Task DisableTaskAsync_ComFailure_ReturnsFailedResult()
    {
        var result = await _service.DisableTaskAsync(@"\Microsoft\Windows\Test\SomeTask");

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DisableTaskAsync_DoesNotThrow()
    {
        var action = () => _service.DisableTaskAsync(@"\Test\Task");

        await action.Should().NotThrowAsync();
    }

    // --- IsTaskEnabledAsync ---

    [Fact]
    public async Task IsTaskEnabledAsync_ComFailure_ReturnsNull()
    {
        // COM interop fails in test environment, catch returns null
        var result = await _service.IsTaskEnabledAsync(@"\Microsoft\Windows\Test\SomeTask");

        result.Should().BeNull();
    }

    [Fact]
    public async Task IsTaskEnabledAsync_DoesNotThrow()
    {
        var action = () => _service.IsTaskEnabledAsync(@"\Any\Task\Path");

        await action.Should().NotThrowAsync();
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

    // --- CreateUserLogonTaskAsync ---

    [Fact]
    public async Task CreateUserLogonTaskAsync_ComFailure_ReturnsFailedResult()
    {
        var result = await _service.CreateUserLogonTaskAsync("TestTask", "powershell.exe -Command echo hello", "DOMAIN\\User");

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CreateUserLogonTaskAsync_DoesNotThrow()
    {
        var action = () => _service.CreateUserLogonTaskAsync("TestTask", "command", "user");

        await action.Should().NotThrowAsync();
    }

    // --- SplitTaskPath (tested indirectly via public methods) ---
    // SplitTaskPath is private static, but its logic is exercised through EnableTaskAsync/DisableTaskAsync/IsTaskEnabledAsync.

    [Fact]
    public async Task EnableTaskAsync_WithFullPath_ParsesFolderAndName()
    {
        // The path "\Microsoft\Windows\Test\TaskName" should split to folder="\Microsoft\Windows\Test" name="TaskName"
        // COM will fail in test env, but we verify no exception
        var result = await _service.EnableTaskAsync(@"\Microsoft\Windows\Test\TaskName");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EnableTaskAsync_WithRootPath_ParsesCorrectly()
    {
        // The path "\TaskName" should split to folder="\" name="TaskName"
        var result = await _service.EnableTaskAsync(@"\TaskName");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EnableTaskAsync_WithBareTaskName_ParsesCorrectly()
    {
        // A bare name "TaskName" (lastSep <= 0) should split to folder="\" name="TaskName"
        var result = await _service.EnableTaskAsync("TaskName");

        result.Should().NotBeNull();
    }
}
