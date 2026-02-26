using System.Text.Json;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Constants;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Utilities;

namespace Winhance.UI.Features.Common.Services;

public class ConfigExportService : IConfigExportService
{
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly IGlobalSettingsPreloader _settingsPreloader;
    private readonly ISystemSettingsDiscoveryService _discoveryService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IWindowsAppsItemsProvider _windowsAppsVM;
    private readonly IExternalAppsItemsProvider _externalAppsVM;
    private readonly IFileSystemService _fileSystemService;
    private readonly IMainWindowProvider _mainWindowProvider;

    public ConfigExportService(
        ILogService logService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        IGlobalSettingsPreloader settingsPreloader,
        ISystemSettingsDiscoveryService discoveryService,
        IInteractiveUserService interactiveUserService,
        IWindowsAppsItemsProvider windowsAppsVM,
        IExternalAppsItemsProvider externalAppsVM,
        IFileSystemService fileSystemService,
        IMainWindowProvider mainWindowProvider)
    {
        _logService = logService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _settingsPreloader = settingsPreloader;
        _discoveryService = discoveryService;
        _interactiveUserService = interactiveUserService;
        _windowsAppsVM = windowsAppsVM;
        _externalAppsVM = externalAppsVM;
        _fileSystemService = fileSystemService;
        _mainWindowProvider = mainWindowProvider;
    }

    private Task EnsureRegistryInitializedAsync()
        => ConfigRegistryInitializer.EnsureInitializedAsync(_compatibleSettingsRegistry, _settingsPreloader, _logService);

    private Microsoft.UI.Xaml.Window? GetMainWindow() => _mainWindowProvider.MainWindow;

