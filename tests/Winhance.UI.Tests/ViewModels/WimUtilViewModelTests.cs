using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WimUtilViewModelTests : IDisposable
{
    private readonly Mock<IOscdimgToolManager> _mockOscdimgToolManager = new();
    private readonly Mock<IIsoService> _mockIsoService = new();
    private readonly Mock<IWimImageService> _mockWimImageService = new();
    private readonly Mock<IWimCustomizationService> _mockWimCustomizationService = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IAutounattendXmlGeneratorService> _mockXmlGeneratorService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();

    private readonly WimUtilViewModel _sut;

    public WimUtilViewModelTests()
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

        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());

        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(a => a().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _sut = new WimUtilViewModel(
            _mockOscdimgToolManager.Object,
            _mockIsoService.Object,
            _mockWimImageService.Object,
            _mockWimCustomizationService.Object,
            _mockTaskProgressService.Object,
            _mockDialogService.Object,
            _mockLogService.Object,
            _mockXmlGeneratorService.Object,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockProcessExecutor.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ── Constructor ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesSubViewModels()
    {
        _sut.Step1.Should().NotBeNull();
        _sut.ImageFormat.Should().NotBeNull();
        _sut.Step2.Should().NotBeNull();
        _sut.Step3.Should().NotBeNull();
        _sut.Step4.Should().NotBeNull();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesCurrentStepToOne()
    {
        _sut.CurrentStep.Should().Be(1);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesAllStepStates()
    {
        _sut.Step1State.Should().NotBeNull();
        _sut.Step2State.Should().NotBeNull();
        _sut.Step3State.Should().NotBeNull();
        _sut.Step4State.Should().NotBeNull();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_Step1State_IsExpandedAndAvailable()
    {
        _sut.Step1State.IsExpanded.Should().BeTrue();
        _sut.Step1State.IsAvailable.Should().BeTrue();
        _sut.Step1State.StepNumber.Should().Be(1);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_Step2Through4_AreNotExpandedOrAvailable()
    {
        _sut.Step2State.IsExpanded.Should().BeFalse();
        _sut.Step2State.IsAvailable.Should().BeFalse();

        _sut.Step3State.IsExpanded.Should().BeFalse();
        _sut.Step3State.IsAvailable.Should().BeFalse();

        _sut.Step4State.IsExpanded.Should().BeFalse();
        _sut.Step4State.IsAvailable.Should().BeFalse();
    }

    // ── Localization labels ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Title_ReturnsLocalizationStringForWimUtilTitle()
    {
        _sut.Title.Should().Be("WIMUtil_Title");
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void CheckboxExtractedAlreadyText_ReturnsLocalizedString()
    {
        _sut.CheckboxExtractedAlreadyText.Should().Be("WIMUtil_CheckboxExtractedAlready");
    }

    // ── NavigateToStep ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void NavigateToStepCommand_NullParameter_DoesNotChangeStep()
    {
        _sut.NavigateToStepCommand.Execute(null);

        _sut.CurrentStep.Should().Be(1);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void NavigateToStepCommand_EmptyString_DoesNotChangeStep()
    {
        _sut.NavigateToStepCommand.Execute("");

        _sut.CurrentStep.Should().Be(1);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void NavigateToStepCommand_NonNumericString_DoesNotChangeStep()
    {
        _sut.NavigateToStepCommand.Execute("abc");

        _sut.CurrentStep.Should().Be(1);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void NavigateToStepCommand_SameStep_CollapsesToZero()
    {
        _sut.NavigateToStepCommand.Execute("1");

        _sut.CurrentStep.Should().Be(0);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void NavigateToStepCommand_Step2WhenNotAvailable_DoesNotNavigate()
    {
        // Step2 is not available until extraction is complete
        _sut.NavigateToStepCommand.Execute("2");

        _sut.CurrentStep.Should().Be(1);
    }

    // ── OnNavigatedToAsync ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task OnNavigatedToAsync_ChecksOscdimgAvailabilityAndUpdatesStep4()
    {
        _mockOscdimgToolManager
            .Setup(o => o.IsOscdimgAvailableAsync())
            .ReturnsAsync(true);

        await _sut.OnNavigatedToAsync();

        _sut.Step4.IsOscdimgAvailable.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task OnNavigatedToAsync_WhenOscdimgNotAvailable_Step4IsOscdimgAvailableIsFalse()
    {
        _mockOscdimgToolManager
            .Setup(o => o.IsOscdimgAvailableAsync())
            .ReturnsAsync(false);

        await _sut.OnNavigatedToAsync();

        _sut.Step4.IsOscdimgAvailable.Should().BeFalse();
    }

    // ── SetMainWindow ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SetMainWindow_IsNoOp_DoesNotThrow()
    {
        var act = () => _sut.SetMainWindow(null!);

        act.Should().NotThrow();
    }

    // ── Forwarded properties ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SelectedIsoPath_ForwardsToStep1()
    {
        _sut.SelectedIsoPath.Should().Be(_sut.Step1.SelectedIsoPath);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void WorkingDirectory_ForwardsToStep1()
    {
        _sut.WorkingDirectory.Should().Be(_sut.Step1.WorkingDirectory);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void IsExtractionComplete_ForwardsToStep1()
    {
        _sut.IsExtractionComplete.Should().Be(_sut.Step1.IsExtractionComplete);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void OutputIsoPath_ForwardsToStep4()
    {
        _sut.OutputIsoPath.Should().Be(_sut.Step4.OutputIsoPath);
    }

    // ── Forwarded commands ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SelectIsoFileCommand_ForwardsToStep1()
    {
        _sut.SelectIsoFileCommand.Should().BeSameAs(_sut.Step1.SelectIsoFileCommand);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void ConvertImageFormatCommand_ForwardsToImageFormat()
    {
        _sut.ConvertImageFormatCommand.Should().BeSameAs(_sut.ImageFormat.ConvertImageFormatCommand);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void GenerateWinhanceXmlCommand_ForwardsToStep2()
    {
        _sut.GenerateWinhanceXmlCommand.Should().BeSameAs(_sut.Step2.GenerateWinhanceXmlCommand);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void ExtractAndAddSystemDriversCommand_ForwardsToStep3()
    {
        _sut.ExtractAndAddSystemDriversCommand.Should().BeSameAs(_sut.Step3.ExtractAndAddSystemDriversCommand);
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void CreateIsoCommand_ForwardsToStep4()
    {
        _sut.CreateIsoCommand.Should().BeSameAs(_sut.Step4.CreateIsoCommand);
    }

    // ── Property change propagation ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void WhenStep1WorkingDirectoryChanges_PropagatesWorkingDirectoryToAllSubViewModels()
    {
        _sut.Step1.WorkingDirectory = "C:\\NewWorkDir";

        _sut.ImageFormat.WorkingDirectory.Should().Be("C:\\NewWorkDir");
        _sut.Step2.WorkingDirectory.Should().Be("C:\\NewWorkDir");
        _sut.Step3.WorkingDirectory.Should().Be("C:\\NewWorkDir");
        _sut.Step4.WorkingDirectory.Should().Be("C:\\NewWorkDir");
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void WhenStep1WorkingDirectoryChanges_RaisesPropertyChangedOnParent()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimUtilViewModel.WorkingDirectory))
                raised = true;
        };

        _sut.Step1.WorkingDirectory = "C:\\Changed";

        raised.Should().BeTrue();
    }

    // ── IDisposable ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var act = () =>
        {
            _sut.Dispose();
            _sut.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Dispose_UnsubscribesFromSubVMPropertyChangedEvents()
    {
        // After dispose, changing Step1 properties should not propagate to parent
        _sut.Dispose();

        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimUtilViewModel.WorkingDirectory))
                raised = true;
        };

        _sut.Step1.WorkingDirectory = "C:\\AfterDispose";

        raised.Should().BeFalse();
    }

    // ── HasExtractedIsoAlready forwarding ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void HasExtractedIsoAlready_SetOnParent_SetsOnStep1()
    {
        _sut.HasExtractedIsoAlready = true;

        _sut.Step1.HasExtractedIsoAlready.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void HasExtractedIsoAlready_GetFromParent_ReturnsStep1Value()
    {
        _sut.Step1.HasExtractedIsoAlready = true;

        _sut.HasExtractedIsoAlready.Should().BeTrue();
    }
}
