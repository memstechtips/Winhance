using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
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

    private readonly Mock<IWimCustomizationService> _mockWimCustomizationService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IAnswerFileValidator> _mockAnswerFileValidator = new();
    private readonly Mock<IAutounattendReader> _mockAutounattendReader = new();
    private readonly Mock<IApplicationModeService> _mockApplicationModeService = new();
    private readonly Mock<IBuilderModeEntry> _mockBuilderModeEntry = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly AnswerFileCheckState _checkState = new();
    private readonly Mock<IWimImageService> _mockWimImageService = new();
    private readonly Mock<ITargetWindowsService> _mockTargetWindows = new();
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

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        _mockDialogService
            .Setup(d => d.ShowSummaryAsync(It.IsAny<SummaryRequest>()))
            .ReturnsAsync(true);

        _sut = CreateSut();
    }

    private WimStep2XmlViewModel CreateSut() => new(
        _mockWimCustomizationService.Object,
        _mockDialogService.Object,
        _mockLocalizationService.Object,
        _mockFileSystemService.Object,
        _mockFilePickerService.Object,
        _mockLogService.Object,
        _mockAnswerFileValidator.Object,
        _mockAutounattendReader.Object,
        _mockApplicationModeService.Object,
        _mockBuilderModeEntry.Object,
        _mockDispatcherService.Object,
        _mockEventBus.Object,
        _checkState,
        _session,
        _mockWimImageService.Object,
        _mockTargetWindows.Object);

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

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
    public void Constructor_WorkingDirectoryDefaultsToEmpty()
    {
        _sut.WorkingDirectory.Should().BeEmpty();
    }

    // The empty-WorkingDirectory guard (issue #506).

    [Fact]
    public void ChooseBuilderCommand_ChoosesItWithoutTouchingTheMode()
    {
        ArrangeAutounattendSession();

        _sut.ChooseBuilderCommand.Execute(null);

        _sut.Choice.Should().Be(AnswerFileChoice.Builder);
        _mockBuilderModeEntry.Verify(e => e.EnterAsync(It.IsAny<BuilderTarget>(), It.IsAny<string?>()), Times.Never);
        _mockApplicationModeService.Verify(m => m.SetBuilderTarget(It.IsAny<BuilderTarget>()), Times.Never);
    }

    [Fact]
    public async Task JoinSessionAsync_TargetsTheMediasBuild()
    {
        ArrangeAutounattendSession();
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockWimImageService.Setup(w => w.DetectImageFormatAsync("C:\\WorkDir")).ReturnsAsync(new ImageFormatInfo { Build = 26300 });

        await _sut.JoinSessionAsync();

        _mockTargetWindows.Verify(t => t.Set(26300), Times.Once);
    }

    [Fact]
    public async Task JoinSessionAsync_WithNoReadableMedia_KeepsTheTarget()
    {
        ArrangeAutounattendSession();

        await _sut.JoinSessionAsync();

        _mockTargetWindows.Verify(t => t.Set(It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task JoinSessionAsync_ReadsTheSameMediaOnlyOnce()
    {
        ArrangeAutounattendSession();
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockWimImageService.Setup(w => w.DetectImageFormatAsync("C:\\WorkDir")).ReturnsAsync(new ImageFormatInfo { Build = 26300 });

        await _sut.JoinSessionAsync();
        await _sut.JoinSessionAsync();

        _mockTargetWindows.Verify(t => t.Set(26300), Times.Once);
    }

    [Fact]
    public async Task JoinSessionAsync_AfterTheSessionStartsAgain_ReadsTheMediaAgain()
    {
        ArrangeAutounattendSession();
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockWimImageService.Setup(w => w.DetectImageFormatAsync("C:\\WorkDir")).ReturnsAsync(new ImageFormatInfo { Build = 26300 });
        await _sut.JoinSessionAsync();

        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);
        await _sut.JoinSessionAsync();

        _mockTargetWindows.Verify(t => t.Set(26300), Times.Exactly(2));
    }

    [Fact]
    public async Task JoinSessionAsync_OutsideASession_ReadsNothing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockWimImageService.Setup(w => w.DetectImageFormatAsync("C:\\WorkDir")).ReturnsAsync(new ImageFormatInfo { Build = 26300 });

        await _sut.JoinSessionAsync();

        _mockTargetWindows.Verify(t => t.Set(It.IsAny<int?>()), Times.Never);
        _session.WorkingDirectory.Should().BeNull();
    }

    private void ArrangeLangIni(string contents)
    {
        ArrangeAutounattendSession();
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.FileExists("C:\\WorkDir\\sources\\lang.ini")).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllTextAsync("C:\\WorkDir\\sources\\lang.ini", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contents);
    }

    [Fact]
    public async Task JoinSessionAsync_MediaWithOneLanguage_PicksItForSetup()
    {
        ArrangeLangIni("[Available UI Languages]\r\nfr-fr = 3\r\n\r\n[Fallback Languages]\r\nfr-fr = en-us\r\n");

        await _sut.JoinSessionAsync();

        _mockApplicationModeService.Verify(m => m.RecordBuilderEdit(It.Is<SettingChoice>(c =>
            c.SettingId == "autounattend-setup-language"
            && c.Value is ChoiceValue.Keyed && ((ChoiceValue.Keyed)c.Value).Key == "fr-FR")), Times.Once);
        _mockEventBus.Verify(b => b.Publish(It.IsAny<BuilderSeededEvent>()), Times.Once);
    }

    [Fact]
    public async Task JoinSessionAsync_MediaWithSeveralLanguages_LeavesSetupAsking()
    {
        ArrangeLangIni("[Available UI Languages]\r\nen-us = 3\r\nde-de = 3\r\n");

        await _sut.JoinSessionAsync();

        _mockApplicationModeService.Verify(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()), Times.Never);
    }

    [Fact]
    public async Task JoinSessionAsync_KeepsALanguageTheUserAlreadyPicked()
    {
        ArrangeLangIni("[Available UI Languages]\r\nen-us = 3\r\n");
        _mockApplicationModeService.Setup(m => m.GetBuilderEdit("autounattend-setup-language"))
            .Returns(new SettingChoice("autounattend-setup-language", new ChoiceValue.Keyed("de-DE", "German (Germany)")));

        await _sut.JoinSessionAsync();

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
    public async Task ChooseOwnXmlCommand_AWinhanceFileWithItsChoices_OpensThemAsASession()
    {
        var path = ArrangeSelectedFile();
        var config = new WinhanceConfigFile();
        _mockAutounattendReader.Setup(r => r.ReadAsync(path)).ReturnsAsync(new WinhanceAnswerFile("26.09.23", config));

        try
        {
            await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(path);
        }

        _mockBuilderModeEntry.Verify(e => e.EnterFromAsync(BuilderTarget.Autounattend, config), Times.Once);
    }

    [Fact]
    public async Task ChooseOwnXmlCommand_DecliningToOpenTheChoices_LeavesTheModeAlone()
    {
        var path = ArrangeSelectedFile();
        _mockAutounattendReader.Setup(r => r.ReadAsync(path)).ReturnsAsync(new WinhanceAnswerFile("26.09.23", new WinhanceConfigFile()));
        _mockDialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        try
        {
            await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(path);
        }

        _mockBuilderModeEntry.Verify(e => e.EnterFromAsync(It.IsAny<BuilderTarget>(), It.IsAny<WinhanceConfigFile>()), Times.Never);
        _sut.Choice.Should().Be(AnswerFileChoice.OwnXml);
    }

    [Fact]
    public async Task ChooseOwnXmlCommand_AnOlderWinhanceFile_SaysToGenerateItAgain()
    {
        var path = ArrangeSelectedFile();
        _mockAutounattendReader.Setup(r => r.ReadAsync(path)).ReturnsAsync(new WinhanceAnswerFile("26.09.01", null));

        try
        {
            await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(path);
        }

        _mockDialogService.Verify(d => d.ShowInformationAsync("WIMUtil_OpenWinhanceFile_NoChoices", "WIMUtil_OpenWinhanceFile_Title", It.IsAny<string>()), Times.Once);
        _mockBuilderModeEntry.Verify(e => e.EnterFromAsync(It.IsAny<BuilderTarget>(), It.IsAny<WinhanceConfigFile>()), Times.Never);
    }

    [Fact]
    public async Task ChooseOwnXmlCommand_AFileWinhanceDidNotMake_AsksNothing()
    {
        var path = ArrangeSelectedFile();

        try
        {
            await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(path);
        }

        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _sut.Choice.Should().Be(AnswerFileChoice.OwnXml);
    }

    [Fact]
    public async Task ChooseOwnXmlCommand_InAnAutounattendSession_AFileWinhanceDidNotMake_SelectsOwnXml()
    {
        ArrangeAutounattendSession();
        using var sut = CreateSut();
        sut.Choice.Should().Be(AnswerFileChoice.Builder);
        var path = Path.Combine(Path.GetTempPath(), $"winhance-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, "<unattend />");
        sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService.Setup(p => p.PickFile(It.IsAny<string[]>(), It.IsAny<string>())).Returns(path);

        try
        {
            await sut.ChooseOwnXmlCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(path);
        }

        sut.Choice.Should().Be(AnswerFileChoice.OwnXml);
    }

    [Fact]
    public async Task ChooseOwnXmlCommand_AFileWinhanceDidNotMake_IsReportedAsTheUsersOwnFile()
    {
        var path = ArrangeSelectedFile();
        _sut.UsesOwnAnswerFile.Should().BeFalse();

        try
        {
            await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(path);
        }

        _sut.UsesOwnAnswerFile.Should().BeTrue();
        _sut.OwnAnswerFileName.Should().Be(Path.GetFileName(path));
    }

    [Fact]
    public void OpenAutounattendCommand_AsksTheShellForTheAutounattendPage()
    {
        _sut.OpenAutounattendCommand.Execute(null);

        _mockEventBus.Verify(b => b.Publish(It.Is<PageRequestedEvent>(e => e.PageTag == "Autounattend")), Times.Once);
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_AWinhanceFileOnTheMedia_OpensItsChoicesLikeAPickedOne()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        var config = new WinhanceConfigFile();
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);
        _mockAutounattendReader.Setup(r => r.ReadAsync(GeneratedXmlPath)).ReturnsAsync(new WinhanceAnswerFile("26.10.27", config));

        await _sut.PickUpMediaAnswerFileAsync();

        _sut.Choice.Should().Be(AnswerFileChoice.OwnXml);
        _sut.SelectedXmlPath.Should().Be(GeneratedXmlPath);
        _sut.Choice.Should().Be(AnswerFileChoice.OwnXml);
        _mockAnswerFileValidator.Verify(v => v.ValidateAsync(GeneratedXmlPath, It.IsAny<CancellationToken>()), Times.Once);
        _mockBuilderModeEntry.Verify(e => e.EnterFromAsync(BuilderTarget.Autounattend, config), Times.Once);
        _sut.ShowsWinhanceFileOnMedia.Should().BeTrue();
        _sut.ShowsOwnFileOnMedia.Should().BeFalse();
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_ACustomFileOnTheMedia_KeepsItWithoutAsking()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);

        await _sut.PickUpMediaAnswerFileAsync();

        _sut.Choice.Should().Be(AnswerFileChoice.OwnXml);
        _sut.XmlStatus.Should().Be("WIMUtil_Status_XmlOnMedia");
        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _sut.ShowsOwnFileOnMedia.Should().BeTrue();
        _sut.ShowsWinhanceFileOnMedia.Should().BeFalse();
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_NoFileOnTheNewMedia_ClearsTheOldOne()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);
        await _sut.PickUpMediaAnswerFileAsync();
        _sut.WorkingDirectory = "D:\\Other";

        await _sut.PickUpMediaAnswerFileAsync();

        _sut.Choice.Should().Be(AnswerFileChoice.None);
        _sut.SelectedXmlPath.Should().BeEmpty();
        _sut.XmlStatus.Should().Be("WIMUtil_Status_NoXmlAdded");
        _sut.HasMediaAnswerFile.Should().BeFalse();
    }

    [Fact]
    public async Task LeavingTheSession_OverAWinhanceFileOnTheMedia_FallsBackToThatFile()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);
        _mockAutounattendReader.Setup(r => r.ReadAsync(GeneratedXmlPath)).ReturnsAsync(new WinhanceAnswerFile("26.10.27", new WinhanceConfigFile()));
        _mockBuilderModeEntry.Setup(e => e.EnterFromAsync(BuilderTarget.Autounattend, It.IsAny<WinhanceConfigFile>()))
            .ReturnsAsync(true).Callback(ArrangeAutounattendSession);
        await _sut.PickUpMediaAnswerFileAsync();
        _sut.Choice.Should().Be(AnswerFileChoice.Builder);
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Normal);

        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);

        _sut.Choice.Should().Be(AnswerFileChoice.OwnXml);
        _sut.SelectedXmlPath.Should().Be(GeneratedXmlPath);
        _sut.NeedsApply.Should().BeFalse();
        _mockFileSystemService.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Never);
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

    private async Task JoinFromThisPageAsync()
    {
        ArrangeAutounattendSession();
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);
        await _sut.JoinSessionAsync();
    }

    [Fact]
    public async Task OpeningThisPageInTheSession_RecordsTheWorkingFolder()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";

        await JoinFromThisPageAsync();

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
    public async Task LeavingBuilder_ClearsTheWorkingFolder()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        await JoinFromThisPageAsync();

        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);

        _session.WorkingDirectory.Should().BeNull();
    }

    [Fact]
    public async Task SwitchingTheTargetAwayFromAutounattend_ClearsTheWorkingFolder()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        await JoinFromThisPageAsync();

        _mockApplicationModeService.Setup(m => m.CurrentBuilderTarget).Returns(BuilderTarget.Config);
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);

        _session.WorkingDirectory.Should().BeNull();
    }

    [Fact]
    public async Task MovingTheWorkingFolderDuringTheSession_UpdatesWhatTheSaveServiceReads()
    {
        await JoinFromThisPageAsync();

        _sut.WorkingDirectory = "D:\\OtherMedia";

        _session.WorkingDirectory.Should().Be("D:\\OtherMedia");
    }

    [Fact]
    public async Task ChooseOwnXmlCommand_WhenCancelled_DoesNothing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns((string?)null);

        await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);

        _sut.Choice.Should().Be(AnswerFileChoice.None);
    }

    [Fact]
    public async Task ChooseOwnXmlCommand_WhenEmptyString_DoesNothing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns(string.Empty);

        await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);

        _sut.Choice.Should().Be(AnswerFileChoice.None);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var vm = new WimStep2XmlViewModel(
            _mockWimCustomizationService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object,
            _mockAnswerFileValidator.Object,
            _mockAutounattendReader.Object,
            _mockApplicationModeService.Object,
            _mockBuilderModeEntry.Object,
            _mockDispatcherService.Object,
            _mockEventBus.Object,
            _checkState,
            _session,
            _mockWimImageService.Object,
            _mockTargetWindows.Object);

        var act = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        act.Should().NotThrow();
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

    [Fact]
    public async Task ChooseOwnXmlCommand_NotWellFormed_RefusesButStillReportsForTheBanner()
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

            await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);

            _sut.Choice.Should().Be(AnswerFileChoice.None);
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
    public async Task JoinSessionAsync_RecordsTheMediasEditionsForTheUnattendPage()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ImageEdition[] editions = [new(1, "Windows 11 Home", "Core"), new(2, "Windows 11 Pro", "Professional")];
        _mockWimImageService.Setup(w => w.DetectImageFormatAsync("C:\\WorkDir")).ReturnsAsync(new ImageFormatInfo { Editions = editions });

        await JoinFromThisPageAsync();

        _session.Editions.Should().Equal(editions);
    }

    [Fact]
    public async Task JoinSessionAsync_OutsideASession_RecordsNoEditions()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockWimImageService.Setup(w => w.DetectImageFormatAsync("C:\\WorkDir"))
            .ReturnsAsync(new ImageFormatInfo { Editions = [new(1, "Windows 11 Home", "Core")] });

        await _sut.JoinSessionAsync();

        _session.Editions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_OutsideASession_ChoosesNone()
    {
        _sut.Choice.Should().Be(AnswerFileChoice.None);
        _sut.IsBuilderChosen.Should().BeFalse();
        _sut.IsOwnXmlChosen.Should().BeFalse();
        _sut.NeedsApply.Should().BeFalse();
        _sut.UsesOwnAnswerFile.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InAnAutounattendSession_ChoosesTheBuilder()
    {
        ArrangeAutounattendSession();

        using var sut = NewSut();

        sut.Choice.Should().Be(AnswerFileChoice.Builder);
        sut.IsBuilderChosen.Should().BeTrue();
    }

    private WimStep2XmlViewModel NewSut() => new(
        _mockWimCustomizationService.Object, _mockDialogService.Object, _mockLocalizationService.Object,
        _mockFileSystemService.Object, _mockFilePickerService.Object, _mockLogService.Object,
        _mockAnswerFileValidator.Object, _mockAutounattendReader.Object, _mockApplicationModeService.Object,
        _mockBuilderModeEntry.Object, _mockDispatcherService.Object, _mockEventBus.Object, _checkState, _session,
        _mockWimImageService.Object, _mockTargetWindows.Object);

    [Fact]
    public async Task ChooseOwnXml_RecordsThePathAndCopiesNothingUntilApply()
    {
        var path = ArrangeSelectedFile();
        try
        {
            await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);

            _sut.Choice.Should().Be(AnswerFileChoice.OwnXml);
            _sut.SelectedXmlPath.Should().Be(path);
            _sut.NeedsApply.Should().BeTrue();
            _mockWimCustomizationService.Verify(s => s.AddXmlToImageAsync(
                It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ChooseOwnXml_WhenAFileIsAlreadyRecorded_DoesNotAskAgain()
    {
        var path = ArrangeSelectedFile();
        try
        {
            await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);
            _sut.Choice = AnswerFileChoice.None;
            _mockFilePickerService.Invocations.Clear();

            await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);

            _sut.Choice.Should().Be(AnswerFileChoice.OwnXml);
            _mockFilePickerService.Verify(p => p.PickFile(It.IsAny<string[]>(), It.IsAny<string>()), Times.Never);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LeavingOwnXml_ClearsItsReportFromTheBanner()
    {
        var path = ArrangeSelectedFile();
        try
        {
            await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);
            _checkState.Subject.Should().Be(path);

            _sut.Choice = AnswerFileChoice.Builder;

            _checkState.Subject.Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ApplyAsync_OwnXml_CopiesTheFileOntoTheMedia()
    {
        var path = ArrangeSelectedFile();
        try
        {
            await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);

            var failure = await _sut.ApplyAsync(new Progress<TaskProgressDetail>(), CancellationToken.None);

            failure.Should().BeNull();
            _mockWimCustomizationService.Verify(s => s.AddXmlToImageAsync(path, "C:\\WorkDir"), Times.Once);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ApplyAsync_OwnXml_WhenAddFails_ReturnsTheReason()
    {
        var path = ArrangeSelectedFile();
        try
        {
            await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);
            _mockWimCustomizationService
                .Setup(s => s.AddXmlToImageAsync(path, "C:\\WorkDir"))
                .ReturnsAsync(OperationResult.Failed("Access to the path is denied"));
            _mockLocalizationService.Setup(l => l.GetString("WIMUtil_Msg_XmlAddFailed")).Returns("Could not add: {0}");

            var failure = await _sut.ApplyAsync(new Progress<TaskProgressDetail>(), CancellationToken.None);

            failure.Should().Be("Could not add: Access to the path is denied");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ApplyAsync_WhenAddThrows_ReturnsTheReason()
    {
        var path = ArrangeSelectedFile();
        try
        {
            await _sut.ChooseOwnXmlCommand.ExecuteAsync(null);
            _mockWimCustomizationService
                .Setup(s => s.AddXmlToImageAsync(path, "C:\\WorkDir"))
                .ThrowsAsync(new IOException("disk full"));

            var failure = await _sut.ApplyAsync(new Progress<TaskProgressDetail>(), CancellationToken.None);

            failure.Should().Be("WIMUtil_Msg_XmlAddFailed");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_AFileOnTheMedia_NeedsNothingApplied()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);

        await _sut.PickUpMediaAnswerFileAsync();

        _sut.Choice.Should().Be(AnswerFileChoice.OwnXml);
        _sut.NeedsApply.Should().BeFalse();
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_InTheSession_AFileOnTheMediaReplacesTheDefaultChoice()
    {
        ArrangeAutounattendSession();
        using var sut = NewSut();
        sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);

        await sut.PickUpMediaAnswerFileAsync();

        sut.Choice.Should().Be(AnswerFileChoice.OwnXml);
        sut.SelectedXmlPath.Should().Be(GeneratedXmlPath);
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_AfterTheUserChoseWinhanceXml_KeepsThatChoice()
    {
        ArrangeAutounattendSession();
        using var sut = NewSut();
        sut.WorkingDirectory = "C:\\WorkDir";
        sut.ChooseBuilderCommand.Execute(null);
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);

        await sut.PickUpMediaAnswerFileAsync();

        sut.Choice.Should().Be(AnswerFileChoice.Builder);
        sut.HasMediaAnswerFile.Should().BeTrue();
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_TheSameMediaAgain_DoesNotReopenItsFile()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);
        _mockAutounattendReader.Setup(r => r.ReadAsync(GeneratedXmlPath)).ReturnsAsync(new WinhanceAnswerFile("26.10.27", new WinhanceConfigFile()));
        _mockBuilderModeEntry.Setup(e => e.EnterFromAsync(BuilderTarget.Autounattend, It.IsAny<WinhanceConfigFile>())).ReturnsAsync(true);

        await _sut.PickUpMediaAnswerFileAsync();
        await _sut.PickUpMediaAnswerFileAsync();

        _mockBuilderModeEntry.Verify(e => e.EnterFromAsync(BuilderTarget.Autounattend, It.IsAny<WinhanceConfigFile>()), Times.Once);
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_InTheSession_JoinsItToTheNewMedia()
    {
        ArrangeAutounattendSession();
        _sut.WorkingDirectory = "C:\\WorkDir";

        await _sut.PickUpMediaAnswerFileAsync();

        _session.WorkingDirectory.Should().Be("C:\\WorkDir");
    }

    [Fact]
    public void LeavingTheSession_FallsBackFromBuilderToNone()
    {
        ArrangeAutounattendSession();
        using var sut = NewSut();
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Normal);

        _mockApplicationModeService.Raise(m => m.ModeChanged += null, this, EventArgs.Empty);

        sut.Choice.Should().Be(AnswerFileChoice.None);
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_AWinhanceFileOnTheMedia_OpensItsSessionWithoutAsking()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        var config = new WinhanceConfigFile();
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);
        _mockAutounattendReader.Setup(r => r.ReadAsync(GeneratedXmlPath)).ReturnsAsync(new WinhanceAnswerFile("26.10.27", config));
        _mockBuilderModeEntry.Setup(e => e.EnterFromAsync(BuilderTarget.Autounattend, config)).ReturnsAsync(true);

        await _sut.PickUpMediaAnswerFileAsync();

        _sut.Choice.Should().Be(AnswerFileChoice.Builder);
        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_AWinhanceFileOverBuilderChanges_AsksFirst()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);
        _mockAutounattendReader.Setup(r => r.ReadAsync(GeneratedXmlPath)).ReturnsAsync(new WinhanceAnswerFile("26.10.27", new WinhanceConfigFile()));
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _mockApplicationModeService.Setup(m => m.HasBuilderChanges).Returns(true);

        await _sut.PickUpMediaAnswerFileAsync();

        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Once);
    }

    [Fact]
    public async Task PickUpMediaAnswerFileAsync_ShowsTheBannerOnlyWhileTheMediaHasAFile()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);
        await _sut.PickUpMediaAnswerFileAsync();
        _sut.HasMediaAnswerFile.Should().BeTrue();

        _sut.WorkingDirectory = "D:\\Other";
        await _sut.PickUpMediaAnswerFileAsync();

        _sut.HasMediaAnswerFile.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteMediaAnswerFileCommand_DeletesTheFileAndClosesTheBanner()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        var onMedia = true;
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(() => onMedia);
        _mockFileSystemService.Setup(f => f.DeleteFile(GeneratedXmlPath)).Callback(() => onMedia = false);
        await _sut.PickUpMediaAnswerFileAsync();

        await _sut.DeleteMediaAnswerFileCommand.ExecuteAsync(null);

        _mockFileSystemService.Verify(f => f.DeleteFile(GeneratedXmlPath), Times.Once);
        _sut.HasMediaAnswerFile.Should().BeFalse();
        _sut.Choice.Should().Be(AnswerFileChoice.None);
        _sut.SelectedXmlPath.Should().BeEmpty();
        _checkState.Subject.Should().BeNull();
    }

    [Fact]
    public async Task DeleteMediaAnswerFileCommand_AFileAddedLater_IsPickedUpAgain()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        var onMedia = true;
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(() => onMedia);
        _mockFileSystemService.Setup(f => f.DeleteFile(GeneratedXmlPath)).Callback(() => onMedia = false);
        await _sut.PickUpMediaAnswerFileAsync();
        await _sut.DeleteMediaAnswerFileCommand.ExecuteAsync(null);
        onMedia = true;

        await _sut.PickUpMediaAnswerFileAsync();

        _sut.Choice.Should().Be(AnswerFileChoice.OwnXml);
        _sut.SelectedXmlPath.Should().Be(GeneratedXmlPath);
    }

    [Fact]
    public async Task DeleteMediaAnswerFileCommand_WhenDeleteFails_SaysSoAndKeepsTheBanner()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.FileExists(GeneratedXmlPath)).Returns(true);
        _mockFileSystemService.Setup(f => f.DeleteFile(GeneratedXmlPath)).Throws(new System.IO.IOException("locked"));
        await _sut.PickUpMediaAnswerFileAsync();

        await _sut.DeleteMediaAnswerFileCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowErrorAsync("WIMUtil_Msg_AnswerFileDeleteFailed", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _sut.HasMediaAnswerFile.Should().BeTrue();
    }
}
