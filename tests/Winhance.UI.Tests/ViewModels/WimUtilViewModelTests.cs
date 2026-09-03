using FluentAssertions;
using Microsoft.UI.Xaml.Controls;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.ViewModels;

public class WimUtilViewModelTests : IDisposable
{
    private readonly Mock<IIsoService> _mockIsoService = new();
    private readonly Mock<IUsbMediaWriter> _mockUsbMediaWriter = new();
    private readonly Mock<IWimImageService> _mockWimImageService = new();
    private readonly Mock<IWimCustomizationService> _mockWimCustomizationService = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ISelectionSaveService> _mockSaves = new();
    private readonly Mock<ISelectionSetBuilder> _mockSelections = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();
    private readonly Mock<IResourceService> _mockResourceService = new();

    private readonly Mock<IAnswerFileValidator> _mockAnswerFileValidator = new();
    private readonly WimUtilViewModel _sut;

    public WimUtilViewModelTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] _) => key);

        _mockLocalizationService.MirrorTryGetString();

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
            _mockIsoService.Object,
            _mockUsbMediaWriter.Object,
            _mockWimImageService.Object,
            _mockWimCustomizationService.Object,
            _mockTaskProgressService.Object,
            _mockDialogService.Object,
            _mockLogService.Object,
            _mockSaves.Object,
            _mockSelections.Object,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockProcessExecutor.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockResourceService.Object,
            _mockAnswerFileValidator.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_InitializesSubViewModels()
    {
        _sut.Step1.Should().NotBeNull();
        _sut.ImageFormat.Should().NotBeNull();
        _sut.Step2.Should().NotBeNull();
        _sut.Step3.Should().NotBeNull();
        _sut.Step4.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_PropagatesStep1WorkingDirectoryToAllSubVMs()
    {
        // Step1 sets WorkingDirectory in its constructor to TempPath\WinhanceWIM.
        // WimUtilViewModel must propagate this initial value to all sub-VMs,
        // because PropertyChanged subscriptions aren't active during construction.
        var expected = _sut.Step1.WorkingDirectory;
        expected.Should().NotBeNullOrEmpty();

        _sut.ImageFormat.WorkingDirectory.Should().Be(expected);
        _sut.Step2.WorkingDirectory.Should().Be(expected);
        _sut.Step3.WorkingDirectory.Should().Be(expected);
        _sut.Step4.WorkingDirectory.Should().Be(expected);
    }

    [Fact]
    public void Constructor_InitializesAllStepStates()
    {
        _sut.Step1State.Should().NotBeNull();
        _sut.Step2State.Should().NotBeNull();
        _sut.Step3State.Should().NotBeNull();
        _sut.Step4State.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_Step1State_IsExpandedAndAvailable()
    {
        _sut.Step1State.IsExpanded.Should().BeTrue();
        _sut.Step1State.IsAvailable.Should().BeTrue();
        _sut.Step1State.StepNumber.Should().Be(1);
    }

    [Fact]
    public void Constructor_Step2Through4_AreNotExpandedOrAvailable()
    {
        _sut.Step2State.IsExpanded.Should().BeFalse();
        _sut.Step2State.IsAvailable.Should().BeFalse();

        _sut.Step3State.IsExpanded.Should().BeFalse();
        _sut.Step3State.IsAvailable.Should().BeFalse();

        _sut.Step4State.IsExpanded.Should().BeFalse();
        _sut.Step4State.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void Title_ReturnsLocalizationStringForWimUtilTitle()
    {
        _sut.Title.Should().Be("WIMUtil_Title");
    }

    [Fact]
    public void CheckboxExtractedAlreadyText_ReturnsLocalizedString()
    {
        _sut.CheckboxExtractedAlreadyText.Should().Be("WIMUtil_CheckboxExtractedAlready");
    }

    [Fact]
    public void NavigateToStepCommand_NullParameter_DoesNotChangeStep()
    {
        _sut.NavigateToStepCommand.Execute(null);

        _sut.Step1State.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void NavigateToStepCommand_EmptyString_DoesNotChangeStep()
    {
        _sut.NavigateToStepCommand.Execute("");

        _sut.Step1State.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void NavigateToStepCommand_NonNumericString_DoesNotChangeStep()
    {
        _sut.NavigateToStepCommand.Execute("abc");

        _sut.Step1State.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void NavigateToStepCommand_Step1_TogglesExpansion()
    {
        _sut.Step1State.IsExpanded.Should().BeTrue();

        _sut.NavigateToStepCommand.Execute("1");
        _sut.Step1State.IsExpanded.Should().BeFalse();

        _sut.NavigateToStepCommand.Execute("1");
        _sut.Step1State.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void NavigateToStepCommand_Step2WhenNotAvailable_DoesNotNavigate()
    {
        _sut.NavigateToStepCommand.Execute("2");

        _sut.Step2State.IsExpanded.Should().BeFalse();
    }



    [Fact]
    public void SelectedIsoPath_ForwardsToStep1()
    {
        _sut.SelectedIsoPath.Should().Be(_sut.Step1.SelectedIsoPath);
    }

    [Fact]
    public void WorkingDirectory_ForwardsToStep1()
    {
        _sut.WorkingDirectory.Should().Be(_sut.Step1.WorkingDirectory);
    }

    [Fact]
    public void IsExtractionComplete_ForwardsToStep1()
    {
        _sut.IsExtractionComplete.Should().Be(_sut.Step1.IsExtractionComplete);
    }

    [Fact]
    public void OutputIsoPath_ForwardsToStep4()
    {
        _sut.OutputIsoPath.Should().Be(_sut.Step4.OutputIsoPath);
    }

    [Fact]
    public void SelectIsoFileCommand_ForwardsToStep1()
    {
        _sut.SelectIsoFileCommand.Should().BeSameAs(_sut.Step1.SelectIsoFileCommand);
    }

    [Fact]
    public void ConvertImageFormatCommand_ForwardsToImageFormat()
    {
        _sut.ConvertImageFormatCommand.Should().BeSameAs(_sut.ImageFormat.ConvertImageFormatCommand);
    }

    [Fact]
    public void GenerateWinhanceXmlCommand_ForwardsToStep2()
    {
        _sut.GenerateWinhanceXmlCommand.Should().BeSameAs(_sut.Step2.GenerateWinhanceXmlCommand);
    }

    [Fact]
    public void ExtractAndAddSystemDriversCommand_ForwardsToStep3()
    {
        _sut.ExtractAndAddSystemDriversCommand.Should().BeSameAs(_sut.Step3.ExtractAndAddSystemDriversCommand);
    }

    [Fact]
    public void CreateMediaCommand_ForwardsToStep4()
    {
        _sut.CreateMediaCommand.Should().BeSameAs(_sut.Step4.CreateMediaCommand);
    }

    [Fact]
    public void WhenStep1WorkingDirectoryChanges_PropagatesWorkingDirectoryToAllSubViewModels()
    {
        _sut.Step1.WorkingDirectory = "C:\\NewWorkDir";

        _sut.ImageFormat.WorkingDirectory.Should().Be("C:\\NewWorkDir");
        _sut.Step2.WorkingDirectory.Should().Be("C:\\NewWorkDir");
        _sut.Step3.WorkingDirectory.Should().Be("C:\\NewWorkDir");
        _sut.Step4.WorkingDirectory.Should().Be("C:\\NewWorkDir");
    }

    [Fact]
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

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var act = () =>
        {
            _sut.Dispose();
            _sut.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_UnsubscribesFromSubVMPropertyChangedEvents()
    {
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

    [Fact]
    public void HasExtractedIsoAlready_SetOnParent_SetsOnStep1()
    {
        _sut.HasExtractedIsoAlready = true;

        _sut.Step1.HasExtractedIsoAlready.Should().BeTrue();
    }

    [Fact]
    public void HasExtractedIsoAlready_GetFromParent_ReturnsStep1Value()
    {
        _sut.Step1.HasExtractedIsoAlready = true;

        _sut.HasExtractedIsoAlready.Should().BeTrue();
    }

    [Fact]
    public void Step2Header_ShowsTheStep2StatusOnceAnXmlIsAdded()
    {
        _sut.Step1.IsExtractionComplete = true;
        _sut.Step2.IsXmlAdded = true;

        _sut.Step2.XmlStatus = "WIMUtil_AnswerFile_Verdict_WillFail";

        _sut.Step2State.StatusText.Should().Be("WIMUtil_AnswerFile_Verdict_WillFail");
    }

    [Fact]
    public void Step2Header_WithoutAnXml_SaysSo()
    {
        _sut.Step1.IsExtractionComplete = true;
        _sut.Step2.XmlStatus = "WIMUtil_AnswerFile_Verdict_Clean";

        _sut.Step2.IsXmlAdded = false;

        _sut.Step2State.StatusText.Should().Be("WIMUtil_Status_NoXmlAdded");
    }

    private static AnswerFileFinding ErrorFinding() =>
        new(AnswerFileRule.CommandEmpty, AnswerFileSeverity.Error, "line 3: settings[specialize]", "Path");

    [Fact]
    public void AnswerFileBanner_ClosedUntilAReportWithFindingsArrives()
    {
        _sut.AnswerFileBannerOpen.Should().BeFalse();
        _sut.AnswerFileBannerTitle.Should().BeEmpty();
        _sut.AnswerFileBannerMessage.Should().BeEmpty();
    }

    [Fact]
    public void AnswerFileBanner_ErrorFindings_OpenRedWithVerdictAndReasons()
    {
        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.AnswerFileCheck.LastReport = new AnswerFileReport([ErrorFinding()]);

        _sut.AnswerFileBannerOpen.Should().BeTrue();
        _sut.AnswerFileBannerSeverity.Should().Be(InfoBarSeverity.Error);
        _sut.AnswerFileBannerTitle.Should().Be("WIMUtil_AnswerFile_Verdict_WillFail");
        _sut.AnswerFileBannerSummary.Should().Be("WIMUtil_AnswerFile_Summary");
        _sut.AnswerFileBannerMessage.Should().Contain("WIMUtil_AnswerFile_Rule_CommandEmpty")
            .And.Contain("line 3: settings[specialize]").And.Contain("Path");
        raised.Should().Contain(nameof(WimUtilViewModel.AnswerFileBannerOpen))
            .And.Contain(nameof(WimUtilViewModel.AnswerFileBannerSeverity))
            .And.Contain(nameof(WimUtilViewModel.AnswerFileBannerTitle))
            .And.Contain(nameof(WimUtilViewModel.AnswerFileBannerSummary))
            .And.Contain(nameof(WimUtilViewModel.AnswerFileBannerMessage));
    }

    [Fact]
    public void AnswerFileBanner_WarningsOnly_OpensAtWarningSeverity()
    {
        _sut.AnswerFileCheck.LastReport = new AnswerFileReport(
            [new AnswerFileFinding(AnswerFileRule.OrderDuplicate, AnswerFileSeverity.Warning, "line 9", "5")]);

        _sut.AnswerFileBannerOpen.Should().BeTrue();
        _sut.AnswerFileBannerSeverity.Should().Be(InfoBarSeverity.Warning);
        _sut.AnswerFileBannerTitle.Should().Be("WIMUtil_AnswerFile_Verdict_MayFail");
    }

    [Fact]
    public void AnswerFileBanner_CleanReport_StaysClosed()
    {
        _sut.AnswerFileCheck.LastReport = new AnswerFileReport([ErrorFinding()]);
        _sut.AnswerFileCheck.LastReport = new AnswerFileReport([]);

        _sut.AnswerFileBannerOpen.Should().BeFalse();
        _sut.AnswerFileBannerTitle.Should().BeEmpty();
        _sut.AnswerFileBannerSummary.Should().BeEmpty();
    }

    [Fact]
    public void WorkingDirectoryChange_ClearsTheAnswerFileReport()
    {
        _sut.AnswerFileCheck.LastReport = new AnswerFileReport([ErrorFinding()]);

        _sut.Step1.WorkingDirectory = "C:\\Other";

        _sut.AnswerFileCheck.LastReport.Should().BeNull();
        _sut.AnswerFileBannerOpen.Should().BeFalse();
    }

    [Fact]
    public async Task AStepCheck_LandsOnTheBanner()
    {
        _mockTaskProgressService.Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(new CancellationTokenSource());
        _mockTaskProgressService.Setup(t => t.CurrentTaskCancellationSource).Returns(new CancellationTokenSource());
        _mockTaskProgressService.Setup(t => t.CreateDetailedProgress()).Returns(new Progress<TaskProgressDetail>());
        _mockDialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>())).ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        _mockWimCustomizationService
            .Setup(c => c.AddDriversAsync(It.IsAny<string>(), null, It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockFileSystemService.Setup(f => f.FileExists(It.Is<string>(x => x.EndsWith("autounattend.xml", StringComparison.Ordinal)))).Returns(true);
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerFileReport([ErrorFinding()]));

        await _sut.Step3.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _sut.AnswerFileBannerOpen.Should().BeTrue();
        _sut.AnswerFileBannerTitle.Should().Be("WIMUtil_AnswerFile_Verdict_WillFail");
    }
}
