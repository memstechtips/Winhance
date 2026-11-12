using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public class ConfigApplicationExecutionService(
    ILogService logService,
    IDialogService dialogService,
    ILocalizationService localizationService,
    IWindowsVersionService windowsVersionService,
    IConfigurationApplicationBridgeService bridgeService,
    IWindowsUIManagementService windowsUIManagementService,
    IExplorerRestartService explorerRestartService,
    IConfigImportOverlayService overlayService,
    IConfigImportState configImportState,
    IConfigAppSelectionService configAppSelectionService,
    IConfigLoadService configLoadService,
    IReviewModeViewModelCoordinator vmCoordinator,
    IPolicyCleanupService policyCleanupService,
    IChangeHistoryService changeHistoryService,
    IInstallOptionsService installOptions,
    ICoverageSummaryService coverageSummary) : IConfigApplicationExecutionService
{
    public async Task ExecuteConfigImportAsync(WinhanceConfigFile config, ImportOptions dialogOptions, string? sourcePath)
    {
        try
        {
            var setAside = configLoadService.DetectIncompatibleSettings(config);

            if (setAside.Count > 0)
            {
                config = configLoadService.FilterConfigForCurrentSystem(config);
                logService.Log(LogLevel.Info, $"Set aside {setAside.Count} settings this PC does not show: {string.Join(", ", setAside)}");
            }

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

            if (selectedSections.Count == 0)
            {
                dialogService.ShowMessage(
                    localizationService.GetStringOrDefault("Config_Import_Error_NoSelection", "There are no changes to apply."),
                    localizationService.GetStringOrDefault("Config_Import_Error_NoSelection_Title", "Nothing to Apply"));
                return;
            }

            bool saveRemovalScripts = true;
            if (hasWindowsApps)
            {
                await configAppSelectionService.SelectWindowsAppsFromConfigAsync(config.WindowsApps);

                if (dialogOptions.ProcessWindowsAppsRemoval)
                {
                    var (shouldContinue, saveScripts) = await configAppSelectionService.ConfirmWindowsAppsRemovalAsync();
                    saveRemovalScripts = saveScripts;
                    if (!shouldContinue)
                    {
                        await configAppSelectionService.ClearWindowsAppsSelectionAsync();
                        selectedSections.Remove("WindowsApps");
                        logService.Log(LogLevel.Info, "User cancelled Windows Apps removal");
                    }
                }
            }

            if (hasExternalApps)
            {
                await configAppSelectionService.SelectExternalAppsFromConfigAsync(config.ExternalApps);
            }

            var importOptions = new ImportOptions
            {
                ProcessWindowsAppsRemoval = hasWindowsApps && selectedSections.Contains("WindowsApps") && dialogOptions.ProcessWindowsAppsRemoval,
                ProcessWindowsAppsInstallation = hasWindowsApps && selectedSections.Contains("WindowsApps") && dialogOptions.ProcessWindowsAppsInstallation,
                ProcessExternalAppsInstallation = hasExternalApps && dialogOptions.ProcessExternalAppsInstallation,
                ProcessExternalAppsRemoval = hasExternalApps && dialogOptions.ProcessExternalAppsRemoval,
                ApplyCleanTaskbar = dialogOptions.ApplyCleanTaskbar,
                ApplyCleanStartMenu = dialogOptions.ApplyCleanStartMenu,
                IsWindowsDefaults = dialogOptions.IsWindowsDefaults,
            };

            importOptions = WithCleanActionSections(importOptions, selectedSections);

            var overlayStatus = localizationService.GetStringOrDefault("Config_Import_Status_Applying", "Sit back, relax and watch while Winhance enhances Windows with your desired settings...");
            overlayService.ShowOverlay(overlayStatus);

            var importScope = configImportState.Begin();
            var changeBatch = changeHistoryService.BeginBatch(BuildImportBatchHeader());
            IReadOnlyList<string> notApplied;

            try
            {
                await ApplyConfigurationWithOptionsAsync(config, selectedSections, importOptions, saveRemovalScripts);

                // When restoring Windows defaults, clean up all policy registry keys AFTER
                // applying settings, because applying "disabled" values can re-create the keys
                if (importOptions.IsWindowsDefaults)
                {
                    logService.Log(LogLevel.Info, "Windows Defaults import: cleaning up policy registry keys");
                    await Task.Run(() => policyCleanupService.CleanupPolicyKeys());
                }
            }
            finally
            {
                notApplied = configImportState.TakeNotApplied();
                importScope.Dispose();
                changeBatch.Dispose();
                overlayService.HideOverlay();
            }

            await coverageSummary.ShowImportSummaryAsync(config, sourcePath, setAside, notApplied);

            // Process Windows Apps installation AFTER overlay is hidden (shows confirmation dialog)
            if (hasWindowsApps && importOptions.ProcessWindowsAppsInstallation)
            {
                logService.Log(LogLevel.Info, "Processing Windows Apps installation");
                await vmCoordinator.InstallWindowsAppsAsync();
            }

            // Process External Apps AFTER success dialog dismissal (needs UI thread)
            if (hasExternalApps && importOptions.ProcessExternalAppsInstallation)
            {
                if (config.AppInstallOptions is { } carried)
                    installOptions.BeginSession(carried);
                try
                {
                    await configAppSelectionService.ProcessExternalAppsInstallationAsync(config.ExternalApps);
                }
                finally
                {
                    if (config.AppInstallOptions is not null)
                        installOptions.EndSession();
                }
            }
            else if (hasExternalApps && importOptions.ProcessExternalAppsRemoval)
            {
                await configAppSelectionService.ProcessExternalAppsRemovalAsync(config.ExternalApps);
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error importing configuration: {ex.Message}");
            overlayService.HideOverlay();
            dialogService.ShowMessage(localizationService.GetString("Config_Import_Error", ex.Message), localizationService.GetString("Dialog_Error"));
        }
    }

    public async Task<IReadOnlyList<string>> ApplySettingsQuietlyAsync(
        WinhanceConfigFile config, string sourceName, bool isWindowsDefaults, bool restartExplorer, bool broadcastRegional)
    {
        var sections = new List<string>();
        foreach (var group in config.SettingSections().Where(s => s.Items.Count > 0).GroupBy(s => s.Group))
        {
            sections.Add(group.Key);
            sections.AddRange(group.Select(s => $"{group.Key}_{s.Feature}"));
        }

        if (sections.Count == 0)
            return [];

        var importScope = configImportState.Begin();
        configImportState.SourceName = sourceName;
        var changeBatch = changeHistoryService.BeginBatch(BuildImportBatchHeader());
        try
        {
            await ApplyConfigurationWithOptionsAsync(config, sections, new ImportOptions { IsWindowsDefaults = isWindowsDefaults }, saveRemovalScripts: false, restartExplorer, broadcastRegional);
            if (isWindowsDefaults)
                await Task.Run(() => policyCleanupService.CleanupPolicyKeys());
            return configImportState.TakeNotApplied();
        }
        finally
        {
            changeBatch.Dispose();
            importScope.Dispose();
        }
    }

    // A ticked clean action needs its Customize section applied even when the file carries nothing else for it.
    public static ImportOptions WithCleanActionSections(ImportOptions options, List<string> selectedSections)
    {
        var actionOnlySubsections = new HashSet<string>();
        foreach (var (wanted, feature) in new[] { (options.ApplyCleanTaskbar, FeatureIds.Taskbar), (options.ApplyCleanStartMenu, FeatureIds.StartMenu) })
        {
            var section = $"Customize_{feature}";
            if (!wanted || selectedSections.Contains(section))
                continue;
            if (!selectedSections.Contains("Customize"))
                selectedSections.Add("Customize");
            selectedSections.Add(section);
            actionOnlySubsections.Add(section);
        }
        return options with { ActionOnlySubsections = actionOnlySubsections };
    }

    public async Task ApplyConfigurationWithOptionsAsync(
        WinhanceConfigFile config,
        List<string> selectedSections,
        ImportOptions options)
    {
        await ApplyConfigurationWithOptionsAsync(config, selectedSections, options, saveRemovalScripts: true);
    }

    private async Task ApplyConfigurationWithOptionsAsync(
        WinhanceConfigFile config,
        List<string> selectedSections,
        ImportOptions options,
        bool saveRemovalScripts,
        bool restartExplorer = true,
        bool broadcastRegional = true)
    {
        logService.Log(LogLevel.Info, $"Applying configuration to: {string.Join(", ", selectedSections)}");

        bool shouldRemoveApps = selectedSections.Contains("WindowsApps") && options.ProcessWindowsAppsRemoval;
        bool hasOptimize = selectedSections.Any(s => s == "Optimize" || s.StartsWith("Optimize_"));
        bool hasCustomize = selectedSections.Any(s => s == "Customize" || s.StartsWith("Customize_"));

        var parallelTasks = new List<Task>();

        // No Task.Run: the ViewModel needs the UI thread for property change notifications.
        if (shouldRemoveApps)
        {
            logService.Log(LogLevel.Info, "Processing Windows Apps removal (parallel branch)");
            parallelTasks.Add(vmCoordinator.RemoveWindowsAppsAsync(skipConfirmation: true, saveRemovalScripts: saveRemovalScripts));
        }

        if (hasOptimize || hasCustomize)
        {
            parallelTasks.Add(ApplyAllSettingsGroupsAsync(config, selectedSections, options, hasOptimize, hasCustomize));
        }

        await Task.WhenAll(parallelTasks);

        if (!restartExplorer && !broadcastRegional)
            return;

        if (restartExplorer)
        {
            overlayService.UpdateStatus(
                localizationService.GetStringOrDefault("Config_Import_Status_Applying", "Sit back, relax and watch while Winhance enhances Windows with your desired settings..."),
                localizationService.GetStringOrDefault("Config_Import_Status_RestartingExplorer", "Restarting Explorer..."));
        }

        await Task.Run(async () =>
        {
            // Per-setting intl broadcasts are skipped during an import, so apps pick the regional changes up here.
            if (broadcastRegional)
                windowsUIManagementService.BroadcastRegionalSettingChange();

            // ONE restart for the whole import, through the shared single-flight service. Individual
            // settings applied during an import never restart Explorer themselves - this is the only
            // shell restart an import performs. Clearing the pending state is part of a successful
            // restart, so an import can never leave a pending-restart bar behind.
            if (restartExplorer)
                await explorerRestartService.RestartAsync().ConfigureAwait(false);
        });
    }

    private async Task ApplyAllSettingsGroupsAsync(
        WinhanceConfigFile config,
        List<string> selectedSections,
        ImportOptions options,
        bool hasOptimize,
        bool hasCustomize)
    {
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
            totalFeatures = 1; // avoids a 0/0 display

        int completedFeatures = 0;

        var statusText = localizationService.GetStringOrDefault("Config_Import_Status_Applying", "Sit back, relax and watch while Winhance enhances Windows with your desired settings...");
        overlayService.UpdateStatus(statusText, $"0/{totalFeatures} features applied");

        Action<string> onFeatureCompleted = featureName =>
        {
            var completed = Interlocked.Increment(ref completedFeatures);
            overlayService.UpdateStatus(statusText, $"{completed}/{totalFeatures} features applied");
            logService.Log(LogLevel.Info, $"Feature completed: {featureName} ({completed}/{totalFeatures})");
        };

        var groupTasks = new List<Task>();

        if (hasOptimize)
        {
            groupTasks.Add(Task.Run(async () =>
            {
                var success = await ApplyFeatureGroupWithOptionsAsync(
                    config.Optimize!, "Optimize", options, selectedSections, onFeatureCompleted);
                logService.Log(LogLevel.Info, $"  Optimize group: {(success ? "Success" : "Failed")}");
            }));
        }

        if (hasCustomize)
        {
            groupTasks.Add(Task.Run(async () =>
            {
                var success = await ApplyFeatureGroupWithOptionsAsync(
                    config.Customize!, "Customize", options, selectedSections, onFeatureCompleted);
                logService.Log(LogLevel.Info, $"  Customize group: {(success ? "Success" : "Failed")}");
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
            logService.Log(LogLevel.Warning, $"{groupName} has no features to apply");
            return false;
        }

        // Build confirmation handler ONCE - identical for all features during import
        Func<string, object?, Task<(bool confirmed, bool checkboxResult)>> confirmationHandler =
            (settingId, value) =>
            {
                if (settingId == "power-plan-selection" || settingId == "updates-policy-mode")
                    return Task.FromResult((true, true));

                if (settingId == SettingIds.CleanTaskbar)
                    return Task.FromResult((true, options?.ApplyCleanTaskbar ?? false));

                if (settingId is SettingIds.CleanStartMenu10 or SettingIds.CleanStartMenu11)
                    return Task.FromResult((true, options?.ApplyCleanStartMenu ?? false));

                return Task.FromResult((true, true));
            };

        var processedFeatureKeys = new HashSet<string>();
        var featureTasks = new List<Task<bool>>();

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
                    logService.Log(LogLevel.Info, $"Skipping {featureName} - not selected by user");
                    continue;
                }

                bool isActionOnly = options.ActionOnlySubsections.Contains(featureKey);
                var actionItems = BuildActionItems(options, featureName);

                var capturedFeatureName = featureName;
                var capturedSection = WithoutDeclinedOptions(section, options);

                featureTasks.Add(Task.Run(async () =>
                {
                    bool featureSuccess = true;

                    if (actionItems.Count > 0)
                    {
                        var actionSection = new ConfigSection
                        {
                            IsIncluded = true,
                            Items = actionItems
                        };

                        logService.Log(LogLevel.Info, $"Executing {actionItems.Count} action command(s) for {capturedFeatureName}");

                        var success = await bridgeService.ApplyConfigurationSectionAsync(
                            actionSection,
                            $"{groupName}.{capturedFeatureName}",
                            confirmationHandler);

                        if (!success)
                        {
                            featureSuccess = false;
                            logService.Log(LogLevel.Warning, $"Failed to apply action commands for {capturedFeatureName}");
                        }
                    }

                    if (!isActionOnly)
                    {
                        logService.Log(LogLevel.Info, $"Applying {capturedSection.Items.Count} settings from {groupName} > {capturedFeatureName}");

                        var success = await bridgeService.ApplyConfigurationSectionAsync(
                            capturedSection,
                            $"{groupName}.{capturedFeatureName}",
                            confirmationHandler);

                        if (!success)
                        {
                            featureSuccess = false;
                            logService.Log(LogLevel.Warning, $"Failed to apply some settings from {groupName} > {capturedFeatureName}");
                        }
                    }

                    onFeatureCompleted?.Invoke(capturedFeatureName);
                    return featureSuccess;
                }));
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
            var actionItems = BuildActionItems(options!, featureName);

            var capturedFeatureName = featureName;
            var capturedActionItems = actionItems;

            featureTasks.Add(Task.Run(async () =>
            {
                if (capturedActionItems.Count > 0)
                {
                    var actionSection = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = capturedActionItems
                    };

                    logService.Log(LogLevel.Info, $"Executing {capturedActionItems.Count} action command(s) for {capturedFeatureName} (not in config file)");

                    var success = await bridgeService.ApplyConfigurationSectionAsync(
                        actionSection,
                        $"{groupName}.{capturedFeatureName}",
                        confirmationHandler);

                    if (!success)
                    {
                        logService.Log(LogLevel.Warning, $"Failed to apply action commands for {capturedFeatureName}");
                        onFeatureCompleted?.Invoke(capturedFeatureName);
                        return false;
                    }
                }
                else
                {
                    logService.Log(LogLevel.Info, $"No action commands to execute for {capturedFeatureName}");
                }

                onFeatureCompleted?.Invoke(capturedFeatureName);
                return true;
            }));
        }

        var results = await Task.WhenAll(featureTasks);
        return results.All(r => r);
    }

    // An unticked import box has to drop the file's own item: the section applies it straight otherwise.
    private static ConfigSection WithoutDeclinedOptions(ConfigSection section, ImportOptions options)
    {
        var kept = section.Items.Where(item => !IsDeclined(item.Id, options)).ToList();
        return kept.Count == section.Items.Count
            ? section
            : new ConfigSection { IsIncluded = section.IsIncluded, Items = kept };
    }

    private static bool IsDeclined(string settingId, ImportOptions options) => settingId switch
    {
        SettingIds.CleanTaskbar => !options.ApplyCleanTaskbar,
        SettingIds.CleanStartMenu10 or SettingIds.CleanStartMenu11 => !options.ApplyCleanStartMenu,
        _ => false,
    };

    private List<ConfigurationItem> BuildActionItems(ImportOptions options, string featureName)
    {
        var items = new List<ConfigurationItem>();

        if (options?.ApplyCleanTaskbar == true && featureName == FeatureIds.Taskbar)
        {
            items.Add(new ConfigurationItem
            {
                Id = SettingIds.CleanTaskbar,
                Name = "Clean Taskbar",
                IsSelected = true,
                InputType = InputType.Toggle
            });
        }

        if (options?.ApplyCleanStartMenu == true && featureName == FeatureIds.StartMenu)
        {
            var settingId = windowsVersionService.IsWindows11() ? SettingIds.CleanStartMenu11 : SettingIds.CleanStartMenu10;
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

    private string BuildImportBatchHeader()
    {
        var label = localizationService.GetString("ChangeHistory_ConfigImport");
        var source = configImportState.SourceName;
        return string.IsNullOrEmpty(source) ? label : $"{label} ({source})";
    }
}
