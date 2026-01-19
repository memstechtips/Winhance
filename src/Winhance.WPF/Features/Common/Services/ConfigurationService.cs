using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.WPF.Features.SoftwareApps.ViewModels;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private const string FileExtension = ".winhance";
        private const string FileFilter = "Winhance Configuration Files|*" + FileExtension;

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogService _logService;
        private readonly IDialogService _dialogService;
        private readonly IGlobalSettingsRegistry _globalSettingsRegistry;
        private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
        private readonly ISystemSettingsDiscoveryService _discoveryService;
        private readonly ConfigurationApplicationBridgeService _bridgeService;
        private readonly IWindowsUIManagementService _windowsUIManagementService;
        private readonly IWindowsVersionService _windowsVersionService;
        private readonly IWindowsThemeQueryService _windowsThemeQueryService;
        private readonly ILocalizationService _localizationService;

        public ConfigurationService(
            IServiceProvider serviceProvider,
            ILogService logService,
            IDialogService dialogService,
            IGlobalSettingsRegistry globalSettingsRegistry,
            ICompatibleSettingsRegistry compatibleSettingsRegistry,
            ISystemSettingsDiscoveryService discoveryService,
            ConfigurationApplicationBridgeService bridgeService,
            IWindowsUIManagementService windowsUIManagementService,
            IWindowsVersionService windowsVersionService,
            IWindowsThemeQueryService windowsThemeQueryService,
            ILocalizationService localizationService)
        {
            _serviceProvider = serviceProvider;
            _logService = logService;
            _dialogService = dialogService;
            _globalSettingsRegistry = globalSettingsRegistry;
            _compatibleSettingsRegistry = compatibleSettingsRegistry;
            _discoveryService = discoveryService;
            _bridgeService = bridgeService;
            _windowsUIManagementService = windowsUIManagementService;
            _windowsVersionService = windowsVersionService;
            _windowsThemeQueryService = windowsThemeQueryService;
            _localizationService = localizationService;
        }

        public async Task ExportConfigurationAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Starting configuration export");

                var config = await CreateConfigurationFromSystemAsync();

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = FileFilter,
                    DefaultExt = FileExtension,
                    Title = "Save Configuration",
                    FileName = $"Winhance_Config_{DateTime.Now:yyyyMMdd}{FileExtension}"
                };

                if (saveFileDialog.ShowDialog() != true)
                {
                    _logService.Log(LogLevel.Info, "Export canceled by user");
                    return;
                }

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                await File.WriteAllTextAsync(saveFileDialog.FileName, json);

                _logService.Log(LogLevel.Info, $"Configuration exported to {saveFileDialog.FileName}");

                var dialog = CustomDialog.CreateInformationDialog(
                    _localizationService.GetString("Config_Export_Success_Title"),
                    _localizationService.GetString("Config_Export_Success_Message", saveFileDialog.FileName),
                    "",
                    DialogType.Success,
                    "CheckCircle",
                    _localizationService.GetString("Button_OK")
                );
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error exporting configuration: {ex.Message}");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("Config_Export_Error_Message", ex.Message),
                    _localizationService.GetString("Config_Export_Error_Title")
                );
            }
        }

        public async Task ImportConfigurationAsync()
        {
            _logService.Log(LogLevel.Info, "Starting configuration import");

            var selectedOption = await _dialogService.ShowConfigImportOptionsDialogAsync();
            if (selectedOption == null)
            {
                _logService.Log(LogLevel.Info, "Import canceled by user");
                return;
            }

            UnifiedConfigurationFile config;

            if (selectedOption == ImportOption.ImportOwn)
            {
                config = await LoadAndValidateConfigurationFromFileAsync();
                if (config == null)
                {
                    _logService.Log(LogLevel.Info, "Import canceled");
                    return;
                }
            }
            else
            {
                config = await LoadRecommendedConfigurationAsync();
                if (config == null) return;
            }

            await ExecuteConfigImportAsync(config);
        }

        public async Task ImportRecommendedConfigurationAsync()
        {
            _logService.Log(LogLevel.Info, "Starting recommended configuration import");

            var config = await LoadRecommendedConfigurationAsync();
            if (config == null) return;

            await ExecuteConfigImportAsync(config);
        }

        private async Task ExecuteConfigImportAsync(UnifiedConfigurationFile config)
        {
            try
            {
                var incompatibleSettings = DetectIncompatibleSettings(config);

                if (incompatibleSettings.Any())
                {
                    config = FilterConfigForCurrentSystem(config);
                    _logService.Log(LogLevel.Info, $"Silently filtered {incompatibleSettings.Count} incompatible settings from config");
                }

                var selectionResult = await ShowSectionSelectionDialogAsync(config);
                if (selectionResult == null)
                {
                    _logService.Log(LogLevel.Info, "Import canceled by user at section selection");
                    return;
                }

                var (sectionSelection, importOptions) = selectionResult.Value;

                if (!sectionSelection.Any(kvp => kvp.Value))
                {
                    _dialogService.ShowMessage(
                        _localizationService.GetString("Config_Import_Error_NoSelection"), 
                        _localizationService.GetString("Config_Import_Error_NoSelection_Title"));
                    return;
                }

                var selectedSections = sectionSelection.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();

                if (selectedSections.Contains("WindowsApps"))
                {
                    await SelectWindowsAppsFromConfigAsync(config.WindowsApps);

                    if (importOptions.ProcessWindowsAppsRemoval)
                    {
                        var shouldContinue = await ConfirmWindowsAppsRemovalAsync();
                        if (!shouldContinue)
                        {
                            await ClearWindowsAppsSelectionAsync();
                            selectedSections.Remove("WindowsApps");
                            _logService.Log(LogLevel.Info, "User cancelled Windows Apps removal");
                        }
                    }
                    else
                    {
                        _logService.Log(LogLevel.Info, "Windows Apps selected for manual processing");
                    }
                }

                if (selectedSections.Contains("ExternalApps"))
                {
                    await SelectExternalAppsFromConfigAsync(config.ExternalApps);

                    if (!importOptions.ProcessExternalAppsInstallation)
                    {
                        _logService.Log(LogLevel.Info, "External Apps selected for manual processing");
                    }
                }

                _windowsUIManagementService.IsConfigImportMode = true;

                ConfigImportOverlayWindow overlayWindow = null;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    overlayWindow = new ConfigImportOverlayWindow(_localizationService.GetString("Config_Import_Status_Applying"));
                    overlayWindow.Show();
                });

                try
                {
                    await ApplyConfigurationWithOptionsAsync(config, selectedSections, importOptions, overlayWindow);
                }
                finally
                {
                    _windowsUIManagementService.IsConfigImportMode = false;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        overlayWindow?.Close();
                    });
                }

                await ShowImportSuccessMessage(selectedSections);

                if (selectedSections.Contains("ExternalApps") && importOptions.ProcessExternalAppsInstallation)
                {
                    _ = Task.Run(async () => await ProcessExternalAppsInstallationAsync(config.ExternalApps));
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error importing configuration: {ex.Message}");
                _dialogService.ShowMessage($"Error importing configuration: {ex.Message}", "Error");
            }
        }

        private async Task<UnifiedConfigurationFile> CreateConfigurationFromSystemAsync()
        {
            var config = new UnifiedConfigurationFile
            {
                Version = "2.0",
                CreatedAt = DateTime.UtcNow
            };

            await PopulateFeatureBasedSections(config);
            await PopulateAppsSections(config);

            return config;
        }

        private async Task PopulateFeatureBasedSections(UnifiedConfigurationFile config)
        {
            var allSettingsByFeature = _compatibleSettingsRegistry.GetAllFilteredSettings();

            int totalOptimizeSettings = 0;
            int totalCustomizeSettings = 0;

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

                        if (setting.Id == "power-plan-selection")
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
                                        ["ACValue"] = acValue,
                                        ["DCValue"] = dcValue
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
                    config.Optimize.Features[featureId] = section;
                    config.Optimize.IsIncluded = true;
                    totalOptimizeSettings += items.Count;
                    _logService.Log(LogLevel.Info, $"Exported {items.Count} settings from {featureId} (Optimize)");
                }
                else
                {
                    config.Customize.Features[featureId] = section;
                    config.Customize.IsIncluded = true;
                    totalCustomizeSettings += items.Count;
                    _logService.Log(LogLevel.Info, $"Exported {items.Count} settings from {featureId} (Customize)");
                }
            }

            _logService.Log(LogLevel.Info, $"Total exported: {totalOptimizeSettings} Optimize settings, {totalCustomizeSettings} Customize settings");
        }


        private async Task PopulateAppsSections(UnifiedConfigurationFile config)
        {
            var windowsAppsVM = _serviceProvider.GetService<WindowsAppsViewModel>();
            if (windowsAppsVM != null)
            {
                if (!windowsAppsVM.IsInitialized)
                    await windowsAppsVM.LoadItemsAsync();

                config.WindowsApps.IsIncluded = true;
                config.WindowsApps.Items = windowsAppsVM.Items
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

                _logService.Log(LogLevel.Info, $"Exported {config.WindowsApps.Items.Count} checked Windows Apps");
            }

            var externalAppsVM = _serviceProvider.GetService<ExternalAppsViewModel>();
            if (externalAppsVM != null)
            {
                if (!externalAppsVM.IsInitialized)
                    await externalAppsVM.LoadItemsAsync();

                config.ExternalApps.IsIncluded = true;
                config.ExternalApps.Items = externalAppsVM.Items
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

                        if (!string.IsNullOrEmpty(item.Definition.WinGetPackageId))
                            configItem.WinGetPackageId = item.Definition.WinGetPackageId;

                        return configItem;
                    }).ToList();

                _logService.Log(LogLevel.Info, $"Exported {config.ExternalApps.Items.Count} checked External Apps");
            }
        }

        private (int? selectedIndex, Dictionary<string, object> customStateValues, string powerPlanGuid, string powerPlanName)
            GetSelectionStateFromState(SettingDefinition setting, SettingStateResult state)
        {
            if (setting.InputType != InputType.Selection)
                return (null, null, null, null);

            if (state?.CurrentValue is not int index)
                return (0, null, null, null);

            if (setting.Id == "power-plan-selection" && state.RawValues != null)
            {
                var guid = state.RawValues.TryGetValue("ActivePowerPlanGuid", out var g) ? g?.ToString() : null;
                var name = state.RawValues.TryGetValue("ActivePowerPlan", out var n) ? n?.ToString() : null;

                _logService.Log(LogLevel.Info, $"[ConfigurationService] Exporting power plan: {name} ({guid})");
                return (index, null, guid, name);
            }

            if (index == ComboBoxResolver.CUSTOM_STATE_INDEX)
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

        private int ResolveValueToIndex(SettingDefinition setting, object? value)
        {
            if (value == null) return 0;

            var intValue = Convert.ToInt32(value);

            if (!setting.CustomProperties.TryGetValue(CustomPropertyKeys.ValueMappings, out var mappingsObj))
                return 0;

            var mappings = (Dictionary<int, Dictionary<string, object?>>)mappingsObj;

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

        private async Task<UnifiedConfigurationFile> LoadAndValidateConfigurationFromFileAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = FileFilter,
                DefaultExt = FileExtension,
                Title = "Open Configuration"
            };

            if (openFileDialog.ShowDialog() != true)
                return null;

            var json = await File.ReadAllTextAsync(openFileDialog.FileName);
            var loadedConfig = JsonConvert.DeserializeObject<UnifiedConfigurationFile>(json);

            if (loadedConfig == null)
            {
                _dialogService.ShowMessage("Failed to load configuration file.", "Error");
                return null;
            }

            if (loadedConfig.Version != "2.0")
            {
                var versionText = loadedConfig.Version ?? "unknown";
                CustomDialog.ShowInformation(
                    _localizationService.GetString("Config_Unsupported_Title"),
                    _localizationService.GetString("Config_Unsupported_Header"),
                    _localizationService.GetString("Config_Unsupported_Message", versionText),
                    ""
                );
                _logService.Log(LogLevel.Warning, $"Rejected incompatible config version: {loadedConfig.Version}");
                return null;
            }

            _logService.Log(LogLevel.Info, $"Loaded config v{loadedConfig.Version}");
            return loadedConfig;
        }


        private async Task<(Dictionary<string, bool> sections, ImportOptions options)?> ShowSectionSelectionDialogAsync(
            UnifiedConfigurationFile config)
        {
            var sectionInfo = new Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)>
            {
                { "WindowsApps", (true, config.WindowsApps.Items.Count > 0, config.WindowsApps.Items.Count) },
                { "ExternalApps", (true, config.ExternalApps.Items.Count > 0, config.ExternalApps.Items.Count) }
            };

            var optimizeCount = config.Optimize.Features.Values.Sum(f => f.Items.Count);
            var customizeCount = config.Customize.Features.Values.Sum(f => f.Items.Count);

            sectionInfo.Add("Optimize", (true, optimizeCount > 0, optimizeCount));
            sectionInfo.Add("Customize", (true, customizeCount > 0, customizeCount));

            return await _dialogService.ShowUnifiedConfigurationImportDialogAsync(
                "",
                _localizationService.GetString("Dialog_Unified_Description_Import"),
                sectionInfo);
        }

        private AppItemViewModel FindMatchingWindowsApp(IEnumerable<AppItemViewModel> vmItems, ConfigurationItem configItem)
        {
            return vmItems.FirstOrDefault(i =>
                (!string.IsNullOrEmpty(configItem.AppxPackageName) && i.Definition?.AppxPackageName == configItem.AppxPackageName) ||
                (!string.IsNullOrEmpty(configItem.CapabilityName) && i.Definition?.CapabilityName == configItem.CapabilityName) ||
                (!string.IsNullOrEmpty(configItem.OptionalFeatureName) && i.Definition?.OptionalFeatureName == configItem.OptionalFeatureName) ||
                i.Id == configItem.Id);
        }

        private AppItemViewModel FindMatchingExternalApp(IEnumerable<AppItemViewModel> vmItems, ConfigurationItem configItem)
        {
            return vmItems.FirstOrDefault(i =>
                (!string.IsNullOrEmpty(configItem.WinGetPackageId) && i.Definition?.WinGetPackageId == configItem.WinGetPackageId) ||
                i.Id == configItem.Id);
        }

        private async Task SelectWindowsAppsFromConfigAsync(ConfigSection windowsAppsSection)
        {
            var vm = _serviceProvider.GetService<WindowsAppsViewModel>();
            if (vm == null) return;

            if (!vm.IsInitialized)
                await vm.LoadItemsAsync();

            foreach (var vmItem in vm.Items)
                vmItem.IsSelected = false;

            if (windowsAppsSection?.Items != null)
            {
                foreach (var configItem in windowsAppsSection.Items)
                {
                    var vmItem = FindMatchingWindowsApp(vm.Items, configItem);
                    if (vmItem != null)
                        vmItem.IsSelected = configItem.IsSelected ?? false;
                }
            }

            var selectedCount = vm.Items.Count(i => i.IsSelected);
            _logService.Log(LogLevel.Info, $"Selected {selectedCount} Windows Apps from config");
        }

        private async Task<bool> ConfirmWindowsAppsRemovalAsync()
        {
            var vm = _serviceProvider.GetService<WindowsAppsViewModel>();
            if (vm == null) return false;

            var selectedCount = vm.Items.Count(i => i.IsSelected);
            if (selectedCount == 0) return true;

            return await vm.ShowRemovalSummaryAndConfirm();
        }

        private async Task ClearWindowsAppsSelectionAsync()
        {
            var vm = _serviceProvider.GetService<WindowsAppsViewModel>();
            if (vm == null) return;

            foreach (var vmItem in vm.Items)
                vmItem.IsSelected = false;
        }

        private async Task SelectExternalAppsFromConfigAsync(ConfigSection externalAppsSection)
        {
            var vm = _serviceProvider.GetService<ExternalAppsViewModel>();
            if (vm == null) return;

            if (!vm.IsInitialized)
                await vm.LoadItemsAsync();

            foreach (var vmItem in vm.Items)
                vmItem.IsSelected = false;

            if (externalAppsSection?.Items != null)
            {
                foreach (var configItem in externalAppsSection.Items)
                {
                    var vmItem = FindMatchingExternalApp(vm.Items, configItem);
                    if (vmItem != null)
                        vmItem.IsSelected = true;
                }
            }

            var selectedCount = vm.Items.Count(i => i.IsSelected);
            _logService.Log(LogLevel.Info, $"Selected {selectedCount} External Apps from config");
        }

        private async Task ProcessExternalAppsInstallationAsync(ConfigSection externalAppsSection)
        {
            var vm = _serviceProvider.GetService<ExternalAppsViewModel>();
            if (vm == null) return;

            if (!vm.IsInitialized)
                await vm.LoadItemsAsync();

            foreach (var vmItem in vm.Items)
                vmItem.IsSelected = false;

            if (externalAppsSection?.Items != null)
            {
                foreach (var configItem in externalAppsSection.Items)
                {
                    var vmItem = FindMatchingExternalApp(vm.Items, configItem);
                    if (vmItem != null)
                        vmItem.IsSelected = true;
                }
            }

            var selectedCount = vm.Items.Count(i => i.IsSelected);
            if (selectedCount > 0)
            {
                _logService.Log(LogLevel.Info, "Starting external apps installation in background");
                await vm.InstallApps(skipConfirmation: true);
            }
        }

        private async Task ApplyConfigurationWithOptionsAsync(
            UnifiedConfigurationFile config,
            List<string> selectedSections,
            ImportOptions options,
            ConfigImportOverlayWindow overlayWindow = null)
        {
            _logService.Log(LogLevel.Info, $"Applying configuration to: {string.Join(", ", selectedSections)}");

            bool hasCustomizations = selectedSections.Any(s => s == "Customize" || s.StartsWith("Customize_"));

            if (selectedSections.Contains("WindowsApps") && options.ProcessWindowsAppsRemoval)
            {
                overlayWindow?.UpdateProgress(_localizationService.GetString("Config_Import_Progress_WindowsApps"));
                var vm = _serviceProvider.GetService<WindowsAppsViewModel>();
                if (vm != null)
                {
                    _logService.Log(LogLevel.Info, "Processing Windows Apps removal");
                    await vm.RemoveApps(skipConfirmation: true);
                }
            }

            if (selectedSections.Any(s => s == "Optimize" || s.StartsWith("Optimize_")))
            {
                overlayWindow?.UpdateProgress(_localizationService.GetString("Config_Import_Progress_Optimize"));
                var success = await ApplyFeatureGroupWithOptionsAsync(config.Optimize, "Optimize", options, selectedSections, overlayWindow);
                _logService.Log(LogLevel.Info, $"  Optimize: {(success ? "Success" : "Failed")}");
            }

            if (hasCustomizations)
            {
                overlayWindow?.UpdateProgress(_localizationService.GetString("Config_Import_Progress_Customize"));
                var success = await ApplyFeatureGroupWithOptionsAsync(config.Customize, "Customize", options, selectedSections, overlayWindow);
                _logService.Log(LogLevel.Info, $"  Customize: {(success ? "Success" : "Failed")}");
            }

            // Always restart explorer at the end to apply all changes
            overlayWindow?.UpdateProgress(_localizationService.GetString("Config_Import_Progress_Refresh"));
            await Task.Run(async () =>
            {
                if (_windowsUIManagementService.IsProcessRunning("explorer"))
                {
                    _logService.Log(LogLevel.Info, "Killing explorer to apply changes");
                    _windowsUIManagementService.KillProcess("explorer");
                    await Task.Delay(1000);
                }
                else
                {
                    _logService.Log(LogLevel.Info, "Explorer not running, will start it");
                }

                await RestartExplorerSilentlyAsync();
            });
        }

        private async Task<bool> ApplyFeatureGroupWithOptionsAsync(
            FeatureGroupSection featureGroup,
            string groupName,
            ImportOptions options,
            List<string> selectedSections,
            ConfigImportOverlayWindow overlayWindow = null)
        {
            bool hasActionOnlySubsections = selectedSections.Any(s =>
                s.StartsWith($"{groupName}_") &&
                options.ActionOnlySubsections.Contains(s));

            if ((featureGroup?.Features == null || !featureGroup.Features.Any()) && !hasActionOnlySubsections)
            {
                _logService.Log(LogLevel.Warning, $"{groupName} has no features to apply");
                return false;
            }

            bool overallSuccess = true;
            var processedFeatureKeys = new HashSet<string>();

            if (featureGroup?.Features != null)
            {
                foreach (var feature in featureGroup.Features)
                {
                    var featureName = feature.Key;
                    var section = feature.Value;

                    var featureKey = $"{groupName}_{featureName}";
                    processedFeatureKeys.Add(featureKey);

                    if (!selectedSections.Contains(featureKey))
                    {
                        _logService.Log(LogLevel.Info, $"Skipping {featureName} - not selected by user");
                        continue;
                    }

                    bool isActionOnly = options.ActionOnlySubsections.Contains(featureKey);

                    Func<string, object?, SettingDefinition, Task<(bool confirmed, bool checkboxResult)>> confirmationHandler =
                        async (settingId, value, setting) =>
                        {
                            if (settingId == "power-plan-selection" || settingId == "updates-policy-mode")
                            {
                                return (true, true);
                            }

                            if (settingId == "theme-mode-windows")
                            {
                                return (true, options?.ApplyThemeWallpaper ?? false);
                            }
                            else if (settingId == "taskbar-clean")
                            {
                                return (true, options?.ApplyCleanTaskbar ?? false);
                            }
                            else if (settingId == "start-menu-clean-10" || settingId == "start-menu-clean-11")
                            {
                                return (true, options?.ApplyCleanStartMenu ?? false);
                            }

                            return (true, true);
                        };

                    // Build action commands if any are requested
                    var itemsToExecute = new List<ConfigurationItem>();

                    if (options?.ApplyCleanTaskbar == true && featureName == FeatureIds.Taskbar)
                    {
                        itemsToExecute.Add(new ConfigurationItem
                        {
                            Id = "taskbar-clean",
                            Name = "Clean Taskbar",
                            IsSelected = true,
                            InputType = InputType.Toggle
                        });
                    }

                    if (options?.ApplyCleanStartMenu == true && featureName == FeatureIds.StartMenu)
                    {
                        var settingId = _windowsVersionService.IsWindows11() ? "start-menu-clean-11" : "start-menu-clean-10";
                        itemsToExecute.Add(new ConfigurationItem
                        {
                            Id = settingId,
                            Name = "Clean Start Menu",
                            IsSelected = true,
                            InputType = InputType.Toggle
                        });
                    }

                    // Execute action commands if any
                    if (itemsToExecute.Any())
                    {
                        var actionSection = new ConfigSection
                        {
                            IsIncluded = true,
                            Items = itemsToExecute
                        };

                        overlayWindow?.UpdateProgress($"Applying {_localizationService.GetString(FeatureRegistry.GetFeatureById(featureName)?.LocalizationKey ?? featureName)} action commands...");
                        _logService.Log(LogLevel.Info, $"Executing {itemsToExecute.Count} action command(s) for {featureName}");

                        var success = await _bridgeService.ApplyConfigurationSectionAsync(
                            actionSection,
                            $"{groupName}.{featureName}",
                            confirmationHandler);

                        if (!success)
                        {
                            overallSuccess = false;
                            _logService.Log(LogLevel.Warning, $"Failed to apply action commands for {featureName}");
                        }
                    }

                    // Apply regular settings if not action-only
                    if (!isActionOnly)
                    {
                        overlayWindow?.UpdateProgress($"Applying {_localizationService.GetString(FeatureRegistry.GetFeatureById(featureName)?.LocalizationKey ?? featureName)} Settings...");
                        _logService.Log(LogLevel.Info, $"Applying {section.Items.Count} settings from {groupName} > {featureName}");

                        var success = await _bridgeService.ApplyConfigurationSectionAsync(
                            section,
                            $"{groupName}.{featureName}",
                            confirmationHandler);

                        if (!success)
                        {
                            overallSuccess = false;
                            _logService.Log(LogLevel.Warning, $"Failed to apply some settings from {groupName} > {featureName}");
                        }
                    }
                }
            }

            var unprocessedActionOnly = selectedSections
                .Where(s => s.StartsWith($"{groupName}_") &&
                           options.ActionOnlySubsections.Contains(s) &&
                           !processedFeatureKeys.Contains(s))
                .ToList();

            foreach (var featureKey in unprocessedActionOnly)
            {
                var featureName = featureKey.Substring(groupName.Length + 1);

                Func<string, object?, SettingDefinition, Task<(bool confirmed, bool checkboxResult)>> confirmationHandler =
                    async (settingId, value, setting) =>
                    {
                        if (settingId == "power-plan-selection" || settingId == "updates-policy-mode")
                        {
                            return (true, true);
                        }

                        if (settingId == "theme-mode-windows")
                        {
                            return (true, options?.ApplyThemeWallpaper ?? false);
                        }
                        else if (settingId == "taskbar-clean")
                        {
                            return (true, options?.ApplyCleanTaskbar ?? false);
                        }
                        else if (settingId == "start-menu-clean-10" || settingId == "start-menu-clean-11")
                        {
                            return (true, options?.ApplyCleanStartMenu ?? false);
                        }

                        return (true, true);
                    };

                var itemsToExecute = new List<ConfigurationItem>();

                if (options?.ApplyCleanTaskbar == true && featureName == FeatureIds.Taskbar)
                {
                    itemsToExecute.Add(new ConfigurationItem
                    {
                        Id = "taskbar-clean",
                        Name = "Clean Taskbar",
                        IsSelected = true,
                        InputType = InputType.Toggle
                    });
                }

                if (options?.ApplyCleanStartMenu == true && featureName == FeatureIds.StartMenu)
                {
                    var settingId = _windowsVersionService.IsWindows11() ? "start-menu-clean-11" : "start-menu-clean-10";
                    itemsToExecute.Add(new ConfigurationItem
                    {
                        Id = settingId,
                        Name = "Clean Start Menu",
                        IsSelected = true,
                        InputType = InputType.Toggle
                    });
                }

                if (options?.ApplyThemeWallpaper == true && featureName == FeatureIds.WindowsTheme)
                {
                    itemsToExecute.Add(new ConfigurationItem
                    {
                        Id = "theme-mode-windows",
                        Name = "Windows Theme",
                        IsSelected = true,
                        InputType = InputType.Selection,
                        SelectedIndex = 0
                    });
                }

                if (itemsToExecute.Any())
                {
                    var actionSection = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = itemsToExecute
                    };

                    overlayWindow?.UpdateProgress($"Applying {_localizationService.GetString(FeatureRegistry.GetFeatureById(featureName)?.LocalizationKey ?? featureName)} action commands...");
                    _logService.Log(LogLevel.Info, $"Executing {itemsToExecute.Count} action command(s) for {featureName} (not in config file)");

                    var success = await _bridgeService.ApplyConfigurationSectionAsync(
                        actionSection,
                        $"{groupName}.{featureName}",
                        confirmationHandler);

                    if (!success)
                    {
                        overallSuccess = false;
                        _logService.Log(LogLevel.Warning, $"Failed to apply action commands for {featureName}");
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Info, $"No action commands to execute for {featureName}");
                }
            }

            return overallSuccess;
        }


        private async Task ShowImportSuccessMessage(List<string> selectedSections)
        {
            var dialog = CustomDialog.CreateInformationDialog(
                    _localizationService.GetString("Config_Import_Success_Title"),
                    _localizationService.GetString("Config_Import_Success_Message"),
                    "",
                    DialogType.Success,
                    "CheckCircle",
                    _localizationService.GetString("Button_OK")
                );
            dialog.ShowDialog();
        }



        private async Task<UnifiedConfigurationFile> LoadRecommendedConfigurationAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading embedded recommended configuration");

                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "Winhance.WPF.Resources.Configs.Winhance_Recommended_Config.winhance";

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
                var config = JsonConvert.DeserializeObject<UnifiedConfigurationFile>(json);

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

        private async Task RestartExplorerSilentlyAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Waiting for explorer restart");

                int retryCount = 0;
                const int maxRetries = 20;

                while (retryCount < maxRetries)
                {
                    bool isRunning = await Task.Run(() =>
                        _windowsUIManagementService.IsProcessRunning("explorer"));

                    if (isRunning)
                    {
                        _logService.Log(LogLevel.Info, "Explorer.exe has auto-restarted");
                        await Task.Delay(1000);
                        return;
                    }

                    retryCount++;
                    await Task.Delay(250);
                }

                _logService.Log(LogLevel.Warning, "Explorer did not auto-restart, starting manually");

                await Task.Run(() =>
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    };
                    System.Diagnostics.Process.Start(startInfo);
                });

                // Verify explorer actually started
                retryCount = 0;
                const int verifyMaxRetries = 10;

                while (retryCount < verifyMaxRetries)
                {
                    await Task.Delay(500);

                    bool isRunning = await Task.Run(() =>
                        _windowsUIManagementService.IsProcessRunning("explorer"));

                    if (isRunning)
                    {
                        _logService.Log(LogLevel.Info, "Explorer.exe started successfully");
                        await Task.Delay(1000);
                        return;
                    }

                    retryCount++;
                }

                _logService.Log(LogLevel.Error, "Failed to verify explorer restart");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error restarting explorer: {ex.Message}");
            }
        }

        private List<string> DetectIncompatibleSettings(UnifiedConfigurationFile config)
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
                    var allSettings = _compatibleSettingsRegistry.GetFilteredSettings(feature.Key);

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

        private UnifiedConfigurationFile FilterConfigForCurrentSystem(UnifiedConfigurationFile config)
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
            if (section?.Features == null) return section;

            var filteredSection = new FeatureGroupSection
            {
                IsIncluded = section.IsIncluded,
                Features = new Dictionary<string, ConfigSection>()
            };

            foreach (var feature in section.Features)
            {
                var allSettings = _compatibleSettingsRegistry.GetFilteredSettings(feature.Key);
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

                filteredSection.Features[feature.Key] = new ConfigSection
                {
                    IsIncluded = feature.Value.IsIncluded,
                    Items = filteredItems
                };
            }

            return filteredSection;
        }
    }
}
