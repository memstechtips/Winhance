using System.Text.Json;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Constants;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.Common.Services;

public class ConfigLoadService : IConfigLoadService
{
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IWindowsVersionService _windowsVersionService;
    private readonly ICatalogSettingsRegistry _catalogSettingsRegistry;
    private readonly IConfigMigrationService _configMigrationService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IMainWindowProvider _mainWindowProvider;
    private readonly IConfigImportState _configImportState;

    public ConfigLoadService(
        ILogService logService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IWindowsVersionService windowsVersionService,
        ICatalogSettingsRegistry catalogSettingsRegistry,
        IConfigMigrationService configMigrationService,
        IInteractiveUserService interactiveUserService,
        IFileSystemService fileSystemService,
        IMainWindowProvider mainWindowProvider,
        IConfigImportState configImportState)
    {
        _logService = logService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _windowsVersionService = windowsVersionService;
        _catalogSettingsRegistry = catalogSettingsRegistry;
        _configMigrationService = configMigrationService;
        _interactiveUserService = interactiveUserService;
        _fileSystemService = fileSystemService;
        _mainWindowProvider = mainWindowProvider;
        _configImportState = configImportState;
    }

    private Microsoft.UI.Xaml.Window? GetMainWindow() => _mainWindowProvider.MainWindow;

    public async Task<WinhanceConfigFile?> LoadAndValidateConfigurationFromFileAsync()
    {
        try
        {
            var window = GetMainWindow();
            if (window == null)
            {
                _logService.Log(LogLevel.Error, "Cannot show file dialog - no main window");
                await _dialogService.ShowErrorAsync(_localizationService.GetString("Dialog_FileDialogUnavailable"));
                return null;
            }

            var filePath = Win32FileDialogHelper.ShowOpenFilePicker(
                window,
                "Open Configuration",
                ConfigFileConstants.FileFilter,
                ConfigFileConstants.FilePattern);

            return string.IsNullOrEmpty(filePath) ? null : await LoadAndValidateConfigurationAsync(filePath);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error loading configuration file: {ex.Message}");
            _dialogService.ShowMessage(_localizationService.GetString("Config_Load_Error", ex.Message), _localizationService.GetString("Dialog_Error"));
            return null;
        }
    }

    public async Task<WinhanceConfigFile?> LoadAndValidateConfigurationAsync(string filePath)
    {
        try
        {
            var read = await ReadFileAsync(filePath);
            if (read.Failure == ConfigReadFailure.Empty)
            {
                _dialogService.ShowMessage(_localizationService.GetString("Config_Load_Failed"), _localizationService.GetString("Dialog_Error"));
                return null;
            }
            if (read.Failure == ConfigReadFailure.UnsupportedVersion)
            {
                await RejectVersionAsync(read.Config!);
                return null;
            }

            _configImportState.SourceName = Path.GetFileName(filePath);
            _configImportState.SourcePath = filePath;
            _logService.Log(LogLevel.Info, $"Loaded config v{read.Config!.Version}");
            return read.Config;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error loading configuration file: {ex.Message}");
            _dialogService.ShowMessage(_localizationService.GetString("Config_Load_Error", ex.Message), _localizationService.GetString("Dialog_Error"));
            return null;
        }
    }

    public Task<WinhanceConfigFile?> LoadRecommendedConfigurationAsync() =>
        LoadEmbeddedAsync(windowsDefaults: false, "recommended", "Config_Load_RecommendedMissing", "Dialog_ImportConfig_Option_Recommended_Title");

    public Task<WinhanceConfigFile?> LoadWindowsDefaultsConfigurationAsync() =>
        LoadEmbeddedAsync(windowsDefaults: true, "Windows defaults", "Config_Load_WindowsDefaultsMissing", "Dialog_ImportConfig_Option_Defaults_Title");

    private async Task<WinhanceConfigFile?> LoadEmbeddedAsync(bool windowsDefaults, string label, string missingKey, string sourceNameKey)
    {
        try
        {
            var resourceName = EmbeddedResourceName(windowsDefaults);
            _logService.Log(LogLevel.Info, $"Loading embedded {label} configuration: {resourceName}");

            var read = await ReadResourceAsync(resourceName);
            if (read.Failure == ConfigReadFailure.ResourceMissing)
            {
                _logService.Log(LogLevel.Error, $"Embedded resource not found: {resourceName}");
                _dialogService.ShowMessage(
                    _localizationService.GetString(missingKey),
                    _localizationService.GetString("Config_Load_ResourceError_Title"));
                return null;
            }
            if (read.Failure == ConfigReadFailure.UnsupportedVersion)
            {
                await RejectVersionAsync(read.Config!);
                return null;
            }
            if (read.Failure == ConfigReadFailure.Empty)
                return null;

            _configImportState.SourceName = _localizationService.GetString(sourceNameKey);
            _configImportState.SourcePath = null;
            _logService.Log(LogLevel.Info, $"Successfully loaded embedded {label} configuration");
            return read.Config;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error loading {label} configuration: {ex.Message}");
            _dialogService.ShowMessage(_localizationService.GetString("Config_Load_Error", ex.Message), _localizationService.GetString("Dialog_Error"));
            return null;
        }
    }

