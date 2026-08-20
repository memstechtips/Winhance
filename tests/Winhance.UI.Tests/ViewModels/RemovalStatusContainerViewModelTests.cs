using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class RemovalStatusContainerViewModelTests
{
    private readonly Mock<IScheduledTaskService> _scheduledTaskService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();

    private RemovalStatusContainerViewModel CreateSut() => new(
        _scheduledTaskService.Object,
        _logService.Object,
        _fileSystemService.Object);

    [Fact]
    public void Constructor_InitializesRemovalStatusItems()
    {
        var sut = CreateSut();

        sut.RemovalStatusItems.Should().NotBeNull();
        sut.RemovalStatusItems.Should().HaveCount(3);
    }

    [Fact]
    public void Constructor_ContainsBloatRemovalItem()
    {
        var sut = CreateSut();

        sut.RemovalStatusItems.Should().Contain(item => item.Name == "Bloat Removal");
    }

    [Fact]
    public void Constructor_ContainsMicrosoftEdgeItem()
    {
        var sut = CreateSut();

        sut.RemovalStatusItems.Should().Contain(item => item.Name == "Microsoft Edge");
    }

    [Fact]
    public void Constructor_ContainsOneDriveItem()
    {
        var sut = CreateSut();

        sut.RemovalStatusItems.Should().Contain(item => item.Name == "OneDrive");
    }

    [Fact]
    public void BloatRemovalItem_HasCorrectConfiguration()
    {
        var sut = CreateSut();
        var item = sut.RemovalStatusItems.First(i => i.Name == "Bloat Removal");

        item.ScriptFileName.Should().Be("BloatRemoval.ps1");
        item.ScheduledTaskName.Should().Be("BloatRemoval");
        item.ActiveColor.Should().Be("#00FF3C");
        item.IconPath.Should().Be("DeleteSweepIconPath");
    }

    [Fact]
    public void MicrosoftEdgeItem_HasCorrectConfiguration()
    {
        var sut = CreateSut();
        var item = sut.RemovalStatusItems.First(i => i.Name == "Microsoft Edge");

        item.ScriptFileName.Should().Be("EdgeRemoval.ps1");
        item.ScheduledTaskName.Should().Be("EdgeRemoval");
        item.ActiveColor.Should().Be("#0078D4");
        item.IconPath.Should().Be("MicrosoftEdgeIconPath");
    }

    [Fact]
    public void OneDriveItem_HasCorrectConfiguration()
    {
        var sut = CreateSut();
        var item = sut.RemovalStatusItems.First(i => i.Name == "OneDrive");

        item.ScriptFileName.Should().Be("OneDriveRemoval.ps1");
        item.ScheduledTaskName.Should().Be("OneDriveRemoval");
        item.ActiveColor.Should().Be("#0078D4");
        item.IconPath.Should().Be("MicrosoftOneDriveIconPath");
    }

    [Fact]
    public async Task RefreshAllStatusesAsync_CallsStartStatusMonitoringOnAllItems()
    {
        _fileSystemService.Setup(f => f.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(@"C:\Scripts\test.ps1");
        _fileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _scheduledTaskService.Setup(s => s.IsTaskRegisteredAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        var sut = CreateSut();

        await sut.RefreshAllStatusesAsync();

        _scheduledTaskService.Verify(s => s.IsTaskRegisteredAsync(It.IsAny<string>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task RefreshAllStatusesAsync_DoesNotThrowOnServiceErrors()
    {
        _scheduledTaskService.Setup(s => s.IsTaskRegisteredAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("test error"));
        _fileSystemService.Setup(f => f.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(@"C:\Scripts\test.ps1");

        var sut = CreateSut();

        var act = async () => await sut.RefreshAllStatusesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void PropertyChanged_EventIsAccessible()
    {
        var sut = CreateSut();
        bool eventHandled = false;

        sut.PropertyChanged += (_, _) => eventHandled = true;

        eventHandled.Should().BeFalse();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_DisposesAllChildItems()
    {
        var sut = CreateSut();
        var items = sut.RemovalStatusItems.ToList();

        sut.Dispose();

        foreach (var item in items)
        {
            item.IsActive.Should().BeFalse();
        }
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var sut = CreateSut();

        sut.Dispose();
        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }
}
