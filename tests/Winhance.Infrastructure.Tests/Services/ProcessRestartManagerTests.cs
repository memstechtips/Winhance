using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ProcessRestartManagerTests
{
    private readonly Mock<IWindowsUIManagementService> _mockUiManagement = new();
    private readonly Mock<IConfigImportState> _mockConfigImportState = new();
    private readonly Mock<IPendingRestartService> _mockPendingRestart = new();
    private readonly Mock<IExplorerRestartService> _mockExplorerRestart = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly ProcessRestartManager _sut;

    public ProcessRestartManagerTests()
    {
        _mockConfigImportState.Setup(c => c.IsActive).Returns(false);
        _sut = new ProcessRestartManager(
            _mockUiManagement.Object,
            _mockConfigImportState.Object,
            _mockPendingRestart.Object,
            _mockExplorerRestart.Object,
            _mockLog.Object);
    }

    // Which broadcast a setting gets is DECLARED on it (ApplyBehavior.NotifyWindows); the catalog-wide conformance
    // test pins the declarations to the settings that really write the personalisation key.
    private static Setting CreateCatalogSetting(
        string id, RestartTarget? restart = null, WindowsChange notify = WindowsChange.None) => new()
    {
        Id = id,
        Display = new Display { Name = $"Setting {id}", Description = $"Description for {id}" },
        Apply = restart is null && notify == WindowsChange.None
            ? ApplyBehavior.None
            : new ApplyBehavior { Restart = restart, NotifyWindows = notify },
    };

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_Setting_NoRestart_DoesNothing()
    {
        var setting = CreateCatalogSetting("cat-no-restart");

        await _sut.HandleProcessAndServiceRestartsAsync(setting);

        _mockUiManagement.Verify(u => u.KillProcess(It.IsAny<string>()), Times.Never);
        _mockExplorerRestart.Verify(e => e.BroadcastShellRefresh(), Times.Never);
        _mockPendingRestart.Verify(p => p.Register(It.IsAny<string>()), Times.Never);
    }

    // Explorer: broadcast + register, NEVER kill. Applying a setting must not take the shell down -
    // several toggles in a row could otherwise leave the user with no shell.

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_Explorer_RegistersPendingAndNeverKills()
    {
        var setting = CreateCatalogSetting("cat-explorer", new RestartProcess("Explorer"));

        await _sut.HandleProcessAndServiceRestartsAsync(setting);
        // The broadcast is deliberately fire-and-forget (blocking the caller - the UI thread on an interactive
        // apply - would cost seconds), so the suite awaits the task it dispatched rather than racing the
        // thread pool.
        await _sut.LastBroadcastTask;

        _mockExplorerRestart.Verify(e => e.BroadcastShellRefresh(), Times.Once);
        _mockPendingRestart.Verify(p => p.Register("cat-explorer"), Times.Once);
        _mockExplorerRestart.Verify(e => e.RestartAsync(), Times.Never,
            "applying a setting must never restart the shell as a side effect");
        _mockUiManagement.Verify(u => u.KillProcess(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_Explorer_DuringConfigImport_RegistersNothing()
    {
        _mockConfigImportState.Setup(c => c.IsActive).Returns(true);
        var setting = CreateCatalogSetting("cat-explorer", new RestartProcess("Explorer"));

        await _sut.HandleProcessAndServiceRestartsAsync(setting);
        await _sut.LastBroadcastTask;

        _mockExplorerRestart.Verify(e => e.BroadcastShellRefresh(), Times.Once);
        _mockPendingRestart.Verify(p => p.Register(It.IsAny<string>()), Times.Never,
            "config import restarts Explorer itself at the end, so it must leave no bar behind");
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_Explorer_WhenSuppressed_StillRegisters()
    {
        var setting = CreateCatalogSetting("cat-explorer", new RestartProcess("Explorer"));

        using (_sut.SuppressRestarts())
        {
            await _sut.HandleProcessAndServiceRestartsAsync(setting);
        }

        _mockPendingRestart.Verify(p => p.Register("cat-explorer"), Times.Once,
            "registering is not a restart, so a suppress scope must not hide the bar from a bulk apply");
    }

    [Fact]
    public async Task FlushCoalescedRestartsAsync_Explorer_RegistersInsteadOfRestarting()
    {
        var settings = new[]
        {
            CreateCatalogSetting("bulk-a", new RestartProcess("Explorer")),
            CreateCatalogSetting("bulk-b", new RestartProcess("Explorer")),
            CreateCatalogSetting("bulk-c", new RestartProcess("notepad")),
        };

        await _sut.FlushCoalescedRestartsAsync(settings);

        _mockPendingRestart.Verify(p => p.Register("bulk-a"), Times.Once);
        _mockPendingRestart.Verify(p => p.Register("bulk-b"), Times.Once);
        _mockExplorerRestart.Verify(e => e.RestartAsync(), Times.Never);
        _mockUiManagement.Verify(u => u.KillProcess("explorer"), Times.Never);
        _mockUiManagement.Verify(u => u.KillProcess("notepad"), Times.Once,
            "non-Explorer targets still coalesce and restart immediately");
        _mockExplorerRestart.Verify(e => e.BroadcastShellRefresh(), Times.Once,
            "one generic broadcast covers the whole coalesced batch");
        _mockExplorerRestart.Verify(e => e.BroadcastThemeRefresh(), Times.Never,
            "no setting in the batch declares WindowsChange.Appearance");
    }

    // The broadcast SPLIT. Three of the four messages are theme/colour notifications, and one of
    // those must be sent synchronously at a per-top-level-window timeout - seconds on a busy
    // desktop. Only a setting that can actually change the theme has any reason to pay for it.

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_ThemeSetting_BroadcastsThemeAndGeneric()
    {
        var setting = CreateCatalogSetting(
            "cat-theme", new RestartProcess("Explorer"), WindowsChange.Appearance);

        await _sut.HandleProcessAndServiceRestartsAsync(setting);
        await _sut.LastBroadcastTask;

        _mockExplorerRestart.Verify(e => e.BroadcastThemeRefresh(), Times.Once,
            "a setting that declares WindowsChange.Appearance is exactly what the theme messages exist for");
        _mockExplorerRestart.Verify(e => e.BroadcastShellRefresh(), Times.Once,
            "the generic message still goes out - the theme set is an ADDITION, not a replacement");
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_NonThemeExplorerSetting_BroadcastsGenericOnly()
    {
        var setting = CreateCatalogSetting(
            "cat-task-view", new RestartProcess("Explorer"));

        await _sut.HandleProcessAndServiceRestartsAsync(setting);
        await _sut.LastBroadcastTask;

        _mockExplorerRestart.Verify(e => e.BroadcastShellRefresh(), Times.Once);
        _mockExplorerRestart.Verify(e => e.BroadcastThemeRefresh(), Times.Never,
            "a setting that declares no appearance change has no business paying for the theme messages");
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_NotifyWithoutRestart_StillBroadcasts()
    {
        // The regression this pins: if the broadcast lived INSIDE the isExplorer branch, a theme setting
        // with no Explorer restart would never be announced and the theme would silently stop applying.
        // Declaring a notice and needing a restart are independent facts; a setting may do either
        // without the other.
        var setting = CreateCatalogSetting("cat-notify-only", restart: null, notify: WindowsChange.Appearance);

        await _sut.HandleProcessAndServiceRestartsAsync(setting);
        await _sut.LastBroadcastTask;

        _mockExplorerRestart.Verify(e => e.BroadcastThemeRefresh(), Times.Once,
            "a declared appearance change must be announced even with no restart to hang it off");
        _mockExplorerRestart.Verify(e => e.BroadcastShellRefresh(), Times.Once);
        _mockPendingRestart.Verify(p => p.Register(It.IsAny<string>()), Times.Never,
            "a notice is not a restart - it has already taken effect, so no bar may be raised");
        _mockUiManagement.Verify(u => u.KillProcess(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FlushCoalescedRestartsAsync_WithOneThemeSetting_BroadcastsThemeOnce()
    {
        var settings = new[]
        {
            CreateCatalogSetting("bulk-plain", new RestartProcess("Explorer")),
            CreateCatalogSetting("bulk-theme", new RestartProcess("Explorer"), WindowsChange.Appearance),
        };

        await _sut.FlushCoalescedRestartsAsync(settings);

        _mockExplorerRestart.Verify(e => e.BroadcastThemeRefresh(), Times.Once,
            "one theme-affecting setting in the batch is enough, and once is enough");
        _mockExplorerRestart.Verify(e => e.BroadcastShellRefresh(), Times.Once);
    }

    // LEGIBILITY. A silent two-second stall is undiagnosable, so the broadcast logs which variant ran
    // and how long it took - and a failure on the background task must still reach the log rather
    // than dying unobserved on the thread pool.

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_Explorer_LogsTheBroadcastVariantAndElapsedTime()
    {
        var setting = CreateCatalogSetting(
            "cat-task-view", new RestartProcess("Explorer"));

        await _sut.HandleProcessAndServiceRestartsAsync(setting);
        await _sut.LastBroadcastTask;

        _mockLog.Verify(
            l => l.Log(LogLevel.Debug,
                It.Is<string>(m => m.Contains("Broadcasting shell refresh")
                    && m.Contains("generic") && m.Contains("cat-task-view")),
                It.IsAny<Exception?>()),
            Times.Once);
        _mockLog.Verify(
            l => l.Log(LogLevel.Debug,
                It.Is<string>(m => m.Contains("Shell broadcast")
                    && m.Contains("cat-task-view") && m.Contains("ms")),
                It.IsAny<Exception?>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleProcessAndServiceRestartsAsync_Explorer_BroadcastFailure_IsLoggedNotSwallowed()
    {
        _mockExplorerRestart
            .Setup(e => e.BroadcastShellRefresh())
            .Throws(new InvalidOperationException("user32 said no"));
        var setting = CreateCatalogSetting(
            "cat-explodes", new RestartProcess("Explorer"));

        await _sut.HandleProcessAndServiceRestartsAsync(setting);
        // Nothing in production awaits this task, so an escaping fault would vanish. Awaiting it here
        // must NOT throw - the failure has to have been caught and logged inside.
        await _sut.LastBroadcastTask;

        _mockLog.Verify(
            l => l.Log(LogLevel.Error,
                It.Is<string>(m => m.Contains("Shell broadcast") && m.Contains("cat-explodes")),
                It.IsAny<Exception?>()),
            Times.Once);
        _mockPendingRestart.Verify(p => p.Register("cat-explodes"), Times.Once,
            "a failed broadcast must not cost the user the pending-restart bar");
    }

    // Non-Explorer targets restart immediately: none of them is the shell, and all return
    // instantly, so deferring them would cost the user a click for no benefit.

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
