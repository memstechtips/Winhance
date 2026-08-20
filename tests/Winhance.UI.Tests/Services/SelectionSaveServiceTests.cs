using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class SelectionSaveServiceTests
{
    private const string ConfigPath = @"C:\Users\Test\Winhance_Config_20260820.winhance";
    private const string XmlPath = @"C:\Users\Test\autounattend.xml";
    private const string MisnamedXmlPath = @"C:\Users\Test\answer-file.xml";

    private static readonly AppChoice[] OneWindowsApp = [new AppChoice("app1", "App 1", null, null, null, null)];

    private static readonly SelectionSet SetWithApps = new(
        Array.Empty<SettingChoice>(), OneWindowsApp, Array.Empty<AppChoice>(), AutounattendChoices.None);

    private readonly Mock<ISelectionSetBuilder> _selections = new();
    private readonly Mock<IConfigFileWriter> _configFiles = new();
    private readonly Mock<IAutounattendWriter> _autounattend = new();
    private readonly Mock<ISaveFilePicker> _picker = new();
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
            .ReturnsAsync(XmlPath);

        _dialogs
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });
    }

    private SelectionSaveService Sut() => new(
        _selections.Object,
        _configFiles.Object,
        _autounattend.Object,
        _picker.Object,
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
