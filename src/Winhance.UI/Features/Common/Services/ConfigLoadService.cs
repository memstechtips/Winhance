using System.Text.Json;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Constants;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI.Features.Common.Services;

public class ConfigLoadService : IConfigLoadService
{
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IWindowsVersionService _windowsVersionService;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly IConfigMigrationService _configMigrationService;
    private readonly IInteractiveUserService _interactiveUserService;

    public ConfigLoadService(
        ILogService logService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IWindowsVersionService windowsVersionService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        IConfigMigrationService configMigrationService,
        IInteractiveUserService interactiveUserService)
    {
        _logService = logService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _windowsVersionService = windowsVersionService;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _configMigrationService = configMigrationService;
        _interactiveUserService = interactiveUserService;
    }

    private Window? GetMainWindow() => App.MainWindow;

    public async Task<UnifiedConfigurationFile?> LoadAndValidateConfigurationFromFileAsync()
    {
        var window = GetMainWindow();
        if (window == null)
        {
            _logService.Log(LogLevel.Error, "Cannot show file dialog - no main window");
            await _dialogService.ShowErrorAsync("Cannot show file dialog.", "Error");
            return null;
        }

        var filePath = Win32FileDialogHelper.ShowOpenFilePicker(
            window,
            "Open Configuration",
            ConfigFileConstants.FileFilter,
            ConfigFileConstants.FilePattern);

        if (string.IsNullOrEmpty(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        var loadedConfig = JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, ConfigFileConstants.JsonOptions);

        if (loadedConfig == null)
        {
            _dialogService.ShowMessage("Failed to load configuration file.", "Error");
            return null;
        }

        // Migrate legacy config items (e.g. Toggle->Selection conversions)
        _configMigrationService.MigrateConfig(loadedConfig);

        if (loadedConfig.Version != "2.0")
        {
            var versionText = loadedConfig.Version ?? "unknown";
            await _dialogService.ShowInformationAsync(
                _localizationService.GetString("Config_Unsupported_Message", versionText)
                    ?? $"This configuration file version ({versionText}) is not compatible with this version of Winhance.",
                _localizationService.GetString("Config_Unsupported_Title") ?? "Incompatible Configuration");
            _logService.Log(LogLevel.Warning, $"Rejected incompatible config version: {loadedConfig.Version}");
            return null;
        }

        _logService.Log(LogLevel.Info, $"Loaded config v{loadedConfig.Version}");
        return loadedConfig;
    }

    public async Task<UnifiedConfigurationFile?> LoadRecommendedConfigurationAsync()
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
                    "The recommended configuration file could not be found in the application.",
                    "Resource Error");
                return null;
            }

            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var config = JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, ConfigFileConstants.JsonOptions);

            _logService.Log(LogLevel.Info, "Successfully loaded embedded recommended configuration");
            return config;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error loading recommended configuration: {ex.Message}");
            _dialogService.ShowMessage($"Error loading configuration: {ex.Message}", "Error");
            return null;
        }
    }

    public async Task<UnifiedConfigurationFile?> LoadWindowsDefaultsConfigurationAsync()
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
                    "The Windows defaults configuration file could not be found in the application.",
                    "Resource Error");
                return null;
            }

            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var config = JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, ConfigFileConstants.JsonOptions);

            _logService.Log(LogLevel.Info, "Successfully loaded embedded Windows defaults configuration");
            return config;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error loading Windows defaults configuration: {ex.Message}");
            _dialogService.ShowMessage($"Error loading configuration: {ex.Message}", "Error");
            return null;
        }
    }

    public async Task<UnifiedConfigurationFile?> LoadUserBackupConfigurationAsync()
    {
        try
        {
            var configDir = Path.Combine(
                _interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "Backup");

            if (!Directory.Exists(configDir))
            {
                _dialogService.ShowMessage(
                    _localizationService.GetString("Config_Backup_NotFound") ?? "No backup configuration files found.",
                    _localizationService.GetString("Config_Backup_NotFound_Title") ?? "No Backup Found");
                return null;
            }

            var backupFiles = Directory.GetFiles(configDir, $"UserBackup_*{ConfigFileConstants.FileExtension}")
                .OrderByDescending(f => f)
                .ToArray();

            if (backupFiles.Length == 0)
            {
                _dialogService.ShowMessage(
                    _localizationService.GetString("Config_Backup_NotFound") ?? "No backup configuration files found.",
                    _localizationService.GetString("Config_Backup_NotFound_Title") ?? "No Backup Found");
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
                    await _dialogService.ShowErrorAsync("Cannot show file dialog.", "Error");
                    return null;
                }

                var dialogTitle = _localizationService.GetString("Config_Backup_Select_Title")
                    ?? "Select Backup File";
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

            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, ConfigFileConstants.JsonOptions);

            if (config == null)
            {
                _dialogService.ShowMessage("Failed to load backup configuration file.", "Error");
                return null;
            }

            // Migrate legacy config items (e.g. Toggle->Selection conversions)
            _configMigrationService.MigrateConfig(config);

            _logService.Log(LogLevel.Info, "Successfully loaded user backup configuration");
            return config;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error loading user backup configuration: {ex.Message}");
            _dialogService.ShowMessage($"Error loading backup configuration: {ex.Message}", "Error");
            return null;
        }
    }

    public List<string> DetectIncompatibleSettings(UnifiedConfigurationFile config)
    {
        var incompatible = new List<string>();
        var isWindows11 = _windowsVersionService.IsWindows11();
        var buildNumber = _windowsVersionService.GetWindowsBuildNumber();

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
                var allSettings = _compatibleSettingsRegistry.GetBypassedSettings(feature.Key);

                foreach (var configItem in feature.Value.Items)
                {
                    var settingDef = allSettings.FirstOrDefault(s => s.Id == configItem.Id);
                    if (settingDef != null)
                    {
                        bool isIncompatible = false;

                        if (settingDef.IsWindows10Only && isWindows11)
                        {
                            isIncompatible = true;
                        }
                        else if (settingDef.IsWindows11Only && !isWindows11)
                        {
                            isIncompatible = true;
                        }
                        else if (settingDef.MinimumBuildNumber.HasValue && buildNumber < settingDef.MinimumBuildNumber.Value)
                        {
                            isIncompatible = true;
                        }
                        else if (settingDef.MaximumBuildNumber.HasValue && buildNumber > settingDef.MaximumBuildNumber.Value)
                        {
                            isIncompatible = true;
                        }
                        else if (settingDef.SupportedBuildRanges?.Count > 0)
                        {
                            bool inRange = settingDef.SupportedBuildRanges.Any(range =>
                                buildNumber >= range.MinBuild && buildNumber <= range.MaxBuild);
                            if (!inRange)
                            {
                                isIncompatible = true;
                            }
                        }

                        if (isIncompatible)
                        {
                            incompatible.Add($"{settingDef.Name} ({feature.Key})");
                        }
                    }
                }
            }
        }

        return incompatible;
    }

    public UnifiedConfigurationFile FilterConfigForCurrentSystem(UnifiedConfigurationFile config)
    {
        var isWindows11 = _windowsVersionService.IsWindows11();
        var buildNumber = _windowsVersionService.GetWindowsBuildNumber();

        var filteredOptimize = FilterFeatureGroup(config.Optimize, isWindows11, buildNumber);
        var filteredCustomize = FilterFeatureGroup(config.Customize, isWindows11, buildNumber);

        return new UnifiedConfigurationFile
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
        bool isWindows11,
        int buildNumber)
    {
        if (section?.Features == null) return section!;

        var filteredFeatures = new Dictionary<string, ConfigSection>();

        foreach (var feature in section.Features)
        {
            var allSettings = _compatibleSettingsRegistry.GetBypassedSettings(feature.Key);
            var filteredItems = new List<ConfigurationItem>();

            foreach (var item in feature.Value.Items)
            {
                var settingDef = allSettings.FirstOrDefault(s => s.Id == item.Id);
                if (settingDef != null)
                {
                    bool isCompatible = true;

                    if (settingDef.IsWindows10Only && isWindows11)
                    {
                        isCompatible = false;
                    }
                    else if (settingDef.IsWindows11Only && !isWindows11)
                    {
                        isCompatible = false;
                    }
                    else if (settingDef.MinimumBuildNumber.HasValue && buildNumber < settingDef.MinimumBuildNumber.Value)
                    {
                        isCompatible = false;
                    }
                    else if (settingDef.MaximumBuildNumber.HasValue && buildNumber > settingDef.MaximumBuildNumber.Value)
                    {
                        isCompatible = false;
                    }
                    else if (settingDef.SupportedBuildRanges?.Count > 0)
                    {
                        bool inRange = settingDef.SupportedBuildRanges.Any(range =>
                            buildNumber >= range.MinBuild && buildNumber <= range.MaxBuild);
                        if (!inRange)
                        {
                            isCompatible = false;
                        }
                    }

                    if (isCompatible)
                    {
                        filteredItems.Add(item);
                    }
                }
                else
                {
                    filteredItems.Add(item);
                }
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
