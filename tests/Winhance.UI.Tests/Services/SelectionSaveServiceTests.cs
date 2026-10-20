using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.WimUtil.Models;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class SelectionSaveServiceTests
{
    private const string ConfigPath = @"C:\Users\Test\Winhance_Config_20260820.winhance";
    private const string XmlPath = @"C:\Users\Test\autounattend.xml";
    private const string MisnamedXmlPath = @"C:\Users\Test\answer-file.xml";
    private const string WorkingDirectory = @"C:\WinhanceWIM";

    private static readonly AppChoice[] OneWindowsApp = [new AppChoice("app1", "App 1", null, null, null, null)];

    private static readonly SelectionSet SetWithApps = new(
        Array.Empty<SettingChoice>(), OneWindowsApp, Array.Empty<AppChoice>());

    private const string AlbumFolder = @"D:\Pictures\Holiday";
    private const string StagedAlbum = @"C:\WinhanceWIM\sources\$OEM$\$$\Web\Wallpaper\Winhance\Holiday";
    private const string LandedAlbum = @"C:\Windows\Web\Wallpaper\Winhance\Holiday";

    private static readonly string[] HeicAlbumFiles = [@"D:\Pictures\Holiday\three.heic"];

    private static readonly string[] AlbumFiles =
        [@"D:\Pictures\Holiday\one.jpg", @"D:\Pictures\Holiday\two.PNG", @"D:\Pictures\Holiday\notes.txt"];

    private SelectionSet? _written;

    private readonly Mock<ISelectionSetBuilder> _selections = new();
    private readonly Mock<IConfigFileWriter> _configFiles = new();
    private readonly Mock<IAutounattendWriter> _autounattend = new();
    private readonly Mock<ISaveFilePicker> _picker = new();
    private readonly Mock<IFileStore> _fileStore = new();
    private readonly Mock<IWindowsThemeService> _theme = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<IWimCustomizationService> _wim = new();
    private readonly Mock<IAppStagingService> _staging = new();
    private readonly Mock<ITaskProgressService> _progress = new();
    private readonly WimUtilSession _session = new();
    private readonly Mock<IAnswerFileValidator> _validator = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<ILocalizationService> _loc = new();
    private readonly Mock<ILogService> _log = new();

    public SelectionSaveServiceTests()
    {
        _loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _loc.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => string.Format(key, args));

        _selections.Setup(s => s.CurrentScope).Returns(CatalogScope.CurrentMachine);

        _autounattend
            .Setup(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()))
            .Callback((SelectionSet set, CatalogScope _, string _) => _written = set)
            .ReturnsAsync(XmlPath);

        _fileStore
            .Setup(w => w.LoadAsync(It.IsAny<SelectionSet>()))
            .ReturnsAsync((SelectionSet set) => set);

        _theme.Setup(t => t.AlbumDestinationFor(AlbumFolder)).Returns(LandedAlbum);

        _files.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => Path.Combine(parts));
        _files.Setup(f => f.GetFileName(It.IsAny<string>())).Returns((string path) => Path.GetFileName(path));
        _files.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _files.Setup(f => f.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())).Returns(AlbumFiles);

        _progress.Setup(p => p.CreateDetailedProgress()).Returns(new Progress<TaskProgressDetail>());
        _progress.Setup(p => p.CurrentTaskCancellationSource).Returns(new CancellationTokenSource());
        ArrangeStaging().ReturnsAsync(Array.Empty<StagedApp>());

        _validator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerFileReport([]));
    }

    private SelectionSaveService Sut() => new(
        _selections.Object,
        _configFiles.Object,
        _autounattend.Object,
        _picker.Object,
        _fileStore.Object,
        _theme.Object,
        _files.Object,
        _wim.Object,
        _staging.Object,
        _progress.Object,
        _session,
        _validator.Object,
        _dialogs.Object,
        _loc.Object,
        _log.Object);

    private void ArrangePicker(string? path) =>
        _picker
            .Setup(p => p.PickSavePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(path);

    private void VerifySummary(Func<SummaryRequest, bool> matches) =>
        _dialogs.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r => matches(r))), Times.Once);

    private void VerifyNoSummary() =>
        _dialogs.Verify(d => d.ShowSummaryAsync(It.IsAny<SummaryRequest>()), Times.Never);

    private static bool HasRenameNote(SummaryRequest request) =>
        request.Sections.SelectMany(s => s.Rows).Any(row => row.Title == "AdvancedTools_Msg_RenameForSetup");

    private void VerifyPickerCalled(Times times) =>
        _picker.Verify(
            p => p.PickSavePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            times);

    private void VerifyDriverStep(Times times) =>
        _wim.Verify(
            w => w.EnsureDriverInstallStepAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            times);

    private void VerifyNothingWritten()
    {
        _configFiles.Verify(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()), Times.Never);
        _autounattend.Verify(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(BuilderTarget.Config, ConfigPath, "Config_Export_NoWindowsApps_Detail")]
    [InlineData(BuilderTarget.Autounattend, XmlPath, "WIMUtil_Generate_Plan_NoWindowsApps_Detail")]
    public async Task NoWindowsApps_SavesAnyway_AndWarnsOnTheResult(BuilderTarget target, string destination, string expectedDetail)
    {
        ArrangePicker(destination);

        string? savedPath = (await Sut().SaveAsync(target, SelectionSet.Empty)).Path;

        savedPath.Should().Be(destination);
        _dialogs.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        VerifySummary(r => r.Verdict!.Tone == SummaryTone.Warning
            && r.Verdict.Title == "WIMUtil_Generate_Plan_NoWindowsApps"
            && r.Verdict.Message == expectedDetail);
    }

    [Fact]
    public async Task AnswerFileFindings_AreOnTheSuccessDialog_AboveTheNoAppsWarning()
    {
        ArrangePicker(XmlPath);
        _validator
            .Setup(v => v.ValidateAsync(XmlPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerFileReport(
            [
                new AnswerFileFinding(AnswerFileRule.DuplicateAccountName, AnswerFileSeverity.Error, "line 12: LocalAccount", "Marco"),
            ]));

        await Sut().SaveAsync(BuilderTarget.Autounattend, SelectionSet.Empty);

        VerifySummary(r => r.Title == "Dialog_Success"
            && r.Verdict!.Title == "WIMUtil_AnswerFile_Verdict_WillFail"
            && r.CopyText != null
            && r.Sections.SelectMany(s => s.Rows).Any(row =>
                row.Title == "WIMUtil_AnswerFile_Rule_DuplicateAccountName" && row.Tone == SummaryTone.Error)
            && r.Sections.SelectMany(s => s.Rows).Any(row =>
                row.Title == "WIMUtil_Generate_Plan_NoWindowsApps" && row.Tone == SummaryTone.Warning));
    }

    [Fact]
    public async Task CleanAnswerFile_SaysSo_AndListsTheNextSteps()
    {
        ArrangePicker(XmlPath);

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps);

        VerifySummary(r => r.Verdict!.Tone == SummaryTone.Success
            && r.Sections.Any(s => s.Heading == "AdvancedTools_Msg_NextSteps" && s.Rows.Count == 3));
    }

    [Fact]
    public async Task ACheckThatFails_StillReportsTheSave()
    {
        ArrangePicker(XmlPath);
        _validator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("file locked"));

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps);

        VerifySummary(r => r.Title == "Dialog_Success" && r.Verdict == null);
        _dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AConfig_IsNeverCheckedAsAnAnswerFile()
    {
        ArrangePicker(ConfigPath);

        await Sut().SaveAsync(BuilderTarget.Config, SetWithApps);

        _validator.Verify(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WindowsAppsSelected_ResultCarriesNoWarning()
    {
        ArrangePicker(ConfigPath);

        await Sut().SaveAsync(BuilderTarget.Config, SetWithApps);

        VerifySummary(r => r.Verdict == null);
    }

    [Theory]
    [InlineData(BuilderTarget.Config)]
    [InlineData(BuilderTarget.Autounattend)]
    public async Task PickerCancelled_WritesNothing(BuilderTarget target)
    {
        ArrangePicker(null);

        string? savedPath = (await Sut().SaveAsync(target, SetWithApps)).Path;

        savedPath.Should().BeNull();
        VerifyNothingWritten();
        VerifyNoSummary();
    }

    [Fact]
    public async Task Autounattend_PickedUnderAnotherName_SavesAndSaysSetupNeedsTheName()
    {
        ArrangePicker(MisnamedXmlPath);

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps);

        _autounattend.Verify(w => w.WriteAsync(SetWithApps, CatalogScope.CurrentMachine, MisnamedXmlPath), Times.Once);
        VerifySummary(r => r.Title == "Dialog_Success" && HasRenameNote(r));
    }

    [Fact]
    public async Task Autounattend_PickedAsAutounattendXml_SaysNothingAboutTheName()
    {
        ArrangePicker(XmlPath);

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps);

        VerifySummary(r => r.Title == "Dialog_Success" && !HasRenameNote(r));
    }

    [Fact]
    public async Task FixedPath_SkipsThePickerAndTheFilenameGuard()
    {
        _autounattend
            .Setup(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()))
            .ReturnsAsync(MisnamedXmlPath);

        string? savedPath = (await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps, new SelectionSaveOptions
        {
            FixedPath = MisnamedXmlPath,
        })).Path;

        savedPath.Should().Be(MisnamedXmlPath);
        VerifyPickerCalled(Times.Never());
        _autounattend.Verify(w => w.WriteAsync(SetWithApps, CatalogScope.CurrentMachine, MisnamedXmlPath), Times.Once);
        VerifySummary(r => !HasRenameNote(r));
    }

    [Fact]
    public async Task Config_Success_ShowsTheConfigDialog()
    {
        ArrangePicker(ConfigPath);

        string? savedPath = (await Sut().SaveAsync(BuilderTarget.Config, SetWithApps)).Path;

        savedPath.Should().Be(ConfigPath);
        _configFiles.Verify(w => w.WriteAsync(SetWithApps, CatalogScope.CurrentMachine, ConfigPath), Times.Once);
        VerifySummary(r => r.Message == "Config_Export_Success_Message" && r.Title == "Config_Export_Success_Title");
    }

    [Fact]
    public async Task Autounattend_Success_ShowsTheXmlDialog_AndReportsTheWrittenPath()
    {
        ArrangePicker(XmlPath);

        string? savedPath = (await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps)).Path;

        savedPath.Should().Be(XmlPath);
        VerifySummary(r => r.Message == "AdvancedTools_Msg_XmlGenSuccess" && r.Title == "Dialog_Success");
    }

    [Fact]
    public async Task Autounattend_Success_ShowsAReport_NeverAConfirmation()
    {
        ArrangePicker(XmlPath);

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps);

        _dialogs.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    [Theory]
    [InlineData(BuilderTarget.Config, ConfigPath, "Config_Export_Success_Message", "AdvancedTools_Msg_XmlGenSuccess")]
    [InlineData(BuilderTarget.Autounattend, XmlPath, "AdvancedTools_Msg_XmlGenSuccess", "Config_Export_Success_Message")]
    public async Task Success_ShowsItsOwnMessage_NeverTheOtherTargets(
        BuilderTarget target,
        string destination,
        string ownMessage,
        string otherTargetsMessage)
    {
        ArrangePicker(destination);

        await Sut().SaveAsync(target, SetWithApps);

        VerifySummary(r => r.Message == ownMessage);
        _dialogs.Verify(d => d.ShowSummaryAsync(It.Is<SummaryRequest>(r => r.Message == otherTargetsMessage)), Times.Never);
    }

    [Fact]
    public async Task ReportSuccessInDialog_False_SavesWithoutADialog()
    {
        string? savedPath = (await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps, new SelectionSaveOptions
        {
            FixedPath = XmlPath,
            ReportSuccessInDialog = false,
        })).Path;

        savedPath.Should().Be(XmlPath);
        VerifyNoSummary();
        _dialogs.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    [Fact]
    public async Task Autounattend_OutsideAWimUtilSession_LeavesTheDriverStepAlone()
    {
        ArrangePicker(XmlPath);

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps);

        VerifyDriverStep(Times.Never());
    }

    [Fact]
    public async Task Autounattend_InAWimUtilSession_EnsuresTheDriverStepOnTheWrittenFile()
    {
        ArrangePicker(XmlPath);
        _session.WorkingDirectory = WorkingDirectory;

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps);

        _wim.Verify(
            w => w.EnsureDriverInstallStepAsync(XmlPath, WorkingDirectory, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Config_InAWimUtilSession_LeavesTheDriverStepAlone()
    {
        ArrangePicker(ConfigPath);
        _session.WorkingDirectory = WorkingDirectory;

        await Sut().SaveAsync(BuilderTarget.Config, SetWithApps);

        VerifyDriverStep(Times.Never());
    }

    [Fact]
    public async Task PickerCancelled_InAWimUtilSession_LeavesTheDriverStepAlone()
    {
        ArrangePicker(null);
        _session.WorkingDirectory = WorkingDirectory;

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps);

        VerifyDriverStep(Times.Never());
    }

    [Fact]
    public async Task DriverStepFailure_StillReportsTheSave_AndLogs()
    {
        ArrangePicker(XmlPath);
        _session.WorkingDirectory = WorkingDirectory;
        _wim
            .Setup(w => w.EnsureDriverInstallStepAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("the media went away"));

        string? savedPath = (await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps)).Path;

        savedPath.Should().Be(XmlPath);
        _log.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(m => m.Contains("the media went away")), null, It.IsAny<string>()),
            Times.Once);
        VerifySummary(r => r.Message == "AdvancedTools_Msg_XmlGenSuccess" && r.Title == "Dialog_Success");
    }

    private static SelectionSet SetWithAlbum(string folder) => new(
        [new SettingChoice("theme-wallpaper-album", new ChoiceValue.Text(folder))],
        OneWindowsApp,
        Array.Empty<AppChoice>());

    private static string AlbumFolderOf(SelectionSet? set) =>
        ((ChoiceValue.Text)set!.Settings[0].Value).Value;

    [Fact]
    public async Task Autounattend_InAWimUtilSession_CopiesTheAlbumOntoTheMediaAndNamesWhereItLands()
    {
        ArrangePicker(XmlPath);
        _session.WorkingDirectory = WorkingDirectory;

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithAlbum(AlbumFolder));

        _files.Verify(f => f.CreateDirectory(StagedAlbum), Times.Once);
        _files.Verify(f => f.CopyFile(@"D:\Pictures\Holiday\one.jpg", StagedAlbum + @"\one.jpg", true), Times.Once);
        _files.Verify(f => f.CopyFile(@"D:\Pictures\Holiday\two.PNG", StagedAlbum + @"\two.PNG", true), Times.Once);
        AlbumFolderOf(_written).Should().Be(LandedAlbum);
    }

    [Fact]
    public async Task Autounattend_InAWimUtilSession_CopiesOnlyPictures()
    {
        ArrangePicker(XmlPath);
        _session.WorkingDirectory = WorkingDirectory;

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithAlbum(AlbumFolder));

        _files.Verify(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Autounattend_InAWimUtilSession_CopiesAHeicSlide()
    {
        ArrangePicker(XmlPath);
        _session.WorkingDirectory = WorkingDirectory;
        _files.Setup(f => f.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(HeicAlbumFiles);

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithAlbum(AlbumFolder));

        _files.Verify(f => f.CopyFile(@"D:\Pictures\Holiday\three.heic", StagedAlbum + @"\three.heic", true), Times.Once);
    }

    [Fact]
    public async Task Autounattend_OutsideAWimUtilSession_LeavesTheAlbumWhereItWasAuthored()
    {
        ArrangePicker(XmlPath);

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithAlbum(AlbumFolder));

        _files.Verify(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        AlbumFolderOf(_written).Should().Be(AlbumFolder);
    }

    [Fact]
    public async Task Config_InAWimUtilSession_LeavesTheAlbumAlone()
    {
        ArrangePicker(ConfigPath);
        _session.WorkingDirectory = WorkingDirectory;
        var set = SetWithAlbum(AlbumFolder);

        await Sut().SaveAsync(BuilderTarget.Config, set);

        _files.Verify(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _configFiles.Verify(w => w.WriteAsync(set, CatalogScope.CurrentMachine, ConfigPath), Times.Once);
    }

    [Fact]
    public async Task AnAlbumThisPcDoesNotHave_IsLogged_AndTravelsAsAuthored()
    {
        ArrangePicker(XmlPath);
        _session.WorkingDirectory = WorkingDirectory;
        _files.Setup(f => f.DirectoryExists(AlbumFolder)).Returns(false);

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithAlbum(AlbumFolder));

        _files.Verify(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        AlbumFolderOf(_written).Should().Be(AlbumFolder);
        _log.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(m => m.Contains(AlbumFolder)), null, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Autounattend_InAWimUtilSession_WithNoAlbum_TouchesNoFiles()
    {
        ArrangePicker(XmlPath);
        _session.WorkingDirectory = WorkingDirectory;

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps);

        _files.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Never);
        _written.Should().Be(SetWithApps);
    }

    [Fact]
    public async Task Autounattend_WithAMediaFolderAndNoWimUtilSession_CopiesTheAlbumOntoThatMedia()
    {
        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithAlbum(AlbumFolder), new SelectionSaveOptions
        {
            FixedPath = XmlPath,
            MediaFolder = WorkingDirectory,
            ReportSuccessInDialog = false,
        });

        _files.Verify(f => f.CreateDirectory(StagedAlbum), Times.Once);
        _files.Verify(f => f.CopyFile(@"D:\Pictures\Holiday\one.jpg", StagedAlbum + @"\one.jpg", true), Times.Once);
        AlbumFolderOf(_written).Should().Be(LandedAlbum);
    }

    [Fact]
    public async Task Autounattend_TheMediaFolderTheCallerNames_WinsOverTheSessions()
    {
        _session.WorkingDirectory = @"D:\StaleMedia";

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps, new SelectionSaveOptions
        {
            FixedPath = XmlPath,
            MediaFolder = WorkingDirectory,
            ReportSuccessInDialog = false,
        });

        _wim.Verify(
            w => w.EnsureDriverInstallStepAsync(XmlPath, WorkingDirectory, It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyDriverStep(Times.Once());
    }

    private static readonly AppChoice[] OneExternalApp =
        [new AppChoice("google-chrome", "Google Chrome", null, null, null, "Google.Chrome")];

    private static SelectionSet SetWithExternalApp(params SettingChoice[] settings) =>
        new(settings, OneWindowsApp, OneExternalApp);

    private Moq.Language.Flow.ISetup<IAppStagingService, Task<IReadOnlyList<StagedApp>>> ArrangeStaging() =>
        _staging.Setup(a => a.StageAsync(
            It.IsAny<string>(),
            It.IsAny<SelectionSet>(),
            It.IsAny<IProgress<TaskProgressDetail>>(),
            It.IsAny<CancellationToken>()));

    private void VerifyStaged(Times times) =>
        _staging.Verify(a => a.StageAsync(
            It.IsAny<string>(),
            It.IsAny<SelectionSet>(),
            It.IsAny<IProgress<TaskProgressDetail>>(),
            It.IsAny<CancellationToken>()), times);

    private Task<SelectionSaveResult> SaveOnTheMediaAsync(SelectionSet set) =>
        Sut().SaveAsync(BuilderTarget.Autounattend, set, new SelectionSaveOptions
        {
            FixedPath = XmlPath,
            MediaFolder = WorkingDirectory,
            ReportSuccessInDialog = false,
        });

    [Fact]
    public async Task Autounattend_OnTheMedia_WithACheckedExternalApp_PutsItsInstallerOnTheMedia()
    {
        var set = SetWithExternalApp();

        await SaveOnTheMediaAsync(set);

        _staging.Verify(a => a.StageAsync(
            WorkingDirectory,
            set,
            It.IsAny<IProgress<TaskProgressDetail>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _progress.Verify(p => p.StartTask("WIMUtil_Status_StagingApps", false), Times.Once);
        _progress.Verify(p => p.CompleteTask(), Times.Once);
        _staging.Verify(a => a.RemoveStaged(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TheBarsSave_InAWimUtilSession_PutsTheAppsOnTheSessionsMedia()
    {
        ArrangePicker(XmlPath);
        _session.WorkingDirectory = WorkingDirectory;

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithExternalApp());

        _staging.Verify(a => a.StageAsync(
            WorkingDirectory,
            It.IsAny<SelectionSet>(),
            It.IsAny<IProgress<TaskProgressDetail>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Autounattend_OutsideAWimUtilSession_StagesNothing()
    {
        ArrangePicker(XmlPath);

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithExternalApp());

        VerifyStaged(Times.Never());
        _staging.Verify(a => a.RemoveStaged(It.IsAny<string>()), Times.Never);
    }

    private static readonly OwnInstaller Reader = new(@"C:\Setup\Reader.exe", "/sAll");

    private static readonly SettingChoice OwnReader = new(OwnInstaller.SettingId, new ChoiceValue.List(
    [
        new ChoiceValue.ListRow(new Dictionary<string, string> { ["file"] = Reader.FilePath, ["switches"] = Reader.SilentSwitches }),
    ]));

    private static readonly SettingChoice StagingOff = new(OwnInstaller.StageAppsId, new ChoiceValue.CheckBox(false));

    private static readonly SettingChoice WinhanceOff = new("autounattend-desktop-shortcut", new ChoiceValue.Option(0));

    private static bool WarnsInstallersAreNotOnMedia(SummaryRequest request) =>
        request.Sections.SelectMany(s => s.Rows).Any(row =>
            row.Title == "Builder_Save_OwnInstallersNotOnMedia" && row.Tone == SummaryTone.Warning);

    [Fact]
    public async Task Autounattend_OnTheMedia_OwnInstallersGoOnTheMediaEvenWithTheCheckboxOff()
    {
        await SaveOnTheMediaAsync(SetWithExternalApp(StagingOff, OwnReader));

        _staging.Verify(a => a.StageAsync(
            WorkingDirectory,
            It.Is<SelectionSet>(staged => staged.ExternalApps.Count == 0
                && OwnInstaller.From(staged).Single() == Reader),
            It.IsAny<IProgress<TaskProgressDetail>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Autounattend_OnTheMedia_TheAnswerFileKnowsAboutTheOwnInstallers()
    {
        await SaveOnTheMediaAsync(SetWithExternalApp(OwnReader));

        OwnInstaller.From(_written!).Should().Equal(Reader);
    }

    // The new PC downloads what was not staged, so the file still names the apps.
    [Fact]
    public async Task Autounattend_OnTheMedia_TheAnswerFileKnowsTheStagingChoice()
    {
        await SaveOnTheMediaAsync(SetWithExternalApp(StagingOff));

        OwnInstaller.StagesApps(_written!).Should().BeFalse();
        _written!.ExternalApps.Should().ContainSingle();
    }

    [Fact]
    public async Task Autounattend_OnTheMedia_WithWinhanceSetToDontAdd_TheAnswerFileCarriesNoOwnInstallers()
    {
        await SaveOnTheMediaAsync(SetWithExternalApp(OwnReader, WinhanceOff));

        OwnInstaller.From(_written!).Should().BeEmpty();
    }

    [Fact]
    public async Task Autounattend_OutsideAWimUtilSession_TheAnswerFileStillCarriesTheOwnInstallers_AndStagesNothing()
    {
        ArrangePicker(XmlPath);

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithExternalApp(OwnReader));

        OwnInstaller.From(_written!).Should().Equal(Reader);
        VerifyStaged(Times.Never());
    }

    [Fact]
    public async Task TheBarsSave_WithOwnInstallersAndNoMedia_WarnsTheyAreNotOnAnyMediaYet()
    {
        ArrangePicker(XmlPath);

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithExternalApp(OwnReader));

        VerifySummary(WarnsInstallersAreNotOnMedia);
    }

    [Fact]
    public async Task TheBarsSave_InAWimUtilSession_WithOwnInstallers_DoesNotWarnAboutMedia()
    {
        ArrangePicker(XmlPath);
        _session.WorkingDirectory = WorkingDirectory;

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithExternalApp(OwnReader));

        VerifySummary(request => !WarnsInstallersAreNotOnMedia(request));
    }

    [Fact]
    public async Task TheBarsSave_WithNoOwnInstallersAndNoMedia_DoesNotWarnAboutMedia()
    {
        ArrangePicker(XmlPath);

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithExternalApp());

        VerifySummary(request => !WarnsInstallersAreNotOnMedia(request));
    }

    [Fact]
    public async Task Autounattend_OnTheMedia_WithTheCheckboxOffAndNoOwnInstallers_ClearsWhatAnEarlierRunStaged()
    {
        await SaveOnTheMediaAsync(SetWithExternalApp(StagingOff));

        _staging.Verify(a => a.RemoveStaged(WorkingDirectory), Times.Once);
        VerifyStaged(Times.Never());
        _progress.Verify(p => p.StartTask(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Autounattend_OnTheMedia_WithWinhanceSetToDontAdd_ClearsWhatAnEarlierRunStaged()
    {
        await SaveOnTheMediaAsync(SetWithExternalApp(OwnReader, WinhanceOff));

        _staging.Verify(a => a.RemoveStaged(WorkingDirectory), Times.Once);
        VerifyStaged(Times.Never());
    }

    [Fact]
    public async Task Autounattend_OnTheMedia_WithWinhanceInstalledAutomatically_Stages()
    {
        await SaveOnTheMediaAsync(SetWithExternalApp(
            new SettingChoice("autounattend-desktop-shortcut", new ChoiceValue.Option(2))));

        VerifyStaged(Times.Once());
        _staging.Verify(a => a.RemoveStaged(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TheBarsSave_OnTheMedia_NamesTheAppsThatCouldNotBeStaged()
    {
        ArrangePicker(XmlPath);
        _session.WorkingDirectory = WorkingDirectory;
        _loc.Setup(l => l.GetString("WIMUtil_Msg_AppsNotStaged", It.IsAny<object[]>()))
            .Returns((string _, object[] args) => "Not on the media:" + args[0]);
        ArrangeStaging().ReturnsAsync((IReadOnlyList<StagedApp>)[new StagedApp("Google Chrome", false), new StagedApp("Steam", true)]);

        await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithExternalApp());

        _dialogs.Verify(d => d.ShowInformationAsync(
            "Not on the media:Google Chrome",
            "Dialog_Information",
            It.IsAny<string>()), Times.Once);
    }

    // WIMUtil gathers the run into one summary of its own, so the service hands it the outcomes instead of
    // opening a dialog over the top of it.
    [Fact]
    public async Task Autounattend_OnTheMedia_ReturnsEveryStagingOutcomeAndOpensNoDialog()
    {
        ArrangeStaging().ReturnsAsync((IReadOnlyList<StagedApp>)[new StagedApp("Google Chrome", true), new StagedApp("Steam", false)]);

        var saved = await SaveOnTheMediaAsync(SetWithExternalApp());

        saved.Path.Should().Be(XmlPath);
        saved.Apps.Should().Equal(new StagedApp("Google Chrome", true), new StagedApp("Steam", false));
        _dialogs.Verify(d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Autounattend_OnTheMedia_StagingThrows_WarnsAndStillReturnsThePath()
    {
        ArrangeStaging().ThrowsAsync(new IOException("The media is full"));

        string? savedPath = (await SaveOnTheMediaAsync(SetWithExternalApp())).Path;

        savedPath.Should().Be(XmlPath);
        _progress.Verify(p => p.FailTask(), Times.Once);
        _progress.Verify(p => p.CompleteTask(), Times.Never);
        _dialogs.Verify(d => d.ShowWarningAsync("The media is full", "Dialog_Warning", It.IsAny<string>()), Times.Once);
        _log.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(m => m.Contains("The media is full")), It.IsAny<Exception>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Autounattend_OnTheMedia_StagingCancelled_EndsTheTaskAsCancelled()
    {
        ArrangeStaging().ThrowsAsync(new OperationCanceledException());

        string? savedPath = (await SaveOnTheMediaAsync(SetWithExternalApp())).Path;

        savedPath.Should().Be(XmlPath);
        _progress.Verify(p => p.CancelTask(), Times.Once);
        _progress.Verify(p => p.FailTask(), Times.Never);
        _dialogs.Verify(d => d.ShowWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task WriteFailure_ReachesTheCaller()
    {
        ArrangePicker(ConfigPath);
        _configFiles
            .Setup(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()))
            .ThrowsAsync(new IOException("disk full"));

        var act = () => Sut().SaveAsync(BuilderTarget.Config, SetWithApps);

        await act.Should().ThrowAsync<IOException>();
        _dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
