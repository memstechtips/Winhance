using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
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
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();

    public ApplicationCloseServiceTests()
    {
        _mockLocalizationService.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _mockLocalizationService.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => $"{key}:{args[0]}");
    }

    private ApplicationCloseService CreateService()
    {
        var svc = new ApplicationCloseService(
            _mockLogService.Object,
            _mockTaskProgressService.Object,
            _mockUserPreferencesService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object);
        // Tests must not actually terminate the test host — swap in a no-op shutdown.
        svc.ShutdownAction = () => { };
        return svc;
    }

    [Fact]
    public void Constructor_WithNullLogService_ThrowsArgumentNullException()
    {
        var act = () => new ApplicationCloseService(
            null!,
            _mockTaskProgressService.Object,
            _mockUserPreferencesService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object);

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
            _mockLocalizationService.Object);

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
            _mockLocalizationService.Object);

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
            _mockLocalizationService.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("dialogService");
    }

    [Fact]
    public void Constructor_WithNullLocalizationService_ThrowsArgumentNullException()
    {
        var act = () => new ApplicationCloseService(
            _mockLogService.Object,
            _mockTaskProgressService.Object,
            _mockUserPreferencesService.Object,
            _mockDialogService.Object,
            null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("localizationService");
    }

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
            .ReturnsAsync(true);

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
            Times.Never);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_WhenTaskRunning_UserCancels_ReturnsFailedResult()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(true);
        _mockTaskProgressService.Setup(t => t.CurrentStatusText).Returns("Installing apps");

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

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
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

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
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

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
        _mockTaskProgressService.Setup(t => t.CurrentStatusText).Returns((string)null!);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(
                It.Is<ConfirmationRequest>(r => r.Message.Contains("Dialog_CloseWhileRunning_UnknownOperation"))))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await service.CheckOperationsAndCloseAsync();

        _mockDialogService.Verify(
            d => d.ShowConfirmationAsync(
                It.Is<ConfirmationRequest>(r => r.Message.Contains("Dialog_CloseWhileRunning_UnknownOperation"))),
            Times.Once);
    }

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
            d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_DontShowSupportTrue_SkipsSponsorsDialog()
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
            d => d.ShowSponsorsDialogAsync(It.IsAny<SponsorsDialogMode>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_DontShowSupportFalse_ShowsSponsorsDialogInExitMode()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(false);

        _mockDialogService
            .Setup(d => d.ShowSponsorsDialogAsync(It.IsAny<SponsorsDialogMode>()))
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
            d => d.ShowSponsorsDialogAsync(SponsorsDialogMode.Exit),
            Times.Once);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_SponsorsDialog_DontShowAgainChecked_SavesPreference()
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
            .Setup(d => d.ShowSponsorsDialogAsync(It.IsAny<SponsorsDialogMode>()))
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
    public async Task CheckOperationsAndCloseAsync_SponsorsDialog_DontShowAgainUnchecked_DoesNotSavePreference()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ReturnsAsync(false);

        _mockDialogService
            .Setup(d => d.ShowSponsorsDialogAsync(It.IsAny<SponsorsDialogMode>()))
            .ReturnsAsync((false, false)); // DontShowAgain = false

        try
        {
            await service.CheckOperationsAndCloseAsync();
        }
        catch
        {
            // Expected: Application.Current is null in unit tests
        }

        _mockUserPreferencesService.Verify(
            u => u.SetPreferenceAsync("DontShowSupport", It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckOperationsAndCloseAsync_WhenPreferenceCheckThrows_DefaultsToShowDialog()
    {
        var service = CreateService();

        _mockTaskProgressService.Setup(t => t.IsTaskRunning).Returns(false);
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("DontShowSupport", false))
            .ThrowsAsync(new Exception("Prefs unavailable"));

        // ShouldShowSupportDialogAsync catches and returns true, so sponsors dialog should show
        _mockDialogService
            .Setup(d => d.ShowSponsorsDialogAsync(It.IsAny<SponsorsDialogMode>()))
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
            d => d.ShowSponsorsDialogAsync(It.IsAny<SponsorsDialogMode>()),
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
            .Setup(d => d.ShowSponsorsDialogAsync(It.IsAny<SponsorsDialogMode>()))
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
