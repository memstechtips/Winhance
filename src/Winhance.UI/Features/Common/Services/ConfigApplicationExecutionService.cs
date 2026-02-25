using System.Threading;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Services;

public class ConfigApplicationExecutionService : IConfigApplicationExecutionService
{
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IWindowsVersionService _windowsVersionService;
    private readonly IConfigurationApplicationBridgeService _bridgeService;
    private readonly IWindowsUIManagementService _windowsUIManagementService;
    private readonly IProcessExecutor _processExecutor;
    private readonly IConfigImportOverlayService _overlayService;
    private readonly IConfigImportState _configImportState;
    private readonly IConfigAppSelectionService _configAppSelectionService;
    private readonly IConfigLoadService _configLoadService;
    private readonly IReviewModeViewModelCoordinator _vmCoordinator;

    public ConfigApplicationExecutionService(
        ILogService logService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IWindowsVersionService windowsVersionService,
        IConfigurationApplicationBridgeService bridgeService,
        IWindowsUIManagementService windowsUIManagementService,
        IProcessExecutor processExecutor,
        IConfigImportOverlayService overlayService,
        IConfigImportState configImportState,
        IConfigAppSelectionService configAppSelectionService,
        IConfigLoadService configLoadService,
        IReviewModeViewModelCoordinator vmCoordinator)
    {
        _logService = logService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _windowsVersionService = windowsVersionService;
        _bridgeService = bridgeService;
        _windowsUIManagementService = windowsUIManagementService;
        _processExecutor = processExecutor;
        _overlayService = overlayService;
        _configImportState = configImportState;
        _configAppSelectionService = configAppSelectionService;
        _configLoadService = configLoadService;
        _vmCoordinator = vmCoordinator;
    }

    public async Task ExecuteConfigImportAsync(UnifiedConfigurationFile config, ImportOptions dialogOptions)
    {
        try
        {
            var incompatibleSettings = _configLoadService.DetectIncompatibleSettings(config);

            if (incompatibleSettings.Any())
            {
                config = _configLoadService.FilterConfigForCurrentSystem(config);
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
            bool saveRemovalScripts = true;
            if (hasWindowsApps)
            {
                await _configAppSelectionService.SelectWindowsAppsFromConfigAsync(config.WindowsApps);

                // Only confirm removal when uninstall is selected
                if (dialogOptions.ProcessWindowsAppsRemoval)
                {
                    var (shouldContinue, saveScripts) = await _configAppSelectionService.ConfirmWindowsAppsRemovalAsync();
                    saveRemovalScripts = saveScripts;
                    if (!shouldContinue)
                    {
                        await _configAppSelectionService.ClearWindowsAppsSelectionAsync();
                        selectedSections.Remove("WindowsApps");
                        _logService.Log(LogLevel.Info, "User cancelled Windows Apps removal");
                    }
                }
            }

            // Pre-select External Apps from config
            if (hasExternalApps)
            {
                await _configAppSelectionService.SelectExternalAppsFromConfigAsync(config.ExternalApps);
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
            importOptions = importOptions with { ActionOnlySubsections = actionOnlySubsections };

            // Show overlay during config application
            var overlayStatus = _localizationService.GetString("Config_Import_Status_Applying")
                ?? "Sit back, relax and watch while Winhance enhances Windows with your desired settings...";
            _overlayService.ShowOverlay(overlayStatus);

            _configImportState.IsActive = true;

            try
            {
                await ApplyConfigurationWithOptionsAsync(config, selectedSections, importOptions, saveRemovalScripts);
            }
            finally
            {
                _configImportState.IsActive = false;
                _overlayService.HideOverlay();
            }

            // Show success message and wait for user dismissal
            await ShowImportSuccessMessage(selectedSections);

            // Process Windows Apps installation AFTER overlay is hidden (shows confirmation dialog)
            if (hasWindowsApps && importOptions.ProcessWindowsAppsInstallation)
            {
                _logService.Log(LogLevel.Info, "Processing Windows Apps installation");
                await _vmCoordinator.InstallWindowsAppsAsync();
            }

            // Process External Apps AFTER success dialog dismissal (needs UI thread)
            if (hasExternalApps && importOptions.ProcessExternalAppsInstallation)
            {
                await _configAppSelectionService.ProcessExternalAppsInstallationAsync(config.ExternalApps);
            }
            else if (hasExternalApps && importOptions.ProcessExternalAppsRemoval)
            {
                await _configAppSelectionService.ProcessExternalAppsRemovalAsync(config.ExternalApps);
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error importing configuration: {ex.Message}");
            _overlayService.HideOverlay();
            _dialogService.ShowMessage($"Error importing configuration: {ex.Message}", "Error");
        }
    }

    public async Task ApplyConfigurationWithOptionsAsync(
        UnifiedConfigurationFile config,
        List<string> selectedSections,
        ImportOptions options)
    {
        await ApplyConfigurationWithOptionsAsync(config, selectedSections, options, saveRemovalScripts: true);
    }

    private async Task ApplyConfigurationWithOptionsAsync(
        UnifiedConfigurationFile config,
        List<string> selectedSections,
        ImportOptions options,
        bool saveRemovalScripts)
    {
        _logService.Log(LogLevel.Info, $"Applying configuration to: {string.Join(", ", selectedSections)}");

        bool shouldRemoveApps = selectedSections.Contains("WindowsApps") && options.ProcessWindowsAppsRemoval;
        bool hasOptimize = selectedSections.Any(s => s == "Optimize" || s.StartsWith("Optimize_"));
        bool hasCustomize = selectedSections.Any(s => s == "Customize" || s.StartsWith("Customize_"));

        var parallelTasks = new List<Task>();

        // Branch 1: Bloat removal (no Task.Run - ViewModel needs UI thread for property change notifications)
        if (shouldRemoveApps)
        {
            _logService.Log(LogLevel.Info, "Processing Windows Apps removal (parallel branch)");
            parallelTasks.Add(_vmCoordinator.RemoveWindowsAppsAsync(skipConfirmation: true, saveRemovalScripts: saveRemovalScripts));
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
                await Task.Delay(1000).ConfigureAwait(false);
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

        // Build confirmation handler ONCE - identical for all features during import
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
                    await Task.Delay(1000).ConfigureAwait(false);
                    return;
                }

                retryCount++;
                await Task.Delay(250).ConfigureAwait(false);
            }

            _logService.Log(LogLevel.Warning, "Explorer did not auto-restart, starting manually");

            await _processExecutor.ShellExecuteAsync("explorer.exe").ConfigureAwait(false);

            // Verify explorer actually started
            retryCount = 0;
            const int verifyMaxRetries = 10;

            while (retryCount < verifyMaxRetries)
            {
                await Task.Delay(500).ConfigureAwait(false);

                bool isRunning = await Task.Run(() =>
                    _windowsUIManagementService.IsProcessRunning("explorer"));

                if (isRunning)
                {
                    _logService.Log(LogLevel.Info, "Explorer.exe started successfully");
                    await Task.Delay(1000).ConfigureAwait(false);
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
}
