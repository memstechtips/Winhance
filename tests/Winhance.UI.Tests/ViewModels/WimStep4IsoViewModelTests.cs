using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WimStep4IsoViewModelTests : IDisposable
{
    private readonly Mock<IOscdimgToolManager> _mockOscdimgToolManager = new();
    private readonly Mock<IIsoService> _mockIsoService = new();
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

        _sut = new WimStep4IsoViewModel(
            _mockOscdimgToolManager.Object,
            _mockIsoService.Object,
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
    }

    // ── Constructor ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesOutputIsoPathToEmpty()
    {
        _sut.OutputIsoPath.Should().BeEmpty();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesIsOscdimgAvailableToFalse()
    {
        _sut.IsOscdimgAvailable.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesIsIsoCreatedToFalse()
    {
        _sut.IsIsoCreated.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesActionCards()
    {
        _sut.DownloadOscdimgCard.Should().NotBeNull();
        _sut.SelectOutputCard.Should().NotBeNull();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_BothActionCardsAreEnabled()
    {
        _sut.DownloadOscdimgCard.IsEnabled.Should().BeTrue();
        _sut.SelectOutputCard.IsEnabled.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_WorkingDirectoryDefaultsToEmpty()
    {
        _sut.WorkingDirectory.Should().BeEmpty();
    }

    // ── DownloadOscdimg command ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task DownloadOscdimgCommand_OnSuccess_SetsIsOscdimgAvailable()
    {
        _mockOscdimgToolManager
            .Setup(o => o.EnsureOscdimgAvailableAsync(
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.DownloadOscdimgCommand.ExecuteAsync(null);

        _sut.IsOscdimgAvailable.Should().BeTrue();
        _sut.DownloadOscdimgCard.IsComplete.Should().BeTrue();
        _sut.DownloadOscdimgCard.IsEnabled.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task DownloadOscdimgCommand_OnFailure_SetsHasFailed()
    {
        _mockOscdimgToolManager
            .Setup(o => o.EnsureOscdimgAvailableAsync(
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.DownloadOscdimgCommand.ExecuteAsync(null);

        _sut.DownloadOscdimgCard.HasFailed.Should().BeTrue();
        _sut.DownloadOscdimgCard.IsEnabled.Should().BeTrue();
        _sut.IsOscdimgAvailable.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task DownloadOscdimgCommand_OnException_SetsHasFailed()
    {
        _mockOscdimgToolManager
            .Setup(o => o.EnsureOscdimgAvailableAsync(
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Install failed"));

        await _sut.DownloadOscdimgCommand.ExecuteAsync(null);

        _sut.DownloadOscdimgCard.HasFailed.Should().BeTrue();
        _sut.DownloadOscdimgCard.IsProcessing.Should().BeFalse();
        _sut.DownloadOscdimgCard.IsEnabled.Should().BeTrue();
    }

    // ── SelectIsoOutputLocation command ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SelectIsoOutputLocationCommand_WhenFileSelected_SetsOutputIsoPath()
    {
        _mockFilePickerService
            .Setup(f => f.PickSaveFile(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("D:\\Output\\Winhance_Windows.iso");

        _sut.SelectIsoOutputLocationCommand.Execute(null);

        _sut.OutputIsoPath.Should().Be("D:\\Output\\Winhance_Windows.iso");
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SelectIsoOutputLocationCommand_WhenCancelled_DoesNotChangeOutputPath()
    {
        _mockFilePickerService
            .Setup(f => f.PickSaveFile(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((string?)null);

        _sut.SelectIsoOutputLocationCommand.Execute(null);

        _sut.OutputIsoPath.Should().BeEmpty();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SelectIsoOutputLocationCommand_WhenFileSelected_UpdatesSelectOutputCardDescription()
    {
        _mockFilePickerService
            .Setup(f => f.PickSaveFile(It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("D:\\Output\\Winhance_Windows.iso");

        _sut.SelectIsoOutputLocationCommand.Execute(null);

        _sut.SelectOutputCard.Description.Should().Contain("Winhance_Windows.iso");
    }

    // ── CreateIso command ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task CreateIsoCommand_WhenOscdimgNotAvailable_ShowsWarning()
    {
        _sut.IsOscdimgAvailable = false;

        await _sut.CreateIsoCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task CreateIsoCommand_WhenOutputPathEmpty_ShowsWarning()
    {
        _sut.IsOscdimgAvailable = true;
        _sut.OutputIsoPath = string.Empty;

        await _sut.CreateIsoCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task CreateIsoCommand_OnSuccess_SetsIsIsoCreated()
    {
        _sut.IsOscdimgAvailable = true;
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
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _sut.CreateIsoCommand.ExecuteAsync(null);

        _sut.IsIsoCreated.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task CreateIsoCommand_OnFailure_DoesNotSetIsIsoCreated()
    {
        _sut.IsOscdimgAvailable = true;
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

        await _sut.CreateIsoCommand.ExecuteAsync(null);

        _sut.IsIsoCreated.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task CreateIsoCommand_AlwaysCallsCompleteTask()
    {
        _sut.IsOscdimgAvailable = true;
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
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _sut.CreateIsoCommand.ExecuteAsync(null);

        _mockTaskProgressService.Verify(t => t.CompleteTask(), Times.Once);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task CreateIsoCommand_DisablesSelectOutputCardDuringCreation()
    {
        _sut.IsOscdimgAvailable = true;
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
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _sut.CreateIsoCommand.ExecuteAsync(null);

        wasDisabledDuring.Should().BeTrue();
        // After completion, card should be re-enabled
        _sut.SelectOutputCard.IsEnabled.Should().BeTrue();
    }

    // ── UpdateDownloadOscdimgCardState ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void UpdateDownloadOscdimgCardState_WhenAvailable_DisablesAndMarkComplete()
    {
        _sut.IsOscdimgAvailable = true;

        _sut.UpdateDownloadOscdimgCardState();

        _sut.DownloadOscdimgCard.IsEnabled.Should().BeFalse();
        _sut.DownloadOscdimgCard.IsComplete.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void UpdateDownloadOscdimgCardState_WhenNotAvailable_EnablesCard()
    {
        _sut.IsOscdimgAvailable = false;

        _sut.UpdateDownloadOscdimgCardState();

        _sut.DownloadOscdimgCard.IsEnabled.Should().BeTrue();
        _sut.DownloadOscdimgCard.IsComplete.Should().BeFalse();
    }

    // ── IDisposable ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var vm = new WimStep4IsoViewModel(
            _mockOscdimgToolManager.Object,
            _mockIsoService.Object,
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

    // ── Property change notifications ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SettingIsOscdimgAvailable_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimStep4IsoViewModel.IsOscdimgAvailable))
                raised = true;
        };

        _sut.IsOscdimgAvailable = true;

        raised.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
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

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
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
}
