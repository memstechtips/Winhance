using FluentAssertions;
using Moq;
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
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();

    private StartupNotificationService CreateService()
    {
        return new StartupNotificationService(
            _mockDialogService.Object,
            _mockPrefsService.Object,
            _mockLogService.Object,
            _mockLocalizationService.Object);
    }

    private void SetupLocalizationDefaults()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns("Localized text");
    }

    // -------------------------------------------------------
    // Null / failed result early returns
    // -------------------------------------------------------

    [Fact]
    public async Task ShowBackupNotificationAsync_WithNullResult_ReturnsImmediately()
    {
        var service = CreateService();

        await service.ShowBackupNotificationAsync(null!);

        _mockDialogService.Verify(
            d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ShowBackupNotificationAsync_WithFailedResult_DoesNotShowDialog()
    {
        var result = BackupResult.CreateFailure("Something went wrong");
        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockDialogService.Verify(
            d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ShowBackupNotificationAsync_WithFailedResultAndErrorMessage_LogsFailure()
    {
        var result = BackupResult.CreateFailure("Disk full");
        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("Backup failed") && s.Contains("Disk full"))),
            Times.Once);
    }

    [Fact]
    public async Task ShowBackupNotificationAsync_WithFailedResultAndEmptyErrorMessage_DoesNotLogFailure()
    {
        var result = new BackupResult { Success = false, ErrorMessage = "" };
        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("Backup failed"))),
            Times.Never);
    }

    [Fact]
    public async Task ShowBackupNotificationAsync_WithFailedResultAndNullErrorMessage_DoesNotLogFailure()
    {
        var result = new BackupResult { Success = false, ErrorMessage = null };
        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("Backup failed"))),
            Times.Never);
    }

    // -------------------------------------------------------
    // Success but no restore point created
    // -------------------------------------------------------

    [Fact]
    public async Task ShowBackupNotificationAsync_SuccessButNoRestorePointCreated_DoesNotShowDialog()
    {
        var result = BackupResult.CreateSuccess(restorePointCreated: false);
        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockDialogService.Verify(
            d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    // -------------------------------------------------------
    // Success with restore point created - dialog shown
    // -------------------------------------------------------

    [Fact]
    public async Task ShowBackupNotificationAsync_WithRestorePointCreated_ShowsDialog()
    {
        SetupLocalizationDefaults();

        var result = BackupResult.CreateSuccess(restorePointCreated: true);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((true, false));

        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockDialogService.Verify(
            d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ShowBackupNotificationAsync_WithRestorePointCreated_LogsDialogShown()
    {
        SetupLocalizationDefaults();

        var result = BackupResult.CreateSuccess(restorePointCreated: true);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((true, false));

        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("Backup notification dialog shown"))),
            Times.Once);
    }

    // -------------------------------------------------------
    // Don't-show-again checkbox
    // -------------------------------------------------------

    [Fact]
    public async Task ShowBackupNotificationAsync_WhenCheckboxChecked_SavesSkipSystemBackupPreference()
    {
        SetupLocalizationDefaults();

        var result = BackupResult.CreateSuccess(restorePointCreated: true);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((true, true)); // Checkbox checked

        _mockPrefsService
            .Setup(p => p.SetPreferenceAsync("SkipSystemBackup", true))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockPrefsService.Verify(
            p => p.SetPreferenceAsync("SkipSystemBackup", true),
            Times.Once);
    }

    [Fact]
    public async Task ShowBackupNotificationAsync_WhenCheckboxChecked_LogsUserOptedOut()
    {
        SetupLocalizationDefaults();

        var result = BackupResult.CreateSuccess(restorePointCreated: true);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((true, true));

        _mockPrefsService
            .Setup(p => p.SetPreferenceAsync("SkipSystemBackup", true))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("skip system backup"))),
            Times.Once);
    }

    [Fact]
    public async Task ShowBackupNotificationAsync_WhenCheckboxNotChecked_DoesNotSavePreference()
    {
        SetupLocalizationDefaults();

        var result = BackupResult.CreateSuccess(restorePointCreated: true);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((true, false)); // Checkbox not checked

        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockPrefsService.Verify(
            p => p.SetPreferenceAsync("SkipSystemBackup", It.IsAny<bool>()),
            Times.Never);
    }

    // -------------------------------------------------------
    // Dialog dismissed / cancelled
    // -------------------------------------------------------

    [Fact]
    public async Task ShowBackupNotificationAsync_WhenDialogDismissed_StillLogsDialogShown()
    {
        SetupLocalizationDefaults();

        var result = BackupResult.CreateSuccess(restorePointCreated: true);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((false, false)); // Dismissed / cancelled

        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("Backup notification dialog shown"))),
            Times.Once);
    }

    // -------------------------------------------------------
    // SystemRestoreWasDisabled flag in message
    // -------------------------------------------------------

    [Fact]
    public async Task ShowBackupNotificationAsync_WhenSystemRestoreWasDisabled_IncludesRestoreEnabledMessage()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Startup_Backup_RestoreEnabled"))
            .Returns("System Restore was re-enabled");
        _mockLocalizationService
            .Setup(l => l.GetString(It.Is<string>(s => s != "Startup_Backup_RestoreEnabled")))
            .Returns("text");

        var result = BackupResult.CreateSuccess(
            restorePointCreated: true,
            systemRestoreWasDisabled: true);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.Is<string>(s => s.Contains("System Restore was re-enabled")),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((true, false));

        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockDialogService.Verify(
            d => d.ShowConfirmationWithCheckboxAsync(
                It.Is<string>(s => s.Contains("System Restore was re-enabled")),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ShowBackupNotificationAsync_WhenSystemRestoreWasNotDisabled_DoesNotIncludeRestoreEnabledMessage()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Startup_Backup_RestoreEnabled"))
            .Returns("System Restore was re-enabled");
        _mockLocalizationService
            .Setup(l => l.GetString(It.Is<string>(s => s != "Startup_Backup_RestoreEnabled")))
            .Returns("text");

        var result = BackupResult.CreateSuccess(
            restorePointCreated: true,
            systemRestoreWasDisabled: false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((true, false));

        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockDialogService.Verify(
            d => d.ShowConfirmationWithCheckboxAsync(
                It.Is<string>(s => !s.Contains("System Restore was re-enabled")),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    // -------------------------------------------------------
    // Localization keys usage
    // -------------------------------------------------------

    [Fact]
    public async Task ShowBackupNotificationAsync_UsesCorrectLocalizationKeys()
    {
        var result = BackupResult.CreateSuccess(restorePointCreated: true);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns("text");

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((true, false));

        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        // Verify all expected localization keys are requested
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_Intro"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_Created"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_RestorePoint"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_ConfigBackup"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_ToRestore"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_RestoreInstructions_RestorePoint"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_RestoreInstructions_ConfigBackup"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_Note"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_Checkbox_DontCreate"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Startup_Backup_Title"), Times.Once);
        _mockLocalizationService.Verify(l => l.GetString("Button_OK"), Times.Once);
    }

    [Fact]
    public async Task ShowBackupNotificationAsync_PassesEmptyCancelButtonText()
    {
        SetupLocalizationDefaults();

        var result = BackupResult.CreateSuccess(restorePointCreated: true);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                ""))
            .ReturnsAsync((true, false));

        var service = CreateService();

        await service.ShowBackupNotificationAsync(result);

        _mockDialogService.Verify(
            d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                ""),
            Times.Once);
    }

    // -------------------------------------------------------
    // Exception handling
    // -------------------------------------------------------

    [Fact]
    public async Task ShowBackupNotificationAsync_WhenDialogThrows_LogsErrorAndDoesNotRethrow()
    {
        SetupLocalizationDefaults();

        var result = BackupResult.CreateSuccess(restorePointCreated: true);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ThrowsAsync(new Exception("Dialog failed"));

        var service = CreateService();

        // Should not throw
        await service.ShowBackupNotificationAsync(result);

        _mockLogService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Error showing backup notification"))),
            Times.Once);
    }

    [Fact]
    public async Task ShowBackupNotificationAsync_WhenPreferencesSaveThrows_LogsError()
    {
        SetupLocalizationDefaults();

        var result = BackupResult.CreateSuccess(restorePointCreated: true);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((true, true)); // Checkbox checked

        _mockPrefsService
            .Setup(p => p.SetPreferenceAsync("SkipSystemBackup", true))
            .ThrowsAsync(new Exception("Save failed"));

        var service = CreateService();

        // The exception from SetPreferenceAsync propagates to the catch block
        await service.ShowBackupNotificationAsync(result);

        _mockLogService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Error showing backup notification"))),
            Times.Once);
    }
}
