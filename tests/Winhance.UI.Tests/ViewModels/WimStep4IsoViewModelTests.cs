using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;
using Winhance.Core.Features.Common.Exceptions;

namespace Winhance.UI.Tests.ViewModels;

public class WimStep4IsoViewModelTests : IDisposable
{
    private readonly Mock<IIsoService> _mockIsoService = new();
    private readonly Mock<IUsbMediaWriter> _mockUsbMediaWriter = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();
    private readonly Mock<ILogService> _mockLogService = new();

    private readonly WimStep4IsoViewModel _sut;

    public WimStep4IsoViewModelTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

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
            .Setup(t => t.CreatePowerShellProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _sut = new WimStep4IsoViewModel(
            _mockIsoService.Object,
            _mockUsbMediaWriter.Object,
            _mockTaskProgressService.Object,
            _mockProcessExecutor.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
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
    public void SelectIsoOutputLocationCommand_WhenFileSelected_UpdatesSelectOutputCardDescription()
    {
        _mockFilePickerService
            .Setup(f => f.PickSaveFile(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("D:\\Output\\Winhance_Windows.iso");

        _sut.SelectIsoOutputLocationCommand.Execute(null);

        _sut.SelectOutputCard.Description.Should().Contain("Winhance_Windows.iso");
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
            It.IsAny<string>(), It.IsAny<string>(),
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
            .Setup(t => t.CreatePowerShellProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.CreateIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(),
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
            .Setup(t => t.CreatePowerShellProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.CreateIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(),
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
            .Setup(t => t.CreatePowerShellProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.CreateIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(),
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
            .Setup(t => t.CreatePowerShellProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _mockIsoService
            .Setup(i => i.CreateIsoAsync(
                It.IsAny<string>(), It.IsAny<string>(),
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
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var vm = new WimStep4IsoViewModel(
            _mockIsoService.Object,
            _mockUsbMediaWriter.Object,
            _mockTaskProgressService.Object,
            _mockProcessExecutor.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object);

        var act = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        act.Should().NotThrow();
    }


    [Fact]
    public void SettingOutputIsoPath_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimStep4IsoViewModel.OutputIsoPath))
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
            if (e.PropertyName == nameof(WimStep4IsoViewModel.IsIsoCreated))
                raised = true;
        };

        _sut.IsIsoCreated = true;

        raised.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMediaCommand_UsbDestination_ErasePromptNamesTheDeviceAndItsSize()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets()).Returns([stick]);
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
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets()).Returns([stick]);
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        _sut.WorkingDirectory = @"C:\work";
        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);
        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _mockUsbMediaWriter.Verify(w => w.Write(It.IsAny<RemovableDrive>(), It.IsAny<string>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMediaCommand_UsbWriterFailsAfterTheErase_SaysTheDriveIsBlank()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets()).Returns([stick]);
        _mockUsbMediaWriter
            .Setup(w => w.Write(stick, @"C:\work", It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
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
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets()).Returns([stick]);
        _mockUsbMediaWriter
            .Setup(w => w.Write(stick, @"C:\work", It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
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
    public async Task SelectUsbDestinationCommand_Executed_ListsTheCandidateDrives()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _mockUsbMediaWriter.Setup(w => w.GetCandidateTargets()).Returns([stick]);

        await _sut.SelectUsbDestinationCommand.ExecuteAsync(null);

        _sut.IsUsbDestination.Should().BeTrue();
        _sut.IsIsoDestination.Should().BeFalse();
        _sut.UsbTargets.Should().ContainSingle();
        _sut.SelectedUsbTarget.Should().Be(stick);
    }
}
