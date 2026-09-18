using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.WimUtil.Interfaces;
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
    private readonly WimUtilSession _session = new();
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

        _dialogs
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        _fileStore
            .Setup(w => w.LoadAsync(It.IsAny<SelectionSet>()))
            .ReturnsAsync((SelectionSet set) => set);

        _theme.Setup(t => t.AlbumDestinationFor(AlbumFolder)).Returns(LandedAlbum);

        _files.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => Path.Combine(parts));
        _files.Setup(f => f.GetFileName(It.IsAny<string>())).Returns((string path) => Path.GetFileName(path));
        _files.Setup(f => f.GetExtension(It.IsAny<string>())).Returns((string path) => Path.GetExtension(path));
        _files.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _files.Setup(f => f.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())).Returns(AlbumFiles);
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
        _session,
        _dialogs.Object,
        _loc.Object,
        _log.Object);

    private void ArrangePicker(string? path) =>
        _picker
            .Setup(p => p.PickSavePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(path);

    private void ArrangeConfirmation(bool confirmed) =>
        _dialogs
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = confirmed });

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
    [InlineData(BuilderTarget.Config, "Dialog_NoAppsSelected_Config_Message")]
    [InlineData(BuilderTarget.Autounattend, "Dialog_NoAppsSelected_Xml_Message")]
    public async Task NoWindowsApps_AsksWithTheTargetsMessage(BuilderTarget target, string expectedMessage)
    {
        ArrangePicker(target == BuilderTarget.Config ? ConfigPath : XmlPath);

        await Sut().SaveAsync(target, SelectionSet.Empty);

        _dialogs.Verify(
            d => d.ShowConfirmationAsync(It.Is<ConfirmationRequest>(r =>
                r.Message == expectedMessage && r.Title == "Dialog_NoAppsSelected_Title")),
            Times.Once);
        VerifyPickerCalled(Times.Once());
    }

    [Fact]
    public async Task NoWindowsApps_Declined_WritesNothing()
    {
        ArrangeConfirmation(false);
        ArrangePicker(ConfigPath);

        string? savedPath = await Sut().SaveAsync(BuilderTarget.Config, SelectionSet.Empty);

        savedPath.Should().BeNull();
        VerifyPickerCalled(Times.Never());
        VerifyNothingWritten();
    }

    [Fact]
    public async Task NoWindowsApps_WhenTheCallerDoesNotWantTheQuestion_WritesSilently()
    {
        string? savedPath = await Sut().SaveAsync(BuilderTarget.Config, SelectionSet.Empty, new SelectionSaveOptions
        {
            FixedPath = ConfigPath,
            ConfirmEmptyAppSelection = false,
            ReportSuccessInDialog = false,
        });

        savedPath.Should().Be(ConfigPath);
        _dialogs.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _configFiles.Verify(w => w.WriteAsync(SelectionSet.Empty, CatalogScope.CurrentMachine, ConfigPath), Times.Once);
    }

    [Fact]
    public async Task WindowsAppsSelected_DoesNotAsk()
    {
        ArrangePicker(ConfigPath);

        await Sut().SaveAsync(BuilderTarget.Config, SetWithApps);

        _dialogs.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    [Theory]
    [InlineData(BuilderTarget.Config)]
    [InlineData(BuilderTarget.Autounattend)]
    public async Task PickerCancelled_WritesNothing(BuilderTarget target)
    {
        ArrangePicker(null);

        string? savedPath = await Sut().SaveAsync(target, SetWithApps);

        savedPath.Should().BeNull();
        VerifyNothingWritten();
        _dialogs.Verify(d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Autounattend_PickedUnderAnotherName_StopsWithTheFilenameWarning()
    {
        ArrangePicker(MisnamedXmlPath);

        string? savedPath = await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps);

        savedPath.Should().BeNull();
        _dialogs.Verify(
            d => d.ShowInformationAsync("AdvancedTools_Msg_InvalidFilename", "Dialog_Warning", ""),
            Times.Once);
        VerifyNothingWritten();
    }

    [Fact]
    public async Task FixedPath_SkipsThePickerAndTheFilenameGuard()
    {
        _autounattend
            .Setup(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()))
            .ReturnsAsync(MisnamedXmlPath);

        string? savedPath = await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps, new SelectionSaveOptions
        {
            FixedPath = MisnamedXmlPath,
        });

        savedPath.Should().Be(MisnamedXmlPath);
        VerifyPickerCalled(Times.Never());
        _autounattend.Verify(w => w.WriteAsync(SetWithApps, CatalogScope.CurrentMachine, MisnamedXmlPath), Times.Once);
        _dialogs.Verify(d => d.ShowInformationAsync("AdvancedTools_Msg_InvalidFilename", It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Config_Success_ShowsTheConfigDialog()
    {
        ArrangePicker(ConfigPath);

        string? savedPath = await Sut().SaveAsync(BuilderTarget.Config, SetWithApps);

        savedPath.Should().Be(ConfigPath);
        _configFiles.Verify(w => w.WriteAsync(SetWithApps, CatalogScope.CurrentMachine, ConfigPath), Times.Once);
        _dialogs.Verify(
            d => d.ShowInformationAsync("Config_Export_Success_Message", "Config_Export_Success_Title", ""),
            Times.Once);
    }

    [Fact]
    public async Task Autounattend_Success_ShowsTheXmlDialog_AndReportsTheWrittenPath()
    {
        ArrangePicker(XmlPath);

        string? savedPath = await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps);

        savedPath.Should().Be(XmlPath);
        _dialogs.Verify(
            d => d.ShowInformationAsync("AdvancedTools_Msg_XmlGenSuccess", "Dialog_Success", ""),
            Times.Once);
    }

    [Fact]
    public async Task Autounattend_Success_ShowsInformation_NeverAConfirmation()
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

        _dialogs.Verify(d => d.ShowInformationAsync(ownMessage, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _dialogs.Verify(d => d.ShowInformationAsync(otherTargetsMessage, It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReportSuccessInDialog_False_SavesWithoutADialog()
    {
        string? savedPath = await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps, new SelectionSaveOptions
        {
            FixedPath = XmlPath,
            ReportSuccessInDialog = false,
        });

        savedPath.Should().Be(XmlPath);
        _dialogs.Verify(d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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

        string? savedPath = await Sut().SaveAsync(BuilderTarget.Autounattend, SetWithApps);

        savedPath.Should().Be(XmlPath);
        _log.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(m => m.Contains("the media went away")), null, It.IsAny<string>()),
            Times.Once);
        _dialogs.Verify(
            d => d.ShowInformationAsync("AdvancedTools_Msg_XmlGenSuccess", "Dialog_Success", ""),
            Times.Once);
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
        _written.Should().BeSameAs(SetWithApps);
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
