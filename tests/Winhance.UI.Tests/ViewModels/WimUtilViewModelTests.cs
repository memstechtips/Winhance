using FluentAssertions;
using Microsoft.UI.Xaml.Controls;
using Moq;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Core.Features.Common.Enums;
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

        _mockLocalizationService
            .Setup(l => l.GetString("WIMUtil_Step_Header", It.IsAny<object[]>()))
            .Returns((string _, object[] args) => $"{args[0]}. {args[1]}");

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
            _mockAnswerFileValidator.Object,
            _mockAutounattendReader.Object,
            _mockApplicationModeService.Object,
            _mockBuilderModeEntry.Object,
            _mockEventBus.Object,
            _session,
            Mock.Of<ITargetWindowsService>(),
            Mock.Of<IWindowsUpdateListService>(),
            _mockUpdateStager.Object,
            _mockOfflineServicingService.Object,
            Mock.Of<IUserPreferencesService>());
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
        // Step1 sets WorkingDirectory in its constructor.
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
    public void Constructor_StepStates_CarryTheirStepNumbers()
    {
        _sut.Step1State.StepNumber.Should().Be(1);
        _sut.Step2State.StepNumber.Should().Be(2);
        _sut.Step3State.StepNumber.Should().Be(3);
        _sut.Step4State.StepNumber.Should().Be(4);
    }

    [Fact]
    public void Constructor_StepStates_CarryTheirIcons()
    {
        _sut.Step1State.Glyph.Should().Be("\uE958");
        _sut.Step2State.Icon.Should().Be(FluentIcons.Common.Icon.WrenchScrewdriver);
        _sut.Step3State.Icon.Should().Be(FluentIcons.Common.Icon.Options);
        _sut.Step4State.Icon.Should().Be(FluentIcons.Common.Icon.Rocket);
    }

    [Fact]
    public void CreatingMedia_LocksSteps2And3_AndUnlocksThemAfterwards()
    {
        _sut.Step1.IsExtractionComplete = true;
        _sut.Step2State.IsAvailable.Should().BeTrue();
        _sut.Step3State.IsAvailable.Should().BeTrue();

        _sut.Step5.IsCreatingMedia = true;

        _sut.Step2State.IsAvailable.Should().BeFalse();
        _sut.Step3State.IsAvailable.Should().BeFalse();
        var expandedBefore = _sut.Step2State.IsExpanded;
        _sut.NavigateToStepCommand.Execute("2");
        _sut.Step2State.IsExpanded.Should().Be(expandedBefore);

        _sut.Step5.IsCreatingMedia = false;

        _sut.Step2State.IsAvailable.Should().BeTrue();
        _sut.Step3State.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void StepHeaders_KeepTheNumberOutOfTheTitle()
    {
        _sut.OnNavigatedTo();

        _sut.Step1State.Title.Should().Be("WIMUtil_Step1_Title");
        _sut.Step2State.Title.Should().Be("WIMUtil_Step2_Title");
        _sut.Step3State.Title.Should().Be("WIMUtil_Step3_Title");
        _sut.Step4State.Title.Should().Be("WIMUtil_Step4_Title");
        _sut.Step3State.AccessibleName.Should().Be("3. WIMUtil_Step3_Title");
    }

    [Fact]
    public void Steps_AreTheFourStatesInOrder_AndOnlyTheLastHasNoNextStep()
    {
        _sut.Steps.Should().Equal(_sut.Step1State, _sut.Step2State, _sut.Step3State, _sut.Step4State);
        _sut.Steps.Select(s => s.HasNextStep).Should().Equal(true, true, true, false);
    }

    [Fact]
    public void SetCurrentStep_MarksExactlyOneStep_AndStartsOnStep1()
    {
        _sut.Steps.Select(s => s.IsCurrent).Should().Equal(true, false, false, false);

        _sut.SetCurrentStep(3);

        _sut.Steps.Select(s => s.IsCurrent).Should().Equal(false, false, true, false);
    }

    [Fact]
    public void ShowStep_ExpandsAnAvailableStep_AndMakesItCurrent()
    {
        _sut.Step1.IsExtractionComplete = true;
        _sut.Step3State.IsExpanded = false;

        _sut.ShowStep(3);

        _sut.Step3State.IsExpanded.Should().BeTrue();
        _sut.Step3State.IsCurrent.Should().BeTrue();
        _sut.Step1State.IsCurrent.Should().BeFalse();
    }

    [Fact]
    public void ShowStep_LeavesALockedStepShut()
    {
        _sut.Step3State.IsAvailable.Should().BeFalse();

        _sut.ShowStep(3).Should().BeFalse();

        _sut.Step3State.IsExpanded.Should().BeFalse();
        _sut.Step1State.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public void StepDoneState_FollowsTheStepsCompletion()
    {
        _sut.Steps.Select(s => s.IsComplete).Should().AllBeEquivalentTo(false);

        _sut.Step1.IsExtractionComplete = true;
        _sut.Step2.Choice = AnswerFileChoice.Builder;

        _sut.Step1State.IsComplete.Should().BeTrue();
        _sut.Step2State.IsComplete.Should().BeTrue();
        _sut.Step4State.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void StepDoneState_LockedStepsShowNoTick_EvenWithAChoiceMade()
    {
        _sut.Step2.Choice = AnswerFileChoice.Builder;
        _sut.Step4.AddUpdate = true;

        _sut.Step2State.IsComplete.Should().BeFalse();
        _sut.Step3State.IsComplete.Should().BeFalse();

        _sut.Step1.IsExtractionComplete = true;

        _sut.Step2State.IsComplete.Should().BeTrue();
        _sut.Step3State.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void NavigateToStepCommand_OpeningAStep_MakesItCurrent()
    {
        _sut.Step1.IsExtractionComplete = true;
        _sut.Step4State.IsExpanded = false;

        _sut.NavigateToStepCommand.Execute("4");

        _sut.Steps.Select(s => s.IsCurrent).Should().Equal(false, false, false, true);

        _sut.NavigateToStepCommand.Execute("4");

        _sut.Step4State.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public void UsbDestination_Step4HeaderSaysWriteToUsb()
    {
        _sut.Step1.IsExtractionComplete = true;
        _sut.Step4State.StatusText.Should().Be("WIMUtil_Status_ReadyToCreateIso");

        _sut.Step5.Destination = WimOutputDestination.Usb;

        _sut.Step4State.Title.Should().Be("WIMUtil_ButtonWriteUsb");
        _sut.Step4State.Icon.Should().Be(FluentIcons.Common.Icon.UsbStick);
        _sut.Step4State.StatusText.Should().Be("WIMUtil_Label_NoUsbSelected");

        _sut.Step5.SelectedUsbTarget = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true);

        _sut.Step4State.StatusText.Should().Be("WIMUtil_Status_ReadyToWriteUsb");

        _sut.Step5.IsIsoCreated = true;

        _sut.Step4State.StatusText.Should().Be("WIMUtil_Desc_UsbWriteSuccess");

        _sut.Step5.Destination = WimOutputDestination.Iso;

        _sut.Step4State.Title.Should().Be("WIMUtil_Step4_Title");
        _sut.Step4State.Icon.Should().Be(FluentIcons.Common.Icon.Rocket);
        _sut.Step4State.StatusText.Should().Be("WIMUtil_Status_IsoCreated");
    }

    [Fact]
    public void WriteToUsb_IsDisabledUntilADriveIsPicked()
    {
        _sut.CreateMediaCommand.CanExecute(null).Should().BeTrue();

        _sut.Step5.Destination = WimOutputDestination.Usb;

        _sut.CreateMediaCommand.CanExecute(null).Should().BeFalse();

        _sut.Step5.SelectedUsbTarget = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true);

        _sut.CreateMediaCommand.CanExecute(null).Should().BeTrue();
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
    public void ShowBuildDetailsCommand_ForwardsToStep5()
    {
        _sut.ShowBuildDetailsCommand.Should().BeSameAs(_sut.Step5.ShowBuildDetailsCommand);
    }

    [Fact]
    public void DeleteWorkingFolderAfterBuild_ReadsAndWritesStep5()
    {
        _sut.DeleteWorkingFolderAfterBuild = true;

        _sut.Step5.DeleteWorkingFolderAfterBuild.Should().BeTrue();
        _sut.BuildCards.Should().BeSameAs(_sut.Step5.BuildCards);
    }

    [Fact]
    public void CreateMediaCommand_ForwardsToStep5()
    {
        _sut.CreateMediaCommand.Should().BeSameAs(_sut.Step5.CreateMediaCommand);
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

    private void ArrangeAutounattendSession()
    {
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _mockApplicationModeService.Setup(m => m.CurrentBuilderTarget).Returns(BuilderTarget.Autounattend);
    }

    [Fact]
    public void ModeTags_FollowTheServicingChoice()
    {
        _sut.ModeTagText.Should().Be("WIMUtil_Tag_DuringSetup");

        _sut.SelectBuiltIntoImage();

        _sut.ModeTagText.Should().Be("WIMUtil_Tag_InImage");
        _sut.InImageTagText.Should().Be("WIMUtil_Tag_InImage");
        _sut.DuringSetupTagText.Should().Be("WIMUtil_Tag_DuringSetup");

        _sut.SelectDuringSetup();

        _sut.ModeTagText.Should().Be("WIMUtil_Tag_DuringSetup");
    }

    [Fact]
    public void WithoutASession_NoAnswerFileIsChosenAndStepThreeShowsNothingForIt()
    {
        _sut.Step2.Choice.Should().Be(AnswerFileChoice.None);
        _sut.ShowsSummaryCards.Should().BeFalse();
        _sut.ShowsAnswerFileCard.Should().BeFalse();
    }

    [Fact]
    public void InASession_TheBuilderIsChosenAndStepThreeShowsTheSummaryCardsAndTheAnswerFileCard()
    {
        ArrangeAutounattendSession();
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);

        _sut.Step2.Choice.Should().Be(AnswerFileChoice.Builder);
        _sut.ShowsSummaryCards.Should().BeTrue();
        _sut.ShowsAnswerFileCard.Should().BeTrue();
    }

    [Fact]
    public void WithTheUsersOwnFile_StepThreeNamesItAndHidesTheSummaryCards()
    {
        var file = Path.Combine(Path.GetTempPath(), "mine.xml");
        _sut.Step2.SelectedXmlPath = file;
        _sut.Step2.Choice = AnswerFileChoice.OwnXml;

        _sut.ShowsSummaryCards.Should().BeFalse();
        _sut.ShowsAnswerFileCard.Should().BeTrue();
        _sut.AnswerFileCardTitle.Should().Be("mine.xml");
        _sut.AnswerFileCardDescription.Should().Be("WIMUtil_Step3_AnswerFile_OwnFile");
    }

    [Fact]
    public void TheCleanupCard_AppearsOnlyWhenBuiltIntoTheImage()
    {
        _sut.ShowsCleanupCard.Should().BeFalse();

        _sut.SelectBuiltIntoImage();
        _sut.ShowsCleanupCard.Should().BeTrue();

        _sut.SelectDuringSetup();
        _sut.ShowsCleanupCard.Should().BeFalse();
    }

    [Fact]
    public void CleanupOptions_AreNoneDuringSetupAndTheTickedBoxesInTheImage()
    {
        _sut.CleanupOptions.Should().Be(ImageCleanupOptions.None);

        _sut.SelectBuiltIntoImage();
        _sut.CleanupOptions.Should().Be(new ImageCleanupOptions(true, true, true, true, true));

        _sut.CleanupResetBase = false;
        _sut.CleanupCrashReports = false;

        _sut.CleanupOptions.Should().Be(new ImageCleanupOptions(false, true, true, true, false));
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
    public void Step3Header_SaysSoWhenNothingWasAdded()
    {
        _sut.Step1.IsExtractionComplete = true;

        _sut.Step3State.IsAvailable.Should().BeTrue();
        _sut.Step3State.IsComplete.Should().BeFalse();
        _sut.Step3State.StatusText.Should().Be("WIMUtil_Status_NoChangesAdded");
    }

    [Fact]
    public void Step3Header_ShowsUpdateAddedOnceTheUpdateIsChosen()
    {
        _sut.Step1.IsExtractionComplete = true;

        _sut.Step4.AddUpdate = true;

        _sut.Step3State.IsComplete.Should().BeTrue();
        _sut.Step3State.StatusText.Should().Be("WIMUtil_Status_UpdateAdded");
    }

    [Fact]
    public void Step3Header_JoinsDriversAndUpdate()
    {
        _sut.Step1.IsExtractionComplete = true;

        _sut.Step3.AddSystemDrivers = true;
        _sut.Step4.AddUpdate = true;

        _sut.Step3State.IsComplete.Should().BeTrue();
        _sut.Step3State.StatusText.Should().Contain("WIMUtil_Status_UpdateAdded").And.Contain(", ");
    }

    [Fact]
    public void CreatingMedia_WhenItEnds_ReadsTheImageAgainForTheConvertCard()
    {
        _mockWimImageService
            .Setup(w => w.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ReturnsAsync(new ImageDetectionResult());
        _sut.Step5.IsCreatingMedia = true;
        _mockWimImageService.Invocations.Clear();

        _sut.Step5.IsCreatingMedia = false;

        _mockWimImageService.Verify(w => w.DetectAllImageFormatsAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Step2Header_ShowsTheStep2StatusOnceAnXmlIsChosen()
    {
        ArrangeXmlAdded();

        _sut.Step2State.StatusText.Should().Be("WIMUtil_Status_XmlSelectSuccess");
    }

    [Fact]
    public void Step2Header_WithoutAnXml_SaysSo()
    {
        _sut.Step1.IsExtractionComplete = true;

        _sut.Step2State.IsComplete.Should().BeFalse();
        _sut.Step2State.StatusText.Should().Be("WIMUtil_Status_NoXmlAdded");
    }

    [Fact]
    public void Step2Header_NamesTheWinhanceAnswerFile()
    {
        _sut.Step1.IsExtractionComplete = true;

        _sut.Step2.Choice = AnswerFileChoice.Builder;

        _sut.Step2State.IsComplete.Should().BeTrue();
        _sut.Step2State.StatusText.Should().Be("WIMUtil_Status_AnswerFileBuilder");
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

        _sut.Step2State.StatusText.Should().Be("WIMUtil_Status_XmlSelectSuccess");
        _sut.AnswerFileBannerOpen.Should().BeTrue();
        _sut.AnswerFileBannerTitle.Should().Be("WIMUtil_AnswerFile_Verdict_WillFail");
    }

    [Fact]
    public void Step2Header_ClearedReport_ReturnsToTheStep2Status()
    {
        ArrangeXmlAdded();
        _sut.AnswerFileCheck.Publish(MediaCopy, new AnswerFileReport([ErrorFinding()]));

        _sut.AnswerFileCheck.Publish(null, null);

        _sut.Step2State.StatusText.Should().Be("WIMUtil_Status_XmlSelectSuccess");
    }

    private void ArrangeXmlAdded()
    {
        _sut.Step1.IsExtractionComplete = true;
        _sut.Step2.Choice = AnswerFileChoice.OwnXml;
        _sut.Step2.XmlStatus = "WIMUtil_Status_XmlSelectSuccess";
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
    public void SelectBuiltIntoImage_ForwardsToImageFormatAndRaisesTheModeProperties()
    {
        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.SelectBuiltIntoImage();

        _sut.ImageFormat.IsBuiltIntoImage.Should().BeTrue();
        _sut.IsBuiltIntoImage.Should().BeTrue();
        _sut.IsDuringSetup.Should().BeFalse();
        raised.Should().Contain(nameof(WimUtilViewModel.IsBuiltIntoImage))
            .And.Contain(nameof(WimUtilViewModel.IsDuringSetup))
;
    }

    [Fact]
    public void SelectDuringSetup_GoesBackFromBuiltIntoImage()
    {
        _sut.SelectBuiltIntoImage();

        _sut.SelectDuringSetup();

        _sut.IsBuiltIntoImage.Should().BeFalse();
        _sut.IsDuringSetup.Should().BeTrue();
    }

    [Fact]
    public void SelectingAMode_WhileTheChoiceIsLocked_ChangesNothing()
    {
        _sut.Step5.IsCreatingMedia = true;

        _sut.SelectBuiltIntoImage();

        _sut.IsBuiltIntoImage.Should().BeFalse();
    }

    [Fact]
    public void ImageMode_ShowsTheEditionCardOnlyBuiltIn()
    {
        _sut.ImageFormat.ShowConversionCard = true;
        _sut.ShowEditionCard.Should().BeFalse();

        _sut.SelectBuiltIntoImage();

        _sut.ShowEditionCard.Should().BeTrue();
        _sut.EditionCardDescription.Should().Be("WIMUtil_Card_Edition_Description");
    }

    [Fact]
    public void ImageMode_PushesTheFlagToSteps2To5AndTheSession()
    {
        _session.BuiltIntoImage.Should().BeFalse();

        _sut.SelectBuiltIntoImage();

        _sut.Step3.IsBuiltIntoImage.Should().BeTrue();
        _sut.Step4.IsBuiltIntoImage.Should().BeTrue();
        _sut.Step5.IsBuiltIntoImage.Should().BeTrue();
        _session.BuiltIntoImage.Should().BeTrue();

        _sut.SelectDuringSetup();

        _sut.Step5.IsBuiltIntoImage.Should().BeFalse();
        _session.BuiltIntoImage.Should().BeFalse();
    }

    [Fact]
    public void ImageMode_Step3HeaderSaysTheImageBuildsThemIn()
    {
        _sut.Step1.IsExtractionComplete = true;
        _sut.Step3.AddSystemDrivers = true;
        _sut.Step4.AddUpdate = true;

        _sut.SelectBuiltIntoImage();

        _sut.Step3State.StatusText.Should().Contain("WIMUtil_Status_DriversReadyForImage")
            .And.Contain("WIMUtil_Status_UpdateReadyForImage");
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
    public void LanguageChanged_RaisesTheServicingWarningTexts()
    {
        var changed = new List<string?>();
        _sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _mockLocalizationService.Raise(l => l.LanguageChanged += null, this, EventArgs.Empty);

        changed.Should().Contain(new[]
        {
            nameof(_sut.MountFolderWarningText),
            nameof(_sut.ShowUsbHardDrivesText),
            nameof(_sut.ShowUsbHardDrivesWarningText),
            nameof(_sut.DuringSetupTitle),
            nameof(_sut.BuiltIntoImageTitle),
            nameof(_sut.YourImageHeadingText),
            nameof(_sut.AnswerFileChoiceTitle),
            nameof(_sut.AddSystemDriversText),
            nameof(_sut.AddUpdateText),
            nameof(_sut.FormatCardTitle),
        });
    }

    [Fact]
    public void FormatCard_SaysWhichFormatTheImageIsAndHowBig()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("WIMUtil_Format_Current", It.IsAny<object[]>()))
            .Returns((string _, object[] args) => $"{args[0]} ({args[1]})");
        const long size = 4_294_967_296L;

        _sut.ImageFormat.CurrentImageFormat = new ImageFormatInfo { Format = ImageFormat.Esd, FileSizeBytes = size };

        _sut.FormatCardDescription.Should().Be(
            $"ESD ({WimImageFormatViewModel.FormatFileSize(size)}) WIMUtil_Format_Description");
    }

    [Fact]
    public void FormatCard_WithBothFormatsOnTheMedia_NamesNoCurrentFormat()
    {
        _sut.ImageFormat.CurrentImageFormat = new ImageFormatInfo { Format = ImageFormat.Wim, FileSizeBytes = 1_000L };
        _sut.ImageFormat.BothFormatsExist = true;

        _sut.FormatCardDescription.Should().Be("WIMUtil_Format_Description");
    }

    [Fact]
    public void FormatCard_UpdatesWhenTheImageChanges()
    {
        var changed = new List<string?>();
        _sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _sut.ImageFormat.CurrentImageFormat = new ImageFormatInfo { Format = ImageFormat.Wim, FileSizeBytes = 1_000L };

        changed.Should().Contain(nameof(_sut.FormatCardDescription));
    }

    [Theory]
    [InlineData(ServicedItemOutcome.Added, false)]
    [InlineData(ServicedItemOutcome.FellBackToSetup, true)]
    public async Task ImageServiced_StepsThreeAndFourShowWhatIsStillOnTheMedia(ServicedItemOutcome drivers, bool driversStillOnMedia)
    {
        const string workDir = "C:\\WorkDir";
        _sut.Step1.WorkingDirectory = workDir;
        _mockWimCustomizationService.Setup(w => w.AreDriversOnMedia(workDir)).Returns(true);
        _sut.Step3.PickUpMedia();
        _mockWimCustomizationService.Setup(w => w.AreDriversOnMedia(workDir)).Returns(driversStillOnMedia);
        _sut.Step4.AddUpdate = true;
        _sut.Step5.IsBuiltIntoImage = true;
        _sut.Step5.OutputIsoPath = "D:\\Output\\test.iso";
        _mockTaskProgressService.Setup(t => t.CurrentTaskCancellationSource).Returns(new CancellationTokenSource());
        _mockTaskProgressService.Setup(t => t.CreateDetailedProgress()).Returns(new Progress<TaskProgressDetail>());
        _mockOfflineServicingService.Setup(o => o.HasStagedItems(workDir)).Returns(true);
        _mockOfflineServicingService
            .Setup(o => o.ServiceAsync(It.Is<OfflineBuildRequest>(r => r.WorkingDirectory == workDir), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<IProgress<ServicingPhaseUpdate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfflineServicingResult(drivers, ServicedItemOutcome.Added));
        _mockOfflineServicingService.Setup(o => o.PhasesFor(It.IsAny<OfflineBuildRequest>())).Returns([ServicingPhase.Mount]);
        _mockUpdateStager.Setup(s => s.IsStaged(workDir)).Returns(false);
        _mockIsoService
            .Setup(i => i.CreateIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BootFileReplacements>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.CreateMediaCommand.ExecuteAsync(null);

        _sut.Step3.HasDriverChoice.Should().Be(driversStillOnMedia);
        _sut.Step4.AddUpdate.Should().BeFalse();
    }
}
