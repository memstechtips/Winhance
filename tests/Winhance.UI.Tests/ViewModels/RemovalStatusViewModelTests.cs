using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class RemovalStatusViewModelTests
{
    private readonly Mock<IScheduledTaskService> _scheduledTaskService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();

    private RemovalStatusViewModel CreateSut(
        string name = "Bloat Removal",
        string iconPath = "DeleteSweepIconPath",
        string activeColor = "#00FF3C",
        string scriptFileName = "BloatRemoval.ps1",
        string scheduledTaskName = "BloatRemoval") => new(
        name, iconPath, activeColor, scriptFileName, scheduledTaskName,
        _scheduledTaskService.Object,
        _logService.Object,
        _fileSystemService.Object);

    // --- Constructor ---

    [Fact]
    public void Constructor_SetsProperties()
    {
        var sut = CreateSut("TestApp", "TestIcon", "#FF0000", "Test.ps1", "TestTask");

        sut.Name.Should().Be("TestApp");
        sut.IconPath.Should().Be("TestIcon");
        sut.ActiveColor.Should().Be("#FF0000");
        sut.ScriptFileName.Should().Be("Test.ps1");
        sut.ScheduledTaskName.Should().Be("TestTask");
    }

    [Fact]
    public void Constructor_DefaultsIsActiveToFalse()
    {
        var sut = CreateSut();

        sut.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Constructor_DefaultsIsLoadingToFalse()
    {
        var sut = CreateSut();

        sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InitializesRemoveCommand()
    {
        var sut = CreateSut();

        sut.RemoveCommand.Should().NotBeNull();
    }

    // --- StartStatusMonitoringAsync ---

    [Fact]
    public async Task StartStatusMonitoringAsync_WhenScriptExists_SetsIsActiveTrue()
    {
        _fileSystemService.Setup(f => f.CombinePath(It.IsAny<string>(), "BloatRemoval.ps1"))
            .Returns(@"C:\Scripts\BloatRemoval.ps1");
        _fileSystemService.Setup(f => f.FileExists(@"C:\Scripts\BloatRemoval.ps1"))
            .Returns(true);
        _scheduledTaskService.Setup(s => s.IsTaskRegisteredAsync("BloatRemoval"))
            .ReturnsAsync(false);

        var sut = CreateSut();
        await sut.StartStatusMonitoringAsync();

        sut.IsActive.Should().BeTrue();
        sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task StartStatusMonitoringAsync_WhenScheduledTaskRegistered_SetsIsActiveTrue()
    {
        _fileSystemService.Setup(f => f.CombinePath(It.IsAny<string>(), "BloatRemoval.ps1"))
            .Returns(@"C:\Scripts\BloatRemoval.ps1");
        _fileSystemService.Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(false);
        _scheduledTaskService.Setup(s => s.IsTaskRegisteredAsync("BloatRemoval"))
            .ReturnsAsync(true);

        var sut = CreateSut();
        await sut.StartStatusMonitoringAsync();

        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task StartStatusMonitoringAsync_WhenNeitherExists_SetsIsActiveFalse()
    {
        _fileSystemService.Setup(f => f.CombinePath(It.IsAny<string>(), "BloatRemoval.ps1"))
            .Returns(@"C:\Scripts\BloatRemoval.ps1");
        _fileSystemService.Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(false);
        _scheduledTaskService.Setup(s => s.IsTaskRegisteredAsync("BloatRemoval"))
            .ReturnsAsync(false);

        var sut = CreateSut();
        await sut.StartStatusMonitoringAsync();

        sut.IsActive.Should().BeFalse();
        sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task StartStatusMonitoringAsync_OnException_SetsIsActiveFalse()
    {
        _fileSystemService.Setup(f => f.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("test"));

        var sut = CreateSut();
        await sut.StartStatusMonitoringAsync();

        sut.IsActive.Should().BeFalse();
    }

    // --- RemoveCommand ---

    [Fact]
    public async Task RemoveCommand_WhenTaskRegistered_UnregistersTask()
    {
        _scheduledTaskService.Setup(s => s.IsTaskRegisteredAsync("BloatRemoval"))
            .ReturnsAsync(true);
        _scheduledTaskService.Setup(s => s.UnregisterScheduledTaskAsync("BloatRemoval"))
            .ReturnsAsync(Winhance.Core.Features.Common.Models.OperationResult.Succeeded());
        _fileSystemService.Setup(f => f.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(@"C:\Scripts\BloatRemoval.ps1");
        _fileSystemService.Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(false);

        var sut = CreateSut();
        sut.RemoveCommand.Execute(null);

        // Wait a bit for async command execution
        await Task.Delay(100);

        _scheduledTaskService.Verify(s => s.UnregisterScheduledTaskAsync("BloatRemoval"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RemoveCommand_WhenScriptExists_DeletesScript()
    {
        _scheduledTaskService.Setup(s => s.IsTaskRegisteredAsync("BloatRemoval"))
            .ReturnsAsync(false);
        _fileSystemService.Setup(f => f.CombinePath(It.IsAny<string>(), "BloatRemoval.ps1"))
            .Returns(@"C:\Scripts\BloatRemoval.ps1");
        _fileSystemService.Setup(f => f.FileExists(@"C:\Scripts\BloatRemoval.ps1"))
            .Returns(true);

        var sut = CreateSut();
        sut.RemoveCommand.Execute(null);

        await Task.Delay(100);

        _fileSystemService.Verify(f => f.DeleteFile(@"C:\Scripts\BloatRemoval.ps1"), Times.AtLeastOnce);
    }

    // --- PropertyChanged notifications ---

    [Fact]
    public async Task StartStatusMonitoringAsync_RaisesPropertyChanged_ForIsLoading()
    {
        _fileSystemService.Setup(f => f.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(@"C:\Scripts\BloatRemoval.ps1");
        _fileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _scheduledTaskService.Setup(s => s.IsTaskRegisteredAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        var sut = CreateSut();
        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        await sut.StartStatusMonitoringAsync();

        changedProperties.Should().Contain("IsLoading");
    }

    [Fact]
    public async Task StartStatusMonitoringAsync_RaisesPropertyChanged_ForIsActive()
    {
        _fileSystemService.Setup(f => f.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(@"C:\Scripts\BloatRemoval.ps1");
        _fileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _scheduledTaskService.Setup(s => s.IsTaskRegisteredAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        var sut = CreateSut();
        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        await sut.StartStatusMonitoringAsync();

        changedProperties.Should().Contain("IsActive");
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_PreventsSubsequentStatusMonitoring()
    {
        _fileSystemService.Setup(f => f.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(@"C:\Scripts\BloatRemoval.ps1");
        _fileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _scheduledTaskService.Setup(s => s.IsTaskRegisteredAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        var sut = CreateSut();
        sut.Dispose();

        await sut.StartStatusMonitoringAsync();

        // IsActive should remain false after dispose, since monitoring should be skipped
        sut.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var sut = CreateSut();

        sut.Dispose();
        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }
}
