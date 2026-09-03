using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.Models;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WimStep3DriversViewModelTests : IDisposable
{
    private static readonly string[] OneDriverInf = ["C:\\DriverDir\\driver.inf"];

    private readonly Mock<IWimCustomizationService> _mockWimCustomizationService = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IResourceService> _mockResourceService = new();
    private readonly Mock<IAnswerFileValidator> _mockAnswerFileValidator = new();
    private readonly AnswerFileCheckState _checkState = new();

    private readonly WimStep3DriversViewModel _sut;

    public WimStep3DriversViewModelTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] _) => key);

        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _sut = new WimStep3DriversViewModel(
            _mockWimCustomizationService.Object,
            _mockTaskProgressService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object,
            _mockResourceService.Object,
            _mockAnswerFileValidator.Object,
            _checkState);
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerFileReport([]));
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_InitializesAreDriversAddedToFalse()
    {
        _sut.AreDriversAdded.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InitializesActionCards()
    {
        _sut.ExtractSystemDriversCard.Should().NotBeNull();
        _sut.SelectCustomDriversCard.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_BothActionCardsAreEnabled()
    {
        _sut.ExtractSystemDriversCard.IsEnabled.Should().BeTrue();
        _sut.SelectCustomDriversCard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WorkingDirectoryDefaultsToEmpty()
    {
        _sut.WorkingDirectory.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAndAddSystemDriversCommand_WhenWorkingDirectoryEmpty_ShowsWarningAndReturns()
    {
        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            "WIMUtil_Msg_WorkingDirectoryRequired",
            It.IsAny<string>()), Times.Once);
        _mockWimCustomizationService.Verify(s => s.AddDriversAsync(
            It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _sut.AreDriversAdded.Should().BeFalse();
    }

    [Fact]
    public async Task SelectAndAddCustomDriversCommand_WhenWorkingDirectoryEmpty_ShowsWarningAndReturns()
    {
        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            "WIMUtil_Msg_WorkingDirectoryRequired",
            It.IsAny<string>()), Times.Once);
        _mockWimCustomizationService.Verify(s => s.AddDriversAsync(
            It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _sut.AreDriversAdded.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAndAddSystemDriversCommand_WhenUserCancels_DoesNotExtract()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _mockWimCustomizationService.Verify(s => s.AddDriversAsync(
            It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _sut.AreDriversAdded.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAndAddSystemDriversCommand_OnSuccess_SetsAreDriversAdded()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                "C:\\WorkDir", null,
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _sut.AreDriversAdded.Should().BeTrue();
        _sut.ExtractSystemDriversCard.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAndAddSystemDriversCommand_OnFailure_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                "C:\\WorkDir", null,
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _sut.ExtractSystemDriversCard.HasFailed.Should().BeTrue();
        _sut.AreDriversAdded.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAndAddSystemDriversCommand_OnException_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Driver extraction error"));

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _sut.ExtractSystemDriversCard.HasFailed.Should().BeTrue();
        _sut.ExtractSystemDriversCard.IsProcessing.Should().BeFalse();
        _sut.ExtractSystemDriversCard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAndAddSystemDriversCommand_DisablesCardWhileProcessing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        bool wasProcessing = false;
        bool wasDisabled = false;

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                wasProcessing = _sut.ExtractSystemDriversCard.IsProcessing;
                wasDisabled = !_sut.ExtractSystemDriversCard.IsEnabled;
                return Task.FromResult(true);
            });

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        wasProcessing.Should().BeTrue();
        wasDisabled.Should().BeTrue();
    }

    [Fact]
    public async Task SelectAndAddCustomDriversCommand_WhenCancelled_DoesNothing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns((string?)null);

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.AreDriversAdded.Should().BeFalse();
    }

    [Fact]
    public async Task SelectAndAddCustomDriversCommand_WhenEmptyString_DoesNothing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns(string.Empty);

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.AreDriversAdded.Should().BeFalse();
    }

    [Fact]
    public async Task SelectAndAddCustomDriversCommand_DirectoryDoesNotExist_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("C:\\DriverDir");

        _mockFileSystemService
            .Setup(f => f.DirectoryExists("C:\\DriverDir"))
            .Returns(false);

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.SelectCustomDriversCard.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SelectAndAddCustomDriversCommand_EmptyDirectory_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("C:\\DriverDir");

        _mockFileSystemService
            .Setup(f => f.DirectoryExists("C:\\DriverDir"))
            .Returns(true);

        _mockFileSystemService
            .Setup(f => f.GetFiles("C:\\DriverDir", "*", SearchOption.AllDirectories))
            .Returns(Array.Empty<string>());

        _mockFileSystemService
            .Setup(f => f.GetDirectories("C:\\DriverDir", "*", SearchOption.AllDirectories))
            .Returns(Array.Empty<string>());

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.SelectCustomDriversCard.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SelectAndAddCustomDriversCommand_OnSuccess_SetsAreDriversAdded()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("C:\\DriverDir");

        _mockFileSystemService
            .Setup(f => f.DirectoryExists("C:\\DriverDir"))
            .Returns(true);

        _mockFileSystemService
            .Setup(f => f.GetFiles("C:\\DriverDir", "*", SearchOption.AllDirectories))
            .Returns(OneDriverInf);

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                "C:\\WorkDir", "C:\\DriverDir",
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.AreDriversAdded.Should().BeTrue();
        _sut.SelectCustomDriversCard.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task SelectAndAddCustomDriversCommand_OnFailure_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("C:\\DriverDir");

        _mockFileSystemService
            .Setup(f => f.DirectoryExists("C:\\DriverDir"))
            .Returns(true);

        _mockFileSystemService
            .Setup(f => f.GetFiles("C:\\DriverDir", "*", SearchOption.AllDirectories))
            .Returns(OneDriverInf);

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                "C:\\WorkDir", "C:\\DriverDir",
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.SelectCustomDriversCard.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SelectAndAddCustomDriversCommand_OnException_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("C:\\DriverDir");

        _mockFileSystemService
            .Setup(f => f.DirectoryExists("C:\\DriverDir"))
            .Returns(true);

        _mockFileSystemService
            .Setup(f => f.GetFiles("C:\\DriverDir", "*", SearchOption.AllDirectories))
            .Returns(OneDriverInf);

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Error adding drivers"));

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.SelectCustomDriversCard.HasFailed.Should().BeTrue();
        _sut.SelectCustomDriversCard.IsProcessing.Should().BeFalse();
        _sut.SelectCustomDriversCard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var vm = new WimStep3DriversViewModel(
            _mockWimCustomizationService.Object,
            _mockTaskProgressService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object,
            _mockResourceService.Object,
            _mockAnswerFileValidator.Object,
            _checkState);

        var act = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void SettingAreDriversAdded_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimStep3DriversViewModel.AreDriversAdded))
                raised = true;
        };

        _sut.AreDriversAdded = true;

        raised.Should().BeTrue();
    }

    private void ArrangeExtractSucceeds()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join("\\", parts));
        _mockFileSystemService.Setup(f => f.FileExists("C:\\WorkDir\\autounattend.xml")).Returns(true);
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync("C:\\WorkDir", null, It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task ExtractAndAddSystemDriversCommand_OnSuccess_ChecksTheAnswerFileAndShowsTheFindings()
    {
        ArrangeExtractSucceeds();
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerFileReport([new AnswerFileFinding(AnswerFileRule.OrderDuplicate, AnswerFileSeverity.Warning, "line 9", "5")]));

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _sut.ExtractSystemDriversCard.IsComplete.Should().BeTrue();
        _mockDialogService.Verify(d => d.ShowTaskOutputDialogAsync(
            "WIMUtil_AnswerFile_DialogTitle",
            It.Is<IReadOnlyList<string>>(l => l.Contains("WIMUtil_AnswerFile_Verdict_MayFail") && l.Contains("WIMUtil_AnswerFile_Summary") && l.Contains("line 9"))), Times.Once);
    }

    [Fact]
    public async Task ExtractAndAddSystemDriversCommand_CleanAnswerFile_ShowsNoFindingsDialog()
    {
        ArrangeExtractSucceeds();

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _mockAnswerFileValidator.Verify(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()), Times.Once);
        _mockDialogService.Verify(d => d.ShowTaskOutputDialogAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public async Task ExtractAndAddSystemDriversCommand_CheckThrows_KeepsTheCardComplete()
    {
        ArrangeExtractSucceeds();
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _sut.ExtractSystemDriversCard.IsComplete.Should().BeTrue();
        _sut.ExtractSystemDriversCard.HasFailed.Should().BeFalse();
        _mockLogService.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("boom"))), Times.Once);
    }

    [Fact]
    public async Task ExtractAndAddSystemDriversCommand_ChecksAfterTheTaskCompleted()
    {
        ArrangeExtractSucceeds();
        var completedBeforeCheck = false;
        _mockTaskProgressService.Setup(t => t.CompleteTask()).Callback(() => completedBeforeCheck = true);
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                completedBeforeCheck.Should().BeTrue();
                return new AnswerFileReport([]);
            });

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _mockAnswerFileValidator.Verify(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtractAndAddSystemDriversCommand_SameFindingsAsStepTwo_StaysSilent()
    {
        ArrangeExtractSucceeds();
        _checkState.LastReport = new AnswerFileReport([new AnswerFileFinding(AnswerFileRule.OrderDuplicate, AnswerFileSeverity.Warning, "line 9", "5")]);
        var rechecked = new AnswerFileReport([new AnswerFileFinding(AnswerFileRule.OrderDuplicate, AnswerFileSeverity.Warning, "line 12", "5")]);
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rechecked);

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowTaskOutputDialogAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
        _checkState.LastReport.Should().BeSameAs(rechecked);
    }

    [Fact]
    public async Task ExtractAndAddSystemDriversCommand_ChangedFindings_ShowsTheDialog()
    {
        ArrangeExtractSucceeds();
        _checkState.LastReport = new AnswerFileReport([new AnswerFileFinding(AnswerFileRule.OrderDuplicate, AnswerFileSeverity.Warning, "line 9", "5")]);
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerFileReport([new AnswerFileFinding(AnswerFileRule.CommandEmpty, AnswerFileSeverity.Error, "line 20", "Path")]));

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowTaskOutputDialogAsync("WIMUtil_AnswerFile_DialogTitle", It.IsAny<IReadOnlyList<string>>()), Times.Once);
    }

    [Fact]
    public async Task ExtractAndAddSystemDriversCommand_WithoutAnAnswerFile_ChecksNothing()
    {
        ArrangeExtractSucceeds();
        _mockFileSystemService.Setup(f => f.FileExists("C:\\WorkDir\\autounattend.xml")).Returns(false);

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _sut.ExtractSystemDriversCard.IsComplete.Should().BeTrue();
        _mockAnswerFileValidator.Verify(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockDialogService.Verify(d => d.ShowTaskOutputDialogAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public async Task SelectAndAddCustomDriversCommand_OnSuccess_ChecksTheAnswerFile()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join("\\", parts));
        _mockFileSystemService.Setup(f => f.FileExists("C:\\WorkDir\\autounattend.xml")).Returns(true);
        _mockFilePickerService.Setup(f => f.PickFolder(It.IsAny<string?>())).Returns("C:\\DriverDir");
        _mockFileSystemService.Setup(f => f.DirectoryExists("C:\\DriverDir")).Returns(true);
        _mockFileSystemService.Setup(f => f.GetFiles("C:\\DriverDir", "*", SearchOption.AllDirectories)).Returns(OneDriverInf);
        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync("C:\\WorkDir", "C:\\DriverDir", It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.SelectCustomDriversCard.IsComplete.Should().BeTrue();
        _mockAnswerFileValidator.Verify(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()), Times.Once);
    }
}
