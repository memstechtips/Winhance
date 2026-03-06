using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class MoreMenuViewModelTests
{
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IVersionService> _mockVersionService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IApplicationCloseService> _mockCloseService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IExplorerWindowManager> _mockExplorerWindowManager = new();

    public MoreMenuViewModelTests()
    {
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
    }

    private MoreMenuViewModel CreateViewModel()
    {
        return new MoreMenuViewModel(
            _mockLocalization.Object,
            _mockVersionService.Object,
            _mockLogService.Object,
            _mockCloseService.Object,
            _mockFileSystemService.Object,
            _mockExplorerWindowManager.Object);
    }

    // -------------------------------------------------------
    // Constructor / Initialization
    // -------------------------------------------------------

    [Fact]
    public void Constructor_InitializesVersionInfo_WithVersionFromService()
    {
        _mockVersionService
            .Setup(v => v.GetCurrentVersion())
            .Returns(new VersionInfo { Version = "v25.06.01" });

        var vm = CreateViewModel();

        vm.VersionInfo.Should().Be("Winhance v25.06.01");
    }

    [Fact]
    public void Constructor_WhenVersionServiceThrows_DefaultsToWinhance()
    {
        _mockVersionService
            .Setup(v => v.GetCurrentVersion())
            .Throws(new InvalidOperationException("No version"));

        var vm = CreateViewModel();

        vm.VersionInfo.Should().Be("Winhance");
    }

    [Fact]
    public void Constructor_SubscribesToLanguageChanged()
    {
        var vm = CreateViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _mockLocalization.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        changedProperties.Should().Contain(nameof(vm.MenuDocumentation));
        changedProperties.Should().Contain(nameof(vm.MenuReportBug));
        changedProperties.Should().Contain(nameof(vm.MenuCheckForUpdates));
        changedProperties.Should().Contain(nameof(vm.MenuWinhanceLogs));
        changedProperties.Should().Contain(nameof(vm.MenuWinhanceScripts));
        changedProperties.Should().Contain(nameof(vm.MenuCloseWinhance));
    }

    // -------------------------------------------------------
    // Localized string properties
    // -------------------------------------------------------

    [Fact]
    public void MenuDocumentation_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("Tooltip_Documentation"))
            .Returns("Docs");

        var vm = CreateViewModel();

        vm.MenuDocumentation.Should().Be("Docs");
    }

    [Fact]
    public void MenuDocumentation_WhenLocalizationReturnsNull_ReturnsFallback()
    {
        _mockLocalization
            .Setup(l => l.GetString("Tooltip_Documentation"))
            .Returns((string?)null!);

        var vm = CreateViewModel();

        vm.MenuDocumentation.Should().Be("Documentation");
    }

    [Fact]
    public void MenuReportBug_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("Tooltip_ReportBug"))
            .Returns("Bug Report");

        var vm = CreateViewModel();

        vm.MenuReportBug.Should().Be("Bug Report");
    }

    [Fact]
    public void MenuReportBug_WhenLocalizationReturnsNull_ReturnsFallback()
    {
        _mockLocalization
            .Setup(l => l.GetString("Tooltip_ReportBug"))
            .Returns((string?)null!);

        var vm = CreateViewModel();

        vm.MenuReportBug.Should().Be("Report a Bug");
    }

    [Fact]
    public void MenuCheckForUpdates_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("Menu_CheckForUpdates"))
            .Returns("Update Check");

        var vm = CreateViewModel();

        vm.MenuCheckForUpdates.Should().Be("Update Check");
    }

    [Fact]
    public void MenuWinhanceLogs_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("Menu_WinhanceLogs"))
            .Returns("Logs");

        var vm = CreateViewModel();

        vm.MenuWinhanceLogs.Should().Be("Logs");
    }

    [Fact]
    public void MenuWinhanceScripts_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("Menu_WinhanceScripts"))
            .Returns("Scripts");

        var vm = CreateViewModel();

        vm.MenuWinhanceScripts.Should().Be("Scripts");
    }

    [Fact]
    public void MenuCloseWinhance_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("Menu_CloseWinhance"))
            .Returns("Exit");

        var vm = CreateViewModel();

        vm.MenuCloseWinhance.Should().Be("Exit");
    }

    // -------------------------------------------------------
    // OpenLogsCommand
    // -------------------------------------------------------

    [Fact]
    public async Task OpenLogsCommand_WhenDirectoryDoesNotExist_CreatesItAndOpens()
    {
        _mockFileSystemService
            .Setup(fs => fs.DirectoryExists(It.IsAny<string>()))
            .Returns(false);
        _mockExplorerWindowManager
            .Setup(e => e.OpenFolderAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel();

        await vm.OpenLogsCommand.ExecuteAsync(null);

        _mockFileSystemService.Verify(
            fs => fs.CreateDirectory(It.IsAny<string>()),
            Times.Once);
        _mockExplorerWindowManager.Verify(
            e => e.OpenFolderAsync(It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenLogsCommand_WhenDirectoryExists_DoesNotCreateIt()
    {
        _mockFileSystemService
            .Setup(fs => fs.DirectoryExists(It.IsAny<string>()))
            .Returns(true);
        _mockExplorerWindowManager
            .Setup(e => e.OpenFolderAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel();

        await vm.OpenLogsCommand.ExecuteAsync(null);

        _mockFileSystemService.Verify(
            fs => fs.CreateDirectory(It.IsAny<string>()),
            Times.Never);
        _mockExplorerWindowManager.Verify(
            e => e.OpenFolderAsync(It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenLogsCommand_WhenExceptionThrown_LogsError()
    {
        _mockFileSystemService
            .Setup(fs => fs.DirectoryExists(It.IsAny<string>()))
            .Throws(new IOException("disk error"));

        var vm = CreateViewModel();

        // Should not throw
        await vm.OpenLogsCommand.ExecuteAsync(null);

        _mockLogService.Verify(
            l => l.LogError(It.Is<string>(s => s.Contains("disk error")), It.IsAny<Exception>()),
            Times.Once);
    }

    // -------------------------------------------------------
    // OpenScriptsCommand
    // -------------------------------------------------------

    [Fact]
    public async Task OpenScriptsCommand_WhenDirectoryDoesNotExist_CreatesItAndOpens()
    {
        _mockFileSystemService
            .Setup(fs => fs.DirectoryExists(It.IsAny<string>()))
            .Returns(false);
        _mockExplorerWindowManager
            .Setup(e => e.OpenFolderAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel();

        await vm.OpenScriptsCommand.ExecuteAsync(null);

        _mockFileSystemService.Verify(
            fs => fs.CreateDirectory(It.IsAny<string>()),
            Times.Once);
        _mockExplorerWindowManager.Verify(
            e => e.OpenFolderAsync(It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenScriptsCommand_WhenDirectoryExists_SkipsCreation()
    {
        _mockFileSystemService
            .Setup(fs => fs.DirectoryExists(It.IsAny<string>()))
            .Returns(true);
        _mockExplorerWindowManager
            .Setup(e => e.OpenFolderAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel();

        await vm.OpenScriptsCommand.ExecuteAsync(null);

        _mockFileSystemService.Verify(
            fs => fs.CreateDirectory(It.IsAny<string>()),
            Times.Never);
    }

    // -------------------------------------------------------
    // CloseApplicationCommand
    // -------------------------------------------------------

    [Fact]
    public async Task CloseApplicationCommand_CallsCheckOperationsAndClose()
    {
        _mockCloseService
            .Setup(c => c.CheckOperationsAndCloseAsync())
            .ReturnsAsync(OperationResult.Succeeded());

        var vm = CreateViewModel();

        await vm.CloseApplicationCommand.ExecuteAsync(null);

        _mockLogService.Verify(
            l => l.LogInformation(It.Is<string>(s => s.Contains("application close"))),
            Times.Once);
        _mockCloseService.Verify(
            c => c.CheckOperationsAndCloseAsync(),
            Times.Once);
    }

    [Fact]
    public async Task CloseApplicationCommand_WhenThrows_LogsError()
    {
        _mockCloseService
            .Setup(c => c.CheckOperationsAndCloseAsync())
            .ThrowsAsync(new InvalidOperationException("cannot close"));

        var vm = CreateViewModel();

        await vm.CloseApplicationCommand.ExecuteAsync(null);

        _mockLogService.Verify(
            l => l.LogError(It.Is<string>(s => s.Contains("cannot close")), It.IsAny<Exception>()),
            Times.Once);
    }

    // -------------------------------------------------------
    // VersionInfo property
    // -------------------------------------------------------

    [Fact]
    public void VersionInfo_Set_RaisesPropertyChanged()
    {
        var vm = CreateViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.VersionInfo))
                raised = true;
        };

        vm.VersionInfo = "Winhance v99.0.0";

        raised.Should().BeTrue();
        vm.VersionInfo.Should().Be("Winhance v99.0.0");
    }

    // -------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------

    [Fact]
    public void Dispose_UnsubscribesFromLanguageChanged()
    {
        var vm = CreateViewModel();
        vm.Dispose();

        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _mockLocalization.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        changedProperties.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var vm = CreateViewModel();

        var act = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        act.Should().NotThrow();
    }
}
