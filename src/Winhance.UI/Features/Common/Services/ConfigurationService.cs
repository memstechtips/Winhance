using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly ILogService _logService;
    private readonly ICatalogSettingsRegistry _catalogSettingsRegistry;
    private readonly ISelectionSetBuilder _selections;
    private readonly IConfigFileWriter _configFiles;
    private readonly ISaveFilePicker _picker;
    private readonly ILocalizationService _localization;
    private readonly IFileSystemService _fileSystem;
    private readonly IInteractiveUserService _interactiveUser;
    private readonly IConfigLoadService _configLoadService;
    private readonly IConfigApplicationExecutionService _configExecutionService;
    private readonly IConfigReviewOrchestrationService _configReviewOrchestrationService;
    private readonly IDialogService _dialogService;

    public ConfigurationService(
        ILogService logService,
        ICatalogSettingsRegistry catalogSettingsRegistry,
        ISelectionSetBuilder selections,
        IConfigFileWriter configFiles,
        ISaveFilePicker picker,
        ILocalizationService localization,
        IFileSystemService fileSystem,
        IInteractiveUserService interactiveUser,
        IConfigLoadService configLoadService,
        IConfigApplicationExecutionService configExecutionService,
        IConfigReviewOrchestrationService configReviewOrchestrationService,
        IDialogService dialogService)
    {
        _logService = logService;
        _catalogSettingsRegistry = catalogSettingsRegistry;
        _selections = selections;
        _configFiles = configFiles;
        _picker = picker;
        _localization = localization;
        _fileSystem = fileSystem;
        _interactiveUser = interactiveUser;
        _configLoadService = configLoadService;
        _configExecutionService = configExecutionService;
        _configReviewOrchestrationService = configReviewOrchestrationService;
        _dialogService = dialogService;
    }

    // Idempotent catalog-registry init on the entry points that read the catalog. Closes the import path's
    // degraded-startup gap: if the Phase-1 init failed, the import self-heals here instead of
    // surfacing a use-before-init error downstream.
    private Task EnsureRegistryInitializedAsync()
        => _catalogSettingsRegistry.InitializeAsync();

    public async Task ExportConfigurationAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, "Starting configuration export");

            await EnsureRegistryInitializedAsync();

            var set = await _selections.FromMachineAsync();

            if (set.WindowsApps.Count == 0)
            {
                var continueAnyway = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
                {
                    Message = _localization.GetString("Dialog_NoAppsSelected_Config_Message"),
                    Title = _localization.GetString("Dialog_NoAppsSelected_Title"),
                })).Confirmed;
                if (!continueAnyway)
                    return;
            }

            var filePath = _picker.PickSavePath(
                _localization.GetString("Config_FileDialog_SaveConfig"),
                ConfigFileConstants.FileFilter,
                ConfigFileConstants.FilePattern,
                $"Winhance_Config_{DateTime.Now:yyyyMMdd}{ConfigFileConstants.FileExtension}",
                "winhance");

            if (string.IsNullOrEmpty(filePath))
            {
                _logService.Log(LogLevel.Info, "Export: no save path chosen");
                return;
            }

            await _configFiles.WriteAsync(set, _selections.CurrentScope, filePath);

            _logService.Log(LogLevel.Info, $"Configuration exported to {filePath}");

            await _dialogService.ShowInformationAsync(
                _localization.GetString("Config_Export_Success_Message", filePath),
                _localization.GetString("Config_Export_Success_Title"));
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error exporting configuration: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                _localization.GetString("Config_Export_Error_Message", ex.Message),
                _localization.GetString("Config_Export_Error_Title"));
        }
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

        WinhanceConfigFile? config = selectedOption switch
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

        if (selectedOption == ImportOption.ImportWindowsDefaults)
        {
            importOptions = importOptions with { IsWindowsDefaults = true };
        }

        if (!importOptions.ReviewBeforeApplying)
            await _configExecutionService.ExecuteConfigImportAsync(config, importOptions);
        else
            await _configReviewOrchestrationService.EnterReviewModeAsync(config, importOptions.IsWindowsDefaults);
    }

    public async Task CreateUserBackupConfigAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, "Creating user backup configuration from current system state");

            await EnsureRegistryInitializedAsync();

            var set = await _selections.FromMachineForBackupAsync();

            var configDir = _fileSystem.CombinePath(
                _interactiveUser.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "Backup");

            _fileSystem.CreateDirectory(configDir);

            var fileName = $"UserBackup_{DateTime.Now:yyyyMMdd_HHmmss}{ConfigFileConstants.FileExtension}";
            var filePath = _fileSystem.CombinePath(configDir, fileName);

            await _configFiles.WriteAsync(set, _selections.CurrentScope, filePath);

            _logService.Log(LogLevel.Info, $"User backup configuration saved to {filePath}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error creating user backup configuration: {ex.Message}");
        }
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
