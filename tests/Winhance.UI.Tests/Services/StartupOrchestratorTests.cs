using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class StartupOrchestratorTests
{
    private readonly Mock<ICatalogSettingsRegistry> _catalogSettingsRegistry = new();
    private readonly Mock<IUserPreferencesService> _preferencesService = new();
    private readonly Mock<IConfigurationService> _configurationService = new();
    private readonly Mock<IScriptMigrationService> _migrationService = new();
    private readonly Mock<IRemovalScriptUpdateService> _updateService = new();
    private readonly Mock<INewBadgeService> _newBadgeService = new();
    private readonly Mock<ILogService> _logService = new();

    public StartupOrchestratorTests()
    {
        _preferencesService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialConfigBackupCompleted, false))
            .Returns(true);
    }

    private StartupOrchestrator CreateSut()
    {
        return new StartupOrchestrator(
            _catalogSettingsRegistry.Object,
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

    [Fact]
    public async Task RunStartupSequenceAsync_InitializesSettingsRegistry()
    {
        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        _catalogSettingsRegistry.Verify(r => r.InitializeAsync(), Times.Once);
    }

    [Fact]
    public async Task RunStartupSequenceAsync_Phase1Failure_ContinuesToPhase2()
    {
        _catalogSettingsRegistry.Setup(r => r.InitializeAsync())
            .ThrowsAsync(new InvalidOperationException("Settings init failed"));

        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        var result = await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        result.Should().NotBeNull();
        _logService.Verify(l => l.LogWarning(It.Is<string>(s =>
            s.Contains("Failed to initialize catalog settings registry"))), Times.Once);
    }

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

    [Fact]
    public async Task RunStartupSequenceAsync_ReturnsStartupResult()
    {
        var sut = CreateSut();
        var (statusProgress, detailedProgress, _) = CreateProgressTracking();

        var result = await sut.RunStartupSequenceAsync(statusProgress, detailedProgress);

        result.Should().NotBeNull();
        result.Should().BeOfType<StartupResult>();
    }

    [Fact]
    public async Task RunStartupSequenceAsync_ExecutesAllPhasesInOrder()
    {
        _preferencesService.Setup(p => p.GetPreference(
            UserPreferenceKeys.InitialConfigBackupCompleted, false))
            .Returns(true);

        var callOrder = new List<string>();
        _catalogSettingsRegistry.Setup(r => r.InitializeAsync())
            .Callback(() => callOrder.Add("Phase1_Registry"))
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
            "Phase3_Migration",
            "Phase4_Update");
    }

    [Fact]
    public async Task RunStartupSequenceAsync_WhenAllPhasesFail_StillReturnsResult()
    {
        _catalogSettingsRegistry.Setup(r => r.InitializeAsync())
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
