using System.Threading;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Customize.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// WinUI 3 implementation of IConfigurationService for importing/exporting Winhance configurations.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private const string FileExtension = ".winhance";
    private const string FileFilter = "Winhance Configuration Files";
    private const string FilePattern = "*.winhance";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly IGlobalSettingsRegistry _globalSettingsRegistry;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly IGlobalSettingsPreloader _settingsPreloader;
    private readonly ISystemSettingsDiscoveryService _discoveryService;
    private readonly ConfigurationApplicationBridgeService _bridgeService;
    private readonly IWindowsUIManagementService _windowsUIManagementService;
    private readonly IWindowsVersionService _windowsVersionService;
    private readonly ILocalizationService _localizationService;
    private readonly IConfigImportOverlayService _overlayService;
    private readonly IConfigReviewModeService _configReviewModeService;
    private readonly IConfigReviewDiffService _configReviewDiffService;
    private readonly ConfigMigrationService _configMigrationService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IProcessExecutor _processExecutor;
    private bool _configImportSaveRemovalScripts = true;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ConfigurationService(
        IServiceProvider serviceProvider,
        ILogService logService,
        IDialogService dialogService,
        IGlobalSettingsRegistry globalSettingsRegistry,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        IGlobalSettingsPreloader settingsPreloader,
        ISystemSettingsDiscoveryService discoveryService,
        ConfigurationApplicationBridgeService bridgeService,
        IWindowsUIManagementService windowsUIManagementService,
        IWindowsVersionService windowsVersionService,
        ILocalizationService localizationService,
        IConfigImportOverlayService overlayService,
        IConfigReviewModeService configReviewModeService,
        IConfigReviewDiffService configReviewDiffService,
        ConfigMigrationService configMigrationService,
        IInteractiveUserService interactiveUserService,
        IProcessExecutor processExecutor)
    {
        _serviceProvider = serviceProvider;
        _processExecutor = processExecutor;
        _logService = logService;
        _dialogService = dialogService;
        _globalSettingsRegistry = globalSettingsRegistry;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _settingsPreloader = settingsPreloader;
        _discoveryService = discoveryService;
        _bridgeService = bridgeService;
        _windowsUIManagementService = windowsUIManagementService;
        _windowsVersionService = windowsVersionService;
        _localizationService = localizationService;
        _overlayService = overlayService;
        _configReviewModeService = configReviewModeService;
        _configReviewDiffService = configReviewDiffService;
        _configMigrationService = configMigrationService;
        _interactiveUserService = interactiveUserService;

        // Listen for review mode exit to clear review state from all loaded settings
        _configReviewModeService.ReviewModeChanged += OnReviewModeChanged;
    }

    private void OnReviewModeChanged(object? sender, EventArgs e)
    {
        if (_configReviewModeService.IsInReviewMode)
        {
            // Review mode was entered - reapply diffs to any already-loaded singleton VMs
            ReapplyReviewDiffsToExistingSettings();
            return;
        }

        // Review mode was exited - clear review state from all loaded SettingItemViewModels
        ClearReviewStateFromAllSettings();
    }

    private void ClearReviewStateFromAllSettings()
    {
        // Clear Optimize feature ViewModels
        var optimizeVm = _serviceProvider.GetService<OptimizeViewModel>();
        if (optimizeVm != null)
        {
            ClearReviewStateFromFeature(optimizeVm.SoundViewModel);
            ClearReviewStateFromFeature(optimizeVm.UpdateViewModel);
            ClearReviewStateFromFeature(optimizeVm.NotificationViewModel);
            ClearReviewStateFromFeature(optimizeVm.PrivacyViewModel);
            ClearReviewStateFromFeature(optimizeVm.PowerViewModel);
            ClearReviewStateFromFeature(optimizeVm.GamingViewModel);
        }

        // Clear Customize feature ViewModels
        var customizeVm = _serviceProvider.GetService<CustomizeViewModel>();
        if (customizeVm != null)
        {
            ClearReviewStateFromFeature(customizeVm.ExplorerViewModel);
            ClearReviewStateFromFeature(customizeVm.StartMenuViewModel);
            ClearReviewStateFromFeature(customizeVm.TaskbarViewModel);
            ClearReviewStateFromFeature(customizeVm.WindowsThemeViewModel);
        }

        _logService.Log(LogLevel.Info, "Cleared review state from all loaded settings");
    }

    private void ClearReviewStateFromFeature(ISettingsFeatureViewModel featureVm)
    {
        foreach (var setting in featureVm.Settings)
        {
            setting.ClearReviewState();
        }
    }

    /// <summary>
    /// Reapplies review diffs to all already-loaded SettingItemViewModels.
    /// Called when entering review mode a second time in the same session,
    /// since singleton VMs may still have settings loaded from the first import.
    /// </summary>
    private void ReapplyReviewDiffsToExistingSettings()
    {
        var settingsLoadingService = _serviceProvider.GetService<ISettingsLoadingService>();
        if (settingsLoadingService == null) return;

        void ReapplyToFeature(ISettingsFeatureViewModel featureVm)
        {
            foreach (var setting in featureVm.Settings)
            {
                // Clear any stale review state first
                setting.ClearReviewState();
                // Build currentState from the VM's actual displayed values
                // so the fallback ComputeDiff sees accurate state, not defaults
                var currentState = new SettingStateResult
                {
                    IsEnabled = setting.IsSelected,
                    CurrentValue = setting.SelectedValue
                };
                // Re-apply the new diff
                settingsLoadingService.ApplyReviewDiffToViewModel(setting, currentState);
            }
        }

        var optimizeVm = _serviceProvider.GetService<OptimizeViewModel>();
        if (optimizeVm != null)
        {
            ReapplyToFeature(optimizeVm.SoundViewModel);
            ReapplyToFeature(optimizeVm.UpdateViewModel);
            ReapplyToFeature(optimizeVm.NotificationViewModel);
            ReapplyToFeature(optimizeVm.PrivacyViewModel);
            ReapplyToFeature(optimizeVm.PowerViewModel);
            ReapplyToFeature(optimizeVm.GamingViewModel);
        }

        var customizeVm = _serviceProvider.GetService<CustomizeViewModel>();
        if (customizeVm != null)
        {
            ReapplyToFeature(customizeVm.ExplorerViewModel);
            ReapplyToFeature(customizeVm.StartMenuViewModel);
            ReapplyToFeature(customizeVm.TaskbarViewModel);
            ReapplyToFeature(customizeVm.WindowsThemeViewModel);
        }

        _logService.Log(LogLevel.Info, "Reapplied review diffs to all existing loaded settings");
    }

    /// <summary>
    /// Ensures the compatible settings registry and settings preloader are initialized.
    /// </summary>
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

    /// <summary>
    /// Gets the main window for file dialogs.
    /// </summary>
    private Window? GetMainWindow()
    {
        return App.MainWindow;
    }

    public async Task ExportConfigurationAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, "Starting configuration export");

            // Ensure registry is initialized before exporting
            await EnsureRegistryInitializedAsync();

            var config = await CreateConfigurationFromSystemAsync();

            var window = GetMainWindow();
            if (window == null)
            {
                _logService.Log(LogLevel.Error, "Cannot show file dialog - no main window");
                await _dialogService.ShowErrorAsync("Cannot show file dialog.", "Error");
                return;
            }

            var defaultFileName = $"Winhance_Config_{DateTime.Now:yyyyMMdd}{FileExtension}";
            var filePath = Win32FileDialogHelper.ShowSaveFilePicker(
                window,
                "Save Configuration",
                FileFilter,
                FilePattern,
                defaultFileName,
                "winhance");

            if (string.IsNullOrEmpty(filePath))
            {
                _logService.Log(LogLevel.Info, "Export canceled by user");
                return;
            }

            var json = JsonSerializer.Serialize(config, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);

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

    public async Task ImportConfigurationAsync()
    {
        _logService.Log(LogLevel.Info, "Starting configuration import");

        // Ensure registry is initialized before importing
        await EnsureRegistryInitializedAsync();

        var (selectedOption, importOptions) = await _dialogService.ShowConfigImportOptionsDialogAsync();
        if (selectedOption == null)
        {
            _logService.Log(LogLevel.Info, "Import canceled by user");
            return;
        }

        UnifiedConfigurationFile? config;

        switch (selectedOption)
        {
            case ImportOption.ImportOwn:
                config = await LoadAndValidateConfigurationFromFileAsync();
                if (config == null)
                {
                    _logService.Log(LogLevel.Info, "Import canceled");
                    return;
                }
                break;

            case ImportOption.ImportRecommended:
                config = await LoadRecommendedConfigurationAsync();
                if (config == null) return;
                break;

            case ImportOption.ImportBackup:
                config = await LoadUserBackupConfigurationAsync();
                if (config == null) return;
                break;

            case ImportOption.ImportWindowsDefaults:
                config = await LoadWindowsDefaultsConfigurationAsync();
                if (config == null) return;
                break;

            default:
                return;
        }

        if (!importOptions.ReviewBeforeApplying)
        {
            await ExecuteConfigImportAsync(config, importOptions);
        }
        else
        {
            await EnterReviewModeAsync(config);
        }
    }

    public async Task ImportRecommendedConfigurationAsync()
    {
        _logService.Log(LogLevel.Info, "Starting recommended configuration import");

        // Ensure registry is initialized before importing
        await EnsureRegistryInitializedAsync();

        var config = await LoadRecommendedConfigurationAsync();
        if (config == null) return;

        // Recommended config always enters review mode so users can see what will change
        await EnterReviewModeAsync(config);
    }

    private async Task ExecuteConfigImportAsync(UnifiedConfigurationFile config, ImportOptions dialogOptions)
    {
        try
        {
            var incompatibleSettings = DetectIncompatibleSettings(config);

            if (incompatibleSettings.Any())
            {
                config = FilterConfigForCurrentSystem(config);
                _logService.Log(LogLevel.Info, $"Silently filtered {incompatibleSettings.Count} incompatible settings from config");
            }

            // Build selected sections from what's available in the config
            var selectedSections = new List<string>();

            bool hasWindowsApps = config.WindowsApps.Items.Count > 0;
            bool hasExternalApps = config.ExternalApps.Items.Count > 0;

            if (hasWindowsApps) selectedSections.Add("WindowsApps");
            if (hasExternalApps) selectedSections.Add("ExternalApps");

            foreach (var feature in config.Optimize.Features)
            {
                if (feature.Value.Items.Any())
                {
                    if (!selectedSections.Contains("Optimize")) selectedSections.Add("Optimize");
                    selectedSections.Add($"Optimize_{feature.Key}");
                }
            }

            foreach (var feature in config.Customize.Features)
            {
                if (feature.Value.Items.Any())
                {
                    if (!selectedSections.Contains("Customize")) selectedSections.Add("Customize");
                    selectedSections.Add($"Customize_{feature.Key}");
                }
            }

            if (!selectedSections.Any())
            {
                _dialogService.ShowMessage(
                    _localizationService.GetString("Config_Import_Error_NoSelection") ?? "No changes to apply.",
                    _localizationService.GetString("Config_Import_Error_NoSelection_Title") ?? "No Changes");
                return;
            }

            // Pre-select Windows Apps from config
            if (hasWindowsApps)
            {
                await SelectWindowsAppsFromConfigAsync(config.WindowsApps);

                // Only confirm removal when uninstall is selected
                if (dialogOptions.ProcessWindowsAppsRemoval)
                {
                    var shouldContinue = await ConfirmWindowsAppsRemovalAsync();
                    if (!shouldContinue)
                    {
                        await ClearWindowsAppsSelectionAsync();
                        selectedSections.Remove("WindowsApps");
                        _logService.Log(LogLevel.Info, "User cancelled Windows Apps removal");
                    }
                }
            }

            // Pre-select External Apps from config
            if (hasExternalApps)
            {
                await SelectExternalAppsFromConfigAsync(config.ExternalApps);
            }

            // Use the user's dialog choices, validated against config availability
            var importOptions = new ImportOptions
            {
                ProcessWindowsAppsRemoval = hasWindowsApps && selectedSections.Contains("WindowsApps") && dialogOptions.ProcessWindowsAppsRemoval,
                ProcessWindowsAppsInstallation = hasWindowsApps && selectedSections.Contains("WindowsApps") && dialogOptions.ProcessWindowsAppsInstallation,
                ProcessExternalAppsInstallation = hasExternalApps && dialogOptions.ProcessExternalAppsInstallation,
                ProcessExternalAppsRemoval = hasExternalApps && dialogOptions.ProcessExternalAppsRemoval,
                ApplyThemeWallpaper = dialogOptions.ApplyThemeWallpaper,
                ApplyCleanTaskbar = dialogOptions.ApplyCleanTaskbar,
                ApplyCleanStartMenu = dialogOptions.ApplyCleanStartMenu,
            };

            // Add action-only subsections for Customize actions that the user enabled
            var actionOnlySubsections = new HashSet<string>();
            if (importOptions.ApplyCleanTaskbar && !selectedSections.Contains($"Customize_{FeatureIds.Taskbar}"))
            {
                if (!selectedSections.Contains("Customize")) selectedSections.Add("Customize");
                selectedSections.Add($"Customize_{FeatureIds.Taskbar}");
                actionOnlySubsections.Add($"Customize_{FeatureIds.Taskbar}");
            }
            if (importOptions.ApplyCleanStartMenu && !selectedSections.Contains($"Customize_{FeatureIds.StartMenu}"))
            {
                if (!selectedSections.Contains("Customize")) selectedSections.Add("Customize");
                selectedSections.Add($"Customize_{FeatureIds.StartMenu}");
                actionOnlySubsections.Add($"Customize_{FeatureIds.StartMenu}");
            }
            if (importOptions.ApplyThemeWallpaper && !selectedSections.Contains($"Customize_{FeatureIds.WindowsTheme}"))
            {
                if (!selectedSections.Contains("Customize")) selectedSections.Add("Customize");
                selectedSections.Add($"Customize_{FeatureIds.WindowsTheme}");
                actionOnlySubsections.Add($"Customize_{FeatureIds.WindowsTheme}");
            }
            importOptions.ActionOnlySubsections = actionOnlySubsections;

            // Show overlay during config application
            var overlayStatus = _localizationService.GetString("Config_Import_Status_Applying")
                ?? "Sit back, relax and watch while Winhance enhances Windows with your desired settings...";
            _overlayService.ShowOverlay(overlayStatus);

            _windowsUIManagementService.IsConfigImportMode = true;

            try
            {
                await ApplyConfigurationWithOptionsAsync(config, selectedSections, importOptions);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _windowsUIManagementService.IsConfigImportMode = false;
                _overlayService.HideOverlay();
            }

            // Show success message and wait for user dismissal
            await ShowImportSuccessMessage(selectedSections);

            // Process Windows Apps installation AFTER overlay is hidden (shows confirmation dialog)
            if (hasWindowsApps && importOptions.ProcessWindowsAppsInstallation)
            {
                var vm = _serviceProvider.GetService<WindowsAppsViewModel>();
                if (vm != null)
                {
                    _logService.Log(LogLevel.Info, "Processing Windows Apps installation");
                    await vm.InstallAppsAsync();
                }
            }

            // Process External Apps AFTER success dialog dismissal (needs UI thread)
            if (hasExternalApps && importOptions.ProcessExternalAppsInstallation)
            {
                await ProcessExternalAppsInstallationAsync(config.ExternalApps);
            }
            else if (hasExternalApps && importOptions.ProcessExternalAppsRemoval)
            {
                await ProcessExternalAppsRemovalAsync(config.ExternalApps);
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error importing configuration: {ex.Message}");
            _overlayService.HideOverlay();
            _dialogService.ShowMessage($"Error importing configuration: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Enters Config Review Mode: filters incompatible settings, pre-selects apps,
    /// then activates review mode so the user can navigate the app and review diffs.
    /// </summary>
    private async Task EnterReviewModeAsync(UnifiedConfigurationFile config)
    {
        try
        {
            // Filter incompatible settings
            var incompatibleSettings = DetectIncompatibleSettings(config);
            if (incompatibleSettings.Any())
            {
                config = FilterConfigForCurrentSystem(config);
                _logService.Log(LogLevel.Info, $"Silently filtered {incompatibleSettings.Count} incompatible settings from config");
            }

            // Force filter ON before computing diffs so version-filtered settings
            // don't generate phantom diffs when the user had the filter toggled off
            _compatibleSettingsRegistry.SetFilterEnabled(true);

            // Enter review mode on the service (eagerly computes diffs and fires events)
            await _configReviewModeService.EnterReviewModeAsync(config);

            // Pre-select Windows Apps from config
            if (config.WindowsApps.Items.Count > 0)
            {
                await SelectWindowsAppsFromConfigAsync(config.WindowsApps);
                _logService.Log(LogLevel.Info, $"Pre-selected {config.WindowsApps.Items.Count} Windows Apps for review");
            }

            // Pre-select External Apps from config
            if (config.ExternalApps.Items.Count > 0)
            {
                await SelectExternalAppsFromConfigAsync(config.ExternalApps);
                _logService.Log(LogLevel.Info, $"Pre-selected {config.ExternalApps.Items.Count} External Apps for review");
            }

            _logService.Log(LogLevel.Info, "Review mode activated - user can now navigate and review changes");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error entering review mode: {ex.Message}");
            _configReviewModeService.ExitReviewMode();
            _dialogService.ShowMessage($"Error entering review mode: {ex.Message}", "Error");
        }
    }

    private async Task<UnifiedConfigurationFile> CreateConfigurationFromSystemAsync(bool isBackup = false)
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
        var windowsAppsVM = _serviceProvider.GetService<WindowsAppsViewModel>();
        if (windowsAppsVM != null)
        {
            if (!windowsAppsVM.IsInitialized)
                await windowsAppsVM.LoadItemsAsync();

            config.WindowsApps.IsIncluded = true;
            config.WindowsApps.Items = windowsAppsVM.Items
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
        }

        if (!useInstalledStatus)
        {
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

                        if (item.Definition.WinGetPackageId != null && item.Definition.WinGetPackageId.Any())
                            configItem.WinGetPackageId = item.Definition.WinGetPackageId[0];

                        return configItem;
                    }).ToList();

                _logService.Log(LogLevel.Info, $"Exported {config.ExternalApps.Items.Count} checked External Apps");
            }
        }
    }

    private (int? selectedIndex, Dictionary<string, object>? customStateValues, string? powerPlanGuid, string? powerPlanName)
        GetSelectionStateFromState(SettingDefinition setting, SettingStateResult? state)
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

    public async Task CreateUserBackupConfigAsync()
    {
        try
        {
            _logService.Log(LogLevel.Info, "Creating user backup configuration from current system state");

            await EnsureRegistryInitializedAsync();

            var config = await CreateConfigurationFromSystemAsync(isBackup: true);

            var configDir = Path.Combine(
                _interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "Backup");

            Directory.CreateDirectory(configDir);

            var fileName = $"UserBackup_{DateTime.Now:yyyyMMdd_HHmmss}{FileExtension}";
            var filePath = Path.Combine(configDir, fileName);

            var json = System.Text.Json.JsonSerializer.Serialize(config, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            _logService.Log(LogLevel.Info, $"User backup configuration saved to {filePath}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error creating user backup configuration: {ex.Message}");
        }
    }

    private async Task<UnifiedConfigurationFile?> LoadAndValidateConfigurationFromFileAsync()
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
            FileFilter,
            FilePattern);

        if (string.IsNullOrEmpty(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        var loadedConfig = JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, JsonOptions);

        if (loadedConfig == null)
        {
            _dialogService.ShowMessage("Failed to load configuration file.", "Error");
            return null;
        }

        // Migrate legacy config items (e.g. Toggle→Selection conversions)
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

    private AppItemViewModel? FindMatchingWindowsApp(IEnumerable<AppItemViewModel> vmItems, ConfigurationItem configItem)
    {
        return vmItems.FirstOrDefault(i =>
            (!string.IsNullOrEmpty(configItem.AppxPackageName) && i.Definition?.AppxPackageName == configItem.AppxPackageName) ||
            (!string.IsNullOrEmpty(configItem.CapabilityName) && i.Definition?.CapabilityName == configItem.CapabilityName) ||
            (!string.IsNullOrEmpty(configItem.OptionalFeatureName) && i.Definition?.OptionalFeatureName == configItem.OptionalFeatureName) ||
            i.Id == configItem.Id);
    }

    private AppItemViewModel? FindMatchingExternalApp(IEnumerable<AppItemViewModel> vmItems, ConfigurationItem configItem)
    {
        return vmItems.FirstOrDefault(i =>
            (!string.IsNullOrEmpty(configItem.WinGetPackageId) &&
             i.Definition?.WinGetPackageId != null &&
             i.Definition.WinGetPackageId.Contains(configItem.WinGetPackageId)) ||
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
                    vmItem.IsSelected = configItem.IsSelected ?? true;
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

        var (confirmed, saveScripts) = await vm.ShowRemovalSummaryAndConfirm();
        _configImportSaveRemovalScripts = saveScripts;
        return confirmed;
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

    private async Task ProcessExternalAppsRemovalAsync(ConfigSection externalAppsSection)
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
            _logService.Log(LogLevel.Info, "Starting external apps uninstallation");
            await vm.UninstallAppsAsync();
        }
    }

    /// <summary>
    /// Installs external apps based on captured user selections from review mode.
    /// Unlike ProcessExternalAppsInstallationAsync, this preserves the user's checkbox choices
    /// instead of re-selecting from the config section.
    /// </summary>
    private async Task ProcessExternalAppsFromUserSelectionAsync(List<string> selectedAppIds)
    {
        var vm = _serviceProvider.GetService<ExternalAppsViewModel>();
        if (vm == null) return;

        if (!vm.IsInitialized)
            await vm.LoadItemsAsync();

        // Set VM selections to match captured user choices
        foreach (var vmItem in vm.Items)
            vmItem.IsSelected = selectedAppIds.Contains(vmItem.Id ?? vmItem.Name);

        var selectedCount = vm.Items.Count(i => i.IsSelected);
        if (selectedCount > 0)
        {
            _logService.Log(LogLevel.Info, $"Starting external apps installation for {selectedCount} user-selected apps");
            await vm.InstallApps(skipConfirmation: true);
        }
    }

    private async Task ApplyConfigurationWithOptionsAsync(
        UnifiedConfigurationFile config,
        List<string> selectedSections,
        ImportOptions options)
    {
        _logService.Log(LogLevel.Info, $"Applying configuration to: {string.Join(", ", selectedSections)}");

        bool shouldRemoveApps = selectedSections.Contains("WindowsApps") && options.ProcessWindowsAppsRemoval;
        bool hasOptimize = selectedSections.Any(s => s == "Optimize" || s.StartsWith("Optimize_"));
        bool hasCustomize = selectedSections.Any(s => s == "Customize" || s.StartsWith("Customize_"));

        var parallelTasks = new List<Task>();

        // Branch 1: Bloat removal (no Task.Run — ViewModel needs UI thread for property change notifications)
        if (shouldRemoveApps)
        {
            var vm = _serviceProvider.GetService<WindowsAppsViewModel>();
            if (vm != null)
            {
                _logService.Log(LogLevel.Info, "Processing Windows Apps removal (parallel branch)");
                parallelTasks.Add(vm.RemoveApps(skipConfirmation: true, saveRemovalScripts: _configImportSaveRemovalScripts));
            }
        }

        // Branch 2: All settings (Optimize + Customize in parallel within)
        if (hasOptimize || hasCustomize)
        {
            parallelTasks.Add(ApplyAllSettingsGroupsAsync(config, selectedSections, options, hasOptimize, hasCustomize));
        }

        await Task.WhenAll(parallelTasks);

        // Always restart explorer at the end to apply all changes
        _overlayService.UpdateStatus(
            _localizationService.GetString("Config_Import_Status_Applying")
                ?? "Sit back, relax and watch while Winhance enhances Windows with your desired settings...",
            _localizationService.GetString("Config_Import_Status_RestartingExplorer")
                ?? "Restarting Explorer...");
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

    private async Task ApplyAllSettingsGroupsAsync(
        UnifiedConfigurationFile config,
        List<string> selectedSections,
        ImportOptions options,
        bool hasOptimize,
        bool hasCustomize)
    {
        // Count total features across both groups for progress reporting
        int totalFeatures = 0;
        if (hasOptimize && config.Optimize?.Features != null)
        {
            totalFeatures += config.Optimize.Features
                .Count(f => selectedSections.Contains($"Optimize_{f.Key}"));
        }
        if (hasCustomize && config.Customize?.Features != null)
        {
            totalFeatures += config.Customize.Features
                .Count(f => selectedSections.Contains($"Customize_{f.Key}"));
        }
        // Count action-only subsections that aren't already in the feature groups
        var actionOnlyExtras = options.ActionOnlySubsections
            .Where(s =>
            {
                if (s.StartsWith("Optimize_"))
                {
                    var featureName = s.Substring("Optimize_".Length);
                    return config.Optimize?.Features?.ContainsKey(featureName) != true;
                }
                if (s.StartsWith("Customize_"))
                {
                    var featureName = s.Substring("Customize_".Length);
                    return config.Customize?.Features?.ContainsKey(featureName) != true;
                }
                return false;
            })
            .ToList();
        totalFeatures += actionOnlyExtras.Count;

        if (totalFeatures == 0)
            totalFeatures = 1; // Avoid division by zero in display

        int completedFeatures = 0;

        var statusText = _localizationService.GetString("Config_Import_Status_Applying")
            ?? "Sit back, relax and watch while Winhance enhances Windows with your desired settings...";
        _overlayService.UpdateStatus(statusText, $"0/{totalFeatures} features applied");

        Action<string> onFeatureCompleted = featureName =>
        {
            var completed = Interlocked.Increment(ref completedFeatures);
            _overlayService.UpdateStatus(statusText, $"{completed}/{totalFeatures} features applied");
            _logService.Log(LogLevel.Info, $"Feature completed: {featureName} ({completed}/{totalFeatures})");
        };

        var groupTasks = new List<Task>();

        if (hasOptimize)
        {
            groupTasks.Add(Task.Run(async () =>
            {
                var success = await ApplyFeatureGroupWithOptionsAsync(
                    config.Optimize!, "Optimize", options, selectedSections, onFeatureCompleted);
                _logService.Log(LogLevel.Info, $"  Optimize group: {(success ? "Success" : "Failed")}");
            }));
        }

        if (hasCustomize)
        {
            groupTasks.Add(Task.Run(async () =>
            {
                var success = await ApplyFeatureGroupWithOptionsAsync(
                    config.Customize!, "Customize", options, selectedSections, onFeatureCompleted);
                _logService.Log(LogLevel.Info, $"  Customize group: {(success ? "Success" : "Failed")}");
            }));
        }

        await Task.WhenAll(groupTasks);
    }

    private async Task<bool> ApplyFeatureGroupWithOptionsAsync(
        FeatureGroupSection featureGroup,
        string groupName,
        ImportOptions options,
        List<string> selectedSections,
        Action<string>? onFeatureCompleted = null)
    {
        bool hasActionOnlySubsections = selectedSections.Any(s =>
            s.StartsWith($"{groupName}_") &&
            options.ActionOnlySubsections.Contains(s));

        if ((featureGroup?.Features == null || !featureGroup.Features.Any()) && !hasActionOnlySubsections)
        {
            _logService.Log(LogLevel.Warning, $"{groupName} has no features to apply");
            return false;
        }

        // Build confirmation handler ONCE — identical for all features during import
        Func<string, object?, SettingDefinition, Task<(bool confirmed, bool checkboxResult)>> confirmationHandler =
            (settingId, value, setting) =>
            {
                if (settingId == "power-plan-selection" || settingId == "updates-policy-mode")
                    return Task.FromResult((true, true));

                if (settingId == "theme-mode-windows")
                    return Task.FromResult((true, options?.ApplyThemeWallpaper ?? false));

                if (settingId == "taskbar-clean")
                    return Task.FromResult((true, options?.ApplyCleanTaskbar ?? false));

                if (settingId == "start-menu-clean-10" || settingId == "start-menu-clean-11")
                    return Task.FromResult((true, options?.ApplyCleanStartMenu ?? false));

                return Task.FromResult((true, true));
            };

        var processedFeatureKeys = new HashSet<string>();
        var featureTasks = new List<Task<bool>>();

        // Phase 1: Features from the config file
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
                var actionItems = BuildActionItems(options, featureName);

                // Capture for closure
                var capturedFeatureName = featureName;
                var capturedSection = section;

                featureTasks.Add(Task.Run(async () =>
                {
                    bool featureSuccess = true;

                    // Execute action commands first if any
                    if (actionItems.Any())
                    {
                        var actionSection = new ConfigSection
                        {
                            IsIncluded = true,
                            Items = actionItems
                        };

                        _logService.Log(LogLevel.Info, $"Executing {actionItems.Count} action command(s) for {capturedFeatureName}");

                        var success = await _bridgeService.ApplyConfigurationSectionAsync(
                            actionSection,
                            $"{groupName}.{capturedFeatureName}",
                            confirmationHandler);

                        if (!success)
                        {
                            featureSuccess = false;
                            _logService.Log(LogLevel.Warning, $"Failed to apply action commands for {capturedFeatureName}");
                        }
                    }

                    // Apply regular settings if not action-only
                    if (!isActionOnly)
                    {
                        _logService.Log(LogLevel.Info, $"Applying {capturedSection.Items.Count} settings from {groupName} > {capturedFeatureName}");

                        var success = await _bridgeService.ApplyConfigurationSectionAsync(
                            capturedSection,
                            $"{groupName}.{capturedFeatureName}",
                            confirmationHandler);

                        if (!success)
                        {
                            featureSuccess = false;
                            _logService.Log(LogLevel.Warning, $"Failed to apply some settings from {groupName} > {capturedFeatureName}");
                        }
                    }

                    onFeatureCompleted?.Invoke(capturedFeatureName);
                    return featureSuccess;
                }));
            }
        }

        // Phase 2: Action-only subsections not already in the config features
        var unprocessedActionOnly = selectedSections
            .Where(s => s.StartsWith($"{groupName}_") &&
                       options.ActionOnlySubsections.Contains(s) &&
                       !processedFeatureKeys.Contains(s))
            .ToList();

        foreach (var featureKey in unprocessedActionOnly)
        {
            var featureName = featureKey.Substring(groupName.Length + 1);
            var actionItems = BuildActionItems(options!, featureName);

            // Handle WindowsTheme action-only case
            if (options?.ApplyThemeWallpaper == true && featureName == FeatureIds.WindowsTheme)
            {
                actionItems.Add(new ConfigurationItem
                {
                    Id = "theme-mode-windows",
                    Name = "Windows Theme",
                    IsSelected = true,
                    InputType = InputType.Selection,
                    SelectedIndex = 0
                });
            }

            var capturedFeatureName = featureName;
            var capturedActionItems = actionItems;

            featureTasks.Add(Task.Run(async () =>
            {
                if (capturedActionItems.Any())
                {
                    var actionSection = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = capturedActionItems
                    };

                    _logService.Log(LogLevel.Info, $"Executing {capturedActionItems.Count} action command(s) for {capturedFeatureName} (not in config file)");

                    var success = await _bridgeService.ApplyConfigurationSectionAsync(
                        actionSection,
                        $"{groupName}.{capturedFeatureName}",
                        confirmationHandler);

                    if (!success)
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to apply action commands for {capturedFeatureName}");
                        onFeatureCompleted?.Invoke(capturedFeatureName);
                        return false;
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Info, $"No action commands to execute for {capturedFeatureName}");
                }

                onFeatureCompleted?.Invoke(capturedFeatureName);
                return true;
            }));
        }

        // Run all features in the group in parallel
        var results = await Task.WhenAll(featureTasks);
        return results.All(r => r);
    }

    private List<ConfigurationItem> BuildActionItems(ImportOptions options, string featureName)
    {
        var items = new List<ConfigurationItem>();

        if (options?.ApplyCleanTaskbar == true && featureName == FeatureIds.Taskbar)
        {
            items.Add(new ConfigurationItem
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
            items.Add(new ConfigurationItem
            {
                Id = settingId,
                Name = "Clean Start Menu",
                IsSelected = true,
                InputType = InputType.Toggle
            });
        }

        return items;
    }

    private async Task ShowImportSuccessMessage(List<string> selectedSections)
    {
        await _dialogService.ShowInformationAsync(
            _localizationService.GetString("Config_Import_Success_Message") ?? "Configuration imported successfully.",
            _localizationService.GetString("Config_Import_Success_Title") ?? "Import Successful");
    }

    private async Task<UnifiedConfigurationFile?> LoadRecommendedConfigurationAsync()
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
            var config = JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, JsonOptions);

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

    private async Task WriteConfigApplicationLogAsync(
        List<string> selectedSections,
        ImportOptions options,
        bool success)
    {
        try
        {
            var logDir = Path.Combine(
                _interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance");
            Directory.CreateDirectory(logDir);

            var logPath = Path.Combine(logDir, $"ConfigImport_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            var lines = new List<string>
            {
                $"Winhance Config Import Log",
                $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Result: {(success ? "Success" : "Failed")}",
                $"",
                $"Sections Applied:",
            };

            foreach (var section in selectedSections)
            {
                lines.Add($"  - {section}");
            }

            lines.Add("");
            lines.Add("Options:");
            lines.Add($"  ProcessWindowsAppsRemoval: {options.ProcessWindowsAppsRemoval}");
            lines.Add($"  ProcessExternalAppsInstallation: {options.ProcessExternalAppsInstallation}");
            lines.Add($"  ApplyThemeWallpaper: {options.ApplyThemeWallpaper}");
            lines.Add($"  ApplyCleanTaskbar: {options.ApplyCleanTaskbar}");
            lines.Add($"  ApplyCleanStartMenu: {options.ApplyCleanStartMenu}");
            lines.Add($"  ReviewBeforeApplying: {options.ReviewBeforeApplying}");

            if (options.ActionOnlySubsections.Any())
            {
                lines.Add($"  ActionOnlySubsections: {string.Join(", ", options.ActionOnlySubsections)}");
            }

            await File.WriteAllLinesAsync(logPath, lines);
            _logService.Log(LogLevel.Info, $"Config application log written to {logPath}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning, $"Failed to write config application log: {ex.Message}");
        }
    }

    private async Task<UnifiedConfigurationFile?> LoadUserBackupConfigurationAsync()
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

            var backupFiles = Directory.GetFiles(configDir, $"UserBackup_*{FileExtension}")
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
                // Single backup file - use directly
                filePath = backupFiles[0];
            }
            else
            {
                // Multiple backup files - show file dialog
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
                    window, dialogTitle, FileFilter, FilePattern, configDir);

                if (string.IsNullOrEmpty(selectedPath))
                {
                    _logService.Log(LogLevel.Info, "Backup import canceled by user");
                    return null;
                }

                filePath = selectedPath;
            }
            _logService.Log(LogLevel.Info, $"Loading user backup configuration from {filePath}");

            var json = await File.ReadAllTextAsync(filePath);
            var config = System.Text.Json.JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, JsonOptions);

            if (config == null)
            {
                _dialogService.ShowMessage("Failed to load backup configuration file.", "Error");
                return null;
            }

            // Migrate legacy config items (e.g. Toggle→Selection conversions)
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

    private async Task<UnifiedConfigurationFile?> LoadWindowsDefaultsConfigurationAsync()
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
            var config = System.Text.Json.JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, JsonOptions);

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

            await _processExecutor.ShellExecuteAsync("explorer.exe").ConfigureAwait(false);

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

    public async Task ApplyReviewedConfigAsync()
    {
        if (!_configReviewModeService.IsInReviewMode || _configReviewModeService.ActiveConfig == null)
        {
            _logService.Log(LogLevel.Warning, "ApplyReviewedConfigAsync called but not in review mode");
            return;
        }

        var config = _configReviewModeService.ActiveConfig;
        var approvedDiffs = _configReviewDiffService.GetApprovedDiffs();

        try
        {
            // Build the list of sections that have approved changes
            var selectedSections = new List<string>();

            // Check if any Windows Apps are selected and determine action
            var softwareAppsVm = _serviceProvider.GetService<SoftwareAppsViewModel>();
            var windowsAppsVm = _serviceProvider.GetService<WindowsAppsViewModel>();
            bool hasWindowsApps = windowsAppsVm?.Items?.Any(a => a.IsSelected) == true;
            bool windowsAppsInstall = softwareAppsVm?.IsWindowsAppsInstallAction == true;
            bool windowsAppsRemove = softwareAppsVm?.IsWindowsAppsRemoveAction == true;
            if (hasWindowsApps && (windowsAppsInstall || windowsAppsRemove))
                selectedSections.Add("WindowsApps");

            // Check if any External Apps are selected and determine action
            var externalAppsVm = _serviceProvider.GetService<ExternalAppsViewModel>();
            bool hasExternalApps = externalAppsVm?.Items?.Any(a => a.IsSelected) == true;
            bool externalAppsInstall = softwareAppsVm?.IsExternalAppsInstallAction == true;
            bool externalAppsRemove = softwareAppsVm?.IsExternalAppsRemoveAction == true;
            if (hasExternalApps && (externalAppsInstall || externalAppsRemove))
                selectedSections.Add("ExternalApps");

            // Check approved Optimize/Customize diffs (including action settings)
            var approvedSettingIds = new HashSet<string>(approvedDiffs.Select(d => d.SettingId));
            var approvedActionSettingIds = new HashSet<string>(
                approvedDiffs.Where(d => d.IsActionSetting).Select(d => d.SettingId));

            if (config.Optimize.Features.Any(f => f.Value.Items.Any(i => approvedSettingIds.Contains(i.Id))))
            {
                selectedSections.Add("Optimize");
                foreach (var feature in config.Optimize.Features)
                {
                    if (feature.Value.Items.Any(i => approvedSettingIds.Contains(i.Id)))
                        selectedSections.Add($"Optimize_{feature.Key}");
                }
            }

            if (config.Customize.Features.Any(f => f.Value.Items.Any(i => approvedSettingIds.Contains(i.Id))))
            {
                selectedSections.Add("Customize");
                foreach (var feature in config.Customize.Features)
                {
                    if (feature.Value.Items.Any(i => approvedSettingIds.Contains(i.Id)))
                        selectedSections.Add($"Customize_{feature.Key}");
                }
            }

            // Build import options based on action choices
            var importOptions = new ImportOptions
            {
                ProcessWindowsAppsRemoval = hasWindowsApps && windowsAppsRemove,
                ProcessWindowsAppsInstallation = hasWindowsApps && windowsAppsInstall,
                ProcessExternalAppsInstallation = hasExternalApps && externalAppsInstall,
                ProcessExternalAppsRemoval = hasExternalApps && externalAppsRemove,
                ApplyThemeWallpaper = approvedSettingIds.Contains("theme-mode-windows"),
                ApplyCleanTaskbar = approvedSettingIds.Contains("taskbar-clean"),
                ApplyCleanStartMenu = approvedSettingIds.Contains("start-menu-clean-10") || approvedSettingIds.Contains("start-menu-clean-11"),
            };

            // Ensure action settings add their parent feature sections even if no regular settings approved
            var actionOnlySubsections = new HashSet<string>();
            if (importOptions.ApplyCleanTaskbar && !selectedSections.Contains($"Customize_{FeatureIds.Taskbar}"))
            {
                if (!selectedSections.Contains("Customize")) selectedSections.Add("Customize");
                selectedSections.Add($"Customize_{FeatureIds.Taskbar}");
                actionOnlySubsections.Add($"Customize_{FeatureIds.Taskbar}");
            }
            if (importOptions.ApplyCleanStartMenu && !selectedSections.Contains($"Customize_{FeatureIds.StartMenu}"))
            {
                if (!selectedSections.Contains("Customize")) selectedSections.Add("Customize");
                selectedSections.Add($"Customize_{FeatureIds.StartMenu}");
                actionOnlySubsections.Add($"Customize_{FeatureIds.StartMenu}");
            }
            if (importOptions.ApplyThemeWallpaper && !selectedSections.Contains($"Customize_{FeatureIds.WindowsTheme}"))
            {
                if (!selectedSections.Contains("Customize")) selectedSections.Add("Customize");
                selectedSections.Add($"Customize_{FeatureIds.WindowsTheme}");
                actionOnlySubsections.Add($"Customize_{FeatureIds.WindowsTheme}");
            }
            importOptions.ActionOnlySubsections = actionOnlySubsections;

            if (!selectedSections.Any())
            {
                _dialogService.ShowMessage(
                    _localizationService.GetString("Config_Import_Error_NoSelection") ?? "No changes to apply.",
                    _localizationService.GetString("Config_Import_Error_NoSelection_Title") ?? "No Changes");
                return;
            }

            // Build a filtered config containing only approved settings
            var filteredConfig = BuildFilteredConfigFromApprovals(config, approvedSettingIds);

            // Capture current external app UI selections BEFORE exiting review mode
            // This preserves user's checkbox changes made during review
            List<string>? selectedExternalAppIds = null;
            if (hasExternalApps && externalAppsVm?.Items != null)
            {
                selectedExternalAppIds = externalAppsVm.Items
                    .Where(a => a.IsSelected)
                    .Select(a => a.Id ?? a.Name)
                    .ToList();
            }

            // Confirm Windows Apps removal if remove action is chosen
            if (hasWindowsApps && windowsAppsRemove)
            {
                var shouldContinue = await ConfirmWindowsAppsRemovalAsync();
                if (!shouldContinue)
                {
                    await ClearWindowsAppsSelectionAsync();
                    selectedSections.Remove("WindowsApps");
                    _logService.Log(LogLevel.Info, "User cancelled Windows Apps removal during review apply");
                }
            }

            // Show overlay
            var overlayStatus = _localizationService.GetString("Config_Import_Status_Applying")
                ?? "Sit back, relax and watch while Winhance enhances Windows with your desired settings...";
            _overlayService.ShowOverlay(overlayStatus);

            _windowsUIManagementService.IsConfigImportMode = true;

            try
            {
                await ApplyConfigurationWithOptionsAsync(filteredConfig, selectedSections, importOptions);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying reviewed config: {ex.Message}");
            }
            finally
            {
                _windowsUIManagementService.IsConfigImportMode = false;
                _overlayService.HideOverlay();
            }

            // Exit review mode after applying
            _configReviewModeService.ExitReviewMode();

            // Show success message and wait for user dismissal
            await ShowImportSuccessMessage(selectedSections);

            // Process External Apps installation AFTER success dialog dismissal (needs UI thread)
            // Use captured user selections instead of config section to honor user's review choices
            if (selectedExternalAppIds != null && selectedExternalAppIds.Count > 0)
            {
                await ProcessExternalAppsFromUserSelectionAsync(selectedExternalAppIds);
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error in ApplyReviewedConfigAsync: {ex.Message}");
            _overlayService.HideOverlay();
            _configReviewModeService.ExitReviewMode();
            _dialogService.ShowMessage($"Error applying configuration: {ex.Message}", "Error");
        }
    }

    public async Task CancelReviewModeAsync()
    {
        if (!_configReviewModeService.IsInReviewMode) return;

        // Clear app selections that were set during EnterReviewModeAsync
        await ClearWindowsAppsSelectionAsync();

        var externalAppsVm = _serviceProvider.GetService<ExternalAppsViewModel>();
        if (externalAppsVm != null)
        {
            foreach (var item in externalAppsVm.Items)
                item.IsSelected = false;
        }

        _configReviewModeService.ExitReviewMode();
        _logService.Log(LogLevel.Info, "Review mode cancelled - all selections cleared");
    }

    /// <summary>
    /// Builds a filtered copy of the config containing only settings that were approved in review mode.
    /// </summary>
    private UnifiedConfigurationFile BuildFilteredConfigFromApprovals(
        UnifiedConfigurationFile original,
        HashSet<string> approvedSettingIds)
    {
        var filtered = new UnifiedConfigurationFile
        {
            Version = original.Version,
            CreatedAt = original.CreatedAt,
            WindowsApps = original.WindowsApps,   // Apps are filtered by checkbox selection, not diffs
            ExternalApps = original.ExternalApps,  // Same - filtered by checkbox selection
        };

        // Filter Optimize features to only include approved settings
        filtered.Optimize = FilterFeatureGroupByApprovals(original.Optimize, approvedSettingIds);
        filtered.Customize = FilterFeatureGroupByApprovals(original.Customize, approvedSettingIds);

        return filtered;
    }

    private FeatureGroupSection FilterFeatureGroupByApprovals(
        FeatureGroupSection original,
        HashSet<string> approvedSettingIds)
    {
        var filteredFeatures = new Dictionary<string, ConfigSection>();

        foreach (var feature in original.Features)
        {
            var approvedItems = feature.Value.Items
                .Where(item => approvedSettingIds.Contains(item.Id))
                .ToList();

            if (approvedItems.Any())
            {
                filteredFeatures[feature.Key] = new ConfigSection
                {
                    IsIncluded = feature.Value.IsIncluded,
                    Items = approvedItems
                };
            }
        }

        return new FeatureGroupSection
        {
            IsIncluded = original.IsIncluded,
            Features = filteredFeatures
        };
    }
}
