using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ProcessRestartManagerTests
{
    private readonly Mock<IWindowsUIManagementService> _mockUiManagement = new();
    private readonly Mock<IConfigImportState> _mockConfigImportState = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly ProcessRestartManager _sut;

    public ProcessRestartManagerTests()
    {
        _mockConfigImportState.Setup(c => c.IsActive).Returns(false);
        _mockUiManagement
            .Setup(u => u.RefreshWindowsGUI(It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());
        _sut = new ProcessRestartManager(
            _mockUiManagement.Object,
            _mockConfigImportState.Object,
            _mockLog.Object);
    }

    private static SettingDefinition CreateSetting(
        string id,
        string? restartProcess = null,
        string? restartService = null) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
        RestartProcess = restartProcess,
        RestartService = restartService,
    };

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_NoRestartRequirements_DoesNothing()
    {
        // Arrange
        var setting = CreateSetting("no-restart");

        // Act
        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        // Assert
        _mockUiManagement.Verify(
            u => u.KillProcess(It.IsAny<string>()), Times.Never);
        _mockLog.Verify(
            l => l.Log(LogLevel.Info, It.IsAny<string>(), It.IsAny<Exception?>()), Times.Never);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_WithNonExplorerProcess_KillsProcess()
    {
        // Arrange — use a non-explorer process to test the simple KillProcess path
        var setting = CreateSetting("proc-restart", restartProcess: "notepad");

        // Act
        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        // Assert
        _mockUiManagement.Verify(u => u.KillProcess("notepad"), Times.Once);
        _mockLog.Verify(
            l => l.Log(LogLevel.Info,
                It.Is<string>(s => s.Contains("notepad") && s.Contains("proc-restart")),
                It.IsAny<Exception?>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_ExplorerInInteractiveMode_CallsRefreshWindowsGUIWithKill()
    {
        // Arrange — interactive mode (not config import); Explorer restart should
        // delegate to RefreshWindowsGUI(killExplorer: true) so theme/settings
        // broadcasts fire AND Explorer is recycled.
        _mockConfigImportState.Setup(c => c.IsActive).Returns(false);
        var setting = CreateSetting("explorer-restart", restartProcess: "Explorer");

        // Act
        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        // Assert
        _mockUiManagement.Verify(u => u.RefreshWindowsGUI(true), Times.Once);
        _mockUiManagement.Verify(u => u.KillProcess(It.IsAny<string>()), Times.Never);
        _mockLog.Verify(
            l => l.Log(LogLevel.Info,
                It.Is<string>(s => s.Contains("Refreshing Windows UI") && s.Contains("explorer-restart")),
                It.IsAny<Exception?>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_WithRestartService_LogsServiceRestart()
    {
        // Arrange -- we use a service name that will cause ServiceController to throw,
        // which exercises the catch block; the key assertion is the log call.
        var setting = CreateSetting("svc-restart", restartService: "FakeTestService12345");

        // Act
        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        // Assert -- the manager logs the attempt and then catches the expected exception
        _mockLog.Verify(
            l => l.Log(LogLevel.Info,
                It.Is<string>(s => s.Contains("FakeTestService12345") && s.Contains("svc-restart")),
                It.IsAny<Exception?>()),
            Times.Once);
        _mockLog.Verify(
            l => l.Log(LogLevel.Warning,
                It.Is<string>(s => s.Contains("Failed to restart service") && s.Contains("FakeTestService12345")),
                It.IsAny<Exception?>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_ExplorerInConfigImportMode_CallsRefreshWindowsGUIWithoutKill()
    {
        // Arrange — config-import mode: Explorer must STILL fire the broadcast
        // immediately for visual feedback, but the kill is deferred to flush.
        _mockConfigImportState.Setup(c => c.IsActive).Returns(true);
        var setting = CreateSetting("import-defer", restartProcess: "Explorer");

        // Act
        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        // Assert
        _mockUiManagement.Verify(u => u.RefreshWindowsGUI(false), Times.Once);
        _mockUiManagement.Verify(u => u.RefreshWindowsGUI(true), Times.Never);
        _mockUiManagement.Verify(u => u.KillProcess(It.IsAny<string>()), Times.Never);
        _mockLog.Verify(
            l => l.Log(LogLevel.Debug,
                It.Is<string>(s => s.Contains("Broadcast Explorer-refresh") && s.Contains("import-defer")),
                It.IsAny<Exception?>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_NonExplorerInConfigImportMode_DefersAndDoesNotBroadcast()
    {
        // Arrange — non-Explorer process restarts are still purely deferred during config import.
        _mockConfigImportState.Setup(c => c.IsActive).Returns(true);
        var setting = CreateSetting("import-other", restartProcess: "notepad");

        // Act
        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        // Assert — no UI broadcast and no kill; just the debug skip log.
        _mockUiManagement.Verify(u => u.RefreshWindowsGUI(It.IsAny<bool>()), Times.Never);
        _mockUiManagement.Verify(u => u.KillProcess(It.IsAny<string>()), Times.Never);
        _mockLog.Verify(
            l => l.Log(LogLevel.Debug,
                It.Is<string>(s => s.Contains("Skipping process restart") && s.Contains("config import")),
                It.IsAny<Exception?>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_NoRestartProcess_DoesNotInvokeRefreshWindowsGUI()
    {
        // Arrange
        _mockConfigImportState.Setup(c => c.IsActive).Returns(false);
        var setting = CreateSetting("no-restart-process");

        // Act
        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        // Assert
        _mockUiManagement.Verify(u => u.RefreshWindowsGUI(It.IsAny<bool>()), Times.Never);
        _mockUiManagement.Verify(u => u.KillProcess(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_KillProcessThrows_LogsWarning()
    {
        // Arrange
        var setting = CreateSetting("failing-proc", restartProcess: "badprocess");
        _mockUiManagement
            .Setup(u => u.KillProcess("badprocess"))
            .Throws(new InvalidOperationException("Process not found"));

        // Act
        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        // Assert
        _mockLog.Verify(
            l => l.Log(LogLevel.Warning,
                It.Is<string>(s => s.Contains("Failed to restart process") && s.Contains("badprocess")),
                It.IsAny<Exception?>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_WildcardService_LogsServiceRestart()
    {
        // Arrange -- wildcard service name triggers the enumeration path
        var setting = CreateSetting("wildcard-svc", restartService: "NonExistent*");

        // Act
        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        // Assert -- the manager should log the initial attempt
        _mockLog.Verify(
            l => l.Log(LogLevel.Info,
                It.Is<string>(s => s.Contains("NonExistent*")),
                It.IsAny<Exception?>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_BothProcessAndService_HandlesEach()
    {
        // Arrange
        var setting = CreateSetting("both", restartProcess: "notepad", restartService: "FakeSvc999");

        // Act
        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        // Assert -- process kill is invoked
        _mockUiManagement.Verify(u => u.KillProcess("notepad"), Times.Once);
        // Service restart is attempted (and fails with a warning because the service doesn't exist)
        _mockLog.Verify(
            l => l.Log(LogLevel.Info,
                It.Is<string>(s => s.Contains("notepad")),
                It.IsAny<Exception?>()),
            Times.Once);
        _mockLog.Verify(
            l => l.Log(LogLevel.Info,
                It.Is<string>(s => s.Contains("FakeSvc999")),
                It.IsAny<Exception?>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_WhenSuppressed_SkipsProcessRestart()
    {
        // Arrange
        var setting = CreateSetting("suppressed", restartProcess: "notepad");

        // Act
        using (_sut.SuppressRestarts())
        {
            await _sut.HandleProcessAndServiceRestartsAsync(setting);
        }

        // Assert -- process kill should NOT be called while suppressed
        _mockUiManagement.Verify(
            u => u.KillProcess(It.IsAny<string>()), Times.Never);
        _mockLog.Verify(
            l => l.Log(LogLevel.Debug,
                It.Is<string>(s => s.Contains("restarts suppressed")),
                It.IsAny<Exception?>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_WhenSuppressed_SkipsServiceRestart()
    {
        // Arrange
        var setting = CreateSetting("suppressed-svc", restartService: "FakeSvc999");

        // Act
        using (_sut.SuppressRestarts())
        {
            await _sut.HandleProcessAndServiceRestartsAsync(setting);
        }

        // Assert -- service restart should NOT be attempted while suppressed
        _mockLog.Verify(
            l => l.Log(LogLevel.Debug,
                It.Is<string>(s => s.Contains("restarts suppressed")),
                It.IsAny<Exception?>()),
            Times.Once);
        _mockLog.Verify(
            l => l.Log(LogLevel.Info,
                It.Is<string>(s => s.Contains("Restarting service")),
                It.IsAny<Exception?>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_AfterSuppressDisposed_RestartsNormally()
    {
        // Arrange
        var setting = CreateSetting("after-suppress", restartProcess: "notepad");

        // Act -- suppress and dispose, then call
        using (_sut.SuppressRestarts()) { }
        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        // Assert -- process kill should be called since suppression was lifted
        _mockUiManagement.Verify(u => u.KillProcess("notepad"), Times.Once);
    }
}
