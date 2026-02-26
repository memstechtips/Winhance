using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

/// <summary>
/// Tests for <see cref="WinGetStartupService"/>.
///
/// This service is fully testable because all its dependencies are interfaces.
/// It orchestrates IWinGetBootstrapper, IInternetConnectivityService,
/// ITaskProgressService, ILocalizationService, and ILogService.
/// </summary>
public class WinGetStartupServiceTests
{
    private readonly Mock<IWinGetBootstrapper> _mockBootstrapper = new();
    private readonly Mock<IInternetConnectivityService> _mockInternet = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgress = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly WinGetStartupService _sut;

    public WinGetStartupServiceTests()
    {
        // Default localization setup: return the key or a readable string
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => key);

        // Default task progress setup: StartTask returns a CancellationTokenSource
        _mockTaskProgress
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _sut = new WinGetStartupService(
            _mockBootstrapper.Object,
            _mockInternet.Object,
            _mockTaskProgress.Object,
            _mockLocalization.Object,
            _mockLogService.Object);
    }

    // --- System WinGet Already Available ---

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenSystemWinGetAvailable_AttemptsUpgrade()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(true);
        _mockBootstrapper.Setup(b => b.UpgradeAppInstallerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockBootstrapper.Setup(b => b.EnsureWinGetReadyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert: should attempt upgrade
        _mockBootstrapper.Verify(
            b => b.UpgradeAppInstallerAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenUpgradeSucceeds_ReInitsCom()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(true);
        _mockBootstrapper.Setup(b => b.UpgradeAppInstallerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockBootstrapper.Setup(b => b.EnsureWinGetReadyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert: after successful upgrade, should re-init COM
        _mockBootstrapper.Verify(
            b => b.EnsureWinGetReadyAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("upgraded successfully")), null),
            Times.Once);
    }

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenUpgradeFails_DoesNotReInitCom()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(true);
        _mockBootstrapper.Setup(b => b.UpgradeAppInstallerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert: should NOT call EnsureWinGetReadyAsync when upgrade fails
        _mockBootstrapper.Verify(
            b => b.EnsureWinGetReadyAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("not needed or not applicable")), null),
            Times.Once);
    }

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenAvailable_DoesNotCheckInternet()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(true);
        _mockBootstrapper.Setup(b => b.UpgradeAppInstallerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert: should NOT check internet when system winget is already available
        _mockInternet.Verify(
            i => i.IsInternetConnectedAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenAvailable_DoesNotStartTask()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(true);
        _mockBootstrapper.Setup(b => b.UpgradeAppInstallerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert: should NOT start a progress task for upgrade path
        _mockTaskProgress.Verify(
            t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
    }

    // --- System WinGet Not Available ---

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenNotAvailable_ChecksInternet()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(false);
        _mockInternet
            .Setup(i => i.IsInternetConnectedAsync(true, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        _mockBootstrapper
            .Setup(b => b.InstallWinGetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert
        _mockInternet.Verify(
            i => i.IsInternetConnectedAsync(true, It.IsAny<CancellationToken>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenNoInternet_SkipsInstall()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(false);
        _mockInternet
            .Setup(i => i.IsInternetConnectedAsync(true, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(false);

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert: should NOT attempt installation
        _mockBootstrapper.Verify(
            b => b.InstallWinGetAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        _mockLogService.Verify(
            l => l.Log(LogLevel.Warning,
                It.Is<string>(s => s.Contains("No internet connection")), null),
            Times.Once);
    }

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenNoInternet_DoesNotStartTask()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(false);
        _mockInternet
            .Setup(i => i.IsInternetConnectedAsync(true, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(false);

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert
        _mockTaskProgress.Verify(
            t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenNotAvailableAndHasInternet_InstallsWinGet()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(false);
        _mockInternet
            .Setup(i => i.IsInternetConnectedAsync(true, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        _mockBootstrapper
            .Setup(b => b.InstallWinGetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert
        _mockBootstrapper.Verify(
            b => b.InstallWinGetAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenInstallSucceeds_CompletesTask()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(false);
        _mockInternet
            .Setup(i => i.IsInternetConnectedAsync(true, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        _mockBootstrapper
            .Setup(b => b.InstallWinGetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert
        _mockTaskProgress.Verify(t => t.StartTask(It.IsAny<string>(), false), Times.Once);
        _mockTaskProgress.Verify(t => t.CompleteTask(), Times.Once);
        _mockLogService.Verify(
            l => l.Log(LogLevel.Info,
                It.Is<string>(s => s.Contains("installed successfully")), null),
            Times.Once);
    }

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenInstallFails_ShowsErrorAndCompletesTask()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(false);
        _mockInternet
            .Setup(i => i.IsInternetConnectedAsync(true, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        _mockBootstrapper
            .Setup(b => b.InstallWinGetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert: should show error progress and still complete the task
        _mockTaskProgress.Verify(
            t => t.UpdateProgress(0, It.IsAny<string>()),
            Times.Once);
        _mockTaskProgress.Verify(t => t.CompleteTask(), Times.Once);
        _mockLogService.Verify(
            l => l.Log(LogLevel.Warning,
                It.Is<string>(s => s.Contains("installation failed")), null),
            Times.Once);
    }

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenInstallThrows_CompletesTaskAndRethrows()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(false);
        _mockInternet
            .Setup(i => i.IsInternetConnectedAsync(true, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        _mockBootstrapper
            .Setup(b => b.InstallWinGetAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Install failed"));

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert: the outer try/catch should catch the exception and log it
        _mockTaskProgress.Verify(t => t.CompleteTask(), Times.Once);
        _mockLogService.Verify(
            l => l.Log(LogLevel.Error,
                It.Is<string>(s => s.Contains("Error in WinGet readiness flow")), null),
            Times.Once);
    }

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenNotAvailableAndInstalls_StartsProgressTask()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(false);
        _mockInternet
            .Setup(i => i.IsInternetConnectedAsync(true, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        _mockBootstrapper
            .Setup(b => b.InstallWinGetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert: should start a progress task with indeterminate=false
        _mockTaskProgress.Verify(
            t => t.StartTask(It.IsAny<string>(), false),
            Times.Once);
    }

    // --- Error Handling ---

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenUpgradeThrows_LogsErrorAndContinues()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(true);
        _mockBootstrapper.Setup(b => b.UpgradeAppInstallerAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Upgrade exploded"));

        // Act: should not throw
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert: outer catch should log the error
        _mockLogService.Verify(
            l => l.Log(LogLevel.Error,
                It.Is<string>(s => s.Contains("Error in WinGet readiness flow")), null),
            Times.Once);
    }

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_WhenInternetCheckThrows_LogsError()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(false);
        _mockInternet
            .Setup(i => i.IsInternetConnectedAsync(true, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("Network check failed"));

        // Act: should not throw
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert
        _mockLogService.Verify(
            l => l.Log(LogLevel.Error,
                It.Is<string>(s => s.Contains("Error in WinGet readiness flow")), null),
            Times.Once);
    }

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_LogsStartupAttempt()
    {
        // Arrange
        _mockBootstrapper.Setup(b => b.IsSystemWinGetAvailable).Returns(true);
        _mockBootstrapper.Setup(b => b.UpgradeAppInstallerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.EnsureWinGetReadyOnStartupAsync();

        // Assert: should log the startup flow
        _mockLogService.Verify(
            l => l.Log(LogLevel.Info,
                It.Is<string>(s => s.Contains("System winget available")), null),
            Times.Once);
    }
}
