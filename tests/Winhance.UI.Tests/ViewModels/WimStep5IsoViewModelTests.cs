using FluentAssertions;
using Moq;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
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
    private readonly Mock<ISelectionSaveService> _mockSaves = new();
    private readonly Mock<ISelectionSetBuilder> _mockSelections = new();
    private readonly Mock<IWimImageService> _mockWimImageService = new();
    private readonly WimUtilSession _session = new();
    private readonly AnswerFileCheckState _checkState = new();
    private readonly List<IMediaChoice> _choices = [];

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

        _mockOfflineServicingService
            .Setup(o => o.PhasesFor(It.IsAny<OfflineBuildRequest>()))
            .Returns(Array.Empty<ServicingPhase>());

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
            _mockOfflineServicingService.Object,
            _mockSaves.Object,
            _mockSelections.Object,
            _session,
            _mockWimImageService.Object,
            _choices);
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
            .Setup(f => f.PickSavePath(It.IsAny<string>(), "ISO Files", "*.iso", "Winhance_Windows.iso", "iso"))
            .Returns("D:\\Output\\Winhance_Windows.iso");

        _sut.SelectIsoOutputLocationCommand.Execute(null);

        _sut.OutputIsoPath.Should().Be("D:\\Output\\Winhance_Windows.iso");
    }

    [Fact]
    public void SelectIsoOutputLocationCommand_WhenCancelled_DoesNotChangeOutputPath()
    {
        _mockFilePickerService
            .Setup(f => f.PickSavePath(It.IsAny<string>(), "ISO Files", "*.iso", "Winhance_Windows.iso", "iso"))
            .Returns((string?)null);

        _sut.SelectIsoOutputLocationCommand.Execute(null);

        _sut.OutputIsoPath.Should().BeEmpty();
    }

    [Fact]
    public void SelectIsoOutputLocationCommand_WhenFileSelected_ShowsTheFullPathOnTheCard()
    {
        _mockFilePickerService
            .Setup(f => f.PickSavePath(It.IsAny<string>(), "ISO Files", "*.iso", "Winhance_Windows.iso", "iso"))
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

        _mockTaskProgressService.Verify(t => t.CompleteTask(), Times.AtLeastOnce);
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

    private Mock<IMediaChoice> ArrangeChoice(string titleKey, List<string> order, string? failure = null)
    {
        var choice = new Mock<IMediaChoice>();
        choice.SetupGet(c => c.NeedsApply).Returns(true);
        choice.SetupGet(c => c.ApplyTitleKey).Returns(titleKey);
        choice
            .Setup(c => c.ApplyAsync(It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add(titleKey))
            .ReturnsAsync(failure);
        _choices.Add(choice.Object);
        return choice;
    }

    [Fact]
    public async Task CreateMediaCommand_AppliesTheChoicesInOrderBeforeServicingAndTheIso()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();
        var order = new List<string>();
        ArrangeChoice("answer", order);
        ArrangeChoice("drivers", order);
        ArrangeChoice("update", order);
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync(It.IsAny<OfflineBuildRequest>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("service"))
            .ReturnsAsync(new OfflineServicingResult(ServicedItemOutcome.Added, ServicedItemOutcome.Added));
        _mockIsoService
            .Setup(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("iso"))
            .ReturnsAsync(true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        order.Should().Equal("answer", "drivers", "update", "service", "iso");
    }

    [Fact]
    public async Task CreateMediaCommand_SkipsAChoiceThatNeedsNothing()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        var order = new List<string>();
        var done = ArrangeChoice("drivers", order);
        done.SetupGet(c => c.NeedsApply).Returns(false);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        order.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateMediaCommand_WhenAChoiceFails_ShowsItAndCreatesNothing()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        var order = new List<string>();
        ArrangeChoice("drivers", order, failure: "No drivers found");
        ArrangeChoice("update", order);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowErrorAsync("No drivers found", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        order.Should().Equal("drivers");
        _mockIsoService.Verify(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _sut.IsIsoCreated.Should().BeFalse();
    }

    [Fact]
    public async Task CreateMediaCommand_WhenAChoiceIsCancelled_CancelsTheTaskAndCreatesNothing()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        var choice = ArrangeChoice("drivers", []);
        choice
            .Setup(c => c.ApplyAsync(It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockTaskProgressService.Verify(t => t.CancelTask(), Times.Once);
        _sut.IsIsoCreated.Should().BeFalse();
    }

    [Fact]
    public async Task CreateMediaCommand_DuringSetupWithEsdChosen_ConvertsRightBeforeTheIso()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        var order = new List<string>();
        _sut.ShowMediaFormat(ImageFormat.Wim, bothExist: false);
        _sut.SelectEsdFormatCommand.Execute(null);
        _mockWimImageService
            .Setup(w => w.ConvertImageAsync("C:\\WorkDir", ImageFormat.Esd, It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("convert"))
            .ReturnsAsync(true);
        _mockIsoService
            .Setup(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("iso"))
            .ReturnsAsync(true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        order.Should().Equal("convert", "iso");
    }

    [Fact]
    public async Task CreateMediaCommand_ImageModeWithEsdChosen_ExportsInThatFormat()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();
        _sut.ShowMediaFormat(ImageFormat.Wim, bothExist: false);
        _sut.SelectEsdFormatCommand.Execute(null);
        _mockWimImageService
            .Setup(w => w.ConvertImageAsync(It.IsAny<string>(), It.IsAny<ImageFormat>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        OfflineBuildRequest? seen = null;
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync(It.IsAny<OfflineBuildRequest>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()))
            .Callback<OfflineBuildRequest, IProgress<TaskProgressDetail>, IProgress<ServicingPhaseUpdate>, CancellationToken>((r, _, _, _) => seen = r)
            .ReturnsAsync(new OfflineServicingResult(ServicedItemOutcome.Added, ServicedItemOutcome.Added));

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        seen!.OutputFormat.Should().Be(ImageFormat.Esd);
    }

    [Fact]
    public async Task CreateMediaCommand_ConversionFails_CreatesNoIso()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        _sut.ShowMediaFormat(ImageFormat.Wim, bothExist: false);
        _sut.SelectEsdFormatCommand.Execute(null);
        _mockWimImageService
            .Setup(w => w.ConvertImageAsync(It.IsAny<string>(), It.IsAny<ImageFormat>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockIsoService.Verify(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _sut.IsIsoCreated.Should().BeFalse();
    }

    [Fact]
    public async Task CreateMediaCommand_BothFormatsOnDisk_ConvertsNothing()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        _sut.ShowMediaFormat(ImageFormat.Wim, bothExist: true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.CanChooseFormat.Should().BeFalse();
        _mockWimImageService.Verify(w => w.ConvertImageAsync(It.IsAny<string>(), It.IsAny<ImageFormat>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ShowMediaFormat_FollowsTheMediaUntilTheUserPicks()
    {
        _sut.ShowMediaFormat(ImageFormat.Esd, bothExist: false);
        _sut.IsEsdFormat.Should().BeTrue();
        _sut.CanChooseFormat.Should().BeTrue();

        _sut.SelectWimFormatCommand.Execute(null);
        _sut.ShowMediaFormat(ImageFormat.Esd, bothExist: false);

        _sut.IsWimFormat.Should().BeTrue();
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
    public async Task CreateMediaCommand_UsbWithFindings_AsksOnceWithTheEraseWarning()
    {
        ArrangeIsoRun(OneError());
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets(It.IsAny<bool>())).Returns([stick]);
        _mockLocalizationService
            .Setup(l => l.GetString("WIMUtil_Msg_UsbEraseConfirm"))
            .Returns("{0} ({1} GB, disk {2}) will be formatted.");
        SummaryRequest? request = null;
        _mockDialogService
            .Setup(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r => r.Title == AnswerFileTitle)))
            .Callback<SummaryRequest>(r => request = r)
            .ReturnsAsync(false);

        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);
        await _sut.CreateMediaCommand.ExecuteAsync(null);

        request!.Message.Should().Contain("SanDisk Ultra").And.Contain("disk 2");
        request.ConfirmButtonText.Should().Be("WIMUtil_Button_EraseAndWrite");
        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _mockUsbMediaWriter.Verify(w => w.Write(It.IsAny<RemovableDrive>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_ImageMode_PlansTheStepsTheChoicesWillStage()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();
        ArrangeChoice("WIMUtil_Build_PrepareDrivers", []).SetupGet(c => c.ServicedIn).Returns(ServicingPhase.Drivers);
        var requests = new List<OfflineBuildRequest>();
        _mockOfflineServicingService
            .Setup(o => o.PhasesFor(It.IsAny<OfflineBuildRequest>()))
            .Callback<OfflineBuildRequest>(requests.Add)
            .Returns([ServicingPhase.Mount, ServicingPhase.Drivers]);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        requests.Should().ContainSingle();
        requests[0].StagesDrivers.Should().BeTrue();
        requests[0].StagesUpdate.Should().BeFalse();
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

    private static readonly ServicingPhase[] StagedPhases = [ServicingPhase.PrepareImage, ServicingPhase.Mount, ServicingPhase.Drivers, ServicingPhase.Save];
    private static readonly string[] HomeAndPro = ["Windows 11 Home", "Windows 11 Pro"];
    private static readonly string[] ProOnly = ["Windows 11 Pro"];

    private void ArrangeImageMode(bool staged = true)
    {
        _sut.IsBuiltIntoImage = true;
        _mockOfflineServicingService.Setup(o => o.HasStagedItems("C:\\WorkDir")).Returns(staged);
        _mockOfflineServicingService
            .Setup(o => o.PhasesFor(It.IsAny<OfflineBuildRequest>()))
            .Returns(staged ? StagedPhases : Array.Empty<ServicingPhase>());
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync(It.Is<OfflineBuildRequest>(r => r.WorkingDirectory == "C:\\WorkDir"), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()))
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
        _mockOfflineServicingService.Verify(o => o.ServiceAsync(It.IsAny<OfflineBuildRequest>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_ImageMode_ServicesBeforeTheBootFilesAndTheIso()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();
        var order = new List<string>();
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync(It.Is<OfflineBuildRequest>(r => r.WorkingDirectory == "C:\\WorkDir"), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()))
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

        _mockOfflineServicingService.Verify(o => o.ServiceAsync(It.IsAny<OfflineBuildRequest>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()), Times.Never);
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
            .Setup(o => o.ServiceAsync(It.Is<OfflineBuildRequest>(r => r.WorkingDirectory == "C:\\WorkDir"), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()))
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
            .Setup(o => o.ServiceAsync(It.Is<OfflineBuildRequest>(r => r.WorkingDirectory == "C:\\WorkDir"), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()))
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
            .Setup(o => o.ServiceAsync(It.Is<OfflineBuildRequest>(r => r.WorkingDirectory == "C:\\WorkDir"), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()))
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

    [Theory]
    [InlineData(@"D:\Work", WimOutputDestination.Iso, @"D:\Output\win.iso", false)]
    [InlineData(@"D:\WinhanceWIM", WimOutputDestination.Iso, @"D:\WinhanceWIM\win.iso", false)]
    [InlineData(@"D:\WinhanceWIM\", WimOutputDestination.Iso, @"D:\Output\win.iso", true)]
    [InlineData(@"D:\winhancewim", WimOutputDestination.Iso, @"D:\Output\win.iso", true)]
    [InlineData(@"D:\WinhanceWIM", WimOutputDestination.Usb, @"D:\WinhanceWIM\win.iso", true)]
    public void CanOfferCleanup_OnlyForOurOwnFolderAndNeverWhenItHoldsTheOutput(
        string workingDirectory, WimOutputDestination destination, string outputIsoPath, bool expected)
    {
        WimStep5IsoViewModel.CanOfferCleanup(workingDirectory, destination, outputIsoPath).Should().Be(expected);
    }

    private void ArrangeCleanupAfterIso(bool ticked)
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        _sut.WorkingDirectory = "C:\\WinhanceWIM";
        _sut.DeleteWorkingFolderAfterBuild = ticked;
        _mockFileSystemService.Setup(f => f.FileExists("C:\\WinhanceWIM\\autounattend.xml")).Returns(true);
        _mockFileSystemService.Setup(f => f.DirectoryExists("C:\\WinhanceWIM")).Returns(true);
    }

    private void VerifyOnlyTheOpenFolderPrompt() =>
        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Once);

    [Fact]
    public async Task CreateMediaCommand_IsoDoneAndBoxTicked_DeletesTheWorkingFolderWithoutAsking()
    {
        ArrangeCleanupAfterIso(ticked: true);
        var deleted = false;
        _sut.WorkingFilesDeleted += (_, _) => deleted = true;

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockFileSystemService.Verify(f => f.DeleteDirectory("C:\\WinhanceWIM", true), Times.Once);
        deleted.Should().BeTrue();
        VerifyOnlyTheOpenFolderPrompt();
    }

    [Fact]
    public async Task CreateMediaCommand_IsoDoneAndBoxNotTicked_KeepsTheWorkingFolder()
    {
        ArrangeCleanupAfterIso(ticked: false);
        var deleted = false;
        _sut.WorkingFilesDeleted += (_, _) => deleted = true;

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockFileSystemService.Verify(f => f.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        deleted.Should().BeFalse();
        VerifyOnlyTheOpenFolderPrompt();
    }

    [Fact]
    public async Task CreateMediaCommand_WorkingFolderDeleteFails_ShowsAnErrorAndKeepsTheWizardState()
    {
        ArrangeCleanupAfterIso(ticked: true);
        _mockFileSystemService
            .Setup(f => f.DeleteDirectory("C:\\WinhanceWIM", true))
            .Throws(new IOException("in use"));
        var deleted = false;
        _sut.WorkingFilesDeleted += (_, _) => deleted = true;

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowErrorAsync(
            It.Is<string>(m => m.Contains("WIMUtil_Msg_CleanupFailed")), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        deleted.Should().BeFalse();
        _sut.IsIsoCreated.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMediaCommand_IsoFailedAndBoxTicked_KeepsTheWorkingFolder()
    {
        ArrangeCleanupAfterIso(ticked: true);
        _mockIsoService
            .Setup(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockFileSystemService.Verify(f => f.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_BoxTickedButTheIsoSitsInsideTheFolder_KeepsTheWorkingFolder()
    {
        ArrangeCleanupAfterIso(ticked: true);
        _sut.OutputIsoPath = "C:\\WinhanceWIM\\win.iso";

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.CanDeleteWorkingFolder.Should().BeFalse();
        _mockFileSystemService.Verify(f => f.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    private static int OptionOf(string settingId, LocKey label) =>
        SettingCatalog.Find(settingId)!.States.ToList().FindIndex(state => state.Label == label);

    private static SettingChoice On(string settingId) => new(settingId, new ChoiceValue.Option(OptionOf(settingId, LocKey.Common.Enabled)));

    private static SettingChoice Off(string settingId) => new(settingId, new ChoiceValue.Option(OptionOf(settingId, LocKey.Common.Disabled)));

    private static readonly AppChoice SomeApp = new("test", "Test", null, null, null, null);

    private void ArrangeSet(params SettingChoice[] settings)
    {
        _sut.WritesAnswerFile = true;
        _mockSelections
            .Setup(s => s.FromBuilderSessionAsync())
            .ReturnsAsync(new SelectionSet(settings, [SomeApp], []));
        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync(new SelectionSaveResult("C:\\WorkDir\\autounattend.xml", []));
    }

    [Fact]
    public async Task CreateMediaCommand_AutounattendSession_WritesTheAnswerFileIntoTheWorkingFolder()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeSet();

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockSaves.Verify(s => s.SaveAsync(BuilderTarget.Autounattend, It.IsAny<SelectionSet>(),
            It.Is<SelectionSaveOptions>(o => o.FixedPath == "C:\\WorkDir\\autounattend.xml" && o.MediaFolder == "C:\\WorkDir" && !o.ReportSuccessInDialog)), Times.Once);
        _sut.BuildCards.Should().Contain(c => c.Title == "WIMUtil_Build_WriteAnswerFile" && c.IsComplete);
    }

    [Fact]
    public async Task CreateMediaCommand_NoAutounattendSession_WritesNoAnswerFile()
    {
        ArrangeIsoRun(new AnswerFileReport([]));

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockSaves.Verify(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()), Times.Never);
        _sut.BuildCards.Should().NotContain(c => c.Title == "WIMUtil_Build_WriteAnswerFile");
    }

    [Fact]
    public async Task CreateMediaCommand_TheSaveProducesNoFile_CreatesNothing()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeSet();
        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync(new SelectionSaveResult(null, []));

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockIsoService.Verify(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_InstallerDownloadCancelled_StopsTheWholeBuild()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeSet();
        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync(new SelectionSaveResult("C:\\WorkDir\\autounattend.xml", []) { StagingCancelled = true });

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockIsoService.Verify(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockDialogService.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _sut.BuildCards.Should().NotContain(c => c.HasFailed);
    }

    [Fact]
    public async Task CreateMediaCommand_CleanRunWithAnAnswerFile_ShowsNoSummaryDialog()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeSet();

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.IsAny<SummaryRequest>()), Times.Never);
        VerifyOnlyTheOpenFolderPrompt();
    }

    [Fact]
    public async Task CreateMediaCommand_AnAppDidNotReachTheMedia_AsksOnceBeforeCreating()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeSet();
        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync(new SelectionSaveResult("C:\\WorkDir\\autounattend.xml", [new StagedApp("Test", StagingOutcome.DownloadFailed)]));
        _mockDialogService.Setup(d => d.ShowSummaryAsync(It.IsAny<SummaryRequest>())).ReturnsAsync(false);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.IsAny<SummaryRequest>()), Times.Once);
        _mockIsoService.Verify(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_ImageMode_BuildsTheRequestFromTheSetAndTheFacadeChoices()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();
        ArrangeSet(On("autounattend-netfx3"), On("autounattend-netfx3-remove-source"));
        var cleanup = new ImageCleanupOptions(ResetBase: true, TempFiles: true);
        _sut.Cleanup = cleanup;
        _sut.EditionIndex = 3;
        OfflineBuildRequest? seen = null;
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync(It.IsAny<OfflineBuildRequest>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()))
            .Callback<OfflineBuildRequest, IProgress<TaskProgressDetail>?, IProgress<ServicingPhaseUpdate>?, CancellationToken>((r, _, _, _) => seen = r)
            .ReturnsAsync(new OfflineServicingResult(ServicedItemOutcome.Added, ServicedItemOutcome.Added));

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        seen.Should().NotBeNull();
        seen!.WorkingDirectory.Should().Be("C:\\WorkDir");
        seen.EditionIndex.Should().Be(3);
        seen.EnableDotNet35.Should().BeTrue();
        seen.RemoveDotNetSource.Should().BeTrue();
        seen.Cleanup.Should().Be(cleanup);
        seen.Selections.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateMediaCommand_ImageModeWithoutAnAnswerFile_AsksForNoSelectionsAndNoDotNet()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();
        OfflineBuildRequest? seen = null;
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync(It.IsAny<OfflineBuildRequest>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()))
            .Callback<OfflineBuildRequest, IProgress<TaskProgressDetail>?, IProgress<ServicingPhaseUpdate>?, CancellationToken>((r, _, _, _) => seen = r)
            .ReturnsAsync(new OfflineServicingResult(ServicedItemOutcome.Added, ServicedItemOutcome.Added));

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        seen!.Selections.Should().BeNull();
        seen.EnableDotNet35.Should().BeFalse();
        seen.RemoveDotNetSource.Should().BeFalse();
        _mockSelections.Verify(s => s.FromBuilderSessionAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_ImageMode_ShowsOneCardPerPhaseAndFollowsTheUpdates()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();
        var titlesAtStart = new List<string>();
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync(It.IsAny<OfflineBuildRequest>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()))
            .Callback<OfflineBuildRequest, IProgress<TaskProgressDetail>?, IProgress<ServicingPhaseUpdate>?, CancellationToken>((_, _, phases, _) =>
            {
                titlesAtStart.AddRange(_sut.BuildCards.Select(c => c.Title));
                phases!.Report(new ServicingPhaseUpdate(ServicingPhase.PrepareImage, ServicingPhaseState.Running));
                phases.Report(new ServicingPhaseUpdate(ServicingPhase.PrepareImage, ServicingPhaseState.Done));
                phases.Report(new ServicingPhaseUpdate(ServicingPhase.Mount, ServicingPhaseState.Running));
                phases.Report(new ServicingPhaseUpdate(ServicingPhase.Mount, ServicingPhaseState.Failed));
                phases.Report(new ServicingPhaseUpdate(ServicingPhase.Drivers, ServicingPhaseState.Skipped));
            })
            .ReturnsAsync(new OfflineServicingResult(ServicedItemOutcome.Added, ServicedItemOutcome.Added));

        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new InlineContext());
        try
        {
            await _sut.CreateMediaCommand.ExecuteAsync(null);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        titlesAtStart.Should().Equal(
            "WIMUtil_Progress_PreparingImage", "WIMUtil_Progress_Mounting", "WIMUtil_Progress_AddingDrivers",
            "WIMUtil_Progress_Saving", "WIMUtil_Status_CreatingIso");
        var cards = _sut.BuildCards.ToDictionary(c => c.Title);
        cards["WIMUtil_Progress_PreparingImage"].IsComplete.Should().BeTrue();
        cards["WIMUtil_Progress_Mounting"].HasFailed.Should().BeTrue();
        cards["WIMUtil_Progress_AddingDrivers"].Description.Should().Be("WIMUtil_Build_Skipped");
        cards["WIMUtil_Status_CreatingIso"].IsComplete.Should().BeTrue();
    }

    private sealed class InlineContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);
    }

    private static int KeepOption(LocKey label) => OptionOf("autounattend-media-editions", label);

    private void ArrangeDuringSetupEditions(int keepOption, ChoiceValue? list, int editionOption)
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        _session.Editions =
        [
            new(1, "Windows 11 Home", "Core"),
            new(2, "Windows 11 Home Single Language", "CoreSingleLanguage"),
            new(3, "Windows 11 Pro", "Professional"),
        ];
        var settings = new List<SettingChoice>
        {
            new("autounattend-media-editions", new ChoiceValue.Option(keepOption)),
            new("autounattend-edition", new ChoiceValue.Option(editionOption)),
        };
        if (list is not null)
            settings.Add(new SettingChoice("autounattend-media-editions-list", list));
        ArrangeSet([.. settings]);
        _mockWimImageService
            .Setup(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EditionRemovalResult(true));
    }

    [Fact]
    public async Task CreateMediaCommand_DuringSetupKeepTicked_ExportsOnlyTheTickedEditions()
    {
        ArrangeDuringSetupEditions(KeepOption(LocKey.Setting.AutounattendMediaEditions.Option2),
            new ChoiceValue.Keys(["Windows 11 Home", "Windows 11 Pro"]), 4);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(w => w.KeepEditionsAsync("C:\\WorkDir",
            It.Is<IReadOnlyCollection<string>>(kept => kept.SequenceEqual(HomeAndPro)),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _sut.BuildCards.Should().Contain(c => c.Title == "WIMUtil_Progress_Exporting" && c.IsComplete);
        _mockOfflineServicingService.Verify(o => o.ServiceAsync(It.IsAny<OfflineBuildRequest>(), It.IsAny<IProgress<TaskProgressDetail>>(),
            It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_DuringSetupKeepOnlyTheChosen_ExportsTheEditionTheEditionCardNames()
    {
        ArrangeDuringSetupEditions(KeepOption(LocKey.Setting.AutounattendMediaEditions.Option1), null, 4);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(w => w.KeepEditionsAsync("C:\\WorkDir",
            It.Is<IReadOnlyCollection<string>>(kept => kept.SequenceEqual(ProOnly)),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMediaCommand_DuringSetupKeepTickedWithNothingTicked_ExportsNothing()
    {
        ArrangeDuringSetupEditions(KeepOption(LocKey.Setting.AutounattendMediaEditions.Option2), new ChoiceValue.Keys([]), 4);
        _mockDialogService.Setup(d => d.ShowSummaryAsync(It.IsAny<SummaryRequest>())).ReturnsAsync(true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_DuringSetupRemovingTheChosenEdition_WarnsOnceThenStillExports()
    {
        ArrangeDuringSetupEditions(KeepOption(LocKey.Setting.AutounattendMediaEditions.Option2),
            new ChoiceValue.Keys(["Windows 11 Home"]), 4);
        _mockDialogService.Setup(d => d.ShowSummaryAsync(It.IsAny<SummaryRequest>())).ReturnsAsync(true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Sections.SelectMany(section => section.Rows).Any(row => row.Title == "WIMUtil_Generate_Plan_ChosenEditionRemoved"))), Times.Once);
        _mockWimImageService.Verify(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMediaCommand_DuringSetupExportFails_MarksTheCardFailedAndStillCreatesTheIso()
    {
        ArrangeDuringSetupEditions(KeepOption(LocKey.Setting.AutounattendMediaEditions.Option1), null, 4);
        _mockWimImageService
            .Setup(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EditionRemovalResult(false));

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.BuildCards.Should().Contain(c => c.Title == "WIMUtil_Progress_Exporting" && c.HasFailed);
        _sut.IsIsoCreated.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMediaCommand_DuringSetupEditionRemovalReportsFailure_FailsTheExportCardAndStillWritesTheAnswerFile()
    {
        ArrangeDuringSetupEditions(KeepOption(LocKey.Setting.AutounattendMediaEditions.Option1), null, 4);
        _mockWimImageService
            .Setup(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EditionRemovalResult(false));

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.BuildCards.Should().Contain(c => c.Title == "WIMUtil_Progress_Exporting" && c.HasFailed
            && c.Description == "WIMUtil_Msg_RemoveEditionsFailed");
        _mockSaves.Verify(s => s.SaveAsync(BuilderTarget.Autounattend, It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()), Times.Once);
        _sut.IsIsoCreated.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMediaCommand_DuringSetupEditionRemovalThrows_FailsTheExportCardAndStillWritesTheAnswerFile()
    {
        ArrangeDuringSetupEditions(KeepOption(LocKey.Setting.AutounattendMediaEditions.Option1), null, 4);
        _mockWimImageService
            .Setup(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("locked"));

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.BuildCards.Should().Contain(c => c.Title == "WIMUtil_Progress_Exporting" && c.HasFailed
            && c.Description == "WIMUtil_Msg_RemoveEditionsFailed");
        _mockSaves.Verify(s => s.SaveAsync(BuilderTarget.Autounattend, It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()), Times.Once);
        _sut.IsIsoCreated.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMediaCommand_DuringSetupEditionRemovalCancelled_CancelsTheTaskAndCreatesNothing()
    {
        ArrangeDuringSetupEditions(KeepOption(LocKey.Setting.AutounattendMediaEditions.Option1), null, 4);
        _mockWimImageService
            .Setup(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockTaskProgressService.Verify(t => t.CancelTask(), Times.Once);
        _mockIsoService.Verify(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _sut.IsIsoCreated.Should().BeFalse();
        _sut.BuildCards.Should().NotContain(c => c.HasFailed);
    }

    [Fact]
    public async Task CreateMediaCommand_DuringSetupOldImageLeftBehind_NamesTheFileOnTheExportCard()
    {
        ArrangeDuringSetupEditions(KeepOption(LocKey.Setting.AutounattendMediaEditions.Option1), null, 4);
        _mockWimImageService
            .Setup(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EditionRemovalResult(true, "C:\\WorkDir\\sources\\install.old.wim"));

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockLocalizationService.Verify(l => l.GetString("WIMUtil_Generate_Summary_OldImageLeft",
            It.Is<object[]>(args => args.Length == 1 && (string)args[0] == "C:\\WorkDir\\sources\\install.old.wim")), Times.Once);
        _sut.BuildCards.Should().Contain(c => c.Title == "WIMUtil_Progress_Exporting" && c.IsComplete
            && c.Description == "WIMUtil_Generate_Summary_OldImageLeft");
    }

    [Fact]
    public async Task CreateMediaCommand_DuringSetupRemoveSourceWithoutDotNet_DeletesTheSourceFolder()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeSet(Off("autounattend-netfx3"), On("autounattend-netfx3-remove-source"));
        _mockFileSystemService.Setup(f => f.DirectoryExists("C:\\WorkDir\\sources\\sxs")).Returns(true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockFileSystemService.Verify(f => f.DeleteDirectory("C:\\WorkDir\\sources\\sxs", true), Times.Once);
        _sut.BuildCards.Should().Contain(c => c.Title == "WIMUtil_Progress_RemovingDotNetSource" && c.IsComplete);
    }

    [Fact]
    public async Task CreateMediaCommand_DuringSetupRemoveSourceWhileDotNetIsOn_KeepsTheSourceFolder()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeSet(On("autounattend-netfx3"), On("autounattend-netfx3-remove-source"));
        _mockFileSystemService.Setup(f => f.DirectoryExists("C:\\WorkDir\\sources\\sxs")).Returns(true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockFileSystemService.Verify(f => f.DeleteDirectory("C:\\WorkDir\\sources\\sxs", It.IsAny<bool>()), Times.Never);
    }

    // 4 is the Pro the image holds; 0 is "Ask me during setup", which names no edition.
    [Theory]
    [InlineData(1, true, true)]
    [InlineData(4, true, false)]
    [InlineData(0, true, false)]
    [InlineData(1, false, false)]
    public async Task CreateMediaCommand_SingleEditionImage_WarnsOnlyWhenTheChosenEditionIsNotTheOneHeld(
        int chosenOption, bool builtIntoImage, bool warns)
    {
        ArrangeDuringSetupEditions(KeepOption(LocKey.Setting.AutounattendMediaEditions.Option0), null, chosenOption);
        _session.Editions = [new(1, "Windows 11 Pro", "Professional")];
        if (builtIntoImage)
            ArrangeImageMode();
        _mockDialogService.Setup(d => d.ShowSummaryAsync(It.IsAny<SummaryRequest>())).ReturnsAsync(true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Sections.SelectMany(section => section.Rows).Any(row => row.Title == "WIMUtil_Generate_Plan_ChosenNotOnMedia"))),
            warns ? Times.Once() : Times.Never());
    }

    // Servicing exports the edition at EditionIndex, so the warning compares against that one, not the whole list.
    [Fact]
    public async Task CreateMediaCommand_ImageModeWithSeveralEditions_WarnsWhenTheChosenOneIsNotTheEditionKept()
    {
        ArrangeDuringSetupEditions(KeepOption(LocKey.Setting.AutounattendMediaEditions.Option0), null, 1);
        ArrangeImageMode();
        _sut.EditionIndex = 3;
        _mockDialogService.Setup(d => d.ShowSummaryAsync(It.IsAny<SummaryRequest>())).ReturnsAsync(true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Sections.SelectMany(section => section.Rows).Any(row => row.Title == "WIMUtil_Generate_Plan_ChosenNotOnMedia"))), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("WIMUtil_Generate_Plan_ChosenNotOnMedia_Detail",
            It.Is<object[]>(args => args.Length == 1 && (string)args[0] == "Windows 11 Pro")), Times.Once);
    }

    [Fact]
    public async Task CreateMediaCommand_ImageModeWithSeveralEditions_StaysQuietWhenTheChosenOneIsTheEditionKept()
    {
        ArrangeDuringSetupEditions(KeepOption(LocKey.Setting.AutounattendMediaEditions.Option0), null, 4);
        ArrangeImageMode();
        _sut.EditionIndex = 3;
        _mockDialogService.Setup(d => d.ShowSummaryAsync(It.IsAny<SummaryRequest>())).ReturnsAsync(true);

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Sections.SelectMany(section => section.Rows).Any(row => row.Title == "WIMUtil_Generate_Plan_ChosenNotOnMedia"))), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_ImageModeServiced_RefreshesTheSessionEditionsFromTheImage()
    {
        ArrangeIsoRun(new AnswerFileReport([]));
        ArrangeImageMode();
        _session.Editions =
        [
            new(1, "Windows 11 Home", "Core"),
            new(2, "Windows 11 Pro", "Professional"),
        ];
        _sut.EditionIndex = 2;
        _mockWimImageService
            .Setup(w => w.DetectImageFormatAsync("C:\\WorkDir"))
            .ReturnsAsync(new ImageFormatInfo { Format = ImageFormat.Wim, Editions = [new(1, "Windows 11 Pro", "Professional")] });

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _session.Editions.Should().Equal(new ImageEdition(1, "Windows 11 Pro", "Professional"));
    }

    [Theory]
    [InlineData(true, null, 2, false)]
    [InlineData(true, null, 1, true)]
    [InlineData(true, 2, 2, true)]
    [InlineData(false, null, 2, true)]
    public void CreateMediaCommand_CanExecute_NeedsAnEditionToKeepForAMultiEditionImageBuiltIn(
        bool builtIntoImage, int? editionIndex, int editionCount, bool canCreate)
    {
        _session.Editions = [.. Enumerable.Range(1, editionCount).Select(i => new ImageEdition(i, "Edition " + i, "Core"))];
        _sut.IsBuiltIntoImage = builtIntoImage;
        _sut.EditionIndex = editionIndex;

        _sut.CreateMediaCommand.CanExecute(null).Should().Be(canCreate);
    }

    [Fact]
    public async Task ShowBuildDetailsCommand_OpensTheTaskLog()
    {
        _mockTaskProgressService.Setup(t => t.GetTerminalOutputLines()).Returns(["line"]);

        await _sut.ShowBuildDetailsCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowTaskOutputDialogAsync("Dialog_TerminalOutput_Title", It.IsAny<IReadOnlyList<string>>()), Times.Once);
    }
}
