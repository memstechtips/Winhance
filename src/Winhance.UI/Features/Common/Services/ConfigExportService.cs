using System.Text.Json;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public class ConfigExportService : IConfigExportService
{
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly ICatalogSettingsRegistry _catalogSettingsRegistry;
    private readonly IWindowsVersionFilterService _windowsVersionFilter;
    private readonly ICatalogSettingStateProvider _settingStateProvider;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IWindowsAppsItemsProvider _windowsAppsVM;
    private readonly IExternalAppsItemsProvider _externalAppsVM;
    private readonly IFileSystemService _fileSystemService;
    private readonly IMainWindowProvider _mainWindowProvider;
    private readonly IApplicationModeService _applicationModeService;
    private readonly IAutounattendXmlGeneratorService _autounattendGenerator;

    public ConfigExportService(
        ILogService logService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        ICatalogSettingsRegistry catalogSettingsRegistry,
        IWindowsVersionFilterService windowsVersionFilter,
        ICatalogSettingStateProvider settingStateProvider,
        IInteractiveUserService interactiveUserService,
        IWindowsAppsItemsProvider windowsAppsVM,
        IExternalAppsItemsProvider externalAppsVM,
        IFileSystemService fileSystemService,
        IMainWindowProvider mainWindowProvider,
        IApplicationModeService applicationModeService,
        IAutounattendXmlGeneratorService autounattendGenerator)
    {
        _logService = logService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _catalogSettingsRegistry = catalogSettingsRegistry;
        _windowsVersionFilter = windowsVersionFilter;
        _settingStateProvider = settingStateProvider;
        _interactiveUserService = interactiveUserService;
        _windowsAppsVM = windowsAppsVM;
        _externalAppsVM = externalAppsVM;
        _fileSystemService = fileSystemService;
        _mainWindowProvider = mainWindowProvider;
        _applicationModeService = applicationModeService;
        _autounattendGenerator = autounattendGenerator;
    }

    // Slice 7d: repointed off the shared initializer helper (which takes the OLD registry interface). The
    // catalog registry's InitializeAsync is an idempotent no-op once resolved, so no IsInitialized pre-check.
    private Task EnsureRegistryInitializedAsync()
        => _catalogSettingsRegistry.InitializeAsync();

    private Microsoft.UI.Xaml.Window? GetMainWindow() => _mainWindowProvider.MainWindow;

    public async Task ExportConfigurationAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, "Starting configuration export");

            await EnsureRegistryInitializedAsync();

            var config = await CreateConfigurationFromSystemAsync();

            if (config.WindowsApps.Items.Count == 0)
            {
                var continueAnyway = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
                {
                    Message = _localizationService.GetString("Dialog_NoAppsSelected_Config_Message"),
                    Title = _localizationService.GetString("Dialog_NoAppsSelected_Title"),
                })).Confirmed;
                if (!continueAnyway)
                    return;
            }

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

    public async Task<UnifiedConfigurationFile> CreateConfigurationFromUiStateAsync(bool isBackup = false)
    {
        // Seed from current system state (Builder reflects the machine), then overlay
        // the user's authored Builder edits so the saved file captures their intent.
        var config = await CreateConfigurationFromSystemAsync(isBackup);
        ApplyBuilderEdits(config);
        return config;
    }

    private void ApplyBuilderEdits(UnifiedConfigurationFile config)
    {
        var edits = _applicationModeService.GetBuilderEdits();
        if (edits.Count == 0)
        {
            return;
        }

        var editsById = edits.ToDictionary(e => e.SettingId, e => e);

        var sections = config.Optimize.Features.Values
            .Concat(config.Customize.Features.Values);

        foreach (var section in sections)
        {
            foreach (var item in section.Items)
            {
                if (!editsById.TryGetValue(item.Id, out var edit))
                {
                    continue;
                }

                switch (edit.InputType)
                {
                    case InputType.Toggle:
                    case InputType.CheckBox:
                    case InputType.Action:
                        item.IsSelected = edit.IsSelected;
                        break;

                    case InputType.Selection:
                        if (edit.CustomStateValues != null)
                        {
                            item.CustomStateValues = edit.CustomStateValues;
                            item.SelectedIndex = null;
                        }
                        else
                        {
                            item.SelectedIndex = edit.SelectedIndex;
                            item.CustomStateValues = null;
                        }
                        break;

                    // NumericRange / AC-DC power edits are not yet recorded; they retain
                    // their seeded value. See BuilderEdit scope note.
                }
            }
        }

        _logService.Log(LogLevel.Info,
            $"[ConfigExportService] Applied {edits.Count} Builder edit(s) onto the seeded configuration");
    }

    public async Task ExportBuilderConfigAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, "Starting Builder configuration export");

            await EnsureRegistryInitializedAsync();

            var config = await CreateConfigurationFromUiStateAsync();

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
                _logService.Log(LogLevel.Info, "Builder export canceled by user");
                return;
            }

            var json = JsonSerializer.Serialize(config, ConfigFileConstants.JsonOptions);
            await _fileSystemService.WriteAllTextAsync(filePath, json);

            _logService.Log(LogLevel.Info, $"Builder configuration exported to {filePath}");

            await _dialogService.ShowInformationAsync(
                _localizationService.GetString("Config_Export_Success_Message", filePath)
                    ?? $"Configuration saved to {filePath}",
                _localizationService.GetString("Config_Export_Success_Title") ?? "Save Successful");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error exporting Builder configuration: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                _localizationService.GetString("Config_Export_Error_Message", ex.Message)
                    ?? $"Error saving configuration: {ex.Message}",
                _localizationService.GetString("Config_Export_Error_Title") ?? "Save Error");
        }
    }

    public async Task ExportBuilderAutounattendAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, "Starting Builder autounattend.xml export");

            await EnsureRegistryInitializedAsync();

            var config = await CreateConfigurationFromUiStateAsync();

            var window = GetMainWindow();
            if (window == null)
            {
                _logService.Log(LogLevel.Error, "Cannot show file dialog - no main window");
                await _dialogService.ShowErrorAsync("Cannot show file dialog.", "Error");
                return;
            }

            var defaultFileName = "autounattend.xml";
            var filePath = Win32FileDialogHelper.ShowSaveFilePicker(
                window,
                "Save autounattend.xml",
                "Autounattend XML File",
                "*.xml",
                defaultFileName,
                "xml");

            if (string.IsNullOrEmpty(filePath))
            {
                _logService.Log(LogLevel.Info, "Builder autounattend export canceled by user");
                return;
            }

            await _autounattendGenerator.GenerateFromConfigAsync(config, filePath);

            _logService.Log(LogLevel.Info, $"Builder autounattend.xml exported to {filePath}");

            await _dialogService.ShowInformationAsync(
                _localizationService.GetString("Config_Export_Success_Message", filePath)
                    ?? $"autounattend.xml saved to {filePath}",
                _localizationService.GetString("Config_Export_Success_Title") ?? "Save Successful");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error exporting Builder autounattend.xml: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                _localizationService.GetString("Config_Export_Error_Message", ex.Message)
                    ?? $"Error saving autounattend.xml: {ex.Message}",
                _localizationService.GetString("Config_Export_Error_Title") ?? "Save Error");
        }
    }

    private async Task PopulateFeatureBasedSections(UnifiedConfigurationFile config)
    {
        // Slice 7d: the old read branched on the registry's mutable SetFilterEnabled state; the
        // show-other-Windows-versions scope is now threaded explicitly (the 7a pattern). A filter-OFF export
        // still includes other-OS settings, with ONE reviewer-bounded id-set delta: the old bypassed set was
        // deliberately NOT alias-normalized (it carried the 6 "-win10" This PC rows ALONGSIDE the 6 canonical
        // merged rows), while GetAll(true) enumerates the canonical-only catalog - so filter-OFF exports drop
        // those 6 duplicate alias rows (state was identical via normalization; import normalizes them anyway).
        var allSettingsByFeature = _catalogSettingsRegistry.GetAll(includeOtherOsVersions: !_windowsVersionFilter.IsFilterEnabled);

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

            // Slice 6, trimmed in Slice 7d: read state from the new-engine full-state provider via its catalog
            // Setting overload (Slice 4bb-2), the drop-in for old discovery + overlay. This service reads no
            // RawValues (custom-state goes through Readings), so the provider is a faithful replacement.
            var states = await _settingStateProvider.GetStatesAsync(settings);

            var items = settings.Select(setting =>
            {
                var state = states.GetValueOrDefault(setting.Id);

                var item = new ConfigurationItem
                {
                    Id = setting.Id,
                    Name = setting.Display.Name,
                    InputType = ControlToInputType(setting.Control)
                };

                // Slice E4, trimmed in Slice 7d: the loop variable IS the catalog Setting, so the export dispatch
                // reads it directly (Control / PowerCfgTarget.Mode). Selection maps to Control in {Selection,
                // PowerPlan} (power-plan is Control.PowerPlan, exported via the Selection path). The InputType
                // persistence WRITE above STAYS and is LOAD-BEARING: ConfigMigrationService gates its
                // Toggle->Selection import migrations on the persisted configItem.InputType, and it seeds the
                // view-model InputType on config import (SettingItemViewModel) - the exact ControlToInputType map
                // keeps those readers correct. (7e-3 moved script-gen dispatch to Control, so FeatureRegistryScriptSection no longer reads it.)
                bool isToggle = setting.Control == ControlKind.Toggle;
                bool isSelection = setting.Control is ControlKind.Selection or ControlKind.PowerPlan;
                bool isNumericRange = setting.Control == ControlKind.Slider;
                bool isPowerCfgSeparate = setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault()?.Mode == PowerModeSupport.Separate;

                if (isToggle)
                {
                    item.IsSelected = state?.IsEnabled ?? false;
                }
                else if (isSelection)
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

                        if (isPowerCfgSeparate && state != null)
                        {
                            object? acValue = state.AcValue;
                            object? dcValue = state.DcValue;

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
                else if (isNumericRange)
                {
                    if (state?.CurrentValue != null)
                    {
                        if (isPowerCfgSeparate)
                        {
                            int? acValue = state.AcValue;
                            int? dcValue = state.DcValue;

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

                if (item.Definition.AppxPackageName?.Length > 0)
                {
                    configItem.AppxPackageName = item.Definition.AppxPackageName;
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
        GetSelectionStateFromState(Setting setting, SettingStateResult? state)
    {
        // Slice E4, trimmed in 7d: the "is this a Selection?" guard reads the catalog Control (Selection incl. power-plan).
        bool isSelection = setting.Control is ControlKind.Selection or ControlKind.PowerPlan;
        if (!isSelection)
            return (null, null, null, null);

        if (state?.CurrentValue is not int index)
            return (0, null, null, null);

        if (setting.Id == SettingIds.PowerPlanSelection)
        {
            // D3: source the active-plan GUID from the new engine's DynamicSelection (the active scheme GUID,
            // lowercased) instead of the old discovery's RawValues["ActivePowerPlanGuid"] (which carried the
            // OS-native case). powercfg GUIDs are case-insensitive, so this is a cosmetic case change on import.
            // The display NAME now reads the new engine's typed DynamicSelectionName (the active plan's raw OS name,
            // proven == old RawValues["ActivePowerPlan"] by the now-retired PowerPlanNameEquivalenceTests) - retires the RawValues read.
            var guid = state.DynamicSelection;
            var name = state.DynamicSelectionName;

            _logService.Log(LogLevel.Info, $"[ConfigExportService] Exporting power plan: {name} ({guid})");
            return (index, null, guid, name);
        }

        if (index == ComboBoxConstants.CustomStateIndex)
        {
            var customValues = new Dictionary<string, object>();

            // D4c: read the live custom-state registry values from the new engine's Readings (keyed identically by
            // ValueName ?? "KeyExists") instead of the legacy detection RawValues. Proven value-identical for every
            // registry key by the now-retired Migration/CustomStateReadingsEquivalenceTests (423/423).
            if (state.Readings != null)
            {
                // Slice E4, trimmed in 7d: source the custom-state registry KEYS from the catalog RegTargets
                // (ValueName ?? "KeyExists"); the key SET is identical to the old def read (the converter
                // groups mirrors by ValueName), proven at migration by the now-retired ConfigExportReaderEquivalenceTests.
                var regKeys = setting.Targets.OfType<RegTarget>().Select(rt => rt.ValueName ?? "KeyExists");
                foreach (var key in regKeys)
                {
                    if (state.Readings.TryGetValue(key, out var value) && value != null)
                    {
                        customValues[key] = value;
                    }
                }
            }

            return (null, customValues.Count > 0 ? customValues : null, null, null);
        }

        return (index, null, null, null);
    }

    private static int ResolveValueToIndex(Setting setting, object? value)
    {
        if (value == null) return 0;

        var intValue = Convert.ToInt32(value);

        // Slice E4, trimmed in 7d: resolve the powercfg AC/DC value to an option index off the catalog States'
        // Set["Power"] (== the old option's ValueMappings["PowerCfgValue"], index-aligned - ConvertPowerCfg builds
        // one State per option), proven at migration by the now-retired ConfigExportReaderEquivalenceTests.
        for (int i = 0; i < setting.States.Count; i++)
        {
            if (setting.States[i].Set.TryGetValue("Power", out var sv) &&
                sv.WritePayload != null && Convert.ToInt32(sv.WritePayload) == intValue)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>Twin of AutounattendXmlGeneratorService.ControlToInputType (private per-service transitional
    /// helper): populates the PERSISTED config-file InputType field from the derived Control. The field is
    /// LOAD-BEARING - ConfigMigrationService's Toggle->Selection import gates read it and it seeds the view-model
    /// InputType on config import - so it outlives this slice until the legacy-config-import path retires. Exact
    /// for the shipped population: PowerPlan settings were InputType.Selection, no setting is CheckBox.</summary>
    private static InputType ControlToInputType(ControlKind control) => control switch
    {
        ControlKind.Selection or ControlKind.PowerPlan => InputType.Selection,
        ControlKind.Slider => InputType.NumericRange,
        ControlKind.Action => InputType.Action,
        _ => InputType.Toggle,
    };
}
