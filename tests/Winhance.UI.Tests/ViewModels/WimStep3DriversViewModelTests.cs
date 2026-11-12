using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.WimUtil.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WimStep3DriversViewModelTests
{
    private const string WorkDir = "C:\\WorkDir";
    private const string Folder = "C:\\Drivers";

    private readonly Mock<IWimCustomizationService> _mockWimCustomizationService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private bool _driversOnMedia;

    private readonly WimStep3DriversViewModel _sut;

    public WimStep3DriversViewModelTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] _) => key);
        _mockWimCustomizationService
            .Setup(s => s.AreDriversOnMedia(WorkDir))
            .Returns(() => _driversOnMedia);
        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockFileSystemService.Setup(f => f.DirectoryExists(Folder)).Returns(true);
        _mockFileSystemService
            .Setup(f => f.GetFiles(Folder, "*.inf", SearchOption.AllDirectories))
            .Returns(["C:\\Drivers\\a.inf"]);

        _sut = new WimStep3DriversViewModel(
            _mockWimCustomizationService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object)
        {
            WorkingDirectory = WorkDir,
        };
    }

    private static IProgress<TaskProgressDetail> Progress => new Progress<TaskProgressDetail>();

    [Fact]
    public void Constructor_StartsWithNothingChosen()
    {
        _sut.AddSystemDrivers.Should().BeFalse();
        _sut.AddFolderDrivers.Should().BeFalse();
        _sut.HasDriverChoice.Should().BeFalse();
        _sut.NeedsApply.Should().BeFalse();
    }

    [Fact]
    public void ApplyTitleKey_NamesTheDriversPhase()
    {
        _sut.ApplyTitleKey.Should().Be("WIMUtil_Build_PrepareDrivers");
    }

    [Fact]
    public void WorkingDirectory_WhenEmpty_StartsEmpty()
    {
        new WimStep3DriversViewModel(
            _mockWimCustomizationService.Object, _mockDialogService.Object, _mockLocalizationService.Object,
            _mockFileSystemService.Object, _mockFilePickerService.Object, _mockLogService.Object)
            .WorkingDirectory.Should().BeEmpty();
    }

    [Fact]
    public void TickingSystemDrivers_ChoosesThemWithoutTouchingTheFolder()
    {
        _sut.AddSystemDrivers = true;

        _sut.HasDriverChoice.Should().BeTrue();
        _sut.NeedsApply.Should().BeTrue();
        _mockWimCustomizationService.Verify(s => s.AddDriversAsync(
            It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChooseDriversFolder_RecordsTheFolderAndTicksTheBox()
    {
        _mockFilePickerService.Setup(p => p.PickFolder(It.IsAny<string>())).Returns(Folder);

        await _sut.ChooseDriversFolderCommand.ExecuteAsync(null);

        _sut.CustomDriversFolder.Should().Be(Folder);
        _sut.AddFolderDrivers.Should().BeTrue();
        _sut.NeedsApply.Should().BeTrue();
        _mockWimCustomizationService.Verify(s => s.AddDriversAsync(
            It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChooseDriversFolder_WhenPickerCancelled_ChoosesNothing()
    {
        _mockFilePickerService.Setup(p => p.PickFolder(It.IsAny<string>())).Returns((string?)null);

        await _sut.ChooseDriversFolderCommand.ExecuteAsync(null);

        _sut.AddFolderDrivers.Should().BeFalse();
        _sut.CustomDriversFolder.Should().BeEmpty();
    }

    [Fact]
    public async Task ChooseDriversFolder_WhenFolderMissing_ShowsErrorAndChoosesNothing()
    {
        _mockFilePickerService.Setup(p => p.PickFolder(It.IsAny<string>())).Returns("C:\\Gone");

        await _sut.ChooseDriversFolderCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowErrorAsync("WIMUtil_Msg_InvalidFolder"), Times.Once);
        _sut.AddFolderDrivers.Should().BeFalse();
    }

    [Fact]
    public async Task ChooseDriversFolder_WhenFolderHasNoDrivers_WarnsAndChoosesNothing()
    {
        _mockFilePickerService.Setup(p => p.PickFolder(It.IsAny<string>())).Returns("C:\\Photos");
        _mockFileSystemService.Setup(f => f.DirectoryExists("C:\\Photos")).Returns(true);
        _mockFileSystemService.Setup(f => f.GetFiles("C:\\Photos", "*.inf", SearchOption.AllDirectories)).Returns([]);

        await _sut.ChooseDriversFolderCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync("WIMUtil_Msg_NoCustomDrivers", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _sut.AddFolderDrivers.Should().BeFalse();
        _sut.CustomDriversFolder.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearDriversFolder_DropsTheFolderAndItsChoice()
    {
        _mockFilePickerService.Setup(p => p.PickFolder(It.IsAny<string>())).Returns(Folder);
        await _sut.ChooseDriversFolderCommand.ExecuteAsync(null);

        _sut.ClearDriversFolderCommand.Execute(null);

        _sut.AddFolderDrivers.Should().BeFalse();
        _sut.CustomDriversFolder.Should().BeEmpty();
        _sut.NeedsApply.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAsync_StagesSystemDriversOnceAndClearsNeedsApply()
    {
        _sut.AddSystemDrivers = true;

        var failure = await _sut.ApplyAsync(Progress, CancellationToken.None);

        failure.Should().BeNull();
        _mockWimCustomizationService.Verify(s => s.AddDriversAsync(
            WorkDir, null, It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Once);
        _sut.NeedsApply.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAsync_AddsSystemAndFolderDriversInTurn()
    {
        _mockFilePickerService.Setup(p => p.PickFolder(It.IsAny<string>())).Returns(Folder);
        _sut.AddSystemDrivers = true;
        await _sut.ChooseDriversFolderCommand.ExecuteAsync(null);

        var failure = await _sut.ApplyAsync(Progress, CancellationToken.None);

        failure.Should().BeNull();
        _mockWimCustomizationService.Verify(s => s.AddDriversAsync(
            WorkDir, null, It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockWimCustomizationService.Verify(s => s.AddDriversAsync(
            WorkDir, Folder, It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_WhenNoSystemDriversFound_ReturnsTheMessage()
    {
        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(WorkDir, null, It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _sut.AddSystemDrivers = true;

        var failure = await _sut.ApplyAsync(Progress, CancellationToken.None);

        failure.Should().Be("WIMUtil_Msg_NoDriversFound");
        _sut.NeedsApply.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyAsync_WhenServiceThrows_ReturnsTheErrorMessage()
    {
        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(WorkDir, null, It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        _sut.AddSystemDrivers = true;

        var failure = await _sut.ApplyAsync(Progress, CancellationToken.None);

        failure.Should().Be("WIMUtil_Msg_DriverExtractionError");
    }

    [Fact]
    public async Task ApplyAsync_WhenCancelled_Rethrows()
    {
        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(WorkDir, null, It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        _sut.AddSystemDrivers = true;

        var act = () => _sut.ApplyAsync(Progress, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void PickUpMedia_WhenMediaHoldsDrivers_ShowsThemAsChosenWithNothingToApply()
    {
        _driversOnMedia = true;

        _sut.PickUpMedia();

        _sut.AddSystemDrivers.Should().BeTrue();
        _sut.NeedsApply.Should().BeFalse();
    }

    [Fact]
    public async Task PickUpMedia_WhenServicingMovedTheDrivers_ClearsTheChoice()
    {
        _sut.AddSystemDrivers = true;
        await _sut.ApplyAsync(Progress, CancellationToken.None);
        _driversOnMedia = false;

        _sut.PickUpMedia();

        _sut.HasDriverChoice.Should().BeFalse();
    }

    [Fact]
    public void WorkingDirectory_WhenChanged_ClearsTheChoices()
    {
        _sut.AddSystemDrivers = true;

        _sut.WorkingDirectory = "C:\\Other";

        _sut.HasDriverChoice.Should().BeFalse();
        _sut.CustomDriversFolder.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, "WIMUtil_Card_ExtractDrivers_Description")]
    [InlineData(true, "WIMUtil_Card_ExtractDrivers_Description_Image")]
    public void SystemDriversDescription_FollowsTheMode(bool inImage, string expected)
    {
        _sut.IsBuiltIntoImage = inImage;

        _sut.SystemDriversDescription.Should().Be(expected);
    }

    [Fact]
    public async Task FolderDriversDescription_NamesTheChosenFolder()
    {
        _mockFilePickerService.Setup(p => p.PickFolder(It.IsAny<string>())).Returns(Folder);

        await _sut.ChooseDriversFolderCommand.ExecuteAsync(null);

        _sut.FolderDriversDescription.Should().Contain(Folder);
    }
}
