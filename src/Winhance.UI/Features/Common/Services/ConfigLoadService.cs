using System.Text.Json;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
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

            if (string.IsNullOrEmpty(filePath))
                return null;

            _configImportState.SourceName = Path.GetFileName(filePath);

            var json = await _fileSystemService.ReadAllTextAsync(filePath);
            var loadedConfig = JsonSerializer.Deserialize<WinhanceConfigFile>(json, ConfigFileConstants.JsonOptions);

            if (loadedConfig == null)
            {
                _dialogService.ShowMessage(_localizationService.GetString("Config_Load_Failed"), _localizationService.GetString("Dialog_Error"));
                return null;
            }

            _configMigrationService.MigrateConfig(loadedConfig);

            if (loadedConfig.Version != "2.0")
            {
                var versionText = loadedConfig.Version ?? "unknown";
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetStringOrDefault("Config_Unsupported_Message", $"This configuration file version ({versionText}) is not compatible with this version of Winhance.", versionText),
                    _localizationService.GetStringOrDefault("Config_Unsupported_Title", "Incompatible Configuration"));
                _logService.Log(LogLevel.Warning, $"Rejected incompatible config version: {loadedConfig.Version}");
                return null;
            }

            _logService.Log(LogLevel.Info, $"Loaded config v{loadedConfig.Version}");
            return loadedConfig;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error loading configuration file: {ex.Message}");
            _dialogService.ShowMessage(_localizationService.GetString("Config_Load_Error", ex.Message), _localizationService.GetString("Dialog_Error"));
            return null;
        }
    }

    public async Task<WinhanceConfigFile?> LoadRecommendedConfigurationAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, "Loading embedded recommended configuration");

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "Winhance.UI.Resources.Configs.Winhance_Recommended_Config.winhance";

            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                _logService.Log(LogLevel.Error, $"Embedded resource not found: {resourceName}");
                _dialogService.ShowMessage(
                    _localizationService.GetString("Config_Load_RecommendedMissing"),
                    _localizationService.GetString("Config_Load_ResourceError_Title"));
                return null;
            }

            _configImportState.SourceName = _localizationService.GetString("Dialog_ImportConfig_Option_Recommended_Title");

            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var config = JsonSerializer.Deserialize<WinhanceConfigFile>(json, ConfigFileConstants.JsonOptions);

            _logService.Log(LogLevel.Info, "Successfully loaded embedded recommended configuration");
            return config;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error loading recommended configuration: {ex.Message}");
            _dialogService.ShowMessage(_localizationService.GetString("Config_Load_Error", ex.Message), _localizationService.GetString("Dialog_Error"));
            return null;
        }
    }

    public async Task<WinhanceConfigFile?> LoadWindowsDefaultsConfigurationAsync()
    {
        try
        {
            var isWindows11 = _windowsVersionService.IsWindows11();
            var resourceName = isWindows11
                ? "Winhance.UI.Resources.Configs.Winhance_Default_Config_Windows11_25H2.winhance"
                : "Winhance.UI.Resources.Configs.Winhance_Default_Config_Windows10_22H2.winhance";

            _logService.Log(LogLevel.Info, $"Loading embedded Windows defaults configuration for {(isWindows11 ? "Windows 11" : "Windows 10")}: {resourceName}");

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                _logService.Log(LogLevel.Error, $"Embedded resource not found: {resourceName}");
                _dialogService.ShowMessage(
                    _localizationService.GetString("Config_Load_WindowsDefaultsMissing"),
                    _localizationService.GetString("Config_Load_ResourceError_Title"));
                return null;
            }

            _configImportState.SourceName = _localizationService.GetString("Dialog_ImportConfig_Option_Defaults_Title");

            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var config = JsonSerializer.Deserialize<WinhanceConfigFile>(json, ConfigFileConstants.JsonOptions);

            _logService.Log(LogLevel.Info, "Successfully loaded embedded Windows defaults configuration");
            return config;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error loading Windows defaults configuration: {ex.Message}");
            _dialogService.ShowMessage(_localizationService.GetString("Config_Load_Error", ex.Message), _localizationService.GetString("Dialog_Error"));
            return null;
        }
    }

    public async Task<WinhanceConfigFile?> LoadUserBackupConfigurationAsync()
    {
        try
        {
            var configDir = _fileSystemService.CombinePath(
                _interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "Backup");

            if (!_fileSystemService.DirectoryExists(configDir))
            {
                _dialogService.ShowMessage(
                    _localizationService.GetStringOrDefault("Config_Backup_NotFound", "No backup configuration files found."),
                    _localizationService.GetStringOrDefault("Config_Backup_NotFound_Title", "No Backup Found"));
                return null;
            }

            var backupFiles = _fileSystemService.GetFiles(configDir, $"UserBackup_*{ConfigFileConstants.FileExtension}")
                .OrderByDescending(f => f)
                .ToArray();

            if (backupFiles.Length == 0)
            {
                _dialogService.ShowMessage(
                    _localizationService.GetStringOrDefault("Config_Backup_NotFound", "No backup configuration files found."),
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
            _configImportState.SourceName = _localizationService.GetString("Dialog_ImportConfig_Option_Backup_Title");

            var json = await _fileSystemService.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<WinhanceConfigFile>(json, ConfigFileConstants.JsonOptions);

            if (config == null)
            {
                _dialogService.ShowMessage(_localizationService.GetString("Config_Backup_LoadFailed"), _localizationService.GetString("Dialog_Error"));
                return null;
            }

            _configMigrationService.MigrateConfig(config);

            _logService.Log(LogLevel.Info, "Successfully loaded user backup configuration");
            return config;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error loading user backup configuration: {ex.Message}");
            _dialogService.ShowMessage(_localizationService.GetString("Config_Backup_LoadError", ex.Message), _localizationService.GetString("Dialog_Error"));
            return null;
        }
    }

    public List<string> DetectIncompatibleSettings(WinhanceConfigFile config)
    {
        var incompatible = new List<string>();
        var buildNumber = _windowsVersionService.GetWindowsBuildNumber();
        var buildRevision = _windowsVersionService.GetWindowsBuildRevision();

        var allSections = new Dictionary<string, FeatureGroupSection>
        {
            ["Optimize"] = config.Optimize,
            ["Customize"] = config.Customize
        };

        foreach (var section in allSections)
        {
            if (section.Value?.Features == null) continue;

            foreach (var feature in section.Value.Features)
            {
                foreach (var configItem in feature.Value.Items)
                {
                    // Gating reads ONLY the catalog Availability model (the source of truth). The only
                    // ids with no EXACT catalog match are the 6 merged "-win10" aliases: file/backup loads normalize them
                    // upstream (ConfigMigrationService), and the embedded Recommended/Win10-defaults configs carry them
                    // with values byte-identical to their canonical peers, which the import bridge applies via
                    // its alias-normalizing GetById (an idempotent duplicate). So an id with no catalog peer is skipped silently.
                    var newSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == configItem.Id);
                    if (newSetting != null && !newSetting.Availability.Allows(new WinBuild(buildNumber, buildRevision)))
                    {
                        incompatible.Add($"{newSetting.Display.Name} ({feature.Key})");
                    }
                }
            }
        }

        return incompatible;
    }

    public WinhanceConfigFile FilterConfigForCurrentSystem(WinhanceConfigFile config)
    {
        var buildNumber = _windowsVersionService.GetWindowsBuildNumber();
        var buildRevision = _windowsVersionService.GetWindowsBuildRevision();

        var filteredOptimize = FilterFeatureGroup(config.Optimize, buildNumber, buildRevision);
        var filteredCustomize = FilterFeatureGroup(config.Customize, buildNumber, buildRevision);

        return new WinhanceConfigFile
        {
            Version = config.Version,
            Optimize = filteredOptimize,
            Customize = filteredCustomize,
            WindowsApps = config.WindowsApps,
            ExternalApps = config.ExternalApps
        };
    }

    private FeatureGroupSection FilterFeatureGroup(
        FeatureGroupSection section,
        int buildNumber,
        int buildRevision)
    {
        if (section?.Features == null) return section!;

        var filteredFeatures = new Dictionary<string, ConfigSection>();

        foreach (var feature in section.Features)
        {
            var filteredItems = new List<ConfigurationItem>();

            foreach (var item in feature.Value.Items)
            {
                // Known catalog setting -> gate via the Availability model; unknown id -> keep. The raw "-win10"
                // alias ids in the embedded configs flow to the import bridge, whose alias-normalizing GetById
                // applies them onto the merged setting - see the DetectIncompatibleSettings comment.
                var newSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == item.Id);
                if (newSetting != null)
                {
                    if (newSetting.Availability.Allows(new WinBuild(buildNumber, buildRevision)))
                    {
                        filteredItems.Add(item);
                    }
                    continue;
                }

                filteredItems.Add(item);
            }

            filteredFeatures[feature.Key] = new ConfigSection
            {
                IsIncluded = feature.Value.IsIncluded,
                Items = filteredItems
            };
        }

        return new FeatureGroupSection
        {
            IsIncluded = section.IsIncluded,
            Features = filteredFeatures
        };
    }
}
