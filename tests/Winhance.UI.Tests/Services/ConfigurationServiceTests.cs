using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ConfigurationServiceTests
{
    private const string PickedPath = @"C:\Users\Test\Winhance_Config_20260819.winhance";
    private const string LocalAppData = @"C:\Users\Test\AppData\Local";
    private const string BackupDir = @"C:\Users\Test\AppData\Local\Winhance\Backup";
    private const string BackupFile = BackupDir + @"\UserBackup.winhance";

    private static readonly AppChoice[] OneWindowsApp = [new AppChoice("app1", "App 1", null, null, null, null)];

    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ICatalogSettingsRegistry> _mockCatalogSettingsRegistry = new();
    private readonly Mock<ISelectionSetBuilder> _mockSelections = new();
    private readonly Mock<ISelectionSaveService> _mockSaves = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUserService = new();
    private readonly Mock<IConfigLoadService> _mockConfigLoadService = new();
    private readonly Mock<IConfigApplicationExecutionService> _mockConfigExecutionService = new();
    private readonly Mock<IConfigReviewOrchestrationService> _mockConfigReviewOrchestrationService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();

    public ConfigurationServiceTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => string.Format(key, args));

        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync(new SaveOutcome(PickedPath, false));
    }

    private ConfigurationService CreateService()
    {
        return new ConfigurationService(
            _mockLogService.Object,
            _mockCatalogSettingsRegistry.Object,
            _mockSelections.Object,
            _mockSaves.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockInteractiveUserService.Object,
            _mockConfigLoadService.Object,
            _mockConfigExecutionService.Object,
            _mockConfigReviewOrchestrationService.Object,
            _mockDialogService.Object);
    }

    private static SelectionSet SetWith(IReadOnlyList<AppChoice> windowsApps) =>
        new(Array.Empty<SettingChoice>(), windowsApps, Array.Empty<AppChoice>(), AutounattendChoices.None);

    private SelectionSet ArrangeMachineSet(IReadOnlyList<AppChoice> windowsApps)
    {
        var set = SetWith(windowsApps);
        _mockSelections.Setup(s => s.FromMachineAsync()).ReturnsAsync(set);
        return set;
    }

    [Fact]
    public async Task Export_HandsTheMachineSetToTheSaveService()
    {
        var set = ArrangeMachineSet(OneWindowsApp);

        await CreateService().ExportConfigurationAsync();

        _mockCatalogSettingsRegistry.Verify(r => r.InitializeAsync(), Times.Once);
        _mockSaves.Verify(s => s.SaveAsync(BuilderTarget.Config, set, null), Times.Once);
    }

    [Fact]
    public async Task Export_WhenTheSaveThrows_ShowsErrorDialog()
    {
        ArrangeMachineSet(OneWindowsApp);
        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ThrowsAsync(new IOException("disk full"));

        await CreateService().ExportConfigurationAsync();

        _mockDialogService.Verify(
            d => d.ShowErrorAsync("Config_Export_Error_Message", "Config_Export_Error_Title", ""),
            Times.Once);
    }

    [Fact]
    public async Task ImportConfigurationAsync_WhenUserCancels_DoesNotProceed()
    {
        _mockDialogService
            .Setup(d => d.ShowConfigImportOptionsDialogAsync())
            .ReturnsAsync(((ImportOption?)null, new ImportOptions()));

        var service = CreateService();
        await service.ImportConfigurationAsync();

        _mockConfigLoadService.Verify(
            l => l.LoadAndValidateConfigurationFromFileAsync(),
            Times.Never);
        _mockConfigExecutionService.Verify(
            e => e.ExecuteConfigImportAsync(It.IsAny<WinhanceConfigFile>(), It.IsAny<ImportOptions>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportConfigurationAsync_WithImportOwn_LoadsFromFile()
    {
        var config = new WinhanceConfigFile { Version = "2.0" };
        var options = new ImportOptions();

        _mockDialogService
            .Setup(d => d.ShowConfigImportOptionsDialogAsync())
            .ReturnsAsync((ImportOption.ImportOwn, options));

        _mockConfigLoadService
            .Setup(l => l.LoadAndValidateConfigurationFromFileAsync())
            .ReturnsAsync(config);

        var service = CreateService();
        await service.ImportConfigurationAsync();

        _mockConfigLoadService.Verify(l => l.LoadAndValidateConfigurationFromFileAsync(), Times.Once);
        // The import entry point ensure-inits the catalog registry (idempotent InitializeAsync) as the
        // degraded-startup self-heal. Red if the ensure-init call is dropped.
        _mockCatalogSettingsRegistry.Verify(r => r.InitializeAsync(), Times.Once);
    }

    [Fact]
    public async Task ImportConfigurationAsync_WithImportRecommended_LoadsRecommended()
    {
        var config = new WinhanceConfigFile();
        var options = new ImportOptions();

        _mockDialogService
            .Setup(d => d.ShowConfigImportOptionsDialogAsync())
            .ReturnsAsync((ImportOption.ImportRecommended, options));

        _mockConfigLoadService
            .Setup(l => l.LoadRecommendedConfigurationAsync())
            .ReturnsAsync(config);

        var service = CreateService();
        await service.ImportConfigurationAsync();

        _mockConfigLoadService.Verify(l => l.LoadRecommendedConfigurationAsync(), Times.Once);
    }

    [Fact]
    public async Task ImportConfigurationAsync_WithImportBackup_LoadsBackup()
    {
        var config = new WinhanceConfigFile();
        var options = new ImportOptions();

        _mockDialogService
            .Setup(d => d.ShowConfigImportOptionsDialogAsync())
            .ReturnsAsync((ImportOption.ImportBackup, options));

        _mockConfigLoadService
            .Setup(l => l.LoadUserBackupConfigurationAsync())
            .ReturnsAsync(config);

        var service = CreateService();
        await service.ImportConfigurationAsync();

        _mockConfigLoadService.Verify(l => l.LoadUserBackupConfigurationAsync(), Times.Once);
    }

    [Fact]
    public async Task ImportConfigurationAsync_WithImportWindowsDefaults_LoadsDefaults()
    {
        var config = new WinhanceConfigFile();
        var options = new ImportOptions();

        _mockDialogService
            .Setup(d => d.ShowConfigImportOptionsDialogAsync())
            .ReturnsAsync((ImportOption.ImportWindowsDefaults, options));

        _mockConfigLoadService
            .Setup(l => l.LoadWindowsDefaultsConfigurationAsync())
            .ReturnsAsync(config);

        var service = CreateService();
        await service.ImportConfigurationAsync();

        _mockConfigLoadService.Verify(l => l.LoadWindowsDefaultsConfigurationAsync(), Times.Once);
    }

    [Fact]
    public async Task ImportConfigurationAsync_WhenConfigIsNull_DoesNotApply()
    {
        var options = new ImportOptions();

        _mockDialogService
            .Setup(d => d.ShowConfigImportOptionsDialogAsync())
            .ReturnsAsync((ImportOption.ImportRecommended, options));

        _mockConfigLoadService
            .Setup(l => l.LoadRecommendedConfigurationAsync())
            .ReturnsAsync((WinhanceConfigFile?)null);

        var service = CreateService();
        await service.ImportConfigurationAsync();

        _mockConfigExecutionService.Verify(
            e => e.ExecuteConfigImportAsync(It.IsAny<WinhanceConfigFile>(), It.IsAny<ImportOptions>()),
            Times.Never);
        _mockConfigReviewOrchestrationService.Verify(
            r => r.EnterReviewModeAsync(It.IsAny<WinhanceConfigFile>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportConfigurationAsync_WithReviewBeforeApplying_EntersReviewMode()
    {
        var config = new WinhanceConfigFile();
        var options = new ImportOptions { ReviewBeforeApplying = true };

        _mockDialogService
            .Setup(d => d.ShowConfigImportOptionsDialogAsync())
            .ReturnsAsync((ImportOption.ImportOwn, options));

        _mockConfigLoadService
            .Setup(l => l.LoadAndValidateConfigurationFromFileAsync())
            .ReturnsAsync(config);

        var service = CreateService();
        await service.ImportConfigurationAsync();

        _mockConfigReviewOrchestrationService.Verify(
            r => r.EnterReviewModeAsync(config),
            Times.Once);
        _mockConfigExecutionService.Verify(
            e => e.ExecuteConfigImportAsync(It.IsAny<WinhanceConfigFile>(), It.IsAny<ImportOptions>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportConfigurationAsync_WithoutReview_ExecutesDirectly()
    {
        var config = new WinhanceConfigFile();
        var options = new ImportOptions { ReviewBeforeApplying = false };

        _mockDialogService
            .Setup(d => d.ShowConfigImportOptionsDialogAsync())
            .ReturnsAsync((ImportOption.ImportOwn, options));

        _mockConfigLoadService
            .Setup(l => l.LoadAndValidateConfigurationFromFileAsync())
            .ReturnsAsync(config);

        var service = CreateService();
        await service.ImportConfigurationAsync();

        _mockConfigExecutionService.Verify(
            e => e.ExecuteConfigImportAsync(config, options),
            Times.Once);
        _mockConfigReviewOrchestrationService.Verify(
            r => r.EnterReviewModeAsync(It.IsAny<WinhanceConfigFile>()),
            Times.Never);
    }

    [Fact]
    public async Task Backup_UsesInstalledApps_AndTheBackupFolder()
    {
        var set = SetWith(OneWindowsApp);
        _mockSelections.Setup(s => s.FromMachineForBackupAsync()).ReturnsAsync(set);

        _mockInteractiveUserService
            .Setup(s => s.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData))
            .Returns(LocalAppData);

        _mockFileSystemService
            .Setup(fs => fs.CombinePath(LocalAppData, "Winhance", "Backup"))
            .Returns(BackupDir);
        _mockFileSystemService
            .Setup(fs => fs.CombinePath(BackupDir, It.IsAny<string>()))
            .Returns(BackupFile);

        await CreateService().CreateUserBackupConfigAsync();

        _mockSelections.Verify(s => s.FromMachineForBackupAsync(), Times.Once);
        _mockSelections.Verify(s => s.FromMachineAsync(), Times.Never);
        _mockFileSystemService.Verify(fs => fs.CombinePath(LocalAppData, "Winhance", "Backup"), Times.Once);
        _mockFileSystemService.Verify(fs => fs.CreateDirectory(BackupDir), Times.Once);

        // The generated file name is the part this service owns; CombinePath is mocked, so assert it here.
        _mockFileSystemService.Verify(
            fs => fs.CombinePath(BackupDir, It.Is<string>(name =>
                name.StartsWith("UserBackup_", StringComparison.Ordinal) && name.EndsWith(".winhance", StringComparison.Ordinal))),
            Times.Once);
        _mockSaves.Verify(
            s => s.SaveAsync(BuilderTarget.Config, set, It.Is<SelectionSaveOptions>(o =>
                o.FixedPath == BackupFile && !o.ConfirmEmptyAppSelection && !o.ReportSuccessInDialog)),
            Times.Once);
    }

    [Fact]
    public async Task ApplyReviewedConfigAsync_DelegatesToOrchestrationService()
    {
        var service = CreateService();
        await service.ApplyReviewedConfigAsync();

        _mockConfigReviewOrchestrationService.Verify(
            r => r.ApplyReviewedConfigAsync(),
            Times.Once);
    }

    [Fact]
    public async Task CancelReviewModeAsync_DelegatesToOrchestrationService()
    {
        var service = CreateService();
        await service.CancelReviewModeAsync();

        _mockConfigReviewOrchestrationService.Verify(
            r => r.CancelReviewModeAsync(),
            Times.Once);
    }

    [Fact]
    public async Task Backup_WhenTheWriterThrows_LogsAndShowsNoDialog()
    {
        _mockSelections.Setup(s => s.FromMachineForBackupAsync()).ReturnsAsync(SetWith(OneWindowsApp));
        _mockInteractiveUserService
            .Setup(s => s.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData))
            .Returns(LocalAppData);
        _mockFileSystemService.Setup(fs => fs.CombinePath(It.IsAny<string[]>())).Returns(BackupFile);
        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ThrowsAsync(new IOException("disk full"));

        await CreateService().CreateUserBackupConfigAsync();

        _mockLogService.Verify(l => l.Log(LogLevel.Error, It.Is<string>(m => m.Contains("disk full")), null), Times.Once);
        _mockDialogService.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
