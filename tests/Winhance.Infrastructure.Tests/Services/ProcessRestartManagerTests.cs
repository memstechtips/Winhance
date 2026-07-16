using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Catalog;
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

    // ---------------------------------------------------------------
    // The catalog-Setting overload extracts (process, service) from ApplyBehavior.Restart and runs the
    // restart logic.
    // ---------------------------------------------------------------

    private static Setting CreateCatalogSetting(string id, RestartTarget? restart = null) => new()
    {
        Id = id,
        Display = new Display { Name = $"Setting {id}", Description = $"Description for {id}" },
        Apply = restart is null ? ApplyBehavior.None : new ApplyBehavior { Restart = restart },
    };

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_Setting_NoRestart_DoesNothing()
    {
        var setting = CreateCatalogSetting("cat-no-restart");

        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        _mockUiManagement.Verify(u => u.KillProcess(It.IsAny<string>()), Times.Never);
        _mockUiManagement.Verify(u => u.RefreshWindowsGUI(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_Setting_ExplorerInteractive_CallsRefreshWindowsGUIWithKill()
    {
        _mockConfigImportState.Setup(c => c.IsActive).Returns(false);
        var setting = CreateCatalogSetting("cat-explorer", new RestartProcess("Explorer"));

        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        _mockUiManagement.Verify(u => u.RefreshWindowsGUI(true), Times.Once);
        _mockUiManagement.Verify(u => u.KillProcess(It.IsAny<string>()), Times.Never);
        _mockLog.Verify(
            l => l.Log(LogLevel.Info,
                It.Is<string>(s => s.Contains("Refreshing Windows UI") && s.Contains("cat-explorer")),
                It.IsAny<Exception?>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_Setting_NonExplorerProcess_KillsProcess()
    {
        var setting = CreateCatalogSetting("cat-proc", new RestartProcess("notepad"));

        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        _mockUiManagement.Verify(u => u.KillProcess("notepad"), Times.Once);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_Setting_RestartService_LogsServiceRestart()
    {
        var setting = CreateCatalogSetting("cat-svc", new RestartService("FakeTestServiceCat"));

        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        _mockLog.Verify(
            l => l.Log(LogLevel.Info,
                It.Is<string>(s => s.Contains("FakeTestServiceCat") && s.Contains("cat-svc")),
                It.IsAny<Exception?>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_Setting_WhenSuppressed_SkipsProcessRestart()
    {
        var setting = CreateCatalogSetting("cat-suppressed", new RestartProcess("notepad"));

        using (_sut.SuppressRestarts())
        {
            await _sut.HandleProcessAndServiceRestartsAsync(setting);
        }

        _mockUiManagement.Verify(u => u.KillProcess(It.IsAny<string>()), Times.Never);
        _mockLog.Verify(
            l => l.Log(LogLevel.Debug,
                It.Is<string>(s => s.Contains("restarts suppressed")),
                It.IsAny<Exception?>()),
            Times.Once);
    }
}
