using FluentAssertions;
using Microsoft.UI.Xaml.Controls;
using Moq;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.WimUtil.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.ViewModels;

public class WimUtilViewModelTests : IDisposable
{
    private readonly Mock<IIsoService> _mockIsoService = new();
    private readonly Mock<IUsbMediaWriter> _mockUsbMediaWriter = new();
    private readonly Mock<IBootManagerService> _mockBootManagerService = new();
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
    private readonly Mock<IAutounattendReader> _mockAutounattendReader = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<IApplicationModeService> _mockApplicationModeService = new();
    private readonly Mock<IBuilderModeEntry> _mockBuilderModeEntry = new();
    private readonly Mock<IWindowsUpdateStager> _mockUpdateStager = new();
    private readonly Mock<IOfflineServicingService> _mockOfflineServicingService = new();
    private readonly WimUtilSession _session = new();
    private readonly WimUtilViewModel _sut;

    // Step1 builds its working directory from the mocked temp path, and every VM joins with a backslash.
    private string MediaCopy => _sut.Step1.WorkingDirectory + "\\autounattend.xml";

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

        _mockBootManagerService
            .Setup(b => b.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BootManagerOptions(Offers2023: false, BootManager.Pca2011, IsOutdated: false));

        _sut = new WimUtilViewModel(
            _mockIsoService.Object,
            _mockUsbMediaWriter.Object,
            _mockBootManagerService.Object,
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
            _mockAnswerFileValidator.Object,
            _mockAutounattendReader.Object,
            _mockApplicationModeService.Object,
            _mockBuilderModeEntry.Object,
            _mockEventBus.Object,
            _session,
            Mock.Of<ITargetWindowsService>(),
            Mock.Of<IWindowsUpdateListService>(),
            _mockUpdateStager.Object,
            _mockOfflineServicingService.Object);
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
        _sut.Step5.Should().NotBeNull();
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
        _sut.Step5.WorkingDirectory.Should().Be(expected);
    }

    [Fact]
    public void Constructor_InitializesAllStepStates()
    {
        _sut.Step1State.Should().NotBeNull();
        _sut.Step2State.Should().NotBeNull();
        _sut.Step3State.Should().NotBeNull();
        _sut.Step4State.Should().NotBeNull();
        _sut.Step5State.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_Step1State_IsExpandedAndAvailable()
    {
        _sut.Step1State.IsExpanded.Should().BeTrue();
        _sut.Step1State.IsAvailable.Should().BeTrue();
        _sut.Step1State.StepNumber.Should().Be(1);
    }

    [Fact]
    public void Constructor_Step2Through5_AreNotExpandedOrAvailable()
    {
        _sut.Step2State.IsExpanded.Should().BeFalse();
        _sut.Step2State.IsAvailable.Should().BeFalse();

        _sut.Step3State.IsExpanded.Should().BeFalse();
        _sut.Step3State.IsAvailable.Should().BeFalse();

        _sut.Step4State.IsExpanded.Should().BeFalse();
        _sut.Step4State.IsAvailable.Should().BeFalse();

        _sut.Step5State.IsExpanded.Should().BeFalse();
        _sut.Step5State.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void Constructor_Step4AndStep5States_CarryTheirStepNumbers()
    {
        _sut.Step4State.StepNumber.Should().Be(4);
        _sut.Step5State.StepNumber.Should().Be(5);
    }

    [Fact]
    public void Title_ReturnsLocalizationStringForWimUtilTitle()
    {
        _sut.Title.Should().Be("WIMUtil_Title");
    }

    [Fact]
    public void PageDescription_KeyMissing_ReturnsFallback()
    {
        _mockLocalizationService.MissingKey("WIMUtil_Subtitle");

        _sut.PageDescription.Should().Be("Create Custom Windows Installation Media");
    }

    [Fact]
    public void PageDescription_KeyStubbed_ReturnsLocalizedString()
    {
        _mockLocalizationService.PresentKey("WIMUtil_Subtitle", "Stubbed page description");

        _sut.PageDescription.Should().Be("Stubbed page description");
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
    public void WorkingDirectory_ForwardsToStep1()
    {
        _sut.WorkingDirectory.Should().Be(_sut.Step1.WorkingDirectory);
    }

    [Fact]
    public void GenerateWinhanceXmlCommand_ForwardsToStep2()
    {
        _sut.GenerateWinhanceXmlCommand.Should().BeSameAs(_sut.Step2.GenerateWinhanceXmlCommand);
    }

    [Fact]
    public void CreateMediaCommand_ForwardsToStep5()
    {
        _sut.CreateMediaCommand.Should().BeSameAs(_sut.Step5.CreateMediaCommand);
    }

    [Fact]
    public void AddWindowsUpdateCard_ForwardsToStep4()
    {
        _sut.AddWindowsUpdateCard.Should().BeSameAs(_sut.Step4.AddWindowsUpdateCard);
    }

    [Fact]
    public void WhenStep1WorkingDirectoryChanges_PropagatesWorkingDirectoryToAllSubViewModels()
    {
        _sut.Step1.WorkingDirectory = "C:\\NewWorkDir";

        _sut.ImageFormat.WorkingDirectory.Should().Be("C:\\NewWorkDir");
        _sut.Step2.WorkingDirectory.Should().Be("C:\\NewWorkDir");
        _sut.Step3.WorkingDirectory.Should().Be("C:\\NewWorkDir");
        _sut.Step4.WorkingDirectory.Should().Be("C:\\NewWorkDir");
        _sut.Step5.WorkingDirectory.Should().Be("C:\\NewWorkDir");
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
    public void WhenTheModeChanges_RaisesIsAutounattendSessionOnParent()
    {
        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);

        raised.Should().Contain(nameof(WimUtilViewModel.IsAutounattendSession));
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
    public void RemovingEditions_LocksTheStepsThatReadTheImageUntilItEnds()
    {
        _mockWimImageService
            .Setup(w => w.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ReturnsAsync(new ImageDetectionResult());
        _sut.Step1.IsExtractionComplete = true;
        _sut.Step4State.IsExpanded = false;
        _sut.Step5State.IsExpanded = false;

        _sut.Step2.IsRemovingEditions = true;

        _sut.Step2State.IsAvailable.Should().BeFalse();
        _sut.Step3State.IsAvailable.Should().BeFalse();
        _sut.Step4State.IsAvailable.Should().BeFalse();
        _sut.Step5State.IsAvailable.Should().BeFalse();
        _sut.Step4State.StatusText.Should().Be("WIMUtil_Status_RemovingEditions");
        _sut.Step5State.StatusText.Should().Be("WIMUtil_Status_RemovingEditions");
        _sut.NavigateToStepCommand.Execute("4");
        _sut.Step4State.IsExpanded.Should().BeFalse();
        _sut.NavigateToStepCommand.Execute("5");
        _sut.Step5State.IsExpanded.Should().BeFalse();

        _sut.Step2.IsRemovingEditions = false;

        _sut.Step2State.IsAvailable.Should().BeTrue();
        _sut.Step3State.IsAvailable.Should().BeTrue();
        _sut.Step4State.IsAvailable.Should().BeTrue();
        _sut.Step5State.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void Step4Header_SaysSoWhenNoUpdateWasAdded()
    {
        _sut.Step1.IsExtractionComplete = true;

        _sut.Step4State.IsAvailable.Should().BeTrue();
        _sut.Step4State.StatusText.Should().Be("WIMUtil_Status_NoUpdateAdded");
    }

    [Fact]
    public void Step4Header_ShowsUpdateAddedOnceTheUpdateIsStaged()
    {
        _sut.Step1.IsExtractionComplete = true;

        _sut.Step4.IsUpdateAdded = true;

        _sut.Step4State.IsComplete.Should().BeTrue();
        _sut.Step4State.StatusText.Should().Be("WIMUtil_Status_UpdateAdded");
    }

    [Fact]
    public void RemovingEditions_LocksStep1AndItsImageActionsUntilItEnds()
    {
        _mockWimImageService
            .Setup(w => w.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ReturnsAsync(new ImageDetectionResult
            {
                WimInfo = new ImageFormatInfo { Format = ImageFormat.Wim, FileSizeBytes = 4_000_000_000L }
            });
        _sut.Step1.IsExtractionComplete = true;
        _sut.ConvertImageCard.IsEnabled.Should().BeTrue();

        _sut.Step2.IsRemovingEditions = true;

        _sut.Step1State.IsAvailable.Should().BeFalse();
        _sut.Step1State.StatusText.Should().Be("WIMUtil_Status_RemovingEditions");
        _sut.ConvertImageCard.IsEnabled.Should().BeFalse();
        _sut.NavigateToStepCommand.Execute("1");
        _sut.Step1State.IsExpanded.Should().BeTrue();

        _sut.Step2.IsRemovingEditions = false;

        _sut.Step1State.IsAvailable.Should().BeTrue();
        _sut.ConvertImageCard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void RemovingEditions_WhenItEnds_ReadsTheImageAgainForTheConvertCard()
    {
        _mockWimImageService
            .Setup(w => w.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ReturnsAsync(new ImageDetectionResult());
        _sut.Step2.IsRemovingEditions = true;
        _mockWimImageService.Invocations.Clear();

        _sut.Step2.IsRemovingEditions = false;

        _mockWimImageService.Verify(w => w.DetectAllImageFormatsAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Step2Header_ShowsTheStep2StatusOnceAnXmlIsAdded()
    {
        ArrangeXmlAdded();

        _sut.Step2State.StatusText.Should().Be("WIMUtil_Status_XmlGenSuccess");
    }

    [Fact]
    public void Step2Header_WithoutAnXml_SaysSo()
    {
        _sut.Step1.IsExtractionComplete = true;
        _sut.Step2.XmlStatus = "WIMUtil_Status_XmlGenSuccess";

        _sut.Step2.IsXmlAdded = false;

        _sut.Step2State.StatusText.Should().Be("WIMUtil_Status_NoXmlAdded");
    }

    [Fact]
    public void Step2Header_ShowsTheVerdictForTheMediaCopy()
    {
        ArrangeXmlAdded();

        _sut.AnswerFileCheck.Publish(MediaCopy, new AnswerFileReport([ErrorFinding()]));

        _sut.Step2State.StatusText.Should().Be("WIMUtil_AnswerFile_Verdict_WillFail");
    }

    [Fact]
    public void Step2Header_ARejectedFileReport_StaysOnTheStep2StatusWhileTheBannerOpens()
    {
        ArrangeXmlAdded();

        _sut.AnswerFileCheck.Publish("C:\\Downloads\\broken.xml", new AnswerFileReport([ErrorFinding()]));

        _sut.Step2State.StatusText.Should().Be("WIMUtil_Status_XmlGenSuccess");
        _sut.AnswerFileBannerOpen.Should().BeTrue();
        _sut.AnswerFileBannerTitle.Should().Be("WIMUtil_AnswerFile_Verdict_WillFail");
    }

    [Fact]
    public void Step2Header_ClearedReport_ReturnsToTheStep2Status()
    {
        ArrangeXmlAdded();
        _sut.AnswerFileCheck.Publish(MediaCopy, new AnswerFileReport([ErrorFinding()]));

        _sut.AnswerFileCheck.Publish(null, null);

        _sut.Step2State.StatusText.Should().Be("WIMUtil_Status_XmlGenSuccess");
    }

    private void ArrangeXmlAdded()
    {
        _sut.Step1.IsExtractionComplete = true;
        _sut.Step2.IsXmlAdded = true;
        _sut.Step2.XmlStatus = "WIMUtil_Status_XmlGenSuccess";
    }

    private static AnswerFileFinding ErrorFinding() =>
        new(AnswerFileRule.CommandEmpty, AnswerFileSeverity.Error, "line 3: settings[specialize]", "Path");

    private static AnswerFileFinding WarningFinding() =>
        new(AnswerFileRule.OrderDuplicate, AnswerFileSeverity.Warning, "line 9", "5");

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

        _sut.AnswerFileCheck.Publish(MediaCopy, new AnswerFileReport([ErrorFinding()]));

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
        _sut.AnswerFileCheck.Publish(MediaCopy, new AnswerFileReport([WarningFinding()]));

        _sut.AnswerFileBannerOpen.Should().BeTrue();
        _sut.AnswerFileBannerSeverity.Should().Be(InfoBarSeverity.Warning);
        _sut.AnswerFileBannerTitle.Should().Be("WIMUtil_AnswerFile_Verdict_MayFail");
    }

    [Fact]
    public void AnswerFileBanner_CleanReport_StaysClosed()
    {
        _sut.AnswerFileCheck.Publish(MediaCopy, new AnswerFileReport([ErrorFinding()]));
        _sut.AnswerFileCheck.Publish(MediaCopy, new AnswerFileReport([]));

        _sut.AnswerFileBannerOpen.Should().BeFalse();
        _sut.AnswerFileBannerTitle.Should().BeEmpty();
        _sut.AnswerFileBannerSummary.Should().BeEmpty();
    }

    [Fact]
    public void WorkingDirectoryChange_ClearsTheAnswerFileReport()
    {
        _sut.AnswerFileCheck.Publish(MediaCopy, new AnswerFileReport([ErrorFinding()]));

        _sut.Step1.WorkingDirectory = "C:\\Other";

        _sut.AnswerFileCheck.LastReport.Should().BeNull();
        _sut.AnswerFileCheck.Subject.Should().BeNull();
        _sut.AnswerFileBannerOpen.Should().BeFalse();
    }

    [Fact]
    public async Task AStepCheck_LandsOnTheBanner()
    {
        ArrangeDriverRun();
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerFileReport([ErrorFinding()]));

        await _sut.Step3.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _sut.AnswerFileBannerOpen.Should().BeTrue();
        _sut.AnswerFileBannerTitle.Should().Be("WIMUtil_AnswerFile_Verdict_WillFail");
    }

    [Fact]
    public async Task AStepCheck_AlsoMovesTheStep2Header()
    {
        ArrangeDriverRun();
        ArrangeXmlAdded();
        _sut.AnswerFileCheck.Publish(MediaCopy, new AnswerFileReport([WarningFinding()]));
        _sut.Step2State.StatusText.Should().Be("WIMUtil_AnswerFile_Verdict_MayFail");
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerFileReport([ErrorFinding()]));

        await _sut.Step3.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _sut.Step2State.StatusText.Should().Be("WIMUtil_AnswerFile_Verdict_WillFail");
        _sut.AnswerFileBannerTitle.Should().Be("WIMUtil_AnswerFile_Verdict_WillFail");
    }

    private void ArrangeDriverRun()
    {
        _mockTaskProgressService.Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(new CancellationTokenSource());
        _mockTaskProgressService.Setup(t => t.CurrentTaskCancellationSource).Returns(new CancellationTokenSource());
        _mockTaskProgressService.Setup(t => t.CreateDetailedProgress()).Returns(new Progress<TaskProgressDetail>());
        _mockDialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>())).ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        _mockWimCustomizationService
            .Setup(c => c.AddDriversAsync(It.IsAny<string>(), null, It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockFileSystemService.Setup(f => f.FileExists(It.Is<string>(x => x.EndsWith("autounattend.xml", StringComparison.Ordinal)))).Returns(true);
    }

    private ServicingChoice BuiltIntoImage => _sut.ServicingChoices.Single(c => c.BuiltIntoImage);

    [Fact]
    public void SelectedServicing_ForwardsToImageFormatAndRaisesIsBuiltIntoImage()
    {
        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.SelectedServicing = BuiltIntoImage;

        _sut.ImageFormat.IsBuiltIntoImage.Should().BeTrue();
        _sut.IsBuiltIntoImage.Should().BeTrue();
        raised.Should().Contain(nameof(WimUtilViewModel.IsBuiltIntoImage))
            .And.Contain(nameof(WimUtilViewModel.ShowConvertImageCard))
            .And.Contain(nameof(WimUtilViewModel.ServicingCardDescription));
        _sut.ServicingCardDescription.Should().Be("WIMUtil_Servicing_BuiltIn_Detail");
    }

    [Fact]
    public void ImageMode_HidesTheConvertHeaderWithItsCard()
    {
        _sut.ShowOptionalConvertText.Should().BeTrue();
        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.SelectedServicing = BuiltIntoImage;

        _sut.ShowConvertImageCard.Should().BeFalse();
        _sut.ShowOptionalConvertText.Should().BeFalse();
        raised.Should().Contain(nameof(WimUtilViewModel.ShowOptionalConvertText));
    }

    [Fact]
    public void ImageMode_PushesTheFlagToSteps2To5()
    {
        _sut.SelectedServicing = BuiltIntoImage;

        _sut.Step2.IsBuiltIntoImage.Should().BeTrue();
        _sut.Step3.IsBuiltIntoImage.Should().BeTrue();
        _sut.Step4.IsBuiltIntoImage.Should().BeTrue();
        _sut.Step5.IsBuiltIntoImage.Should().BeTrue();

        _sut.SelectedServicing = _sut.ServicingChoices.Single(c => !c.BuiltIntoImage);

        _sut.Step5.IsBuiltIntoImage.Should().BeFalse();
    }

    [Fact]
    public void ImageMode_TellsStep5WhenTheImageIsReady()
    {
        _sut.SelectedServicing = BuiltIntoImage;

        _sut.ImageFormat.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            Editions = [new ImageEdition(1, "Windows 11 Pro", "Professional")],
        };

        _sut.Step5.IsReadyForServicing.Should().BeTrue();
        _sut.EditionCardDescription.Should().Be("WIMUtil_Card_Edition_Description_Ready");
    }

    [Fact]
    public void ImageMode_Step3AndStep4HeadersSayStep5BuildsThemIn()
    {
        _sut.Step1.IsExtractionComplete = true;
        _sut.Step3.AreDriversAdded = true;
        _sut.Step4.IsUpdateAdded = true;

        _sut.SelectedServicing = BuiltIntoImage;

        _sut.Step3State.StatusText.Should().Be("WIMUtil_Status_DriversReadyForImage");
        _sut.Step4State.StatusText.Should().Be("WIMUtil_Status_UpdateReadyForImage");
    }

    [Fact]
    public void RemovingEditions_LocksTheServicingChoiceUntilItEnds()
    {
        _mockWimImageService
            .Setup(w => w.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ReturnsAsync(new ImageDetectionResult());
        _sut.CanChangeServicing.Should().BeTrue();

        _sut.Step2.IsRemovingEditions = true;
        _sut.CanChangeServicing.Should().BeFalse();

        _sut.Step2.IsRemovingEditions = false;
        _sut.CanChangeServicing.Should().BeTrue();
    }

    [Fact]
    public void CreatingMedia_LocksStep1AndTheServicingChoiceUntilItEnds()
    {
        _sut.Step5.IsCreatingMedia = true;

        _sut.Step1State.IsAvailable.Should().BeFalse();
        _sut.CanChangeServicing.Should().BeFalse();
        _sut.ImageFormat.IsImageLocked.Should().BeTrue();
        _sut.NavigateToStepCommand.Execute("1");
        _sut.Step1State.IsExpanded.Should().BeTrue();

        _sut.Step5.IsCreatingMedia = false;

        _sut.Step1State.IsAvailable.Should().BeTrue();
        _sut.CanChangeServicing.Should().BeTrue();
        _sut.ImageFormat.IsImageLocked.Should().BeFalse();
    }

    [Fact]
    public void CreatingMedia_EndingDoesNotUnlockTheImageWhileEditionsAreBeingRemoved()
    {
        _sut.Step2.IsRemovingEditions = true;
        _sut.Step5.IsCreatingMedia = true;

        _sut.Step5.IsCreatingMedia = false;

        _sut.ImageFormat.IsImageLocked.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ShowServicingTimeNote_OnlyInImageModeWithSomethingStagedOnDisk(bool imageMode, bool staged, bool expected)
    {
        _mockOfflineServicingService.Setup(o => o.HasStagedItems(It.IsAny<string>())).Returns(staged);
        _sut.SelectedServicing = _sut.ServicingChoices.Single(c => c.BuiltIntoImage == imageMode);

        _sut.ShowServicingTimeNote.Should().Be(expected);
    }

    [Fact]
    public void ShowServicingTimeNote_HeldFlagsWithNothingStaged_StaysHidden()
    {
        _mockOfflineServicingService.Setup(o => o.HasStagedItems(It.IsAny<string>())).Returns(false);
        _sut.SelectedServicing = _sut.ServicingChoices.Single(c => c.BuiltIntoImage);
        _sut.Step3.AreDriversAdded = true;
        _sut.Step4.IsUpdateAdded = true;

        _sut.ShowServicingTimeNote.Should().BeFalse();
    }

    [Fact]
    public void ShowServicingTimeNote_ReopenedMediaAlreadyHoldingDrivers_Shows()
    {
        _mockOfflineServicingService.Setup(o => o.HasStagedItems(It.IsAny<string>())).Returns(true);
        _sut.SelectedServicing = _sut.ServicingChoices.Single(c => c.BuiltIntoImage);

        _sut.Step3.AreDriversAdded.Should().BeFalse();
        _sut.ShowServicingTimeNote.Should().BeTrue();
    }

    [Fact]
    public void LanguageChanged_RaisesTheServicingWarningTexts()
    {
        var changed = new List<string?>();
        _sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _mockLocalizationService.Raise(l => l.LanguageChanged += null, this, EventArgs.Empty);

        changed.Should().Contain(new[]
        {
            nameof(_sut.HostDismWarningText),
            nameof(_sut.MountFolderWarningText),
            nameof(_sut.ShowUsbHardDrivesText),
            nameof(_sut.ShowUsbHardDrivesWarningText),
        });
    }

    [Theory]
    [InlineData(ServicedItemOutcome.Added, false)]
    [InlineData(ServicedItemOutcome.FellBackToSetup, true)]
    public async Task ImageServiced_StepsThreeAndFourShowWhatIsStillOnTheMedia(ServicedItemOutcome drivers, bool driversStillOnMedia)
    {
        const string workDir = "C:\\WorkDir";
        _sut.Step1.WorkingDirectory = workDir;
        _mockWimCustomizationService.Setup(w => w.AreDriversOnMedia(workDir)).Returns(driversStillOnMedia);
        _sut.Step3.AreDriversAdded = true;
        _sut.Step4.IsUpdateAdded = true;
        _sut.Step5.IsBuiltIntoImage = true;
        _sut.Step5.IsReadyForServicing = true;
        _sut.Step5.OutputIsoPath = "D:\\Output\\test.iso";
        _mockTaskProgressService.Setup(t => t.CurrentTaskCancellationSource).Returns(new CancellationTokenSource());
        _mockTaskProgressService.Setup(t => t.CreateDetailedProgress()).Returns(new Progress<TaskProgressDetail>());
        _mockOfflineServicingService.Setup(o => o.HasStagedItems(workDir)).Returns(true);
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync(workDir, It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfflineServicingResult(drivers, ServicedItemOutcome.Added));
        _mockUpdateStager.Setup(s => s.IsStaged(workDir)).Returns(false);
        _mockIsoService
            .Setup(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.Step3.AreDriversAdded.Should().Be(driversStillOnMedia);
        _sut.Step4.IsUpdateAdded.Should().BeFalse();
    }
}
