using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WimStep1ViewModelTests
{
    private readonly Mock<IIsoService> _mockIsoService = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();
    private readonly Mock<ILogService> _mockLogService = new();

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

        _sut = new WimStep1ViewModel(
            _mockIsoService.Object,
            _mockTaskProgressService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object);
    }

    // ── Constructor ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesSelectedIsoPathToEmpty()
    {
        _sut.SelectedIsoPath.Should().BeEmpty();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesWorkingDirectoryToTempWinhanceWIM()
    {
        _sut.WorkingDirectory.Should().Be("C:\\Temp\\WinhanceWIM");
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesActionCards()
    {
        _sut.SelectIsoCard.Should().NotBeNull();
        _sut.SelectDirectoryCard.Should().NotBeNull();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_SelectIsoCardIsEnabled()
    {
        _sut.SelectIsoCard.IsEnabled.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_SelectDirectoryCardIsEnabled()
    {
        _sut.SelectDirectoryCard.IsEnabled.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_DefaultsHasExtractedIsoAlreadyToFalse()
    {
        _sut.HasExtractedIsoAlready.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_DefaultsCanStartExtractionToFalse()
    {
        _sut.CanStartExtraction.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_DefaultsIsExtractingToFalse()
    {
        _sut.IsExtracting.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_DefaultsIsExtractionCompleteToFalse()
    {
        _sut.IsExtractionComplete.Should().BeFalse();
    }

    // ── SelectIsoFile command ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SelectIsoFileCommand_WhenFileSelected_SetsSelectedIsoPath()
    {
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns("C:\\ISOs\\windows.iso");

        _sut.SelectIsoFileCommand.Execute(null);

        _sut.SelectedIsoPath.Should().Be("C:\\ISOs\\windows.iso");
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SelectIsoFileCommand_WhenFileSelected_UpdatesSelectIsoCardDescription()
    {
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns("C:\\ISOs\\windows.iso");

        _sut.SelectIsoFileCommand.Execute(null);

        _sut.SelectIsoCard.Description.Should().Be("C:\\ISOs\\windows.iso");
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SelectIsoFileCommand_WhenFileSelected_SetsCanStartExtractionIfWorkingDirSet()
    {
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns("C:\\ISOs\\windows.iso");

        _sut.SelectIsoFileCommand.Execute(null);

        _sut.CanStartExtraction.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SelectIsoFileCommand_WhenCancelled_DoesNotChangeSelectedIsoPath()
    {
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns((string?)null);

        _sut.SelectIsoFileCommand.Execute(null);

        _sut.SelectedIsoPath.Should().BeEmpty();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SelectIsoFileCommand_WhenEmptyString_DoesNotChangeSelectedIsoPath()
    {
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns(string.Empty);

        _sut.SelectIsoFileCommand.Execute(null);

        _sut.SelectedIsoPath.Should().BeEmpty();
    }

    // ── SelectWorkingDirectory command ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task SelectWorkingDirectoryCommand_WhenCancelled_DoesNotChangeWorkingDirectory()
    {
        var originalDir = _sut.WorkingDirectory;
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns((string?)null);

        await _sut.SelectWorkingDirectoryCommand.ExecuteAsync(null);

        _sut.WorkingDirectory.Should().Be(originalDir);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task SelectWorkingDirectoryCommand_WhenNotExtractedAlready_CreatesSubDirectory()
    {
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("D:\\WorkDir");

        await _sut.SelectWorkingDirectoryCommand.ExecuteAsync(null);

        _mockFileSystemService.Verify(f => f.CreateDirectory("D:\\WorkDir\\WinhanceWIM"), Times.Once);
        _sut.WorkingDirectory.Should().Be("D:\\WorkDir\\WinhanceWIM");
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
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
            .Returns(new[] { "D:\\ExtractedIso\\sources", "D:\\ExtractedIso\\boot" });

        _mockFileSystemService
            .Setup(f => f.GetFileName("D:\\ExtractedIso\\sources"))
            .Returns("sources");

        _mockFileSystemService
            .Setup(f => f.GetFileName("D:\\ExtractedIso\\boot"))
            .Returns("boot");

        await _sut.SelectWorkingDirectoryCommand.ExecuteAsync(null);

        _sut.WorkingDirectory.Should().Be("D:\\ExtractedIso");
        _sut.IsExtractionComplete.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
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
            .Returns(new[] { "D:\\InvalidDir\\randomfolder" });

        _mockFileSystemService
            .Setup(f => f.GetFileName("D:\\InvalidDir\\randomfolder"))
            .Returns("randomfolder");

        await _sut.SelectWorkingDirectoryCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowErrorAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
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

    // ── ValidateExtractedIsoDirectory ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task ValidateExtractedIsoDirectory_DriveRoot_ReturnsFalse()
    {
        _mockFileSystemService
            .Setup(f => f.GetPathRoot("D:\\"))
            .Returns("D:\\");

        var result = await _sut.ValidateExtractedIsoDirectory("D:\\");

        result.Should().BeFalse();
    }

    // ── OnHasExtractedIsoAlreadyChanged ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void WhenHasExtractedIsoAlreadySetToTrue_UpdatesSelectDirectoryCardDescription()
    {
        _sut.HasExtractedIsoAlready = true;

        _sut.SelectDirectoryCard.Description.Should().Be("WIMUtil_Label_SelectExtracted");
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void WhenHasExtractedIsoAlreadySetToFalse_ResetsSelectDirectoryCardDescription()
    {
        _sut.HasExtractedIsoAlready = true;
        _sut.HasExtractedIsoAlready = false;

        _sut.SelectDirectoryCard.Description.Should().NotBe("WIMUtil_Label_SelectExtracted");
    }

    // ── StartIsoExtraction command ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
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
            .Setup(t => t.CreatePowerShellProgress())
            .Returns(new Progress<Core.Features.Common.Models.TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.ExtractIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<Core.Features.Common.Models.TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.StartIsoExtractionCommand.ExecuteAsync(null);

        _sut.IsExtractionComplete.Should().BeTrue();
        _sut.IsExtracting.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
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
            .Setup(t => t.CreatePowerShellProgress())
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

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
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
            .Setup(t => t.CreatePowerShellProgress())
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

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
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
            .Setup(t => t.CreatePowerShellProgress())
            .Returns(new Progress<Core.Features.Common.Models.TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.ExtractIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<Core.Features.Common.Models.TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.StartIsoExtractionCommand.ExecuteAsync(null);

        _mockTaskProgressService.Verify(t => t.CompleteTask(), Times.Once);
    }

    // ── Property change notifications ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
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

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
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
