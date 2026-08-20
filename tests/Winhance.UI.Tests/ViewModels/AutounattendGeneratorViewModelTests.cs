using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.ViewModels;

public class AutounattendGeneratorViewModelTests
{
    private const string XmlPath = @"C:\Users\Test\autounattend.xml";

    private readonly Mock<ISelectionSaveService> _saves = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<ISelectionSetBuilder> _selections = new();

    public AutounattendGeneratorViewModelTests()
    {
        _localizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
        _localizationService.MirrorTryGetString();

        _selections.Setup(s => s.FromMachineAsync()).ReturnsAsync(SelectionSet.Empty);

        _saves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync(XmlPath);
    }

    private AutounattendGeneratorViewModel CreateSut() =>
        new(
            _saves.Object,
            _dialogService.Object,
            _localizationService.Object,
            _logService.Object,
            _selections.Object);

    private void ArrangeSnapshotConfirmed() =>
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

    [Fact]
    public void Constructor_SetsDefaults()
    {
        var sut = CreateSut();

        sut.IsGenerating.Should().BeFalse();
    }

    [Fact]
    public void GenerateCardHeader_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.GenerateCardHeader.Should().Be("Dialog_GenerateXml");
    }

    [Fact]
    public void GenerateCardDescription_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.GenerateCardDescription.Should().Be("AdvancedTools_GenerateCard_Snapshot_Description");
    }

    [Fact]
    public void InfoBarTitle_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.InfoBarTitle.Should().Be("AdvancedTools_InfoBar_MoreOptionsTitle");
    }

    [Fact]
    public void InfoBarMessage_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.InfoBarMessage.Should().Be("AdvancedTools_InfoBar_MoreOptionsMessage");
    }

    [Fact]
    public void GenerateButtonText_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.GenerateButtonText.Should().Be("WIMUtil_ButtonGenerate");
    }

    [Fact]
    public void GenerateCardHeader_WhenLocalizationReturnsNull_UsesFallback()
    {
        _localizationService.Setup(l => l.GetString("Dialog_GenerateXml"))
            .Returns((string)null!);

        var sut = CreateSut();

        sut.GenerateCardHeader.Should().Be("Generate Autounattend XML");
    }

    [Fact]
    public void GenerateButtonText_WhenLocalizationReturnsNull_UsesFallback()
    {
        _localizationService.Setup(l => l.GetString("WIMUtil_ButtonGenerate"))
            .Returns((string)null!);

        var sut = CreateSut();

        sut.GenerateButtonText.Should().Be("Generate");
    }

    [Fact]
    public async Task GenerateAutounattendXmlCommand_WhenUserCancelsConfirmation_DoesNotSave()
    {
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        var sut = CreateSut();

        await sut.GenerateAutounattendXmlCommand.ExecuteAsync(null);

        _saves.Verify(s => s.SaveAsync(
            It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAutounattendXmlCommand_SavesTheMachineSnapshot_WithTheServiceDefaults()
    {
        ArrangeSnapshotConfirmed();

        var sut = CreateSut();

        await sut.GenerateAutounattendXmlCommand.ExecuteAsync(null);

        _selections.Verify(s => s.FromMachineAsync(), Times.Once);
        _saves.Verify(s => s.SaveAsync(BuilderTarget.Autounattend, SelectionSet.Empty, null), Times.Once);
    }

    [Fact]
    public async Task GenerateAutounattendXmlCommand_WhenTheSaveThrows_ShowsErrorDialog()
    {
        ArrangeSnapshotConfirmed();
        _saves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ThrowsAsync(new IOException("disk full"));

        var sut = CreateSut();

        await sut.GenerateAutounattendXmlCommand.ExecuteAsync(null);

        _dialogService.Verify(d => d.ShowErrorAsync(
            "AdvancedTools_Msg_XmlGenError", "Dialog_XmlGenError", ""), Times.Once);
        sut.IsGenerating.Should().BeFalse();
    }

    [Fact]
    public void IsGenerating_DefaultsFalse()
    {
        var sut = CreateSut();

        sut.IsGenerating.Should().BeFalse();
    }

    [Fact]
    public void IsGenerating_CanBeSetAndNotifiesPropertyChanged()
    {
        var sut = CreateSut();
        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        sut.IsGenerating = true;

        sut.IsGenerating.Should().BeTrue();
        changedProperties.Should().Contain("IsGenerating");
    }
}
