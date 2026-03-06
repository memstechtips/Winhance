using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ApplicationCloseServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IUserPreferencesService> _mockUserPreferencesService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();

    private ApplicationCloseService CreateService()
    {
        return new ApplicationCloseService(
            _mockLogService.Object,
            _mockTaskProgressService.Object,
            _mockUserPreferencesService.Object,
            _mockDialogService.Object,
            _mockProcessExecutor.Object);
    }

    // -------------------------------------------------------
    // Constructor null guard tests
    // -------------------------------------------------------

    [Fact]
    public void Constructor_WithNullLogService_ThrowsArgumentNullException()
    {
        var act = () => new ApplicationCloseService(
            null!,
            _mockTaskProgressService.Object,
            _mockUserPreferencesService.Object,
            _mockDialogService.Object,
            _mockProcessExecutor.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logService");
    }

    [Fact]
    public void Constructor_WithNullTaskProgressService_ThrowsArgumentNullException()
    {
        var act = () => new ApplicationCloseService(
            _mockLogService.Object,
            null!,
            _mockUserPreferencesService.Object,
            _mockDialogService.Object,
            _mockProcessExecutor.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("taskProgressService");
    }

    [Fact]
    public void Constructor_WithNullUserPreferencesService_ThrowsArgumentNullException()
    {
        var act = () => new ApplicationCloseService(
            _mockLogService.Object,
            _mockTaskProgressService.Object,
            null!,
            _mockDialogService.Object,
            _mockProcessExecutor.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("userPreferencesService");
    }

    [Fact]
    public void Constructor_WithNullDialogService_ThrowsArgumentNullException()
    {
        var act = () => new ApplicationCloseService(
            _mockLogService.Object,
            _mockTaskProgressService.Object,
            _mockUserPreferencesService.Object,
            null!,
            _mockProcessExecutor.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("dialogService");
    }

    [Fact]
    public void Constructor_WithNullProcessExecutor_ThrowsArgumentNullException()
    {
        var act = () => new ApplicationCloseService(
            _mockLogService.Object,
            _mockTaskProgressService.Object,
            _mockUserPreferencesService.Object,
            _mockDialogService.Object,
            null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("processExecutor");
    }

    // -------------------------------------------------------
    // BeforeShutdown property
    // -------------------------------------------------------

    [Fact]
    public void BeforeShutdown_DefaultsToNull()
    {
        var service = CreateService();
        service.BeforeShutdown.Should().BeNull();
    }

    [Fact]
    public void BeforeShutdown_CanBeSetAndRetrieved()
    {
        var service = CreateService();
        Func<Task> hook = () => Task.CompletedTask;

        service.BeforeShutdown = hook;

        service.BeforeShutdown.Should().BeSameAs(hook);
    }

    // -------------------------------------------------------
    // CheckOperationsAndCloseAsync - BeforeShutdown hook
    // -------------------------------------------------------

    [Fact]
    public async Task CheckOperationsAndCloseAsync_WhenBeforeShutdownSet_InvokesHook()
    {
        var hookInvoked = false;
        var service = CreateService();
        service.BeforeShutdown = () =>
        {
            hookInvoked = true;
            return Task.CompletedTask;
        };

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(true); // Skip donation dialog to avoid Application.Current.Exit()

        // Application.Current.Exit() will throw in test context; catch and verify hook ran
        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        hookInvoked.Should().BeTrue();
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_WhenBeforeShutdownThrows_LogsErrorAndContinues()
    {
        var service = CreateService();
        service.BeforeShutdown = () => throw new InvalidOperationException("Cleanup failed");

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(true);

        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        _mockLogService.Verify(
            l => l.LogError(It.Is<string>(s => s.Contains("Error running cleanup tasks")), It.IsAny<Exception>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_WhenBeforeShutdownIsNull_DoesNotThrow()
    {
        var service = CreateService();
        service.BeforeShutdown = null;

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(true);

        // Should not throw due to null BeforeShutdown; may throw due to Application.Current being null
        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        // No LogError for "Error running cleanup tasks" should have been called
        _mockLogService.Verify(
            l => l.LogError(It.Is<string>(s => s.Contains("Error running cleanup tasks")), It.IsAny<Exception>()),
            Times.Never);
    }

    // -------------------------------------------------------
    // CheckOperationsAndCloseAsync - Running operations
    // -------------------------------------------------------

    [Fact]
    public async Task CheckOperationsAndCloseAsync_WhenTaskRunning_UserCancels_ReturnsFailedResult()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Setup(t => t.CurrentStatusText).Returns("Installing apps");

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(false); // User clicks Cancel

        var result = await service.CheckOperationsAndCloseAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("User cancelled application close");
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_WhenTaskRunning_UserCancels_LogsCancellation()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Setup(t => t.CurrentStatusText).Returns("Installing apps");

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(false);

        await service.CheckOperationsAndCloseAsync();

        _mockLogService.Verify(
            l => l.LogInformation(It.Is<string>(s => s.Contains("User cancelled application close"))),
            Times.Once);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_WhenTaskRunning_UserConfirms_CancelsTask()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Setup(t => t.CurrentStatusText).Returns("Applying settings");

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(true); // User clicks Yes

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(true);

        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        _mockTaskProgressService.Verify(t => t.CancelCurrentTask(), Times.Once);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_WhenTaskRunning_NullStatusText_UsesDefaultMessage()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Setup(t => t.CurrentStatusText).Returns((string?)null);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(
                It.Is<string>(s => s.Contains("an operation")),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(false);

        await service.CheckOperationsAndCloseAsync();

        _mockDialogService.Verify(
            d => d.ShowConfirmationAsync(
                It.Is<string>(s => s.Contains("an operation")),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    // -------------------------------------------------------
    // CheckOperationsAndCloseAsync - No running operations
    // -------------------------------------------------------

    [Fact]
    public async Task CheckOperationsAndCloseAsync_NoRunningTask_DoesNotShowConfirmationDialog()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(true);

        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        _mockDialogService.Verify(
            d => d.ShowConfirmationAsync(
                It.IsAny<string>(),
                It.Is<string>(s => s.Contains("Operation in Progress")),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    // -------------------------------------------------------
    // CheckOperationsAndCloseAsync - Donation dialog
    // -------------------------------------------------------

    [Fact]
    public async Task CheckOperationsAndCloseAsync_DontShowSupportTrue_SkipsDonationDialog()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(true);

        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        _mockDialogService.Verify(
            d => d.ShowDonationDialogAsync(It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_DontShowSupportFalse_ShowsDonationDialog()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(false);

        _mockDialogService
            .Setup(d => d.ShowDonationDialogAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((false, false));

        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        _mockDialogService.Verify(
            d => d.ShowDonationDialogAsync(It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_DonationDialog_DontShowAgainChecked_SavesPreference()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(false);
        _mockUserPreferencesService
            .Setup(u => u.SetPreferenceAsync("DontShowSupport", true))
            .ReturnsAsync(OperationResult.Succeeded());

        _mockDialogService
            .Setup(d => d.ShowDonationDialogAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((false, true)); // DontShowAgain = true

        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        _mockUserPreferencesService.Verify(
            u => u.SetPreferenceAsync("DontShowSupport", true),
            Times.Once);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_DonationDialog_UserClicksYes_OpensDonationPage()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(false);

        _mockDialogService
            .Setup(d => d.ShowDonationDialogAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((true, false)); // Result = true (Yes)

        _mockProcessExecutor
            .Setup(p => p.ShellExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        _mockProcessExecutor.Verify(
            p => p.ShellExecuteAsync(
                "https://ko-fi.com/memstechtips",
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_DonationDialog_UserClicksNo_DoesNotOpenDonationPage()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(false);

        _mockDialogService
            .Setup(d => d.ShowDonationDialogAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((false, false)); // Result = false (No)

        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        _mockProcessExecutor.Verify(
            p => p.ShellExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -------------------------------------------------------
    // CheckOperationsAndCloseAsync - Exception handling
    // -------------------------------------------------------

    [Fact]
    public async Task CheckOperationsAndCloseAsync_WhenPreferenceCheckThrows_DefaultsToShowDialog()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ThrowsAsync(new Exception("Prefs unavailable"));

        // ShouldShowSupportDialogAsync catches and returns true, so donation dialog should show
        _mockDialogService
            .Setup(d => d.ShowDonationDialogAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((false, false));

        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        _mockDialogService.Verify(
            d => d.ShowDonationDialogAsync(It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_WhenDonationPageOpenFails_LogsErrorAndContinues()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(false);

        _mockDialogService
            .Setup(d => d.ShowDonationDialogAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((true, false)); // User clicks Yes

        _mockProcessExecutor
            .Setup(p => p.ShellExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Browser not found"));

        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        _mockLogService.Verify(
            l => l.LogError(It.Is<string>(s => s.Contains("Error opening donation page")), It.IsAny<Exception>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_WhenSavePreferenceFails_LogsError()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(false);
        _mockUserPreferencesService
            .Setup(u => u.SetPreferenceAsync("DontShowSupport", true))
            .ReturnsAsync(OperationResult.Failed("Save error"));

        _mockDialogService
            .Setup(d => d.ShowDonationDialogAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((false, true)); // DontShowAgain = true

        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        _mockLogService.Verify(
            l => l.LogError(It.Is<string>(s => s.Contains("Failed to save DontShowSupport preference"))),
            Times.Once);
    }
}
