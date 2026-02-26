using FluentAssertions;
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
    private readonly Mock<ICompatibleSettingsRegistry> _mockCompatibleSettingsRegistry = new();
    private readonly Mock<IGlobalSettingsPreloader> _mockSettingsPreloader = new();
    private readonly Mock<IConfigExportService> _mockConfigExportService = new();
    private readonly Mock<IConfigLoadService> _mockConfigLoadService = new();
    private readonly Mock<IConfigApplicationExecutionService> _mockConfigExecutionService = new();
    private readonly Mock<IConfigReviewOrchestrationService> _mockConfigReviewOrchestrationService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();

    public ConfigurationServiceTests()
    {
        _mockCompatibleSettingsRegistry.Setup(r => r.IsInitialized).Returns(true);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
    }

    private ConfigurationService CreateService()
    {
        return new ConfigurationService(
            _mockLogService.Object,
            _mockCompatibleSettingsRegistry.Object,
            _mockSettingsPreloader.Object,
            _mockConfigExportService.Object,
            _mockConfigLoadService.Object,
            _mockConfigExecutionService.Object,
            _mockConfigReviewOrchestrationService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object);
    }

    // -------------------------------------------------------
    // ExportConfigurationAsync
    // -------------------------------------------------------

    [Fact]
    public async Task ExportConfigurationAsync_DelegatesToConfigExportService()
    {
        var service = CreateService();
        await service.ExportConfigurationAsync();

        _mockConfigExportService.Verify(e => e.ExportConfigurationAsync(), Times.Once);
    }

    // -------------------------------------------------------
    // ImportConfigurationAsync
    // -------------------------------------------------------

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
            e => e.ExecuteConfigImportAsync(It.IsAny<UnifiedConfigurationFile>(), It.IsAny<ImportOptions>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportConfigurationAsync_WithImportOwn_LoadsFromFile()
    {
        var config = new UnifiedConfigurationFile { Version = "2.0" };
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
    }

    [Fact]
    public async Task ImportConfigurationAsync_WithImportRecommended_LoadsRecommended()
    {
        var config = new UnifiedConfigurationFile();
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
        var config = new UnifiedConfigurationFile();
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
        var config = new UnifiedConfigurationFile();
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
            .ReturnsAsync((UnifiedConfigurationFile?)null);

        var service = CreateService();
        await service.ImportConfigurationAsync();

        _mockConfigExecutionService.Verify(
            e => e.ExecuteConfigImportAsync(It.IsAny<UnifiedConfigurationFile>(), It.IsAny<ImportOptions>()),
            Times.Never);
        _mockConfigReviewOrchestrationService.Verify(
            r => r.EnterReviewModeAsync(It.IsAny<UnifiedConfigurationFile>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportConfigurationAsync_WithReviewBeforeApplying_EntersReviewMode()
    {
        var config = new UnifiedConfigurationFile();
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
            e => e.ExecuteConfigImportAsync(It.IsAny<UnifiedConfigurationFile>(), It.IsAny<ImportOptions>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportConfigurationAsync_WithoutReview_ExecutesDirectly()
    {
        var config = new UnifiedConfigurationFile();
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
            r => r.EnterReviewModeAsync(It.IsAny<UnifiedConfigurationFile>()),
            Times.Never);
    }

    // -------------------------------------------------------
    // ImportRecommendedConfigurationAsync
    // -------------------------------------------------------

    [Fact]
    public async Task ImportRecommendedConfigurationAsync_LoadsAndEntersReviewMode()
    {
        var config = new UnifiedConfigurationFile();

        _mockConfigLoadService
            .Setup(l => l.LoadRecommendedConfigurationAsync())
            .ReturnsAsync(config);

        var service = CreateService();
        await service.ImportRecommendedConfigurationAsync();

        _mockConfigLoadService.Verify(l => l.LoadRecommendedConfigurationAsync(), Times.Once);
        _mockConfigReviewOrchestrationService.Verify(
            r => r.EnterReviewModeAsync(config),
            Times.Once);
    }

    [Fact]
    public async Task ImportRecommendedConfigurationAsync_WhenConfigIsNull_DoesNotEnterReviewMode()
    {
        _mockConfigLoadService
            .Setup(l => l.LoadRecommendedConfigurationAsync())
            .ReturnsAsync((UnifiedConfigurationFile?)null);

        var service = CreateService();
        await service.ImportRecommendedConfigurationAsync();

        _mockConfigReviewOrchestrationService.Verify(
            r => r.EnterReviewModeAsync(It.IsAny<UnifiedConfigurationFile>()),
            Times.Never);
    }

    // -------------------------------------------------------
    // CreateUserBackupConfigAsync
    // -------------------------------------------------------

    [Fact]
    public async Task CreateUserBackupConfigAsync_DelegatesToExportService()
    {
        var service = CreateService();
        await service.CreateUserBackupConfigAsync();

        _mockConfigExportService.Verify(e => e.CreateUserBackupConfigAsync(), Times.Once);
    }

    // -------------------------------------------------------
    // ApplyReviewedConfigAsync
    // -------------------------------------------------------

    [Fact]
    public async Task ApplyReviewedConfigAsync_DelegatesToOrchestrationService()
    {
        var service = CreateService();
        await service.ApplyReviewedConfigAsync();

        _mockConfigReviewOrchestrationService.Verify(
            r => r.ApplyReviewedConfigAsync(),
            Times.Once);
    }

    // -------------------------------------------------------
    // CancelReviewModeAsync
    // -------------------------------------------------------

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