    public async Task<WinhanceConfigFile?> LoadUserBackupConfigurationAsync()
    {
        try
        {
            var configDir = _fileSystemService.CombinePath(
                AppDataPaths.UserRoot(_interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData)),
                "Backup");

            if (!_fileSystemService.DirectoryExists(configDir))
            {
                _dialogService.ShowMessage(
                    _localizationService.GetStringOrDefault("Config_Backup_NotFound", "No backup configuration files were found. A backup is automatically created on first app launch."),
                    _localizationService.GetStringOrDefault("Config_Backup_NotFound_Title", "No Backup Found"));
                return null;
            }

            var backupFiles = _fileSystemService.GetFiles(configDir, $"UserBackup_*{ConfigFileConstants.FileExtension}")
                .OrderByDescending(f => f)
                .ToArray();

            if (backupFiles.Length == 0)
            {
                _dialogService.ShowMessage(
                    _localizationService.GetStringOrDefault("Config_Backup_NotFound", "No backup configuration files were found. A backup is automatically created on first app launch."),
                    _localizationService.GetStringOrDefault("Config_Backup_NotFound_Title", "No Backup Found"));
                return null;
            }

            string filePath;

            if (backupFiles.Length == 1)
            {
                filePath = backupFiles[0];
            }
            else
            {
                var window = GetMainWindow();
                if (window == null)
                {
                    _logService.Log(LogLevel.Error, "Cannot show file dialog - no main window");
                    await _dialogService.ShowErrorAsync(_localizationService.GetString("Dialog_FileDialogUnavailable"));
                    return null;
                }

                var dialogTitle = _localizationService.GetStringOrDefault("Config_Backup_Select_Title", "Select Backup File");
                var selectedPath = Win32FileDialogHelper.ShowOpenFilePicker(
                    window, dialogTitle, ConfigFileConstants.FileFilter, ConfigFileConstants.FilePattern, configDir);

                if (string.IsNullOrEmpty(selectedPath))
                {
                    _logService.Log(LogLevel.Info, "Backup import canceled by user");
                    return null;
                }

                filePath = selectedPath;
            }
            _logService.Log(LogLevel.Info, $"Loading user backup configuration from {filePath}");

            var read = await ReadFileAsync(filePath);
            if (read.Failure == ConfigReadFailure.Empty)
            {
                _dialogService.ShowMessage(_localizationService.GetString("Config_Backup_LoadFailed"), _localizationService.GetString("Dialog_Error"));
                return null;
            }
            if (read.Failure == ConfigReadFailure.UnsupportedVersion)
            {
                await RejectVersionAsync(read.Config!);
                return null;
            }

            _configImportState.SourceName = _localizationService.GetString("Dialog_ImportConfig_Option_Backup_Title");
            _configImportState.SourcePath = null;
            _logService.Log(LogLevel.Info, "Successfully loaded user backup configuration");
            return read.Config;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error loading user backup configuration: {ex.Message}");
            _dialogService.ShowMessage(_localizationService.GetString("Config_Backup_LoadError", ex.Message), _localizationService.GetString("Dialog_Error"));
            return null;
        }
    }

    public async Task<WinhanceConfigFile?> ReadConfigurationAsync(string filePath)
    {
        try
        {
            return Accepted(await ReadFileAsync(filePath), filePath);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Could not read {filePath}: {ex.Message}");
            return null;
        }
    }

    public WinhanceConfigFile? ParseConfiguration(byte[] content, string source)
    {
        try
        {
            using var reader = new StreamReader(new MemoryStream(content));
            return Accepted(Parse(reader.ReadToEnd()), source);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Could not read {source}: {ex.Message}");
            return null;
        }
    }

    private WinhanceConfigFile? Accepted(ConfigRead read, string source)
    {
        if (read.Failure == ConfigReadFailure.UnsupportedVersion)
            _logService.Log(LogLevel.Warning, $"Not loading {source}: config version {read.Config!.Version}");
        return read.Failure == ConfigReadFailure.None ? read.Config : null;
    }

    public async Task<WinhanceConfigFile?> ReadEmbeddedConfigurationAsync(bool windowsDefaults)
    {
        var resourceName = EmbeddedResourceName(windowsDefaults);
        try
        {
            var read = await ReadResourceAsync(resourceName);
            if (read.Failure == ConfigReadFailure.ResourceMissing)
                _logService.Log(LogLevel.Error, $"Embedded resource not found: {resourceName}");
            return read.Failure == ConfigReadFailure.None ? read.Config : null;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Could not read {resourceName}: {ex.Message}");
            return null;
        }
    }

    private enum ConfigReadFailure { None, Empty, UnsupportedVersion, ResourceMissing }

    // Config is set for UnsupportedVersion too, so the caller can name the version it refused.
    private readonly record struct ConfigRead(WinhanceConfigFile? Config, ConfigReadFailure Failure);

    private async Task<ConfigRead> ReadFileAsync(string filePath) =>
        Parse(await _fileSystemService.ReadAllTextAsync(filePath));

    private async Task<ConfigRead> ReadResourceAsync(string resourceName)
    {
        using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null)
            return new ConfigRead(null, ConfigReadFailure.ResourceMissing);
        using var reader = new StreamReader(stream);
        return Parse(await reader.ReadToEndAsync());
    }

    private ConfigRead Parse(string json)
    {
        var config = JsonSerializer.Deserialize<WinhanceConfigFile>(json, ConfigFileConstants.JsonOptions);
        if (config is null)
            return new ConfigRead(null, ConfigReadFailure.Empty);
        _configMigrationService.MigrateConfig(config);
        return new ConfigRead(config, config.Version == "2.0" ? ConfigReadFailure.None : ConfigReadFailure.UnsupportedVersion);
    }

    private async Task RejectVersionAsync(WinhanceConfigFile config)
    {
        var versionText = config.Version ?? "unknown";
        await _dialogService.ShowInformationAsync(
            _localizationService.GetStringOrDefault("Config_Unsupported_Message", $"This configuration file version ({versionText}) is not compatible with this version of Winhance.", versionText),
            _localizationService.GetStringOrDefault("Config_Unsupported_Title", "Unsupported Configuration Version"));
        _logService.Log(LogLevel.Warning, $"Rejected incompatible config version: {config.Version}");
    }

    private string EmbeddedResourceName(bool windowsDefaults) =>
        !windowsDefaults ? "Winhance.UI.Resources.Configs.Winhance_Recommended_Config.winhance"
        : _windowsVersionService.IsWindows11() ? "Winhance.UI.Resources.Configs.Winhance_Default_Config_Windows11_25H2.winhance"
        : "Winhance.UI.Resources.Configs.Winhance_Default_Config_Windows10_22H2.winhance";

    public List<string> DetectIncompatibleSettings(WinhanceConfigFile config)
    {
        var incompatible = new List<string>();
        foreach (var (_, feature, _, items) in config.SettingSections())
        {
            foreach (var configItem in items)
            {
                // An old id is resolved here too. An id nothing answers to is not this PC's to set aside;
                // the import summary names it as no longer in Winhance.
                var newSetting = string.IsNullOrEmpty(configItem.Id) ? null : SettingCatalog.Find(configItem.Id);
                if (newSetting != null && !IsOnThisMachine(newSetting.Id))
                    incompatible.Add($"{_localizationService.SettingName(newSetting)} ({_localizationService.FeatureName(newSetting.Id, feature)})");
            }
        }

        return incompatible;
    }

    // A null section, feature or item is dropped here, so nothing after the filter meets one.
    public WinhanceConfigFile FilterConfigForCurrentSystem(WinhanceConfigFile config)
    {
        var sections = config.SettingSections().ToList();
        FeatureGroupSection Filtered(string group, FeatureGroupSection? source) => new()
        {
            IsIncluded = source?.IsIncluded ?? false,
            Features = sections.Where(s => s.Group == group).ToDictionary(
                s => s.Feature,
                s => new ConfigSection { IsIncluded = s.Section.IsIncluded, Items = s.Items.Where(IsKeptOnThisMachine).ToList() }),
        };

        return new WinhanceConfigFile
        {
            Version = config.Version,
            Optimize = Filtered("Optimize", config.Optimize),
            Customize = Filtered("Customize", config.Customize),
            WindowsApps = config.WindowsApps,
            ExternalApps = config.ExternalApps,
            AppInstallOptions = config.AppInstallOptions,
        };
    }

    private bool IsOnThisMachine(string settingId) =>
        _catalogSettingsRegistry.GetById(settingId, CatalogScope.CurrentMachine) is not null;

    // Known catalog setting -> kept only when this machine shows it; unknown id -> keep.
    private bool IsKeptOnThisMachine(ConfigurationItem item) =>
        (string.IsNullOrEmpty(item.Id) ? null : SettingCatalog.Find(item.Id)) is not { } setting || IsOnThisMachine(setting.Id);
}
