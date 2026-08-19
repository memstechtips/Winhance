using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ConfigurationServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ICatalogSettingsRegistry> _mockCatalogSettingsRegistry = new();
    private readonly Mock<IConfigExportService> _mockConfigExportService = new();
    private readonly Mock<IConfigLoadService> _mockConfigLoadService = new();
    private readonly Mock<IConfigApplicationExecutionService> _mockConfigExecutionService = new();
    private readonly Mock<IConfigReviewOrchestrationService> _mockConfigReviewOrchestrationService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private ConfigurationService CreateService()
    {
        return new ConfigurationService(
            _mockLogService.Object,
            _mockCatalogSettingsRegistry.Object,
            _mockConfigExportService.Object,
            _mockConfigLoadService.Object,
            _mockConfigExecutionService.Object,
            _mockConfigReviewOrchestrationService.Object,
            _mockDialogService.Object);
    }

    [Fact]
    public async Task ExportConfigurationAsync_DelegatesToConfigExportService()
    {
        var service = CreateService();
        await service.ExportConfigurationAsync();

        _mockConfigExportService.Verify(e => e.ExportConfigurationAsync(), Times.Once);
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
    public async Task CreateUserBackupConfigAsync_DelegatesToExportService()
    {
        var service = CreateService();
        await service.CreateUserBackupConfigAsync();

        _mockConfigExportService.Verify(e => e.CreateUserBackupConfigAsync(), Times.Once);
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
}
