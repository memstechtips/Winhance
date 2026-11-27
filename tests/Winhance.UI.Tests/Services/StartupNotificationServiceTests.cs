using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class StartupNotificationServiceTests
{
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<IUserPreferencesService> _mockPrefsService = new();
    private readonly Mock<ISystemBackupService> _mockBackupService = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();

    public StartupNotificationServiceTests()
    {
        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());
        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<TaskProgressDetail>());
    }

    private StartupNotificationService CreateService()
    {
        return new StartupNotificationService(
            _mockDialogService.Object,
            _mockPrefsService.Object,
            _mockBackupService.Object,
            _mockTaskProgressService.Object,
            _mockLogService.Object,
            _mockLocalizationService.Object);
    }

    private void SetupLocalizationDefaults()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns("Localized text");
    }

    [Fact]
    public async Task ShowFirstLaunchRestoreOfferAsync_WhenAlreadyOffered_ReturnsImmediately()
    {
        _mockPrefsService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialRestorePointOffered, false))
            .Returns(true);

        var service = CreateService();

        await service.ShowFirstLaunchRestoreOfferAsync();

        _mockDialogService.Verify(
            d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task ShowFirstLaunchRestoreOfferAsync_WhenFirstLaunch_ShowsConfirmationDialog()
    {
        SetupLocalizationDefaults();
        _mockPrefsService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialRestorePointOffered, false))
            .Returns(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        var service = CreateService();

        await service.ShowFirstLaunchRestoreOfferAsync();

        _mockDialogService.Verify(
            d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()),
            Times.Once);
    }

    [Fact]
    public async Task ShowFirstLaunchRestoreOfferAsync_SetsInitialRestorePointOfferedBeforeDialog()
    {
        SetupLocalizationDefaults();
        _mockPrefsService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialRestorePointOffered, false))
            .Returns(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        var service = CreateService();

        await service.ShowFirstLaunchRestoreOfferAsync();

        _mockPrefsService.Verify(
            p => p.SetPreferenceAsync(UserPreferenceKeys.InitialRestorePointOffered, true),
            Times.Once);
    }

    [Fact]
    public async Task ShowFirstLaunchRestoreOfferAsync_WhenUserClicksCreate_CallsCreateRestorePointAsync()
    {
        SetupLocalizationDefaults();
        _mockPrefsService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialRestorePointOffered, false))
            .Returns(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        _mockBackupService
            .Setup(b => b.CreateRestorePointAsync(
                It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BackupResult.CreateSuccess(restorePointCreated: true));

        var service = CreateService();

        await service.ShowFirstLaunchRestoreOfferAsync();

        _mockBackupService.Verify(
            b => b.CreateRestorePointAsync(
                It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ShowFirstLaunchRestoreOfferAsync_WhenRestorePointSucceeds_ShowsInformationDialog()
    {
        SetupLocalizationDefaults();
        _mockPrefsService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialRestorePointOffered, false))
            .Returns(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        _mockBackupService
            .Setup(b => b.CreateRestorePointAsync(
                It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BackupResult.CreateSuccess(restorePointCreated: true));

        var service = CreateService();

        await service.ShowFirstLaunchRestoreOfferAsync();

        _mockDialogService.Verify(
            d => d.ShowInformationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ShowFirstLaunchRestoreOfferAsync_WhenRestorePointFails_ShowsWarningDialog()
    {
        SetupLocalizationDefaults();
        _mockPrefsService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialRestorePointOffered, false))
            .Returns(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        _mockBackupService
            .Setup(b => b.CreateRestorePointAsync(
                It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BackupResult.CreateFailure("Something went wrong"));

        var service = CreateService();

        await service.ShowFirstLaunchRestoreOfferAsync();

        _mockDialogService.Verify(
            d => d.ShowWarningAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShowFirstLaunchRestoreOfferAsync_FailsAfterTurningProtectionOn_OneDialogAsksWhetherToKeepIt(bool turnOff)
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        _mockPrefsService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialRestorePointOffered, false))
            .Returns(false);
        var asked = new List<ConfirmationRequest>();
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .Callback((ConfirmationRequest r) => asked.Add(r))
            .ReturnsAsync(() => new ConfirmationResponse { Confirmed = asked.Count == 1 || turnOff });
        _mockBackupService
            .Setup(b => b.CreateRestorePointAsync(
                It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BackupResult.CreateFailure("disk full", turnedProtectionOn: true));

        var service = CreateService();

        await service.ShowFirstLaunchRestoreOfferAsync();

        asked.Should().HaveCount(2);
        asked[1].Title.Should().Be("Startup_Backup_Title_Fail");
        asked[1].Message.Should().Contain("Startup_Backup_RestoreCreatedFail").And.Contain("RestorePoint_Fail_ProtectionTurnedOn");
        asked[1].ConfirmButtonText.Should().Be("RestorePoint_Fail_Button_TurnOff");
        _mockBackupService.Verify(b => b.TurnOffSystemRestoreAsync(), turnOff ? Times.Once() : Times.Never());
        _mockDialogService.Verify(
            d => d.ShowWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ShowFirstLaunchRestoreOfferAsync_WhenCancelled_EndsTheTaskAsCancelledWithNoDialog()
    {
        SetupLocalizationDefaults();
        _mockPrefsService.Setup(p => p.GetPreference(UserPreferenceKeys.InitialRestorePointOffered, false)).Returns(false);
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        _mockBackupService
            .Setup(b => b.CreateRestorePointAsync(
                It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await CreateService().ShowFirstLaunchRestoreOfferAsync();

        _mockTaskProgressService.Verify(t => t.CancelTask(), Times.Once);
        _mockTaskProgressService.Verify(t => t.FailTask(), Times.Never);
        _mockDialogService.Verify(d => d.ShowWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ShowFirstLaunchRestoreOfferAsync_WhenUserClicksSkip_DoesNotCallBackupService()
    {
        SetupLocalizationDefaults();
        _mockPrefsService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialRestorePointOffered, false))
            .Returns(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        var service = CreateService();

        await service.ShowFirstLaunchRestoreOfferAsync();

        _mockBackupService.Verify(
            b => b.CreateRestorePointAsync(
                It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ShowFirstLaunchRestoreOfferAsync_WhenUserClicksSkip_LogsSkip()
    {
        SetupLocalizationDefaults();
        _mockPrefsService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialRestorePointOffered, false))
            .Returns(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        var service = CreateService();

        await service.ShowFirstLaunchRestoreOfferAsync();

        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("skipped")), It.IsAny<Exception?>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ShowFirstLaunchRestoreOfferAsync_UsesCorrectLocalizationKeys()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns("text");
        _mockPrefsService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialRestorePointOffered, false))
            .Returns(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        var service = CreateService();

        await service.ShowFirstLaunchRestoreOfferAsync();

        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_Intro"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_ConfigCreated"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_RestoreOffer"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_SkipWarning"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_Title"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_Button_Create"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_Button_Skip"), Times.Once);
    }

    [Fact]
    public async Task ShowFirstLaunchRestoreOfferAsync_WhenDialogThrows_LogsErrorAndDoesNotRethrow()
    {
        SetupLocalizationDefaults();
        _mockPrefsService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialRestorePointOffered, false))
            .Returns(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ThrowsAsync(new Exception("Dialog failed"));

        var service = CreateService();

        await service.ShowFirstLaunchRestoreOfferAsync();

        _mockLogService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Error showing first launch restore offer")), It.IsAny<Exception?>(), It.IsAny<string>()),
            Times.Once);
    }
}
