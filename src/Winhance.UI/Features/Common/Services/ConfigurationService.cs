using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Thin facade that preserves the IConfigurationService contract.
/// All work is delegated to focused sub-services.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ILogService _logService;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly IGlobalSettingsPreloader _settingsPreloader;
    private readonly IConfigExportService _configExportService;
    private readonly IConfigLoadService _configLoadService;
    private readonly IConfigApplicationExecutionService _configExecutionService;
    private readonly IConfigReviewOrchestrationService _configReviewOrchestrationService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;

    public ConfigurationService(
        ILogService logService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        IGlobalSettingsPreloader settingsPreloader,
        IConfigExportService configExportService,
        IConfigLoadService configLoadService,
        IConfigApplicationExecutionService configExecutionService,
        IConfigReviewOrchestrationService configReviewOrchestrationService,
        IDialogService dialogService,
        ILocalizationService localizationService)
    {
        _logService = logService;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _settingsPreloader = settingsPreloader;
        _configExportService = configExportService;
        _configLoadService = configLoadService;
        _configExecutionService = configExecutionService;
        _configReviewOrchestrationService = configReviewOrchestrationService;
        _dialogService = dialogService;
        _localizationService = localizationService;
    }

    private async Task EnsureRegistryInitializedAsync()
    {
        if (!_compatibleSettingsRegistry.IsInitialized)
        {
            _logService.Log(LogLevel.Info, "Initializing compatible settings registry for configuration service");
            await _compatibleSettingsRegistry.InitializeAsync();
        }

        if (!_settingsPreloader.IsPreloaded)
        {
            _logService.Log(LogLevel.Info, "Preloading settings for configuration service");
            await _settingsPreloader.PreloadAllSettingsAsync();
        }
    }

    public async Task ExportConfigurationAsync()
    {
        await _configExportService.ExportConfigurationAsync();
    }

    public async Task ImportConfigurationAsync()
    {
        _logService.Log(LogLevel.Info, "Starting configuration import");

        await EnsureRegistryInitializedAsync();

        var (selectedOption, importOptions) = await _dialogService.ShowConfigImportOptionsDialogAsync();
        if (selectedOption == null)
        {
            _logService.Log(LogLevel.Info, "Import canceled by user");
            return;
        }

        UnifiedConfigurationFile? config = selectedOption switch
        {
            ImportOption.ImportOwn => await _configLoadService.LoadAndValidateConfigurationFromFileAsync(),
            ImportOption.ImportRecommended => await _configLoadService.LoadRecommendedConfigurationAsync(),
            ImportOption.ImportBackup => await _configLoadService.LoadUserBackupConfigurationAsync(),
            ImportOption.ImportWindowsDefaults => await _configLoadService.LoadWindowsDefaultsConfigurationAsync(),
            _ => null
        };

        if (config == null)
        {
            if (selectedOption != ImportOption.ImportOwn)
                return;
            _logService.Log(LogLevel.Info, "Import canceled");
            return;
        }

        if (!importOptions.ReviewBeforeApplying)
            await _configExecutionService.ExecuteConfigImportAsync(config, importOptions);
        else
            await _configReviewOrchestrationService.EnterReviewModeAsync(config);
    }

    public async Task ImportRecommendedConfigurationAsync()
    {
        _logService.Log(LogLevel.Info, "Starting recommended configuration import");

        await EnsureRegistryInitializedAsync();

        var config = await _configLoadService.LoadRecommendedConfigurationAsync();
        if (config == null) return;

        // Recommended config always enters review mode so users can see what will change
        await _configReviewOrchestrationService.EnterReviewModeAsync(config);
    }

    public async Task CreateUserBackupConfigAsync()
    {
        await _configExportService.CreateUserBackupConfigAsync();
    }

    public async Task ApplyReviewedConfigAsync()
    {
        await _configReviewOrchestrationService.ApplyReviewedConfigAsync();
    }

    public async Task CancelReviewModeAsync()
    {
        await _configReviewOrchestrationService.CancelReviewModeAsync();
    }
}
