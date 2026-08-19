using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
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
    private readonly Mock<IAutounattendWriter> _autounattend = new();
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
        _selections.Setup(s => s.CurrentScope).Returns(CatalogScope.CurrentMachine);
    }

    private AutounattendGeneratorViewModel CreateSut() =>
        new(
            _autounattend.Object,
            _dialogService.Object,
            _localizationService.Object,
            _logService.Object,
            _selections.Object);

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

        sut.GenerateCardDescription.Should().Be("AdvancedTools_GenerateCard_Description");
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
    public void NavigateToWimUtilRequested_CanBeSubscribedTo()
    {
        var sut = CreateSut();
        bool eventRaised = false;

        sut.NavigateToWimUtilRequested += (_, _) => eventRaised = true;

        eventRaised.Should().BeFalse();
    }

    [Fact]
    public void SetMainWindow_DoesNotThrow()
    {
        var sut = CreateSut();

        // SetMainWindow requires a Microsoft.UI.Xaml.Window which we can't easily mock in unit tests,
        // but we can test with null to verify it does not throw
        var act = () => sut.SetMainWindow(null!);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task GenerateAutounattendXmlCommand_WhenUserCancelsConfirmation_DoesNotGenerate()
    {
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        var sut = CreateSut();
        sut.SetMainWindow(null!);

        await sut.GenerateAutounattendXmlCommand.ExecuteAsync(null);

        _autounattend.Verify(w => w.WriteAsync(
            It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAutounattendXmlCommand_WhenMainWindowIsNull_ReturnsAfterConfirmation()
    {
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        var sut = CreateSut();

        await sut.GenerateAutounattendXmlCommand.ExecuteAsync(null);

        _autounattend.Verify(w => w.WriteAsync(
            It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()), Times.Never);
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
