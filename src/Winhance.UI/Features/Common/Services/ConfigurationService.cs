using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly ILogService _logService;
    private readonly ICatalogSettingsRegistry _catalogSettingsRegistry;
    private readonly ISelectionSetBuilder _selections;
    private readonly ISelectionSaveService _saves;
    private readonly ILocalizationService _localization;
    private readonly IFileSystemService _fileSystem;
    private readonly IInteractiveUserService _interactiveUser;
    private readonly IConfigLoadService _configLoadService;
    private readonly IConfigApplicationExecutionService _configExecutionService;
    private readonly IConfigReviewOrchestrationService _configReviewOrchestrationService;
    private readonly IDialogService _dialogService;
    private readonly IApplicationModeService _mode;
    private readonly IUserPreferencesService _preferences;

    private const string ConfigReviewIntroDontShowKey = "ConfigReviewModeIntroDontShow";

    public ConfigurationService(
        ILogService logService,
        ICatalogSettingsRegistry catalogSettingsRegistry,
        ISelectionSetBuilder selections,
        ISelectionSaveService saves,
        ILocalizationService localization,
        IFileSystemService fileSystem,
        IInteractiveUserService interactiveUser,
        IConfigLoadService configLoadService,
        IConfigApplicationExecutionService configExecutionService,
        IConfigReviewOrchestrationService configReviewOrchestrationService,
        IDialogService dialogService,
        IApplicationModeService mode,
        IUserPreferencesService preferences)
    {
        _logService = logService;
        _catalogSettingsRegistry = catalogSettingsRegistry;
        _selections = selections;
        _saves = saves;
        _localization = localization;
        _fileSystem = fileSystem;
        _interactiveUser = interactiveUser;
        _configLoadService = configLoadService;
        _configExecutionService = configExecutionService;
        _configReviewOrchestrationService = configReviewOrchestrationService;
        _dialogService = dialogService;
        _mode = mode;
        _preferences = preferences;
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

            await _saves.SaveAsync(BuilderTarget.Config, set);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error exporting configuration: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                _localization.GetString("Config_Export_Error_Message", ex.Message),
                _localization.GetString("Config_Export_Error_Title"));
        }
    }

    // Settings > Import and the mode switcher's Config Review both land here, from any mode.
    public async Task ImportConfigurationAsync()
    {
        _logService.Log(LogLevel.Info, "Starting configuration import");

        if (!await ModeSwitchConfirmation.ConfirmLeavingAsync(_mode, _dialogService, _localization)
            || !await ShowConfigReviewIntroIfNeededAsync())
            return;

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

        importOptions = PreTickFrom(config, importOptions);
        await LeaveCurrentModeAsync(importOptions.ReviewBeforeApplying);

        if (!importOptions.ReviewBeforeApplying)
            await _configExecutionService.ExecuteConfigImportAsync(config, importOptions);
        else
            await _configReviewOrchestrationService.EnterReviewModeAsync(config, importOptions.IsWindowsDefaults);
    }

    // The open mode is left only once a file is in hand, so cancelling the picker keeps it. Builder writes nothing to
    // this PC, so an apply has to leave it first; entering review tears Builder down itself.
    private async Task LeaveCurrentModeAsync(bool review)
    {
        if (_mode.CurrentMode == WinhanceMode.ConfigReview)
            await _configReviewOrchestrationService.CancelReviewModeAsync();
        else if (_mode.CurrentMode == WinhanceMode.Builder && !review)
            _mode.EnterNormalMode();
    }

    private async Task<bool> ShowConfigReviewIntroIfNeededAsync()
    {
        if (_preferences.GetPreference(ConfigReviewIntroDontShowKey, false))
            return true;

        var response = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Title = _localization.GetString("Dialog_ConfigReviewIntro_Title"),
            Message = _localization.GetString("Dialog_ConfigReviewIntro_Message"),
            CheckboxText = _localization.GetString("Dialog_Mode_DontShowAgain"),
            CheckboxInitiallyChecked = false,
            ConfirmButtonText = _localization.GetString("Dialog_ConfigReviewIntro_Confirm"),
            CancelButtonText = _localization.GetString("Button_Cancel"),
        });

        if (response.Confirmed && response.CheckboxChecked)
            await _preferences.SetPreferenceAsync(ConfigReviewIntroDontShowKey, true);

        return response.Confirmed;
    }

    // The import dialog's defaults with review skipped, so a scripted run applies what Import would.
    private static readonly ImportOptions CommandLineImportOptions = new()
    {
        ProcessWindowsAppsRemoval = true,
        ProcessExternalAppsInstallation = true,
        ApplyCleanTaskbar = true,
        ApplyCleanStartMenu = true,
    };

    public async Task ImportConfigurationFromFileAsync(string filePath)
    {
        _logService.Log(LogLevel.Info, $"Starting configuration import from the command line: {filePath}");

        await EnsureRegistryInitializedAsync();

        var config = await _configLoadService.LoadAndValidateConfigurationAsync(filePath);
        if (config == null)
        {
            _logService.Log(LogLevel.Warning, $"Command-line config not applied: {filePath}");
            return;
        }

        await _configExecutionService.ExecuteConfigImportAsync(config, PreTickFrom(config, CommandLineImportOptions));
    }

    // A clean action has to be selected in the file, not merely present: an unticked one is a config saying
    // "leave it alone".
    public static ImportOptions PreTickFrom(WinhanceConfigFile file, ImportOptions chosen) => chosen with
    {
        ApplyCleanTaskbar = chosen.ApplyCleanTaskbar && Carries(file, "taskbar-clean", requireSelected: true),
        ApplyCleanStartMenu = chosen.ApplyCleanStartMenu
            && (Carries(file, "start-menu-clean-10", requireSelected: true)
                || Carries(file, "start-menu-clean-11", requireSelected: true)),
    };

    private static bool Carries(WinhanceConfigFile file, string settingId, bool requireSelected) =>
        AllItems(file).Any(item => item.Id == settingId && (!requireSelected || item.IsSelected == true));

    private static IEnumerable<ConfigurationItem> AllItems(WinhanceConfigFile file) =>
        file.Customize.Features.Values
            .Concat(file.Optimize.Features.Values)
            .Concat(file.Autounattend.Features.Values)
            .SelectMany(section => section.Items);

    public async Task CreateUserBackupConfigAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, "Creating user backup configuration from current system state");

            await EnsureRegistryInitializedAsync();

            var set = await _selections.FromMachineForBackupAsync();

            var configDir = _fileSystem.CombinePath(
                AppDataPaths.UserRoot(_interactiveUser.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData)),
                "Backup");

            _fileSystem.CreateDirectory(configDir);

            var fileName = $"UserBackup_{DateTime.Now:yyyyMMdd_HHmmss}{ConfigFileConstants.FileExtension}";
            var filePath = _fileSystem.CombinePath(configDir, fileName);

            await _saves.SaveAsync(BuilderTarget.Config, set, new SelectionSaveOptions
            {
                FixedPath = filePath,
                ReportSuccessInDialog = false,
            });
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