    public async Task ExportConfigurationAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, "Starting configuration export");

            await EnsureRegistryInitializedAsync();

            var config = await CreateConfigurationFromSystemAsync();

            var window = GetMainWindow();
            if (window == null)
            {
                _logService.Log(LogLevel.Error, "Cannot show file dialog - no main window");
                await _dialogService.ShowErrorAsync("Cannot show file dialog.", "Error");
                return;
            }

            var defaultFileName = $"Winhance_Config_{DateTime.Now:yyyyMMdd}{ConfigFileConstants.FileExtension}";
            var filePath = Win32FileDialogHelper.ShowSaveFilePicker(
                window,
                "Save Configuration",
                ConfigFileConstants.FileFilter,
                ConfigFileConstants.FilePattern,
                defaultFileName,
                "winhance");

            if (string.IsNullOrEmpty(filePath))
            {
                _logService.Log(LogLevel.Info, "Export canceled by user");
                return;
            }

            var json = JsonSerializer.Serialize(config, ConfigFileConstants.JsonOptions);
            await _fileSystemService.WriteAllTextAsync(filePath, json);

            _logService.Log(LogLevel.Info, $"Configuration exported to {filePath}");

            await _dialogService.ShowInformationAsync(
                _localizationService.GetString("Config_Export_Success_Message", filePath)
                    ?? $"Configuration exported to {filePath}",
                _localizationService.GetString("Config_Export_Success_Title") ?? "Export Successful");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error exporting configuration: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                _localizationService.GetString("Config_Export_Error_Message", ex.Message)
                    ?? $"Error exporting configuration: {ex.Message}",
                _localizationService.GetString("Config_Export_Error_Title") ?? "Export Error");
        }
    }

    public async Task CreateUserBackupConfigAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, "Creating user backup configuration from current system state");

            await EnsureRegistryInitializedAsync();

            var config = await CreateConfigurationFromSystemAsync(isBackup: true);

            var configDir = _fileSystemService.CombinePath(
                _interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "Backup");

            _fileSystemService.CreateDirectory(configDir);

            var fileName = $"UserBackup_{DateTime.Now:yyyyMMdd_HHmmss}{ConfigFileConstants.FileExtension}";
            var filePath = _fileSystemService.CombinePath(configDir, fileName);

            var json = JsonSerializer.Serialize(config, ConfigFileConstants.JsonOptions);
            await _fileSystemService.WriteAllTextAsync(filePath, json);

            _logService.Log(LogLevel.Info, $"User backup configuration saved to {filePath}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error creating user backup configuration: {ex.Message}");
        }
    }

    public async Task<UnifiedConfigurationFile> CreateConfigurationFromSystemAsync(bool isBackup = false)
    {
        var config = new UnifiedConfigurationFile
        {
            Version = "2.0",
            CreatedAt = DateTime.UtcNow
        };

        await PopulateFeatureBasedSections(config);
        await PopulateAppsSections(config, isBackup);

        return config;
    }

    private async Task PopulateFeatureBasedSections(UnifiedConfigurationFile config)
    {
        var allSettingsByFeature = _compatibleSettingsRegistry.GetAllFilteredSettings();

        int totalOptimizeSettings = 0;
        int totalCustomizeSettings = 0;
        var optimizeFeatures = new Dictionary<string, ConfigSection>();
        var customizeFeatures = new Dictionary<string, ConfigSection>();

        foreach (var kvp in allSettingsByFeature)
        {
            var featureId = kvp.Key;
            var settings = kvp.Value.ToList();

            if (!settings.Any())
                continue;

            var isOptimize = FeatureDefinitions.OptimizeFeatures.Contains(featureId);
            var isCustomize = FeatureDefinitions.CustomizeFeatures.Contains(featureId);

            if (!isOptimize && !isCustomize)
            {
                _logService.Log(LogLevel.Warning, $"Feature {featureId} is neither Optimize nor Customize, skipping");
                continue;
            }

            var states = await _discoveryService.GetSettingStatesAsync(settings);

            var items = settings.Select(setting =>
            {
                var state = states.GetValueOrDefault(setting.Id);

                var item = new ConfigurationItem
                {
                    Id = setting.Id,
                    Name = setting.Name,
                    InputType = setting.InputType
                };

                if (setting.InputType == InputType.Toggle)
                {
                    item.IsSelected = state?.IsEnabled ?? false;
                }
                else if (setting.InputType == InputType.Selection)
                {
                    var (selectedIndex, customStateValues, powerPlanGuid, powerPlanName) = GetSelectionStateFromState(setting, state);

                    if (setting.Id == SettingIds.PowerPlanSelection)
                    {
                        item.PowerPlanGuid = powerPlanGuid;
                        item.PowerPlanName = powerPlanName;
                    }
                    else
                    {
                        bool hasAcDcPowerSettings = false;

                        if (setting.PowerCfgSettings?.Any() == true &&
                            setting.PowerCfgSettings[0].PowerModeSupport == PowerModeSupport.Separate &&
                            state?.RawValues != null)
                        {
                            var acValue = state.RawValues.TryGetValue("ACValue", out var acVal) ? acVal : null;
                            var dcValue = state.RawValues.TryGetValue("DCValue", out var dcVal) ? dcVal : null;

                            if (acValue != null || dcValue != null)
                            {
                                var acIndex = ResolveValueToIndex(setting, acValue);
                                var dcIndex = ResolveValueToIndex(setting, dcValue);

                                item.PowerSettings = new Dictionary<string, object>
                                {
                                    ["ACIndex"] = acIndex,
                                    ["DCIndex"] = dcIndex
                                };
                                hasAcDcPowerSettings = true;
                            }
                        }

                        if (!hasAcDcPowerSettings)
                        {
                            item.SelectedIndex = selectedIndex;
                        }

                        item.CustomStateValues = customStateValues;
                    }
                }
                else if (setting.InputType == InputType.NumericRange)
                {
                    if (state?.CurrentValue != null)
                    {
                        if (setting.PowerCfgSettings?.Any() == true &&
                            setting.PowerCfgSettings[0].PowerModeSupport == PowerModeSupport.Separate &&
                            state.RawValues != null)
                        {
                            var acValue = state.RawValues.TryGetValue("ACValue", out var acVal) ? acVal : null;
                            var dcValue = state.RawValues.TryGetValue("DCValue", out var dcVal) ? dcVal : null;

                            if (acValue != null || dcValue != null)
                            {
                                item.PowerSettings = new Dictionary<string, object>
                                {
                                    ["ACValue"] = acValue!,
                                    ["DCValue"] = dcValue!
                                };
                            }
                        }
                        else
                        {
                            item.PowerSettings = new Dictionary<string, object>
                            {
                                ["Value"] = state.CurrentValue
                            };
                        }
                    }
                }

                return item;
            }).ToList();

            var section = new ConfigSection
            {
                IsIncluded = true,
                Items = items
            };

            if (isOptimize)
            {
                optimizeFeatures[featureId] = section;
                config.Optimize.IsIncluded = true;
                totalOptimizeSettings += items.Count;
                _logService.Log(LogLevel.Info, $"Exported {items.Count} settings from {featureId} (Optimize)");
            }
            else
            {
                customizeFeatures[featureId] = section;
                config.Customize.IsIncluded = true;
                totalCustomizeSettings += items.Count;
                _logService.Log(LogLevel.Info, $"Exported {items.Count} settings from {featureId} (Customize)");
            }
        }

        config.Optimize.Features = optimizeFeatures;
        config.Customize.Features = customizeFeatures;
        _logService.Log(LogLevel.Info, $"Total exported: {totalOptimizeSettings} Optimize settings, {totalCustomizeSettings} Customize settings");
    }

    private async Task PopulateAppsSections(UnifiedConfigurationFile config, bool useInstalledStatus = false)
    {
        if (!_windowsAppsVM.IsInitialized)
            await _windowsAppsVM.LoadItemsAsync();

        config.WindowsApps.IsIncluded = true;
        config.WindowsApps.Items = _windowsAppsVM.Items
            .Where(item => useInstalledStatus ? item.IsInstalled : item.IsSelected)
            .Select(item =>
            {
                var configItem = new ConfigurationItem
                {
                    Id = item.Id,
                    Name = item.Name,
                    IsSelected = true,
                    InputType = InputType.Toggle
                };

                if (!string.IsNullOrEmpty(item.Definition.AppxPackageName))
                {
                    configItem.AppxPackageName = item.Definition.AppxPackageName;
                    if (item.Definition.SubPackages?.Length > 0)
                        configItem.SubPackages = item.Definition.SubPackages;
                }
                else if (!string.IsNullOrEmpty(item.Definition.CapabilityName))
                    configItem.CapabilityName = item.Definition.CapabilityName;
                else if (!string.IsNullOrEmpty(item.Definition.OptionalFeatureName))
                    configItem.OptionalFeatureName = item.Definition.OptionalFeatureName;

                return configItem;
            }).ToList();

        _logService.Log(LogLevel.Info, $"Exported {config.WindowsApps.Items.Count} {(useInstalledStatus ? "installed" : "checked")} Windows Apps");

        if (!useInstalledStatus)
        {
            if (!_externalAppsVM.IsInitialized)
                await _externalAppsVM.LoadItemsAsync();

            config.ExternalApps.IsIncluded = true;
            config.ExternalApps.Items = _externalAppsVM.Items
                .Where(item => item.IsSelected)
                .Select(item =>
                {
                    var configItem = new ConfigurationItem
                    {
                        Id = item.Id,
                        Name = item.Name,
                        IsSelected = true,
                        InputType = InputType.Toggle
                    };

                    if (item.Definition.WinGetPackageId != null && item.Definition.WinGetPackageId.Any())
                        configItem.WinGetPackageId = item.Definition.WinGetPackageId[0];

                    return configItem;
                }).ToList();

            _logService.Log(LogLevel.Info, $"Exported {config.ExternalApps.Items.Count} checked External Apps");
        }
    }

    private (int? selectedIndex, Dictionary<string, object>? customStateValues, string? powerPlanGuid, string? powerPlanName)
        GetSelectionStateFromState(SettingDefinition setting, SettingStateResult? state)
    {
        if (setting.InputType != InputType.Selection)
            return (null, null, null, null);

        if (state?.CurrentValue is not int index)
            return (0, null, null, null);

        if (setting.Id == SettingIds.PowerPlanSelection && state.RawValues != null)
        {
            var guid = state.RawValues.TryGetValue("ActivePowerPlanGuid", out var g) ? g?.ToString() : null;
            var name = state.RawValues.TryGetValue("ActivePowerPlan", out var n) ? n?.ToString() : null;

            _logService.Log(LogLevel.Info, $"[ConfigExportService] Exporting power plan: {name} ({guid})");
            return (index, null, guid, name);
        }

        if (index == ComboBoxConstants.CustomStateIndex)
        {
            var customValues = new Dictionary<string, object>();

            if (state.RawValues != null)
            {
                foreach (var registrySetting in setting.RegistrySettings)
                {
                    var key = registrySetting.ValueName ?? "KeyExists";
                    if (state.RawValues.TryGetValue(key, out var value) && value != null)
                    {
                        customValues[key] = value;
                    }
                }
            }

            return (null, customValues.Count > 0 ? customValues : null, null, null);
        }

        return (index, null, null, null);
    }

    private static int ResolveValueToIndex(SettingDefinition setting, object? value)
    {
        if (value == null) return 0;

        var intValue = Convert.ToInt32(value);

        if (setting.ComboBox?.ValueMappings == null)
            return 0;

        var mappings = setting.ComboBox.ValueMappings;

        foreach (var mapping in mappings)
        {
            if (mapping.Value.TryGetValue("PowerCfgValue", out var expectedValue) &&
                expectedValue != null && Convert.ToInt32(expectedValue) == intValue)
            {
                return mapping.Key;
            }
        }

        return 0;
    }
}
