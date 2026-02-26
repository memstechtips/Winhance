using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class AutounattendGeneratorViewModelTests
{
    private readonly Mock<IAutounattendXmlGeneratorService> _xmlGeneratorService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<ILogService> _logService = new();

    // Dependencies for WindowsAppsViewModel
    private readonly Mock<IWindowsAppsService> _windowsAppsService = new();
    private readonly Mock<IAppInstallationService> _appInstallationService = new();
    private readonly Mock<IAppUninstallationService> _appUninstallationService = new();
    private readonly Mock<ITaskProgressService> _progressService = new();
    private readonly Mock<ILogService> _winLogService = new();
    private readonly Mock<IDialogService> _winDialogService = new();
    private readonly Mock<ILocalizationService> _winLocalizationService = new();
    private readonly Mock<IInternetConnectivityService> _connectivityService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();

    public AutounattendGeneratorViewModelTests()
    {
        _localizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
        _winLocalizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
        _dispatcherService.Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _dispatcherService.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);
    }

    private WindowsAppsViewModel CreateWindowsAppsVm() => new(
        _windowsAppsService.Object,
        _appInstallationService.Object,
        _appUninstallationService.Object,
        _progressService.Object,
        _winLogService.Object,
        _winDialogService.Object,
        _winLocalizationService.Object,
        _connectivityService.Object,
        _dispatcherService.Object);

    private AutounattendGeneratorViewModel CreateSut(WindowsAppsViewModel? windowsAppsVm = null)
    {
        var winVm = windowsAppsVm ?? CreateWindowsAppsVm();
        return new AutounattendGeneratorViewModel(
            _xmlGeneratorService.Object,
            _dialogService.Object,
            _localizationService.Object,
            _logService.Object,
            winVm);
    }

    // --- Constructor / defaults ---

    [Fact]
    public void Constructor_SetsDefaults()
    {
        var sut = CreateSut();

        sut.IsGenerating.Should().BeFalse();
    }

    // --- Localized text properties ---

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

    // --- Localized text fallback ---

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

    // --- NavigateToWimUtilRequested event ---

    [Fact]
    public void NavigateToWimUtilRequested_CanBeSubscribedTo()
    {
        var sut = CreateSut();
        bool eventRaised = false;

        sut.NavigateToWimUtilRequested += (_, _) => eventRaised = true;

        eventRaised.Should().BeFalse();
    }

    // --- SetMainWindow ---

    [Fact]
    public void SetMainWindow_DoesNotThrow()
    {
        var sut = CreateSut();

        // SetMainWindow requires a Microsoft.UI.Xaml.Window which we can't easily mock in unit tests,
        // but we can test with null to verify it does not throw
        var act = () => sut.SetMainWindow(null!);

        act.Should().NotThrow();
    }

    // --- GenerateAutounattendXmlCommand ---

    [Fact]
    public async Task GenerateAutounattendXmlCommand_WhenUserCancelsConfirmation_DoesNotGenerate()
    {
        _dialogService.Setup(d => d.ShowConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var sut = CreateSut();
        sut.SetMainWindow(null!);

        // Execute the command
        await sut.GenerateAutounattendXmlCommand.ExecuteAsync(null);

        _xmlGeneratorService.Verify(s => s.GenerateFromCurrentSelectionsAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<Winhance.Core.Features.Common.Models.ConfigurationItem>>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAutounattendXmlCommand_WhenMainWindowIsNull_ReturnsAfterConfirmation()
    {
        _dialogService.Setup(d => d.ShowConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var sut = CreateSut();
        // Do not set main window (it defaults to null)

        await sut.GenerateAutounattendXmlCommand.ExecuteAsync(null);

        _xmlGeneratorService.Verify(s => s.GenerateFromCurrentSelectionsAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<Winhance.Core.Features.Common.Models.ConfigurationItem>>()), Times.Never);
    }

    // --- IsGenerating property ---

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
