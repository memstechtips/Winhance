using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.WimUtil.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WimStep2XmlViewModelTests : IDisposable
{
    private const string GeneratedXmlPath = "C:\\WorkDir\\autounattend.xml";

    private readonly Mock<ISelectionSaveService> _mockSaves = new();
    private readonly Mock<IWimCustomizationService> _mockWimCustomizationService = new();
    private readonly Mock<ISelectionSetBuilder> _mockSelections = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IResourceService> _mockResourceService = new();
    private readonly Mock<IAnswerFileValidator> _mockAnswerFileValidator = new();
    private readonly Mock<IAutounattendReader> _mockAutounattendReader = new();
    private readonly Mock<IApplicationModeService> _mockApplicationModeService = new();
    private readonly Mock<IBuilderModeEntry> _mockBuilderModeEntry = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly AnswerFileCheckState _checkState = new();
    private readonly Mock<IWimImageService> _mockWimImageService = new();
    private readonly Mock<ITargetWindowsService> _mockTargetWindows = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly CancellationTokenSource _removalCancellation = new();
    private readonly WimUtilSession _session = new();

    private readonly WimStep2XmlViewModel _sut;

    public WimStep2XmlViewModelTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] _) => key);
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerFileReport([]));

        _mockFileSystemService
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));

        _mockFileSystemService
            .Setup(f => f.GetFileName(It.IsAny<string>()))
            .Returns((string path) => System.IO.Path.GetFileName(path));

        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(a => a().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        // Default: a set carrying a Windows app so generate doesn't show the no-apps warning
        _mockSelections
            .Setup(s => s.FromBuilderSessionAsync())
            .ReturnsAsync(new SelectionSet(
                Array.Empty<SettingChoice>(),
                [new AppChoice("test", "Test", null, null, null, null)],
                Array.Empty<AppChoice>()));

        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync(new SelectionSaveResult(GeneratedXmlPath, []));

        // Generate names what it is about to do and waits for a yes; tests about the generation itself
        // answer it here.
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        _mockDialogService
            .Setup(d => d.ShowSummaryAsync(It.IsAny<SummaryRequest>()))
            .ReturnsAsync(true);

        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(_removalCancellation);

        _sut = new WimStep2XmlViewModel(
            _mockSaves.Object,
            _mockWimCustomizationService.Object,
            _mockSelections.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object,
            _mockResourceService.Object,
            _mockAnswerFileValidator.Object,
            _mockAutounattendReader.Object,
            _mockApplicationModeService.Object,
            _mockBuilderModeEntry.Object,
            _mockDispatcherService.Object,
            _mockEventBus.Object,
            _checkState,
            _session,
            _mockWimImageService.Object,
            _mockTargetWindows.Object,
            _mockTaskProgressService.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _removalCancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    private static List<SummaryRow> Rows(SummaryRequest request) => request.Sections.SelectMany(s => s.Rows).ToList();

    private static List<string> RowText(SummaryRequest request) =>
        Rows(request).SelectMany(r => new[] { r.Title, r.Detail, r.Code }).OfType<string>().ToList();

    private void ArrangeAutounattendSession()
    {
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _mockApplicationModeService.Setup(m => m.CurrentBuilderTarget).Returns(BuilderTarget.Autounattend);
    }

    [Fact]
    public void Constructor_InitializesSelectedXmlPathToEmpty()
    {
        _sut.SelectedXmlPath.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_InitializesXmlStatusFromLocalization()
    {
        _sut.XmlStatus.Should().Be("WIMUtil_Status_NoXmlAdded");
    }

    [Fact]
    public void Constructor_InitializesIsXmlAddedToFalse()
    {
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InitializesActionCards()
    {
        _sut.GenerateWinhanceXmlCard.Should().NotBeNull();
        _sut.DownloadXmlCard.Should().NotBeNull();
        _sut.SelectXmlCard.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_AllActionCardsAreEnabled()
    {
        _sut.GenerateWinhanceXmlCard.IsEnabled.Should().BeTrue();
        _sut.DownloadXmlCard.IsEnabled.Should().BeTrue();
        _sut.SelectXmlCard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WorkingDirectoryDefaultsToEmpty()
    {
        _sut.WorkingDirectory.Should().BeEmpty();
    }

    // The empty-WorkingDirectory guard (issue #506).

    [Fact]
    public async Task DownloadUnattendedWinstallXmlCommand_WhenWorkingDirectoryEmpty_ShowsWarningAndReturns()
    {
        await _sut.DownloadUnattendedWinstallXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            "WIMUtil_Msg_WorkingDirectoryRequired",
            It.IsAny<string>()), Times.Once);
        _mockWimCustomizationService.Verify(s => s.DownloadUnattendedWinstallXmlAsync(
            It.IsAny<string>(),
            It.IsAny<IProgress<TaskProgressDetail>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WhenWorkingDirectoryEmpty_ShowsWarningAndReturns()
    {
        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            "WIMUtil_Msg_WorkingDirectoryRequired",
            It.IsAny<string>()), Times.Once);
        _mockSaves.Verify(s => s.SaveAsync(
            It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()), Times.Never);
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public async Task SelectXmlFileCommand_WhenWorkingDirectoryEmpty_ShowsWarningAndReturns()
    {
        await _sut.SelectXmlFileCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            "WIMUtil_Msg_WorkingDirectoryRequired",
            It.IsAny<string>()), Times.Once);
        _mockWimCustomizationService.Verify(s => s.AddXmlToImageAsync(
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_OnSuccess_SetsIsXmlAdded()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeTrue();
        _sut.SelectedXmlPath.Should().Be(GeneratedXmlPath);
        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WritesIntoTheWorkingDirectory_WithoutASuccessDialog()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockSaves.Verify(s => s.SaveAsync(
            BuilderTarget.Autounattend,
            It.IsAny<SelectionSet>(),
            It.Is<SelectionSaveOptions>(o => o.FixedPath == GeneratedXmlPath && !o.ReportSuccessInDialog)),
            Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_LeavesTheDriverStepToTheSaveService()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeTrue();
        _mockWimCustomizationService.Verify(
            s => s.EnsureDriverInstallStepAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockWimCustomizationService.Verify(
            s => s.EnsureDriverInstallStepAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WhenNothingWasSaved_LeavesTheCardIncomplete()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync(SelectionSaveResult.Nothing);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeFalse();
        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_NamesWhatItWillDoBeforeItStarts()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Confirm_Title"
            && r.ConfirmButtonText == "Button_Continue"
            && RowText(r).Contains("WIMUtil_Generate_Plan_Write")
            && RowText(r).Contains("C:\\WorkDir\\autounattend.xml")
            && RowText(r).Contains("WIMUtil_Generate_Plan_Check")
            && r.Verdict == null)), Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_NoWindowsApps_SaysSoInThePlanInsteadOfAskingAgain()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        _mockSelections.Setup(s => s.FromBuilderSessionAsync()).ReturnsAsync(SelectionSet.Empty);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Verdict!.Tone == SummaryTone.Warning && r.Verdict.Title == "WIMUtil_Generate_Plan_NoWindowsApps")), Times.Once);
        _mockSaves.Verify(s => s.SaveAsync(
            BuilderTarget.Autounattend, It.IsAny<SelectionSet>(), It.Is<SelectionSaveOptions>(o => !o.ReportSuccessInDialog)), Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WhenDeclined_WritesNothing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        _mockDialogService
            .Setup(d => d.ShowSummaryAsync(It.IsAny<SummaryRequest>()))
            .ReturnsAsync(false);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockSaves.Verify(s => s.SaveAsync(
            It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()), Times.Never);
        _sut.IsXmlAdded.Should().BeFalse();
        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_OnSuccess_SummarisesTheFileTheAppsAndTheCheck()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync(new SelectionSaveResult(
                GeneratedXmlPath,
                [new StagedApp("Brave", true), new StagedApp("7-Zip", false)]));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Summary_Title"
            && r.ConfirmButtonText == ""
            && Rows(r).Any(row => row.Title == "Brave" && row.Tone == SummaryTone.Success)
            && Rows(r).Any(row => row.Title == "7-Zip" && row.Tone == SummaryTone.Warning)
            && r.Verdict!.Tone == SummaryTone.Success
            && r.Verdict.Title == "WIMUtil_AnswerFile_Verdict_Clean"
            && r.CopyText!.Contains(GeneratedXmlPath))), Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WhenTheCheckThrows_SaysSoInTheSummary()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Summary_Title"
            && r.Verdict!.Tone == SummaryTone.Warning
            && r.Verdict.Title == "WIMUtil_Generate_Summary_CheckFailed")), Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_OnSuccess_ClearsOtherCardCompletions()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _sut.DownloadXmlCard.IsComplete = true;
        _sut.SelectXmlCard.IsComplete = true;

        ArrangeAutounattendSession();

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.DownloadXmlCard.IsComplete.Should().BeFalse();
        _sut.SelectXmlCard.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_OnException_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();

        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ThrowsAsync(new Exception("Generation failed"));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.GenerateWinhanceXmlCard.HasFailed.Should().BeTrue();
        _mockDialogService.Verify(d => d.ShowErrorAsync(
            "WIMUtil_Msg_XmlGenError", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task StartAutounattendSessionCommand_FromNormal_EntersBuilderWithTheAutounattendTarget()
    {
        await _sut.StartAutounattendSessionCommand.ExecuteAsync(null);

        _mockBuilderModeEntry.Verify(e => e.EnterAsync(BuilderTarget.Autounattend), Times.Once);
        _mockApplicationModeService.Verify(m => m.SetBuilderTarget(It.IsAny<BuilderTarget>()), Times.Never);
    }

    [Fact]
    public async Task StartAutounattendSessionCommand_AlreadyInBuilder_SwitchesTheTargetAndKeepsTheSession()
    {
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);

        await _sut.StartAutounattendSessionCommand.ExecuteAsync(null);

        _mockApplicationModeService.Verify(m => m.SetBuilderTarget(BuilderTarget.Autounattend), Times.Once);
        _mockBuilderModeEntry.Verify(e => e.EnterAsync(It.IsAny<BuilderTarget>()), Times.Never);
    }

    [Fact]
    public async Task StartAutounattendSessionCommand_TargetsTheMediasBuild()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockWimImageService.Setup(w => w.DetectImageFormatAsync("C:\\WorkDir")).ReturnsAsync(new ImageFormatInfo { Build = 26300 });
        _mockBuilderModeEntry.Setup(e => e.EnterAsync(BuilderTarget.Autounattend)).ReturnsAsync(true);

        await _sut.StartAutounattendSessionCommand.ExecuteAsync(null);

        _mockTargetWindows.Verify(t => t.SetAsync(26300), Times.Once);
    }

    [Fact]
    public async Task StartAutounattendSessionCommand_WithNoReadableMedia_KeepsTheTarget()
    {
        _mockBuilderModeEntry.Setup(e => e.EnterAsync(BuilderTarget.Autounattend)).ReturnsAsync(true);

        await _sut.StartAutounattendSessionCommand.ExecuteAsync(null);

        _mockTargetWindows.Verify(t => t.SetAsync(It.IsAny<int?>()), Times.Never);
    }

    private void ArrangeLangIni(string contents)
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.FileExists("C:\\WorkDir\\sources\\lang.ini")).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllTextAsync("C:\\WorkDir\\sources\\lang.ini", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contents);
        _mockBuilderModeEntry.Setup(e => e.EnterAsync(BuilderTarget.Autounattend)).ReturnsAsync(true);
    }

    [Fact]
    public async Task StartAutounattendSessionCommand_MediaWithOneLanguage_PicksItForSetup()
    {
        ArrangeLangIni("[Available UI Languages]\r\nfr-fr = 3\r\n\r\n[Fallback Languages]\r\nfr-fr = en-us\r\n");

        await _sut.StartAutounattendSessionCommand.ExecuteAsync(null);

        _mockApplicationModeService.Verify(m => m.RecordBuilderEdit(It.Is<SettingChoice>(c =>
            c.SettingId == "autounattend-setup-language"
            && c.Value is ChoiceValue.Keyed && ((ChoiceValue.Keyed)c.Value).Key == "fr-FR")), Times.Once);
        _mockEventBus.Verify(b => b.Publish(It.IsAny<BuilderSeededEvent>()), Times.Once);
    }

    [Fact]
    public async Task StartAutounattendSessionCommand_MediaWithSeveralLanguages_LeavesSetupAsking()
    {
        ArrangeLangIni("[Available UI Languages]\r\nen-us = 3\r\nde-de = 3\r\n");

        await _sut.StartAutounattendSessionCommand.ExecuteAsync(null);

        _mockApplicationModeService.Verify(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()), Times.Never);
    }

    [Fact]
    public async Task StartAutounattendSessionCommand_KeepsALanguageTheUserAlreadyPicked()
    {
        ArrangeLangIni("[Available UI Languages]\r\nen-us = 3\r\n");
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _mockApplicationModeService.Setup(m => m.GetBuilderEdit("autounattend-setup-language"))
            .Returns(new SettingChoice("autounattend-setup-language", new ChoiceValue.Keyed("de-DE", "German (Germany)")));

        await _sut.StartAutounattendSessionCommand.ExecuteAsync(null);

        _mockApplicationModeService.Verify(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()), Times.Never);
    }

    private string ArrangeSelectedFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"winhance-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, "<unattend />");
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService.Setup(p => p.PickFile(It.IsAny<string[]>(), It.IsAny<string>())).Returns(path);
        _mockWimCustomizationService.Setup(s => s.AddXmlToImageAsync(path, "C:\\WorkDir")).ReturnsAsync(OperationResult.Succeeded());
        return path;
    }

    [Fact]
    public async Task SelectXmlFileCommand_AWinhanceFileWithItsChoices_OpensThemAsASession()
    {
        var path = ArrangeSelectedFile();
        var config = new WinhanceConfigFile();
        _mockAutounattendReader.Setup(r => r.ReadAsync(path)).ReturnsAsync(new WinhanceAnswerFile("26.09.23", config));

        try
        {
            await _sut.SelectXmlFileCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(path);
        }

        _mockBuilderModeEntry.Verify(e => e.EnterFromAsync(BuilderTarget.Autounattend, config), Times.Once);
    }

    [Fact]
    public async Task SelectXmlFileCommand_DecliningToOpenTheChoices_LeavesTheModeAlone()
    {
        var path = ArrangeSelectedFile();
        _mockAutounattendReader.Setup(r => r.ReadAsync(path)).ReturnsAsync(new WinhanceAnswerFile("26.09.23", new WinhanceConfigFile()));
        _mockDialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        try
        {
            await _sut.SelectXmlFileCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(path);
        }

        _mockBuilderModeEntry.Verify(e => e.EnterFromAsync(It.IsAny<BuilderTarget>(), It.IsAny<WinhanceConfigFile>()), Times.Never);
        _sut.SelectXmlCard.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task SelectXmlFileCommand_AnOlderWinhanceFile_SaysToGenerateItAgain()
    {
        var path = ArrangeSelectedFile();
        _mockAutounattendReader.Setup(r => r.ReadAsync(path)).ReturnsAsync(new WinhanceAnswerFile("26.09.01", null));

        try
        {
            await _sut.SelectXmlFileCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(path);
        }

        _mockDialogService.Verify(d => d.ShowInformationAsync("WIMUtil_OpenWinhanceFile_NoChoices", "WIMUtil_OpenWinhanceFile_Title", It.IsAny<string>()), Times.Once);
        _mockBuilderModeEntry.Verify(e => e.EnterFromAsync(It.IsAny<BuilderTarget>(), It.IsAny<WinhanceConfigFile>()), Times.Never);
    }

    [Fact]
    public async Task SelectXmlFileCommand_AFileWinhanceDidNotMake_AsksNothing()
    {
        var path = ArrangeSelectedFile();

        try
        {
            await _sut.SelectXmlFileCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(path);
        }

        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _sut.SelectXmlCard.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_AWinhanceFileOnTheMedia_OpensItsChoicesLikeAPickedOne()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        var config = new WinhanceConfigFile();
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);
        _mockAutounattendReader.Setup(r => r.ReadAsync(GeneratedXmlPath)).ReturnsAsync(new WinhanceAnswerFile("26.10.27", config));

        await _sut.PickUpMediaAnswerFileAsync();

        _sut.IsXmlAdded.Should().BeTrue();
        _sut.SelectedXmlPath.Should().Be(GeneratedXmlPath);
        _sut.SelectXmlCard.IsComplete.Should().BeTrue();
        _sut.SelectXmlCard.Description.Should().Be("WIMUtil_Card_SelectXML_OnMedia_Description");
        _mockAnswerFileValidator.Verify(v => v.ValidateAsync(GeneratedXmlPath, It.IsAny<CancellationToken>()), Times.Once);
        _mockBuilderModeEntry.Verify(e => e.EnterFromAsync(BuilderTarget.Autounattend, config), Times.Once);
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_ACustomFileOnTheMedia_KeepsItWithoutAsking()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);

        await _sut.PickUpMediaAnswerFileAsync();

        _sut.IsXmlAdded.Should().BeTrue();
        _sut.XmlStatus.Should().Be("WIMUtil_Status_XmlOnMedia");
        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_NoFileOnTheNewMedia_ClearsTheOldOne()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);
        await _sut.PickUpMediaAnswerFileAsync();
        _sut.WorkingDirectory = "D:\\Other";

        await _sut.PickUpMediaAnswerFileAsync();

        _sut.IsXmlAdded.Should().BeFalse();
        _sut.SelectedXmlPath.Should().BeEmpty();
        _sut.SelectXmlCard.IsComplete.Should().BeFalse();
        _sut.SelectXmlCard.Description.Should().Be("WIMUtil_Card_SelectXML_Description");
        _sut.XmlStatus.Should().Be("WIMUtil_Status_NoXmlAdded");
    }

    [Fact]
    public void IsAutounattendSession_FollowsModeChanged()
    {
        _sut.IsAutounattendSession.Should().BeFalse();
        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        ArrangeAutounattendSession();
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);

        _sut.IsAutounattendSession.Should().BeTrue();
        raised.Should().Contain(nameof(WimStep2XmlViewModel.IsAutounattendSession));
    }

    // The real EnterAsync raises ModeChanged from inside EnterBuilderMode, before it returns.
    private void ArrangeEnteringFromThisCard()
    {
        _mockBuilderModeEntry
            .Setup(e => e.EnterAsync(BuilderTarget.Autounattend))
            .Returns(() =>
            {
                ArrangeAutounattendSession();
                _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);
                return Task.FromResult(true);
            });
    }

    [Fact]
    public async Task EnteringTheAutounattendSession_FromThisCard_RecordsTheWorkingFolder()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeEnteringFromThisCard();

        await _sut.StartAutounattendSessionCommand.ExecuteAsync(null);

        _session.WorkingDirectory.Should().Be("C:\\WorkDir");
    }

    [Fact]
    public void ASessionTheBarStarted_DoesNotRecordTheWorkingFolder()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";

        ArrangeAutounattendSession();
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);

        _session.WorkingDirectory.Should().BeNull();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_InASessionTheBarStarted_NamesTheMediaAndLeavesTheSessionAlone()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockSaves.Verify(s => s.SaveAsync(
            BuilderTarget.Autounattend,
            It.IsAny<SelectionSet>(),
            It.Is<SelectionSaveOptions>(o => o.MediaFolder == "C:\\WorkDir")),
            Times.Once);
        _session.WorkingDirectory.Should().BeNull();
    }

    [Fact]
    public async Task LeavingBuilder_ClearsTheWorkingFolder()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeEnteringFromThisCard();
        await _sut.StartAutounattendSessionCommand.ExecuteAsync(null);

        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);

        _session.WorkingDirectory.Should().BeNull();
    }

    [Fact]
    public async Task SwitchingTheTargetAwayFromAutounattend_ClearsTheWorkingFolder()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeEnteringFromThisCard();
        await _sut.StartAutounattendSessionCommand.ExecuteAsync(null);

        _mockApplicationModeService.Setup(m => m.CurrentBuilderTarget).Returns(BuilderTarget.Config);
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);

        _session.WorkingDirectory.Should().BeNull();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_NamesTheOwnInstallersItWillCopy()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        var installers = new SettingChoice(OwnInstaller.SettingId, new ChoiceValue.List(
        [
            new ChoiceValue.ListRow(new Dictionary<string, string> { ["file"] = "C:\\Setup\\Reader.exe", ["switches"] = "/sAll" }),
        ]));
        _mockSelections
            .Setup(s => s.FromBuilderSessionAsync())
            .ReturnsAsync(new SelectionSet([installers], [new AppChoice("test", "Test", null, null, null, null)], []));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Confirm_Title"
            && Rows(r).Any(row => row.Title == "WIMUtil_Generate_Plan_OwnInstallers" && row.Detail == "Reader.exe"))), Times.Once);
    }

    private static readonly SettingChoice OneOwnInstaller = new(OwnInstaller.SettingId, new ChoiceValue.List(
    [
        new ChoiceValue.ListRow(new Dictionary<string, string> { ["file"] = "C:\\Setup\\Reader.exe", ["switches"] = "" }),
    ]));

    private void ArrangeSessionWithVlc(params SettingChoice[] settings)
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        _mockLocalizationService
            .Setup(l => l.GetString("WIMUtil_Generate_Plan_Download_Detail", It.IsAny<object[]>()))
            .Returns((string _, object[] args) => (string)args[0]);
        _mockSelections
            .Setup(s => s.FromBuilderSessionAsync())
            .ReturnsAsync(new SelectionSet(
                settings,
                [new AppChoice("test", "Test", null, null, null, null)],
                [new AppChoice("vlc", "VLC", null, null, null, null)]));
    }

    [Theory]
    [InlineData(true, "VLC, Winhance")]
    [InlineData(false, "Winhance")]
    public async Task GenerateWinhanceXmlCommand_PlansToDownloadTheAppsOnlyWhileTheStagingTickIsSet(bool staged, string downloads)
    {
        ArrangeSessionWithVlc(new SettingChoice(OwnInstaller.StageAppsId, new ChoiceValue.CheckBox(staged)), OneOwnInstaller);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Confirm_Title"
            && Rows(r).Any(row => row.Title == "WIMUtil_Generate_Plan_Download" && row.Detail == downloads))), Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WithNothingTheSaveWouldStage_PlansNoDownload()
    {
        ArrangeSessionWithVlc(new SettingChoice(OwnInstaller.StageAppsId, new ChoiceValue.CheckBox(false)));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Confirm_Title"
            && Rows(r).All(row => row.Title != "WIMUtil_Generate_Plan_Download"))), Times.Once);
    }

    [Fact]
    public async Task MovingTheWorkingFolderDuringTheSession_UpdatesWhatTheSaveServiceReads()
    {
        ArrangeEnteringFromThisCard();
        await _sut.StartAutounattendSessionCommand.ExecuteAsync(null);

        _sut.WorkingDirectory = "D:\\OtherMedia";

        _session.WorkingDirectory.Should().Be("D:\\OtherMedia");
    }

    [Fact]
    public void GenerateCardDescription_FollowsTheSession()
    {
        _sut.GenerateWinhanceXmlCard.Description.Should().Be("WIMUtil_Card_GenerateWinhanceXML_Snapshot_Description");

        ArrangeAutounattendSession();
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);

        _sut.GenerateWinhanceXmlCard.Description.Should().Be("WIMUtil_Card_GenerateWinhanceXML_Session_Description");
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_OutsideAnAutounattendSession_WarnsAndSavesNothing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            "WIMUtil_Card_GenerateWinhanceXML_Start", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockSaves.Verify(s => s.SaveAsync(
            It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()), Times.Never);
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WritesTheSessionsSet_NotAMachineSnapshot()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        var session = new SelectionSet(
            [new SettingChoice("authored", new ChoiceValue.Toggle(true))],
            Array.Empty<AppChoice>(),
            Array.Empty<AppChoice>());
        _mockSelections.Setup(s => s.FromBuilderSessionAsync()).ReturnsAsync(session);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockSaves.Verify(s => s.SaveAsync(
            BuilderTarget.Autounattend,
            It.Is<SelectionSet>(set => set.Settings.Single().SettingId == "authored"),
            It.IsAny<SelectionSaveOptions>()), Times.Once);
    }

    [Fact]
    public async Task DownloadUnattendedWinstallXmlCommand_OnSuccess_SetsIsXmlAdded()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";

        _mockWimCustomizationService
            .Setup(s => s.DownloadUnattendedWinstallXmlAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("downloaded content");

        _mockWimCustomizationService
            .Setup(s => s.AddXmlToImageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());

        await _sut.DownloadUnattendedWinstallXmlCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeTrue();
        _sut.DownloadXmlCard.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadUnattendedWinstallXmlCommand_WhenAddFails_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";

        _mockWimCustomizationService
            .Setup(s => s.DownloadUnattendedWinstallXmlAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("downloaded content");

        _mockWimCustomizationService
            .Setup(s => s.AddXmlToImageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Failed("Disk full"));

        await _sut.DownloadUnattendedWinstallXmlCommand.ExecuteAsync(null);

        _sut.DownloadXmlCard.HasFailed.Should().BeTrue();
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadUnattendedWinstallXmlCommand_OnException_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";

        _mockWimCustomizationService
            .Setup(s => s.DownloadUnattendedWinstallXmlAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Download failed"));

        await _sut.DownloadUnattendedWinstallXmlCommand.ExecuteAsync(null);

        _sut.DownloadXmlCard.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadUnattendedWinstallXmlCommand_OnSuccess_ClearsOtherCardCompletions()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _sut.GenerateWinhanceXmlCard.IsComplete = true;
        _sut.SelectXmlCard.IsComplete = true;

        _mockWimCustomizationService
            .Setup(s => s.DownloadUnattendedWinstallXmlAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("C:\\WorkDir\\autounattend.xml");

        _mockWimCustomizationService
            .Setup(s => s.AddXmlToImageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());

        await _sut.DownloadUnattendedWinstallXmlCommand.ExecuteAsync(null);

        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeFalse();
        _sut.SelectXmlCard.IsComplete.Should().BeFalse();
        _sut.DownloadXmlCard.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadUnattendedWinstallXmlCommand_PassesCorrectDestinationPath()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";

        _mockWimCustomizationService
            .Setup(s => s.DownloadUnattendedWinstallXmlAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("C:\\WorkDir\\autounattend.xml");

        _mockWimCustomizationService
            .Setup(s => s.AddXmlToImageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());

        await _sut.DownloadUnattendedWinstallXmlCommand.ExecuteAsync(null);

        _mockWimCustomizationService.Verify(s => s.DownloadUnattendedWinstallXmlAsync(
            "C:\\WorkDir\\autounattend.xml",
            It.IsAny<IProgress<TaskProgressDetail>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectXmlFileCommand_WhenCancelled_DoesNothing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns((string?)null);

        await _sut.SelectXmlFileCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public async Task SelectXmlFileCommand_WhenEmptyString_DoesNothing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns(string.Empty);

        await _sut.SelectXmlFileCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public void ClearOtherXmlCardCompletions_ExceptGenerate_ClearsDownloadAndSelect()
    {
        _sut.GenerateWinhanceXmlCard.IsComplete = true;
        _sut.DownloadXmlCard.IsComplete = true;
        _sut.SelectXmlCard.IsComplete = true;

        _sut.ClearOtherXmlCardCompletions("generate");

        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeTrue();
        _sut.DownloadXmlCard.IsComplete.Should().BeFalse();
        _sut.SelectXmlCard.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void ClearOtherXmlCardCompletions_ExceptDownload_ClearsGenerateAndSelect()
    {
        _sut.GenerateWinhanceXmlCard.IsComplete = true;
        _sut.DownloadXmlCard.IsComplete = true;
        _sut.SelectXmlCard.IsComplete = true;

        _sut.ClearOtherXmlCardCompletions("download");

        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeFalse();
        _sut.DownloadXmlCard.IsComplete.Should().BeTrue();
        _sut.SelectXmlCard.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void ClearOtherXmlCardCompletions_ExceptSelect_ClearsGenerateAndDownload()
    {
        _sut.GenerateWinhanceXmlCard.IsComplete = true;
        _sut.DownloadXmlCard.IsComplete = true;
        _sut.SelectXmlCard.IsComplete = true;

        _sut.ClearOtherXmlCardCompletions("select");

        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeFalse();
        _sut.DownloadXmlCard.IsComplete.Should().BeFalse();
        _sut.SelectXmlCard.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var vm = new WimStep2XmlViewModel(
            _mockSaves.Object,
            _mockWimCustomizationService.Object,
            _mockSelections.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object,
            _mockResourceService.Object,
            _mockAnswerFileValidator.Object,
            _mockAutounattendReader.Object,
            _mockApplicationModeService.Object,
            _mockBuilderModeEntry.Object,
            _mockDispatcherService.Object,
            _mockEventBus.Object,
            _checkState,
            _session,
            _mockWimImageService.Object,
            _mockTargetWindows.Object,
            _mockTaskProgressService.Object);

        var act = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void SettingIsXmlAdded_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimStep2XmlViewModel.IsXmlAdded))
                raised = true;
        };

        _sut.IsXmlAdded = true;

        raised.Should().BeTrue();
    }

    [Fact]
    public void SettingXmlStatus_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimStep2XmlViewModel.XmlStatus))
                raised = true;
        };

        _sut.XmlStatus = "New status";

        raised.Should().BeTrue();
    }

    private static AnswerFileReport OneError() =>
        new([new AnswerFileFinding(AnswerFileRule.CommandEmpty, AnswerFileSeverity.Error, "line 3: settings[specialize]", "Path")]);

    [Fact]
    public async Task GenerateWinhanceXmlCommand_OnSuccess_ChecksTheMediaCopyAndShowsTheFindings()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        var report = OneError();
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.XmlStatus.Should().Be("WIMUtil_Status_XmlGenSuccess");
        _checkState.Subject.Should().Be("C:\\WorkDir\\autounattend.xml");
        _checkState.LastReport.Should().BeSameAs(report);
        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeTrue();

        // Generate reports the whole run at once, so the findings ride in that summary rather than
        // opening a second dialog behind it.
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Summary_Title"
            && r.Verdict!.Tone == SummaryTone.Error
            && r.Verdict.Message == "WIMUtil_AnswerFile_Summary"
            && RowText(r).Contains("line 3: settings[specialize]")
            && RowText(r).Contains("Path"))), Times.Once);
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_AnswerFile_DialogTitle")), Times.Never);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_CleanAnswerFile_PublishesTheReportAndSaysSoInTheSummary()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.XmlStatus.Should().Be("WIMUtil_Status_XmlGenSuccess");
        _checkState.Subject.Should().Be("C:\\WorkDir\\autounattend.xml");
        _checkState.LastReport!.Findings.Should().BeEmpty();
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Summary_Title"
            && r.Verdict!.Title == "WIMUtil_AnswerFile_Verdict_Clean"
            && !r.Sections.Any(sec => sec.Heading == "WIMUtil_AnswerFile_FindingsHeading"))), Times.Once);
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_AnswerFile_DialogTitle")), Times.Never);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_CheckThrows_KeepsTheCardComplete()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeTrue();
        _sut.GenerateWinhanceXmlCard.HasFailed.Should().BeFalse();
        _sut.XmlStatus.Should().Be("WIMUtil_Status_XmlGenSuccess");
        _mockLogService.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("boom")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_PublishesTheReportForTheBanner()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        var report = OneError();
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _checkState.Subject.Should().Be("C:\\WorkDir\\autounattend.xml");
        _checkState.LastReport.Should().BeSameAs(report);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_CheckThrows_LeavesNoStaleReport()
    {
        _checkState.Publish("C:\\OldDir\\autounattend.xml", OneError());
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _checkState.Subject.Should().BeNull();
        _checkState.LastReport.Should().BeNull();
    }

    [Fact]
    public async Task DownloadUnattendedWinstallXmlCommand_OnSuccess_ChecksTheMediaCopy()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockWimCustomizationService
            .Setup(s => s.DownloadUnattendedWinstallXmlAsync(It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("downloaded content");
        _mockWimCustomizationService
            .Setup(s => s.AddXmlToImageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());

        await _sut.DownloadUnattendedWinstallXmlCommand.ExecuteAsync(null);

        _mockAnswerFileValidator.Verify(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()), Times.Once);
        _sut.XmlStatus.Should().Be("WIMUtil_Status_XmlDownloadSuccess");
        _checkState.Subject.Should().Be("C:\\WorkDir\\autounattend.xml");
        _checkState.LastReport!.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectXmlFileCommand_NotWellFormed_RefusesButStillReportsForTheBanner()
    {
        var selected = Path.Combine(Path.GetTempPath(), "winhance-broken-" + Guid.NewGuid().ToString("N") + ".xml");
        await File.WriteAllTextAsync(selected, "<unattend><settings></unattend>");
        try
        {
            _sut.WorkingDirectory = "C:\\WorkDir";
            _mockFilePickerService.Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>())).Returns(selected);
            var report = new AnswerFileReport([new AnswerFileFinding(AnswerFileRule.NotWellFormed, AnswerFileSeverity.Error, "line 1", "mismatched tag")]);
            _mockAnswerFileValidator
                .Setup(v => v.ValidateAsync(selected, It.IsAny<CancellationToken>()))
                .ReturnsAsync(report);

            await _sut.SelectXmlFileCommand.ExecuteAsync(null);

            _sut.SelectXmlCard.HasFailed.Should().BeTrue();
            _sut.XmlStatus.Should().Be("WIMUtil_Status_XmlInvalid");
            _checkState.Subject.Should().Be(selected);
            _checkState.LastReport.Should().BeSameAs(report);
            _mockDialogService.Verify(d => d.ShowErrorAsync("WIMUtil_Msg_XmlInvalidError", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _mockWimCustomizationService.Verify(c => c.AddXmlToImageAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
        finally
        {
            File.Delete(selected);
        }
    }

    [Fact]
    public async Task SelectXmlFileCommand_OnSuccess_ChecksTheMediaCopyNotTheSelectedFile()
    {
        var selected = Path.Combine(Path.GetTempPath(), "winhance-select-" + Guid.NewGuid().ToString("N") + ".xml");
        await File.WriteAllTextAsync(selected, "<unattend xmlns=\"urn:schemas-microsoft-com:unattend\" />");
        try
        {
            _sut.WorkingDirectory = "C:\\WorkDir";
            _mockFilePickerService.Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>())).Returns(selected);
            _mockWimCustomizationService
                .Setup(s => s.AddXmlToImageAsync(selected, "C:\\WorkDir"))
                .ReturnsAsync(OperationResult.Succeeded());

            await _sut.SelectXmlFileCommand.ExecuteAsync(null);

            _sut.SelectXmlCard.IsComplete.Should().BeTrue();
            _sut.XmlStatus.Should().Be("WIMUtil_Status_XmlSelectSuccess");
            _checkState.Subject.Should().Be("C:\\WorkDir\\autounattend.xml");
            _mockAnswerFileValidator.Verify(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()), Times.Once);
            _mockAnswerFileValidator.Verify(v => v.ValidateAsync(selected, It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            File.Delete(selected);
        }
    }

    [Fact]
    public async Task SelectXmlFileCommand_WhenAddFails_ShowsTheFailureReason()
    {
        var selected = Path.Combine(Path.GetTempPath(), "winhance-select-" + Guid.NewGuid().ToString("N") + ".xml");
        await File.WriteAllTextAsync(selected, "<unattend xmlns=\"urn:schemas-microsoft-com:unattend\" />");
        try
        {
            _sut.WorkingDirectory = "C:\\WorkDir";
            _mockLocalizationService.Setup(l => l.GetString("WIMUtil_Msg_XmlValidAddFailed")).Returns("Could not add: {0}");
            _mockFilePickerService.Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>())).Returns(selected);
            _mockWimCustomizationService
                .Setup(s => s.AddXmlToImageAsync(selected, "C:\\WorkDir"))
                .ReturnsAsync(OperationResult.Failed("Access to the path is denied"));

            await _sut.SelectXmlFileCommand.ExecuteAsync(null);

            _sut.SelectXmlCard.HasFailed.Should().BeTrue();
            _mockDialogService.Verify(d => d.ShowErrorAsync(
                "Could not add: Access to the path is denied", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
        finally
        {
            File.Delete(selected);
        }
    }

    private const string LeftoverImage = "C:\\WorkDir\\sources\\install.old.wim";
    private static readonly string[] HomeAndPro = ["Windows 11 Home", "Windows 11 Pro"];
    private static readonly string[] ProOnly = ["Windows 11 Pro"];

    private static ChoiceValue.Option KeepAll => Keep(LocKey.Setting.AutounattendMediaEditions.Option0);

    private static ChoiceValue.Option KeepChosen => Keep(LocKey.Setting.AutounattendMediaEditions.Option1);

    private static ChoiceValue.Option KeepTicked => Keep(LocKey.Setting.AutounattendMediaEditions.Option2);

    private static ChoiceValue.Option Keep(LocKey label) =>
        new(SettingCatalog.Find("autounattend-media-editions")!.States.ToList().FindIndex(state => state.Label == label));

    private void ArrangeMediaEditions(params ChoiceValue?[] mediaEditionsThenListThenEdition)
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeAutounattendSession();
        _session.Editions =
        [
            new(1, "Windows 11 Home", "Core"),
            new(2, "Windows 11 Home Single Language", "CoreSingleLanguage"),
            new(3, "Windows 11 Pro", "Professional"),
        ];

        var ids = new[] { "autounattend-media-editions", "autounattend-media-editions-list", "autounattend-edition" };
        var settings = ids.Zip(mediaEditionsThenListThenEdition)
            .Where(pair => pair.Second is not null)
            .Select(pair => new SettingChoice(pair.First, pair.Second!))
            .ToList();
        _mockSelections
            .Setup(s => s.FromBuilderSessionAsync())
            .ReturnsAsync(new SelectionSet(settings, [new AppChoice("test", "Test", null, null, null, null)], []));
        _mockWimImageService
            .Setup(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EditionRemovalResult(true));
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_KeepsOnlyTheTickedEditions_AfterTheFileIsWritten()
    {
        ArrangeMediaEditions(KeepTicked, new ChoiceValue.Keys(["Windows 11 Home", "Windows 11 Pro"]), new ChoiceValue.Option(4));
        var calls = new List<string>();
        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .Callback(() => calls.Add("save"))
            .ReturnsAsync(new SelectionSaveResult(GeneratedXmlPath, []));
        _mockWimImageService
            .Setup(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("keep"))
            .ReturnsAsync(new EditionRemovalResult(true));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        calls.Should().Equal("save", "keep");
        _mockWimImageService.Verify(w => w.KeepEditionsAsync("C:\\WorkDir",
            It.Is<IReadOnlyCollection<string>>(kept => kept.SequenceEqual(HomeAndPro)),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Confirm_Title"
            && RowText(r).Contains("WIMUtil_Generate_Plan_RemoveEditions")
            && Rows(r).All(row => row.Tone != SummaryTone.Warning))), Times.Once);
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Summary_Title"
            && Rows(r).Any(row => row.Title == "Windows 11 Pro" && row.Tone == SummaryTone.Success)
            && Rows(r).Any(row => row.Title == "Windows 11 Home Single Language" && row.Detail == "WIMUtil_Generate_Result_EditionRemoved"))), Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_KeepOnlyTheChosenEdition_KeepsTheEditionTheEditionCardNames()
    {
        ArrangeMediaEditions(KeepChosen, null, new ChoiceValue.Option(4));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(w => w.KeepEditionsAsync("C:\\WorkDir",
            It.Is<IReadOnlyCollection<string>>(kept => kept.SequenceEqual(ProOnly)),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_RemovingTheChosenEdition_WarnsButStillRuns()
    {
        ArrangeMediaEditions(KeepTicked, new ChoiceValue.Keys(["Windows 11 Home"]), new ChoiceValue.Option(4));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            Rows(r).Any(row => row.Title == "WIMUtil_Generate_Plan_ChosenEditionRemoved" && row.Tone == SummaryTone.Warning))), Times.Once);
        _mockWimImageService.Verify(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Keeping the chosen edition with "Ask me during setup" names no edition; keeping the ticked ones with nothing
    // ticked keeps none.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenerateWinhanceXmlCommand_AChoiceKeepingNoEdition_KeepsThemAllAndSaysSo(bool ticked)
    {
        ArrangeMediaEditions(ticked ? KeepTicked : KeepChosen, new ChoiceValue.Keys([]), new ChoiceValue.Option(0));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            Rows(r).Any(row => row.Title == "WIMUtil_Generate_Plan_NoEditionLeft" && row.Tone == SummaryTone.Warning))), Times.Once);
        _mockWimImageService.Verify(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_KeepAllEditions_LeavesTheImageAlone()
    {
        ArrangeMediaEditions(KeepAll, new ChoiceValue.Keys(["Windows 11 Home"]), new ChoiceValue.Option(4));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WhenTheEditionsCannotBeRemoved_TheFileStillCountsAndTheSummarySaysSo()
    {
        ArrangeMediaEditions(KeepChosen, null, new ChoiceValue.Option(4));
        _mockWimImageService
            .Setup(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("locked"));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeTrue();
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Summary_Title"
            && Rows(r).Any(row => row.Title == "WIMUtil_Msg_RemoveEditionsFailed" && row.Tone == SummaryTone.Warning))), Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_GeneratedTwice_KeepsTheSameEditionsAndReportsNoFailure()
    {
        ArrangeMediaEditions(KeepTicked, new ChoiceValue.Keys(HomeAndPro), new ChoiceValue.Option(4));
        _mockWimImageService.Setup(w => w.DetectImageFormatAsync("C:\\WorkDir")).ReturnsAsync(new ImageFormatInfo
        {
            Editions = [new(1, "Windows 11 Home", "Core"), new(2, "Windows 11 Pro", "Professional")],
        });

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);
        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _session.Editions.Should().Equal(new ImageEdition(1, "Windows 11 Home", "Core"), new ImageEdition(2, "Windows 11 Pro", "Professional"));
        _mockWimImageService.Verify(w => w.KeepEditionsAsync("C:\\WorkDir",
            It.Is<IReadOnlyCollection<string>>(kept => kept.SequenceEqual(HomeAndPro)),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            RowText(r).Contains("WIMUtil_Msg_RemoveEditionsFailed"))), Times.Never);
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Summary_Title")), Times.Exactly(2));
    }

    // An edition the install-key table does not know has no option either; with no edition chosen it must not
    // be the one edition kept.
    [Fact]
    public async Task GenerateWinhanceXmlCommand_KeepOnlyTheChosenEditionWithNoneChosen_KeepsThemAllAndSaysSo()
    {
        ArrangeMediaEditions(KeepChosen, null, null);
        _session.Editions = [.. _session.Editions, new(4, "Windows 11 IoT Enterprise", "IoTEnterprise")];

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            Rows(r).Any(row => row.Title == "WIMUtil_Generate_Plan_NoEditionLeft" && row.Tone == SummaryTone.Warning))), Times.Once);
        _mockWimImageService.Verify(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_RemovesEditionsAsATaskTheUserCanCancel()
    {
        ArrangeMediaEditions(KeepChosen, null, new ChoiceValue.Option(4));
        var lockedWhileRunning = false;
        _mockWimImageService
            .Setup(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Callback(() => lockedWhileRunning = _sut.IsRemovingEditions)
            .ReturnsAsync(new EditionRemovalResult(true));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        lockedWhileRunning.Should().BeTrue();
        _sut.IsRemovingEditions.Should().BeFalse();
        _mockTaskProgressService.Verify(t => t.StartTask("WIMUtil_Status_RemovingEditions", true), Times.Once);
        _mockWimImageService.Verify(w => w.KeepEditionsAsync("C:\\WorkDir", It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<IProgress<TaskProgressDetail>?>(), _removalCancellation.Token), Times.Once);
        _mockTaskProgressService.Verify(t => t.CompleteTask(), Times.Once);
        _mockTaskProgressService.Verify(t => t.FailTask(), Times.Never);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WhenTheRemovalIsCancelled_TheFileStillCountsAndTheSummarySaysSo()
    {
        ArrangeMediaEditions(KeepChosen, null, new ChoiceValue.Option(4));
        _mockWimImageService
            .Setup(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeTrue();
        _sut.IsRemovingEditions.Should().BeFalse();
        _session.Editions.Should().HaveCount(3);
        _mockTaskProgressService.Verify(t => t.CancelTask(), Times.Once);
        _mockTaskProgressService.Verify(t => t.FailTask(), Times.Never);
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Summary_Title"
            && Rows(r).Any(row => row.Title == "WIMUtil_Msg_RemoveEditionsCancelled" && row.Tone == SummaryTone.Warning)
            && !RowText(r).Contains("WIMUtil_Generate_Result_EditionRemoved"))), Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WhenTheServiceReportsFailure_TheSummarySaysTheImageIsUnchanged()
    {
        ArrangeMediaEditions(KeepChosen, null, new ChoiceValue.Option(4));
        _mockWimImageService
            .Setup(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EditionRemovalResult.Failed);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _session.Editions.Should().HaveCount(3);
        _mockTaskProgressService.Verify(t => t.FailTask(), Times.Once);
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Summary_Title"
            && Rows(r).Any(row => row.Title == "WIMUtil_Msg_RemoveEditionsFailed" && row.Tone == SummaryTone.Warning)
            && !RowText(r).Contains("WIMUtil_Generate_Result_EditionRemoved"))), Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WhenTheOldImageIsLeftBehind_TheSummaryNamesTheFileToDelete()
    {
        ArrangeMediaEditions(KeepChosen, null, new ChoiceValue.Option(4));
        _mockWimImageService
            .Setup(w => w.KeepEditionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EditionRemovalResult(true, LeftoverImage));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockLocalizationService.Verify(l => l.GetString("WIMUtil_Generate_Summary_OldImageLeft",
            It.Is<object[]>(args => args.Length == 1 && LeftoverImage.Equals(args[0]))), Times.Once);
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            r.Title == "WIMUtil_Generate_Summary_Title"
            && Rows(r).Any(row => row.Title == "WIMUtil_Generate_Summary_OldImageLeft" && row.Tone == SummaryTone.Warning)
            && Rows(r).Any(row => row.Title == "Windows 11 Pro" && row.Tone == SummaryTone.Success)
            && r.CopyText!.Contains("WIMUtil_Generate_Summary_OldImageLeft"))), Times.Once);
    }

    [Fact]
    public async Task StartAutounattendSessionCommand_RecordsTheMediasEditionsForTheUnattendPage()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ImageEdition[] editions = [new(1, "Windows 11 Home", "Core"), new(2, "Windows 11 Pro", "Professional")];
        _mockWimImageService.Setup(w => w.DetectImageFormatAsync("C:\\WorkDir")).ReturnsAsync(new ImageFormatInfo { Editions = editions });
        _mockBuilderModeEntry.Setup(e => e.EnterAsync(BuilderTarget.Autounattend)).ReturnsAsync(true);

        await _sut.StartAutounattendSessionCommand.ExecuteAsync(null);

        _session.Editions.Should().Equal(editions);
    }

    [Fact]
    public async Task StartAutounattendSessionCommand_WhenEnteringFails_RecordsNoEditions()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockWimImageService.Setup(w => w.DetectImageFormatAsync("C:\\WorkDir"))
            .ReturnsAsync(new ImageFormatInfo { Editions = [new(1, "Windows 11 Home", "Core")] });
        _mockBuilderModeEntry.Setup(e => e.EnterAsync(BuilderTarget.Autounattend)).ReturnsAsync(false);

        await _sut.StartAutounattendSessionCommand.ExecuteAsync(null);

        _session.Editions.Should().BeEmpty();
    }

    private void ArrangeProOnlyImage(int chosenOption, bool builtIntoImage)
    {
        ArrangeMediaEditions(KeepAll, null, new ChoiceValue.Option(chosenOption));
        _session.Editions = [new(1, "Windows 11 Pro", "Professional")];
        _sut.IsBuiltIntoImage = builtIntoImage;
        _mockLocalizationService
            .Setup(l => l.GetString("WIMUtil_Generate_Plan_ChosenNotOnMedia_Detail", It.IsAny<object[]>()))
            .Returns((string _, object[] args) => (string)args[0]);
    }

    private void VerifyNoChosenNotOnMediaRow() =>
        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r =>
            Rows(r).Any(row => row.Title == "WIMUtil_Generate_Plan_ChosenNotOnMedia"))), Times.Never);

    [Fact]
    public async Task GenerateWinhanceXmlCommand_ImageModeChosenEditionDiffers_WarnsWithTheEditionTheImageHolds()
    {
        ArrangeProOnlyImage(chosenOption: 1, builtIntoImage: true);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r => Rows(r).Any(row =>
            row.Title == "WIMUtil_Generate_Plan_ChosenNotOnMedia" && row.Detail == "Windows 11 Pro" && row.Tone == SummaryTone.Warning))), Times.Once);
    }

    // 4 is the Pro the image holds; 0 is "Ask me during setup", which names no edition.
    [Theory]
    [InlineData(4)]
    [InlineData(0)]
    public async Task GenerateWinhanceXmlCommand_ImageModeNoConflictingEdition_NoRow(int chosenOption)
    {
        ArrangeProOnlyImage(chosenOption, builtIntoImage: true);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        VerifyNoChosenNotOnMediaRow();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_SetupMode_NoChosenNotOnMediaRow()
    {
        ArrangeProOnlyImage(chosenOption: 1, builtIntoImage: false);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        VerifyNoChosenNotOnMediaRow();
    }
}
