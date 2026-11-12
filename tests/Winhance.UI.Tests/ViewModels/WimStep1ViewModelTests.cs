using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.WimUtil.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WimStep1ViewModelTests
{
    private static readonly string[] ExtractedIsoDirs = ["D:\\ExtractedIso\\sources", "D:\\ExtractedIso\\boot"];
    private static readonly string[] InvalidDirContents = ["D:\\InvalidDir\\randomfolder"];

    private readonly Mock<IIsoService> _mockIsoService = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IUserPreferencesService> _mockUserPreferences = new();

    private readonly WimStep1ViewModel _sut;

    public WimStep1ViewModelTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockFileSystemService
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));

        _mockFileSystemService
            .Setup(f => f.GetTempPath())
            .Returns("C:\\Temp");

        _mockFileSystemService
            .Setup(f => f.GetFileName(It.IsAny<string>()))
            .Returns((string p) => System.IO.Path.GetFileName(p));

        _sut = CreateSut();
    }

    private WimStep1ViewModel CreateSut() =>
        new(
            _mockIsoService.Object,
            _mockTaskProgressService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object,
            _mockUserPreferences.Object);

    private const long Gb = 1024L * 1024 * 1024;

    private void SavedWorkingDirectory(string path) =>
        _mockUserPreferences
            .Setup(p => p.GetPreference(UserPreferenceKeys.WimUtilWorkingDirectory, It.IsAny<string>()))
            .Returns(path);

    [Fact]
    public void Constructor_InitializesSelectedIsoPathToEmpty()
    {
        _sut.SelectedIsoPath.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_NoNtfsDrive_FallsBackToTempWinhanceWIM()
    {
        _sut.WorkingDirectory.Should().Be("C:\\Temp\\WinhanceWIM");
    }

    [Fact]
    public void Constructor_PicksTheNtfsDriveWithTheMostFreeSpace()
    {
        _mockFileSystemService
            .Setup(f => f.GetFixedNtfsDrives())
            .Returns(new[] { ("C:\\", 40 * Gb), ("E:\\", 300 * Gb), ("D:\\", 120 * Gb) });

        var sut = CreateSut();

        sut.WorkingDirectory.Should().Be("E:\\\\WinhanceWIM");
    }

    [Fact]
    public void Constructor_SavedFolderOnAPresentDrive_WinsOverTheAutomaticChoice()
    {
        _mockFileSystemService.Setup(f => f.GetFixedNtfsDrives()).Returns(new[] { ("E:\\", 300 * Gb) });
        _mockFileSystemService.Setup(f => f.GetPathRoot("D:\\Builds\\WinhanceWIM")).Returns("D:\\");
        _mockFileSystemService.Setup(f => f.DirectoryExists("D:\\")).Returns(true);
        SavedWorkingDirectory("D:\\Builds\\WinhanceWIM");

        var sut = CreateSut();

        sut.WorkingDirectory.Should().Be("D:\\Builds\\WinhanceWIM");
        sut.SelectDirectoryCard.Description.Should().StartWith("WIMUtil_Label_Using: D:\\Builds\\WinhanceWIM");
    }

    [Fact]
    public void Constructor_SavedFolderOnAMissingDrive_IsIgnored()
    {
        _mockFileSystemService.Setup(f => f.GetFixedNtfsDrives()).Returns(new[] { ("E:\\", 300 * Gb) });
        _mockFileSystemService.Setup(f => f.GetPathRoot("X:\\WinhanceWIM")).Returns("X:\\");
        SavedWorkingDirectory("X:\\WinhanceWIM");

        var sut = CreateSut();

        sut.WorkingDirectory.Should().Be("E:\\\\WinhanceWIM");
    }

    [Theory]
    [InlineData(5L, true)]
    [InlineData(19L, true)]
    [InlineData(20L, false)]
    [InlineData(250L, false)]
    public void Constructor_LowFreeSpace_WarnsWithoutBlocking(long freeGb, bool expectWarning)
    {
        _mockFileSystemService.Setup(f => f.GetAvailableFreeSpace(It.IsAny<string>())).Returns(freeGb * Gb);
        _mockFilePickerService.Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>())).Returns("C:\\win.iso");

        var sut = CreateSut();
        sut.SelectIsoFileCommand.Execute(null);

        sut.ShowSpaceWarning.Should().Be(expectWarning);
        sut.SelectDirectoryCard.Description.Should().EndWith("(WIMUtil_Label_FreeSpace)");
        sut.CanStartExtraction.Should().BeTrue();
    }

    [Fact]
    public void Constructor_FreeSpaceUnknown_ShowsNoWarningAndNoFigure()
    {
        _mockFileSystemService.Setup(f => f.GetAvailableFreeSpace(It.IsAny<string>())).Returns((long?)null);

        var sut = CreateSut();

        sut.ShowSpaceWarning.Should().BeFalse();
        sut.SelectDirectoryCard.Description.Should().NotContain("WIMUtil_Label_FreeSpace");
    }

    [Fact]
    public async Task SelectWorkingDirectoryCommand_WhenNotExtractedAlready_RemembersTheFolder()
    {
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("D:\\WorkDir");

        await _sut.SelectWorkingDirectoryCommand.ExecuteAsync(null);

        _mockUserPreferences.Verify(
            p => p.SetPreferenceAsync(UserPreferenceKeys.WimUtilWorkingDirectory, "D:\\WorkDir\\WinhanceWIM"), Times.Once);
    }

    [Fact]
    public async Task SelectWorkingDirectoryCommand_WhenExtractedAlready_DoesNotRememberTheMediaFolder()
    {
        _sut.HasExtractedIsoAlready = true;
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("D:\\ExtractedIso");

        await _sut.SelectWorkingDirectoryCommand.ExecuteAsync(null);

        _mockUserPreferences.Verify(
            p => p.SetPreferenceAsync(UserPreferenceKeys.WimUtilWorkingDirectory, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ResetAfterWorkingFilesDeleted_ReturnsTheWizardToExtraction()
    {
        _mockFilePickerService.Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>())).Returns("C:\\win.iso");
        _sut.SelectIsoFileCommand.Execute(null);
        _sut.IsExtractionComplete = true;
        _sut.SelectIsoCard.IsComplete = true;

        _sut.ResetAfterWorkingFilesDeleted();

        _sut.IsExtractionComplete.Should().BeFalse();
        _sut.SelectIsoCard.IsComplete.Should().BeFalse();
        _sut.SelectIsoCard.Description.Should().Be("C:\\win.iso");
        _sut.CanStartExtraction.Should().BeTrue();
    }

    [Fact]
    public void Constructor_InitializesActionCards()
    {
        _sut.SelectIsoCard.Should().NotBeNull();
        _sut.SelectDirectoryCard.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_SelectIsoCardIsEnabled()
    {
        _sut.SelectIsoCard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_SelectDirectoryCardIsEnabled()
    {
        _sut.SelectDirectoryCard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_DefaultsHasExtractedIsoAlreadyToFalse()
    {
        _sut.HasExtractedIsoAlready.Should().BeFalse();
    }

    [Fact]
    public void Constructor_DefaultsCanStartExtractionToFalse()
    {
        _sut.CanStartExtraction.Should().BeFalse();
    }

    [Fact]
    public void Constructor_DefaultsIsExtractingToFalse()
    {
        _sut.IsExtracting.Should().BeFalse();
    }

    [Fact]
    public void Constructor_DefaultsIsExtractionCompleteToFalse()
    {
        _sut.IsExtractionComplete.Should().BeFalse();
    }

    [Fact]
    public void SelectIsoFileCommand_WhenFileSelected_SetsSelectedIsoPath()
    {
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns("C:\\ISOs\\windows.iso");

        _sut.SelectIsoFileCommand.Execute(null);

        _sut.SelectedIsoPath.Should().Be("C:\\ISOs\\windows.iso");
    }

    [Fact]
    public void SelectIsoFileCommand_WhenFileSelected_UpdatesSelectIsoCardDescription()
    {
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns("C:\\ISOs\\windows.iso");

        _sut.SelectIsoFileCommand.Execute(null);

        _sut.SelectIsoCard.Description.Should().Be("C:\\ISOs\\windows.iso");
    }

    [Fact]
    public void SelectIsoFileCommand_WhenFileSelected_SetsCanStartExtractionIfWorkingDirSet()
    {
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns("C:\\ISOs\\windows.iso");

        _sut.SelectIsoFileCommand.Execute(null);

        _sut.CanStartExtraction.Should().BeTrue();
    }

    [Fact]
    public void SelectIsoFileCommand_WhenCancelled_DoesNotChangeSelectedIsoPath()
    {
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns((string?)null);

        _sut.SelectIsoFileCommand.Execute(null);

        _sut.SelectedIsoPath.Should().BeEmpty();
    }

    [Fact]
    public void SelectIsoFileCommand_WhenEmptyString_DoesNotChangeSelectedIsoPath()
    {
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns(string.Empty);

        _sut.SelectIsoFileCommand.Execute(null);

        _sut.SelectedIsoPath.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectWorkingDirectoryCommand_WhenCancelled_DoesNotChangeWorkingDirectory()
    {
        var originalDir = _sut.WorkingDirectory;
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns((string?)null);

        await _sut.SelectWorkingDirectoryCommand.ExecuteAsync(null);

        _sut.WorkingDirectory.Should().Be(originalDir);
    }

    [Fact]
    public async Task SelectWorkingDirectoryCommand_WhenNotExtractedAlready_CreatesSubDirectory()
    {
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("D:\\WorkDir");

        await _sut.SelectWorkingDirectoryCommand.ExecuteAsync(null);

        _mockFileSystemService.Verify(f => f.CreateDirectory("D:\\WorkDir\\WinhanceWIM"), Times.Once);
        _sut.WorkingDirectory.Should().Be("D:\\WorkDir\\WinhanceWIM");
    }

    [Fact]
    public async Task SelectWorkingDirectoryCommand_WhenExtractedAlready_ValidatesDirectoryStructure()
    {
        _sut.HasExtractedIsoAlready = true;
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("D:\\ExtractedIso");

        _mockFileSystemService
            .Setup(f => f.GetPathRoot("D:\\ExtractedIso"))
            .Returns("D:\\");

        _mockFileSystemService
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _mockFileSystemService
            .Setup(f => f.GetDirectories("D:\\ExtractedIso"))
            .Returns(ExtractedIsoDirs);

        _mockFileSystemService
            .Setup(f => f.GetFileName("D:\\ExtractedIso\\sources"))
            .Returns("sources");

        _mockFileSystemService
            .Setup(f => f.GetFileName("D:\\ExtractedIso\\boot"))
            .Returns("boot");
        var mediaReady = 0;
        _sut.MediaReady += (_, _) => mediaReady++;

        await _sut.SelectWorkingDirectoryCommand.ExecuteAsync(null);

        _sut.WorkingDirectory.Should().Be("D:\\ExtractedIso");
        _sut.IsExtractionComplete.Should().BeTrue();
        mediaReady.Should().Be(1);

        // The card's tick says it worked; a dialog repeating that is one more click for nothing.
        _mockDialogService.Verify(d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SelectWorkingDirectoryCommand_WhenExtractedAlready_InvalidDirectory_ShowsError()
    {
        _sut.HasExtractedIsoAlready = true;
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("D:\\InvalidDir");

        _mockFileSystemService
            .Setup(f => f.GetPathRoot("D:\\InvalidDir"))
            .Returns("D:\\");

        _mockFileSystemService
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _mockFileSystemService
            .Setup(f => f.GetDirectories("D:\\InvalidDir"))
            .Returns(InvalidDirContents);

        _mockFileSystemService
            .Setup(f => f.GetFileName("D:\\InvalidDir\\randomfolder"))
            .Returns("randomfolder");

        await _sut.SelectWorkingDirectoryCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowErrorAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SelectWorkingDirectoryCommand_WhenCreateDirectoryFails_SetsWorkingDirectoryToEmpty()
    {
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("D:\\WorkDir");

        _mockFileSystemService
            .Setup(f => f.CreateDirectory(It.IsAny<string>()))
            .Throws(new UnauthorizedAccessException("Access denied"));

        await _sut.SelectWorkingDirectoryCommand.ExecuteAsync(null);

        _sut.WorkingDirectory.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateExtractedIsoDirectory_DriveRoot_ReturnsFalse()
    {
        _mockFileSystemService
            .Setup(f => f.GetPathRoot("D:\\"))
            .Returns("D:\\");

        var result = await _sut.ValidateExtractedIsoDirectory("D:\\");

        result.Should().BeFalse();
    }

    [Fact]
    public void WhenHasExtractedIsoAlreadySetToTrue_UpdatesSelectDirectoryCardDescription()
    {
        _sut.HasExtractedIsoAlready = true;

        _sut.SelectDirectoryCard.Description.Should().Be("WIMUtil_Label_SelectExtracted");
    }

    [Fact]
    public void WhenHasExtractedIsoAlreadySetToFalse_ResetsSelectDirectoryCardDescription()
    {
        _sut.HasExtractedIsoAlready = true;
        _sut.HasExtractedIsoAlready = false;

        _sut.SelectDirectoryCard.Description.Should().NotBe("WIMUtil_Label_SelectExtracted");
    }

    [Fact]
    public void WhenHasExtractedIsoAlreadySetToFalse_BeforeMediaIsReady_ReturnsToTheDefaultFolder()
    {
        var defaultFolder = _sut.WorkingDirectory;
        _sut.HasExtractedIsoAlready = true;
        _sut.WorkingDirectory = "D:\\SomeonesMedia";

        _sut.HasExtractedIsoAlready = false;

        _sut.WorkingDirectory.Should().Be(defaultFolder);
    }

    [Fact]
    public void WhenHasExtractedIsoAlreadySetToFalse_WithMediaInUse_KeepsTheMediaFolder()
    {
        _sut.HasExtractedIsoAlready = true;
        _sut.WorkingDirectory = "D:\\SomeonesMedia";
        _sut.IsExtractionComplete = true;

        _sut.HasExtractedIsoAlready = false;

        _sut.WorkingDirectory.Should().Be("D:\\SomeonesMedia");
    }

    [Fact]
    public async Task StartIsoExtractionCommand_OnSuccess_SetsIsExtractionComplete()
    {
        _sut.SelectedIsoPath = "C:\\test.iso";
        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<Core.Features.Common.Models.TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.ExtractIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<Core.Features.Common.Models.TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var mediaReady = 0;
        _sut.MediaReady += (_, _) => mediaReady++;

        await _sut.StartIsoExtractionCommand.ExecuteAsync(null);

        _sut.IsExtractionComplete.Should().BeTrue();
        _sut.IsExtracting.Should().BeFalse();
        mediaReady.Should().Be(1);
    }

    [Fact]
    public async Task StartIsoExtractionCommand_OnFailure_DoesNotSetIsExtractionComplete()
    {
        _sut.SelectedIsoPath = "C:\\test.iso";
        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<Core.Features.Common.Models.TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.ExtractIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<Core.Features.Common.Models.TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.StartIsoExtractionCommand.ExecuteAsync(null);

        _sut.IsExtractionComplete.Should().BeFalse();
    }

    [Fact]
    public async Task StartIsoExtractionCommand_DisablesCardsWhileExtracting()
    {
        _sut.SelectedIsoPath = "C:\\test.iso";
        bool isoCardEnabledDuringExtraction = true;
        bool dirCardEnabledDuringExtraction = true;

        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<Core.Features.Common.Models.TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.ExtractIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<Core.Features.Common.Models.TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                isoCardEnabledDuringExtraction = _sut.SelectIsoCard.IsEnabled;
                dirCardEnabledDuringExtraction = _sut.SelectDirectoryCard.IsEnabled;
                return Task.FromResult(true);
            });

        await _sut.StartIsoExtractionCommand.ExecuteAsync(null);

        isoCardEnabledDuringExtraction.Should().BeFalse();
        dirCardEnabledDuringExtraction.Should().BeFalse();
    }

    [Fact]
    public async Task StartIsoExtractionCommand_AlwaysCallsCompleteTask()
    {
        _sut.SelectedIsoPath = "C:\\test.iso";
        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<Core.Features.Common.Models.TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.ExtractIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<Core.Features.Common.Models.TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.StartIsoExtractionCommand.ExecuteAsync(null);

        _mockTaskProgressService.Verify(t => t.CompleteTask(), Times.AtLeastOnce);
    }

    [Fact]
    public void SettingSelectedIsoPath_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimStep1ViewModel.SelectedIsoPath))
                raised = true;
        };

        _sut.SelectedIsoPath = "C:\\new.iso";

        raised.Should().BeTrue();
    }

    [Fact]
    public void SettingIsExtracting_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimStep1ViewModel.IsExtracting))
                raised = true;
        };

        _sut.IsExtracting = true;

        raised.Should().BeTrue();
    }
}
