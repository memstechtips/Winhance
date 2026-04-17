using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.EventHandlers;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class StartupOrchestratorTests
{
    private readonly Mock<ICompatibleSettingsRegistry> _settingsRegistry = new();
    private readonly Mock<IGlobalSettingsPreloader> _settingsPreloader = new();
    private readonly Mock<IUserPreferencesService> _preferencesService = new();
    private readonly Mock<IConfigurationService> _configurationService = new();
    private readonly Mock<IScriptMigrationService> _migrationService = new();
    private readonly Mock<IRemovalScriptUpdateService> _updateService = new();
    private readonly Mock<INewBadgeService> _newBadgeService = new();
    private readonly Mock<ILogService> _logService = new();

    public StartupOrchestratorTests()
    {
        // Default preferences
        _preferencesService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialConfigBackupCompleted, false))
            .Returns(true);
    }

    private StartupOrchestrator CreateSut(TooltipRefreshEventHandler? tooltipHandler = null)
    {
        return new StartupOrchestrator(
            _settingsRegistry.Object,
            _settingsPreloader.Object,
            tooltipHandler!,
            _preferencesService.Object,
            _configurationService.Object,
            _migrationService.Object,
            _updateService.Object,
            _newBadgeService.Object,
            _logService.Object);
    }

    private (Progress<string> StatusProgress, Progress<TaskProgressDetail> DetailedProgress,
        List<string> StatusReports) CreateProgressTracking()
    {
        var statusReports = new List<string>();
        var statusProgress = new Progress<string>(s => statusReports.Add(s));
        var detailedProgress = new Progress<TaskProgressDetail>();
        return (statusProgress, detailedProgress, statusReports);
    }

    // --- Phase 1: Settings registry initialization ---

    [Fact]
    public async Task RunStartupSequenceAsync_InitializesSettingsRegistry()
    {
        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        _settingsRegistry.Verify(r => r.InitializeAsync(), Times.Once);
    }

    [Fact]
    public async Task RunStartupSequenceAsync_PreloadsSettings()
    {
        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        _settingsPreloader.Verify(p => p.PreloadAllSettingsAsync(), Times.Once);
    }

    [Fact]
    public async Task RunStartupSequenceAsync_Phase1Failure_ContinuesToPhase2()
    {
        _settingsRegistry.Setup(r => r.InitializeAsync())
            .ThrowsAsync(new InvalidOperationException("Settings init failed"));

        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        var result = await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        result.Should().NotBeNull();
        _logService.Verify(l => l.LogWarning(It.Is<string>(s =>
            s.Contains("Failed to initialize settings registry"))), Times.Once);
    }

    // --- Phase 2: User backup config ---

    [Fact]
    public async Task RunStartupSequenceAsync_WhenBackupAlreadyCompleted_SkipsBackupPhase()
    {
        _preferencesService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialConfigBackupCompleted, false))
            .Returns(true);

        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        _configurationService.Verify(c => c.CreateUserBackupConfigAsync(), Times.Never);
    }

    [Fact]
    public async Task RunStartupSequenceAsync_WhenBackupNotCompleted_CreatesBackup()
    {
        _preferencesService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialConfigBackupCompleted, false))
            .Returns(false);
        _configurationService.Setup(c => c.CreateUserBackupConfigAsync())
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        _configurationService.Verify(c => c.CreateUserBackupConfigAsync(), Times.Once);
    }

    [Fact]
    public async Task RunStartupSequenceAsync_WhenBackupSucceeds_SetsPreferenceToTrue()
    {
        _preferencesService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialConfigBackupCompleted, false))
            .Returns(false);
        _configurationService.Setup(c => c.CreateUserBackupConfigAsync())
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        _preferencesService.Verify(p => p.SetPreferenceAsync(
            UserPreferenceKeys.InitialConfigBackupCompleted, true), Times.Once);
    }

    [Fact]
    public async Task RunStartupSequenceAsync_Phase2Failure_ContinuesToPhase3()
    {
        _preferencesService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialConfigBackupCompleted, false))
            .Returns(false);
        _configurationService.Setup(c => c.CreateUserBackupConfigAsync())
            .ThrowsAsync(new InvalidOperationException("Backup failed"));

        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        var result = await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        result.Should().NotBeNull();
        _logService.Verify(l => l.LogWarning(It.Is<string>(s =>
            s.Contains("User backup config failed"))), Times.Once);
    }

    // --- IsFirstLaunch ---

    [Fact]
    public async Task RunStartupSequenceAsync_WhenBackupAlreadyCompleted_IsFirstLaunchIsFalse()
    {
        _preferencesService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialConfigBackupCompleted, false))
            .Returns(true);

        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        var result = await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        result.IsFirstLaunch.Should().BeFalse();
    }

    [Fact]
    public async Task RunStartupSequenceAsync_WhenBackupNotCompletedAndSucceeds_IsFirstLaunchIsTrue()
    {
        _preferencesService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialConfigBackupCompleted, false))
            .Returns(false);
        _configurationService.Setup(c => c.CreateUserBackupConfigAsync())
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        var result = await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        result.IsFirstLaunch.Should().BeTrue();
    }

    // --- Phase 3: Script migration ---

    [Fact]
    public async Task RunStartupSequenceAsync_RunsScriptMigration()
    {
        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        _migrationService.Verify(m => m.MigrateFromOldPathsAsync(), Times.Once);
    }

    [Fact]
    public async Task RunStartupSequenceAsync_Phase3Failure_ContinuesToPhase4()
    {
        _migrationService.Setup(m => m.MigrateFromOldPathsAsync())
            .ThrowsAsync(new InvalidOperationException("Migration failed"));

        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        var result = await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        result.Should().NotBeNull();
        _logService.Verify(l => l.LogWarning(It.Is<string>(s =>
            s.Contains("Script migration failed"))), Times.Once);
    }

    // --- Phase 4: Script updates ---

    [Fact]
    public async Task RunStartupSequenceAsync_ChecksForScriptUpdates()
    {
        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        _updateService.Verify(u => u.CheckAndUpdateScriptsAsync(), Times.Once);
    }

    [Fact]
    public async Task RunStartupSequenceAsync_Phase4Failure_StillReturnsResult()
    {
        _updateService.Setup(u => u.CheckAndUpdateScriptsAsync())
            .ThrowsAsync(new InvalidOperationException("Update check failed"));

        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        var result = await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        result.Should().NotBeNull();
        _logService.Verify(l => l.LogWarning(It.Is<string>(s =>
            s.Contains("Script update check failed"))), Times.Once);
    }

    // --- Return value ---

    [Fact]
    public async Task RunStartupSequenceAsync_ReturnsStartupResult()
    {
        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        var result = await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        result.Should().NotBeNull();
        result.Should().BeOfType<StartupResult>();
    }

    // --- Full sequence execution ---

    [Fact]
    public async Task RunStartupSequenceAsync_ExecutesAllPhasesInOrder()
    {
        _preferencesService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialConfigBackupCompleted, false))
            .Returns(true);

        var callOrder = new List<string>();
        _settingsRegistry.Setup(r => r.InitializeAsync())
            .Callback(() => callOrder.Add("Phase1_Registry"))
            .Returns(Task.CompletedTask);
        _settingsPreloader.Setup(p => p.PreloadAllSettingsAsync())
            .Callback(() => callOrder.Add("Phase1_Preload"))
            .Returns(Task.CompletedTask);
        _migrationService.Setup(m => m.MigrateFromOldPathsAsync())
            .Callback(() => callOrder.Add("Phase3_Migration"))
            .ReturnsAsync(new ScriptMigrationResult());
        _updateService.Setup(u => u.CheckAndUpdateScriptsAsync())
            .Callback(() => callOrder.Add("Phase4_Update"))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        callOrder.Should().ContainInOrder(
            "Phase1_Registry",
            "Phase1_Preload",
            "Phase3_Migration",
            "Phase4_Update");
    }

    // --- Resilience: All phases fail gracefully ---

    [Fact]
    public async Task RunStartupSequenceAsync_WhenAllPhasesFail_StillReturnsResult()
    {
        _settingsRegistry.Setup(r => r.InitializeAsync())
            .ThrowsAsync(new Exception("Phase 1 fail"));
        _preferencesService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialConfigBackupCompleted, false))
            .Returns(false);
        _configurationService.Setup(c => c.CreateUserBackupConfigAsync())
            .ThrowsAsync(new Exception("Phase 2 fail"));
        _migrationService.Setup(m => m.MigrateFromOldPathsAsync())
            .ThrowsAsync(new Exception("Phase 3 fail"));
        _updateService.Setup(u => u.CheckAndUpdateScriptsAsync())
            .ThrowsAsync(new Exception("Phase 4 fail"));

        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        var result = await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        result.Should().NotBeNull();
    }
}
