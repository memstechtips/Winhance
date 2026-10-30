using FluentAssertions;
using Moq;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.WimUtil.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;
using Winhance.Core.Features.Common.Exceptions;

namespace Winhance.UI.Tests.ViewModels;

public class WimStep5IsoViewModelTests
{
    private readonly Mock<IWimCustomizationService> _mockWimCustomizationService = new();
    private readonly Mock<IIsoService> _mockIsoService = new();
    private readonly Mock<IUsbMediaWriter> _mockUsbMediaWriter = new();
    private readonly Mock<IBootManagerService> _mockBootManagerService = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IAnswerFileValidator> _mockAnswerFileValidator = new();
    private readonly Mock<IOfflineServicingService> _mockOfflineServicingService = new();
    private readonly AnswerFileCheckState _checkState = new();

    private readonly WimStep5IsoViewModel _sut;

    public WimStep5IsoViewModelTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] _) => key);

        _mockFileSystemService
            .Setup(f => f.GetFileName(It.IsAny<string>()))
            .Returns((string p) => System.IO.Path.GetFileName(p));

        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _mockBootManagerService
            .Setup(b => b.PrepareAsync(It.IsAny<string>(), It.IsAny<BootManager>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BootFileReplacements.None);

        _sut = new WimStep5IsoViewModel(
            _mockWimCustomizationService.Object,
            _mockIsoService.Object,
            _mockUsbMediaWriter.Object,
            _mockBootManagerService.Object,
            _mockTaskProgressService.Object,
            _mockProcessExecutor.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object,
            _mockAnswerFileValidator.Object,
            _checkState,
            _mockOfflineServicingService.Object);
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerFileReport([]));
    }

    [Fact]
    public void Constructor_InitializesOutputIsoPathToEmpty()
    {
        _sut.OutputIsoPath.Should().BeEmpty();
    }


    [Fact]
    public void Constructor_InitializesIsIsoCreatedToFalse()
    {
        _sut.IsIsoCreated.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InitializesActionCards()
    {
        _sut.SelectOutputCard.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_TheOutputCardIsEnabled()
    {
        _sut.SelectOutputCard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WorkingDirectoryDefaultsToEmpty()
    {
        _sut.WorkingDirectory.Should().BeEmpty();
    }




    [Fact]
    public void SelectIsoOutputLocationCommand_WhenFileSelected_SetsOutputIsoPath()
    {
        _mockFilePickerService
            .Setup(f => f.PickSaveFile(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("D:\\Output\\Winhance_Windows.iso");

        _sut.SelectIsoOutputLocationCommand.Execute(null);

        _sut.OutputIsoPath.Should().Be("D:\\Output\\Winhance_Windows.iso");
    }

    [Fact]
    public void SelectIsoOutputLocationCommand_WhenCancelled_DoesNotChangeOutputPath()
    {
        _mockFilePickerService
            .Setup(f => f.PickSaveFile(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((string?)null);

        _sut.SelectIsoOutputLocationCommand.Execute(null);

        _sut.OutputIsoPath.Should().BeEmpty();
    }

    [Fact]
    public void SelectIsoOutputLocationCommand_WhenFileSelected_ShowsTheFullPathOnTheCard()
    {
        _mockFilePickerService
            .Setup(f => f.PickSaveFile(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("D:\\Output\\Winhance_Windows.iso");

        _sut.SelectIsoOutputLocationCommand.Execute(null);

        _sut.SelectOutputCard.Description.Should().Contain("D:\\Output\\Winhance_Windows.iso");
    }


    [Fact]
    public async Task CreateMediaCommand_WhenOutputPathEmpty_ShowsWarning()
    {
        _sut.OutputIsoPath = string.Empty;

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateMediaCommand_WhenWorkingDirectoryEmpty_ShowsWarning()
    {
        _sut.OutputIsoPath = "D:\\Output\\test.iso";
        _sut.WorkingDirectory = string.Empty;

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockIsoService.Verify(i => i.CreateIsoAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
            It.IsAny<IProgress<Winhance.Core.Features.Common.Models.TaskProgressDetail>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_OnSuccess_SetsIsIsoCreated()
    {
        _sut.OutputIsoPath = "D:\\Output\\test.iso";
        _sut.WorkingDirectory = "C:\\WorkDir";

        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.CreateIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // User clicks "Close" when asked to open folder
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.IsIsoCreated.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMediaCommand_OnFailure_DoesNotSetIsIsoCreated()
    {
        _sut.OutputIsoPath = "D:\\Output\\test.iso";
        _sut.WorkingDirectory = "C:\\WorkDir";

        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.CreateIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.IsIsoCreated.Should().BeFalse();
    }

    [Fact]
    public async Task CreateMediaCommand_AlwaysCallsCompleteTask()
    {
        _sut.OutputIsoPath = "D:\\Output\\test.iso";
        _sut.WorkingDirectory = "C:\\WorkDir";

        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.CreateIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockTaskProgressService.Verify(t => t.CompleteTask(), Times.Once);
    }

    [Fact]
    public async Task CreateMediaCommand_DisablesSelectOutputCardDuringCreation()
    {
        _sut.OutputIsoPath = "D:\\Output\\test.iso";
        _sut.WorkingDirectory = "C:\\WorkDir";
        bool wasDisabledDuring = false;

        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.CreateIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                wasDisabledDuring = !_sut.SelectOutputCard.IsEnabled;
                return Task.FromResult(true);
            });

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        wasDisabledDuring.Should().BeTrue();
        _sut.SelectOutputCard.IsEnabled.Should().BeTrue();
    }




    [Fact]
    public void SettingOutputIsoPath_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimStep5IsoViewModel.OutputIsoPath))
                raised = true;
        };

        _sut.OutputIsoPath = "D:\\new.iso";

        raised.Should().BeTrue();
    }

    [Fact]
    public void SettingIsIsoCreated_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimStep5IsoViewModel.IsIsoCreated))
                raised = true;
        };

        _sut.IsIsoCreated = true;

        raised.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMediaCommand_UsbDestination_ErasePromptNamesTheDeviceAndItsSize()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets(It.IsAny<bool>())).Returns([stick]);
        _mockLocalizationService
            .Setup(l => l.GetString("WIMUtil_Msg_UsbEraseConfirm"))
            .Returns("{0} ({1} GB, disk {2}) will be formatted.");

        ConfirmationRequest? request = null;
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .Callback<ConfirmationRequest>(r => request = r)
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        _sut.WorkingDirectory = @"C:\work";
        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);
        await _sut.CreateMediaCommand.ExecuteAsync(null);

        request.Should().NotBeNull();
        request!.Message.Should().Contain("SanDisk Ultra")
            .And.Contain(stick.SizeGigabytes.ToString("F1"))
            .And.Contain("disk 2");
    }

    [Fact]
    public async Task CreateMediaCommand_UsbDestinationAndUserDeclines_WritesNothing()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets(It.IsAny<bool>())).Returns([stick]);
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        _sut.WorkingDirectory = @"C:\work";
        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);
        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockUsbMediaWriter.Verify(w => w.Write(It.IsAny<RemovableDrive>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_UsbWriterFailsAfterTheErase_SaysTheDriveIsBlank()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets(It.IsAny<bool>())).Returns([stick]);
        _mockUsbMediaWriter
            .Setup(w => w.Write(stick, @"C:\work", It.IsAny<BootFileReplacements>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Throws(new UsbMediaErasedException(stick, wasCancelled: false, new InvalidOperationException("The device is not ready.")));
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        _sut.WorkingDirectory = @"C:\work";
        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);
        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockTaskProgressService.Verify(t => t.FailTask(), Times.Once);
        _mockDialogService.Verify(d => d.ShowErrorAsync(
            It.Is<string>(m => m.Contains("WIMUtil_Msg_UsbWriteFailed") && m.Contains("WIMUtil_Msg_UsbDriveErased")),
            It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateMediaCommand_UsbWriteCancelledAfterTheErase_WarnsAndEndsTheTaskAsCancelled()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets(It.IsAny<bool>())).Returns([stick]);
        _mockUsbMediaWriter
            .Setup(w => w.Write(stick, @"C:\work", It.IsAny<BootFileReplacements>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Throws(new UsbMediaErasedException(stick, wasCancelled: true, new OperationCanceledException()));
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        _sut.WorkingDirectory = @"C:\work";
        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);
        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockTaskProgressService.Verify(t => t.CancelTask(), Times.Once);
        _mockTaskProgressService.Verify(t => t.FailTask(), Times.Never);
        _mockDialogService.Verify(d => d.ShowWarningAsync(
            It.Is<string>(m => m.Contains("WIMUtil_Msg_UsbDriveErased")), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _sut.IsIsoCreated.Should().BeFalse();
    }

    [Fact]
    public async Task SelectUsbDestinationCommand_ByDefault_AsksForRemovableDrivesOnly()
    {
        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);

        _mockUsbMediaWriter.Verify(w => w.GetCandidateTargets(false), Times.Once);
        _mockUsbMediaWriter.Verify(w => w.GetCandidateTargets(true), Times.Never);
    }

    [Fact]
    public async Task ShowUsbHardDrives_TurnedOnOnTheUsbStep_ListsTheDrivesAgainIncludingHardDrives()
    {
        var hardDrive = new RemovableDrive(3, "WD Elements", 2_000_398_934_016L, "USB", IsSystemDisk: false, HasRemovableMedia: false);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets(true)).Returns([hardDrive]);
        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);
        _sut.UsbTargets.Should().BeEmpty();

        _sut.ShowUsbHardDrives = true;

        SpinWait.SpinUntil(() => _sut.UsbTargets.Count == 1, TimeSpan.FromSeconds(5)).Should().BeTrue();
        _sut.UsbTargets.Single().Should().Be(hardDrive);
    }

    [Fact]
    public void ShowUsbHardDrives_TurnedOnOnTheIsoStep_DoesNotQueryTheDrives()
    {
        _sut.ShowUsbHardDrives = true;

        _mockUsbMediaWriter.Verify(w => w.GetCandidateTargets(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task SelectUsbDestinationCommand_Executed_ListsTheCandidateDrives()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets(It.IsAny<bool>())).Returns([stick]);

        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);

        _sut.IsUsbDestination.Should().BeTrue();
        _sut.IsIsoDestination.Should().BeFalse();
        _sut.UsbTargets.Should().ContainSingle();
        _sut.SelectedUsbTarget.Should().Be(stick);
    }

    [Fact]
    public async Task SelectUsbDestinationCommand_HardDriveListedFirst_PicksTheStick()
    {
        var hardDrive = new RemovableDrive(1, "WD Elements", 2_000_398_934_016L, "USB", IsSystemDisk: false, HasRemovableMedia: false);
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets(It.IsAny<bool>())).Returns([hardDrive, stick]);

        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);

        _sut.SelectedUsbTarget.Should().Be(stick);
    }

    [Fact]
    public async Task SelectUsbDestinationCommand_OnlyAHardDrive_PicksNothing()
    {
        var hardDrive = new RemovableDrive(1, "WD Elements", 2_000_398_934_016L, "USB", IsSystemDisk: false, HasRemovableMedia: false);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets(It.IsAny<bool>())).Returns([hardDrive]);

        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);

        _sut.UsbTargets.Should().ContainSingle();
        _sut.SelectedUsbTarget.Should().BeNull();
    }

    [Fact]
    public async Task CreateMediaCommand_IsoDestination_VerifiesTheDriverInstallStepFirst()
    {
        _sut.OutputIsoPath = "D:\\Output\\test.iso";
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockIsoService
            .Setup(i => i.CreateIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockWimCustomizationService.Verify(
            s => s.EnsureDriverInstallStepAsync("C:\\WorkDir", It.IsAny<CancellationToken>()), Times.Once);
        _sut.IsIsoCreated.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMediaCommand_UsbDestination_VerifiesTheDriverInstallStepFirst()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets(It.IsAny<bool>())).Returns([stick]);
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        _sut.WorkingDirectory = "C:\\WorkDir";
        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);
        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockWimCustomizationService.Verify(
            s => s.EnsureDriverInstallStepAsync("C:\\WorkDir", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMediaCommand_SelfHealedDriverStep_StillCreatesTheIso()
    {
        _sut.OutputIsoPath = "D:\\Output\\test.iso";
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockWimCustomizationService
            .Setup(s => s.EnsureDriverInstallStepAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DriverInstallStepResult.CreatedXml);
        _mockIsoService
            .Setup(i => i.CreateIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.IsIsoCreated.Should().BeTrue();
    }

    private const string AnswerFileTitle = "WIMUtil_AnswerFile_DialogTitle";

    private void ArrangeIsoRun(AnswerFileReport report)
    {
        _sut.OutputIsoPath = "D:\\Output\\test.iso";
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join("\\", parts));
        _mockFileSystemService.Setup(f => f.FileExists("C:\\WorkDir\\autounattend.xml")).Returns(true);
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockIsoService
            .Setup(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // The "open folder" prompt after a successful ISO: Close.
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });
    }

    private static AnswerFileReport OneError() =>
        new([new AnswerFileFinding(AnswerFileRule.CommandTooLong, AnswerFileSeverity.Error, "line 40: settings[specialize]", "300 characters, limit 259")]);

    [Fact]
    public async Task CreateMediaCommand_FindingsAndCancel_CreatesNothing()
    {
        ArrangeIsoRun(OneError());
        _mockDialogService
            .Setup(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r => r.Title == AnswerFileTitle)))
            .ReturnsAsync(false);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.IsIsoCreated.Should().BeFalse();
        _mockIsoService.Verify(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == AnswerFileTitle
            && r.ConfirmButtonText == "WIMUtil_Button_ContinueAnyway"
            && r.Verdict!.Title == "WIMUtil_AnswerFile_Verdict_WillFail"
            && r.Verdict.Message == "WIMUtil_AnswerFile_Summary"
            && r.Sections.SelectMany(sec => sec.Rows).Any(row => row.Detail == "300 characters, limit 259"))), Times.Once);
    }

    [Fact]
    public async Task CreateMediaCommand_FindingsAndContinueAnyway_CreatesTheIso()
    {
        ArrangeIsoRun(OneError());
        _mockDialogService
            .Setup(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r => r.Title == AnswerFileTitle)))
            .ReturnsAsync(true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.IsIsoCreated.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMediaCommand_PublishesTheReportForTheBanner()
    {
        var report = OneError();
        ArrangeIsoRun(report);
        _mockDialogService
            .Setup(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r => r.Title == AnswerFileTitle)))
            .ReturnsAsync(true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _checkState.Subject.Should().Be("C:\\WorkDir\\autounattend.xml");
        _checkState.LastReport.Should().BeSameAs(report);
    }

    [Fact]
    public async Task CreateMediaCommand_CleanAnswerFile_AsksNothing()
    {
        ArrangeIsoRun(new AnswerFileReport([]));

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.IsIsoCreated.Should().BeTrue();
        _mockAnswerFileValidator.Verify(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()), Times.Once);
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.IsAny<SummaryRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_IsoWithoutOutputPath_AsksAboutTheOutputBeforeTheAnswerFile()
    {
        ArrangeIsoRun(OneError());
        _sut.OutputIsoPath = string.Empty;

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync("WIMUtil_Msg_OutputRequired", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockAnswerFileValidator.Verify(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_UsbFindingsAndCancel_WritesNothingAndNeverAsksToErase()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets(It.IsAny<bool>())).Returns([stick]);
        _sut.WorkingDirectory = "C:\\work";
        _mockFileSystemService.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join("\\", parts));
        _mockFileSystemService.Setup(f => f.FileExists("C:\\work\\autounattend.xml")).Returns(true);
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync("C:\\work\\autounattend.xml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OneError());
        _mockDialogService
            .Setup(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r => r.Title == AnswerFileTitle)))
            .ReturnsAsync(false);

        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);
        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockUsbMediaWriter.Verify(w => w.Write(It.IsAny<RemovableDrive>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.Is<ConfirmationRequest>(r => r.Title == "WIMUtil_Msg_UsbEraseTitle")), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_EnsuresTheDriverStepBeforeCheckingTheAnswerFile()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        var ensured = false;
        _mockWimCustomizationService
            .Setup(s => s.EnsureDriverInstallStepAsync("C:\\WorkDir", It.IsAny<CancellationToken>()))
            .Callback(() => ensured = true)
            .ReturnsAsync(DriverInstallStepResult.AlreadyPresent);
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                ensured.Should().BeTrue();
                return new AnswerFileReport([]);
            });

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockAnswerFileValidator.Verify(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMediaCommand_CheckThrows_CreatesTheIso()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.IsIsoCreated.Should().BeTrue();
        _mockLogService.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("boom")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InspectBootManagerAsync_MediaOffers2023_ShowsBothChoicesWith2011Selected()
    {
        _mockBootManagerService
            .Setup(b => b.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BootManagerOptions(Offers2023: true, BootManager.Pca2011, IsOutdated: false));

        await _sut.InspectBootManagerAsync();

        _sut.ShowBootManagerChoice.Should().BeTrue();
        _sut.BootManagerChoices.Select(c => c.Value).Should().Equal(BootManager.Pca2011, BootManager.Ca2023);
        _sut.SelectedBootManager!.Value.Should().Be(BootManager.Pca2011);
        _sut.BootManagerDescription.Should().Be("WIMUtil_Card_BootManager_Description");
    }

    [Fact]
    public async Task InspectBootManagerAsync_ThisPcRevokes2011_Selects2023AndSaysWhy()
    {
        _mockBootManagerService
            .Setup(b => b.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BootManagerOptions(Offers2023: true, BootManager.Ca2023, IsOutdated: false));

        await _sut.InspectBootManagerAsync();

        _sut.SelectedBootManager!.Value.Should().Be(BootManager.Ca2023);
        _sut.BootManagerDescription.Should().Be("WIMUtil_Card_BootManager_DescriptionRevoked");
    }

    [Fact]
    public async Task InspectBootManagerAsync_NothingToChoose_HidesTheCardButStillWarnsAboutOldMedia()
    {
        _mockBootManagerService
            .Setup(b => b.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BootManagerOptions(Offers2023: false, BootManager.Pca2011, IsOutdated: true));

        await _sut.InspectBootManagerAsync();

        _sut.ShowBootManagerChoice.Should().BeFalse();
        _sut.BootManagerChoices.Should().BeEmpty();
        _sut.IsBootManagerOutdated.Should().BeTrue();
    }

    [Fact]
    public async Task InspectBootManagerAsync_ReadFails_HidesTheCard()
    {
        _mockBootManagerService
            .Setup(b => b.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boot.wim is locked"));

        await _sut.InspectBootManagerAsync();

        _sut.ShowBootManagerChoice.Should().BeFalse();
        _sut.SelectedBootManager.Should().BeNull();
    }

    [Fact]
    public async Task CreateMediaCommand_2023Selected_HandsThePreparedFilesToTheIsoWriter()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        _mockBootManagerService
            .Setup(b => b.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BootManagerOptions(Offers2023: true, BootManager.Ca2023, IsOutdated: false));
        var prepared = new BootFileReplacements(new Dictionary<string, string> { [@"efi\boot\bootx64.efi"] = @"C:\temp\bootmgfw_EX.efi" });
        _mockBootManagerService
            .Setup(b => b.PrepareAsync("C:\\WorkDir", BootManager.Ca2023, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prepared);
        await _sut.InspectBootManagerAsync();

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockIsoService.Verify(i => i.CreateIsoAsync("C:\\WorkDir", "D:\\Output\\test.iso", prepared,
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private void ArrangeImageMode(bool staged = true)
    {
        _sut.IsBuiltIntoImage = true;
        _sut.IsReadyForServicing = true;
        _mockOfflineServicingService.Setup(o => o.HasStagedItems("C:\\WorkDir")).Returns(staged);
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync("C:\\WorkDir", It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfflineServicingResult(ServicedItemOutcome.Added, ServicedItemOutcome.Added));
    }

    [Fact]
    public async Task CreateMediaCommand_ImageMode_SkipsTheEnsureSteps()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockWimCustomizationService.Verify(s => s.EnsureDriverInstallStepAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockWimCustomizationService.Verify(s => s.EnsureUpdateInstallStepAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _sut.IsIsoCreated.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMediaCommand_ImageModeServiced_RechecksTheAnswerFileAfterServicing()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockAnswerFileValidator.Verify(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateMediaCommand_ImageModeNothingStaged_ChecksTheAnswerFileOnce()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode(staged: false);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockAnswerFileValidator.Verify(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMediaCommand_SetupMode_StillEnsuresBothSteps()
    {
        ArrangeIsoRun(new AnswerFileReport([]));

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockWimCustomizationService.Verify(s => s.EnsureDriverInstallStepAsync("C:\\WorkDir", It.IsAny<CancellationToken>()), Times.Once);
        _mockWimCustomizationService.Verify(s => s.EnsureUpdateInstallStepAsync("C:\\WorkDir", It.IsAny<CancellationToken>()), Times.Once);
        _mockOfflineServicingService.Verify(o => o.ServiceAsync(It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_ImageModeNotPrepared_WarnsAndCreatesNothing()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();
        _sut.IsReadyForServicing = false;

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync("WIMUtil_Msg_ServicingNotPrepared", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockIsoService.Verify(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockOfflineServicingService.Verify(o => o.ServiceAsync(It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_ImageMode_ServicesBeforeTheBootFilesAndTheIso()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();
        var order = new List<string>();
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync("C:\\WorkDir", It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("service"))
            .ReturnsAsync(new OfflineServicingResult(ServicedItemOutcome.Added, ServicedItemOutcome.NotStaged));
        _mockBootManagerService
            .Setup(b => b.PrepareAsync(It.IsAny<string>(), It.IsAny<BootManager>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("boot"))
            .ReturnsAsync(BootFileReplacements.None);
        _mockIsoService
            .Setup(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("iso"))
            .ReturnsAsync(true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        order.Should().Equal("service", "boot", "iso");
    }

    [Fact]
    public async Task CreateMediaCommand_ImageModeNothingStaged_DoesNotService()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode(staged: false);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockOfflineServicingService.Verify(o => o.ServiceAsync(It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _sut.IsIsoCreated.Should().BeTrue();
    }

    [Theory]
    [InlineData(ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.Added, "WIMUtil_Msg_ServicingFellBack_Drivers")]
    [InlineData(ServicedItemOutcome.NotStaged, ServicedItemOutcome.FellBackToSetup, "WIMUtil_Msg_ServicingFellBack_Update")]
    [InlineData(ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.FellBackToSetup, "WIMUtil_Msg_ServicingFellBack_Both")]
    public async Task CreateMediaCommand_ServicingFellBack_NamesWhatFellBackAndStillCreatesTheIso(
        ServicedItemOutcome drivers, ServicedItemOutcome update, string messageKey)
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync("C:\\WorkDir", It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfflineServicingResult(drivers, update));

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(messageKey, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _sut.IsIsoCreated.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMediaCommand_ServicingCancelled_CancelsTheTaskAndCreatesNothing()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync("C:\\WorkDir", It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockTaskProgressService.Verify(t => t.CancelTask(), Times.Once);
        _mockIsoService.Verify(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _sut.IsIsoCreated.Should().BeFalse();
    }

    [Fact]
    public async Task CreateMediaCommand_UsbServicingRunsOutOfSpace_FailsBeforeTheStickIsWritten()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets(It.IsAny<bool>())).Returns([stick]);
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeImageMode();
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync("C:\\WorkDir", It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InsufficientDiskSpaceException("C:", 12, 3, "servicing"));

        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);
        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockTaskProgressService.Verify(t => t.FailTask(), Times.Once);
        _mockDialogService.Verify(d => d.ShowWarningAsync("WIMUtil_Msg_InsufficientSpace_Create", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockUsbMediaWriter.Verify(w => w.Write(It.IsAny<RemovableDrive>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _sut.SelectUsbCard.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMediaCommand_ImageModeNotPreparedNothingStaged_CreatesTheIso()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode(staged: false);
        _sut.IsReadyForServicing = false;

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync("WIMUtil_Msg_ServicingNotPrepared", It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _sut.IsIsoCreated.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMediaCommand_IsCreatingMediaOnlyWhileItRuns()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        var duringRun = false;
        _mockIsoService
            .Setup(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Callback(() => duringRun = _sut.IsCreatingMedia)
            .ReturnsAsync(true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        duringRun.Should().BeTrue();
        _sut.IsCreatingMedia.Should().BeFalse();
    }

    [Fact]
    public async Task CreateMediaCommand_ImageMode_RaisesImageServicedWithTheResult()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();
        OfflineServicingResult? raised = null;
        _sut.ImageServiced += (_, result) => raised = result;

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        raised.Should().Be(new OfflineServicingResult(ServicedItemOutcome.Added, ServicedItemOutcome.Added));
    }
}
