using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Customize.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Services;

public class ConfigReviewOrchestrationService : IConfigReviewOrchestrationService
{
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IConfigReviewModeService _configReviewModeService;
    private readonly IConfigReviewDiffService _configReviewDiffService;
    private readonly IConfigImportOverlayService _overlayService;
    private readonly IConfigImportState _configImportState;
    private readonly IConfigAppSelectionService _configAppSelectionService;
    private readonly IConfigApplicationExecutionService _configExecutionService;
    private readonly IConfigLoadService _configLoadService;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly IEventBus _eventBus;
    private readonly ISettingReviewDiffApplier _reviewDiffApplier;
    private readonly SoftwareAppsViewModel _softwareAppsVM;
    private readonly WindowsAppsViewModel _windowsAppsVM;
    private readonly ExternalAppsViewModel _externalAppsVM;
    private readonly OptimizeViewModel _optimizeVM;
    private readonly CustomizeViewModel _customizeVM;

    public ConfigReviewOrchestrationService(
        ILogService logService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IConfigReviewModeService configReviewModeService,
        IConfigReviewDiffService configReviewDiffService,
        IConfigImportOverlayService overlayService,
        IConfigImportState configImportState,
        IConfigAppSelectionService configAppSelectionService,
        IConfigApplicationExecutionService configExecutionService,
        IConfigLoadService configLoadService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        IEventBus eventBus,
        ISettingReviewDiffApplier reviewDiffApplier,
        SoftwareAppsViewModel softwareAppsVM,
        WindowsAppsViewModel windowsAppsVM,
        ExternalAppsViewModel externalAppsVM,
        OptimizeViewModel optimizeVM,
        CustomizeViewModel customizeVM)
    {
        _logService = logService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _configReviewModeService = configReviewModeService;
        _configReviewDiffService = configReviewDiffService;
        _overlayService = overlayService;
        _configImportState = configImportState;
        _configAppSelectionService = configAppSelectionService;
        _configExecutionService = configExecutionService;
        _configLoadService = configLoadService;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _eventBus = eventBus;
        _reviewDiffApplier = reviewDiffApplier;
        _softwareAppsVM = softwareAppsVM;
        _windowsAppsVM = windowsAppsVM;
        _externalAppsVM = externalAppsVM;
        _optimizeVM = optimizeVM;
        _customizeVM = customizeVM;

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
        _eventBus.Publish(new ReviewModeExitedEvent());
        _logService.Log(LogLevel.Info, "Published ReviewModeExitedEvent to clear review state from all loaded settings");
    }

    /// <summary>
    /// Reapplies review diffs to all already-loaded SettingItemViewModels.
    /// Called when entering review mode a second time in the same session,
    /// since singleton VMs may still have settings loaded from the first import.
    /// </summary>
    private void ReapplyReviewDiffsToExistingSettings()
    {
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
                _reviewDiffApplier.ApplyReviewDiffToViewModel(setting, currentState);
            }
        }

        ReapplyToFeature(_optimizeVM.SoundViewModel);
        ReapplyToFeature(_optimizeVM.UpdateViewModel);
        ReapplyToFeature(_optimizeVM.NotificationViewModel);
        ReapplyToFeature(_optimizeVM.PrivacyViewModel);
        ReapplyToFeature(_optimizeVM.PowerViewModel);
        ReapplyToFeature(_optimizeVM.GamingViewModel);

        ReapplyToFeature(_customizeVM.ExplorerViewModel);
        ReapplyToFeature(_customizeVM.StartMenuViewModel);
        ReapplyToFeature(_customizeVM.TaskbarViewModel);
        ReapplyToFeature(_customizeVM.WindowsThemeViewModel);

        _logService.Log(LogLevel.Info, "Reapplied review diffs to all existing loaded settings");
    }

    public async Task EnterReviewModeAsync(UnifiedConfigurationFile config)
    {
        try
        {
            // Filter incompatible settings
            var incompatibleSettings = _configLoadService.DetectIncompatibleSettings(config);
            if (incompatibleSettings.Any())
            {
                config = _configLoadService.FilterConfigForCurrentSystem(config);
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
                await _configAppSelectionService.SelectWindowsAppsFromConfigAsync(config.WindowsApps);
                _logService.Log(LogLevel.Info, $"Pre-selected {config.WindowsApps.Items.Count} Windows Apps for review");
            }

            // Pre-select External Apps from config
            if (config.ExternalApps.Items.Count > 0)
            {
                await _configAppSelectionService.SelectExternalAppsFromConfigAsync(config.ExternalApps);
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
            bool hasWindowsApps = _windowsAppsVM.Items?.Any(a => a.IsSelected) == true;
            bool windowsAppsInstall = _softwareAppsVM.IsWindowsAppsInstallAction;
            bool windowsAppsRemove = _softwareAppsVM.IsWindowsAppsRemoveAction;
            if (hasWindowsApps && (windowsAppsInstall || windowsAppsRemove))
                selectedSections.Add("WindowsApps");

            // Check if any External Apps are selected and determine action
            bool hasExternalApps = _externalAppsVM.Items?.Any(a => a.IsSelected) == true;
            bool externalAppsInstall = _softwareAppsVM.IsExternalAppsInstallAction;
            bool externalAppsRemove = _softwareAppsVM.IsExternalAppsRemoveAction;
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
            importOptions = importOptions with { ActionOnlySubsections = actionOnlySubsections };

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
            if (hasExternalApps && _externalAppsVM.Items != null)
            {
                selectedExternalAppIds = _externalAppsVM.Items
                    .Where(a => a.IsSelected)
                    .Select(a => a.Id ?? a.Name)
                    .ToList();
            }

            // Confirm Windows Apps removal if remove action is chosen
            bool saveRemovalScripts = true;
            if (hasWindowsApps && windowsAppsRemove)
            {
                var (shouldContinue, saveScripts) = await _configAppSelectionService.ConfirmWindowsAppsRemovalAsync();
                saveRemovalScripts = saveScripts;
                if (!shouldContinue)
                {
                    await _configAppSelectionService.ClearWindowsAppsSelectionAsync();
                    selectedSections.Remove("WindowsApps");
                    _logService.Log(LogLevel.Info, "User cancelled Windows Apps removal during review apply");
                }
            }

            // Show overlay
            var overlayStatus = _localizationService.GetString("Config_Import_Status_Applying")
                ?? "Sit back, relax and watch while Winhance enhances Windows with your desired settings...";
            _overlayService.ShowOverlay(overlayStatus);

            _configImportState.IsActive = true;

            try
            {
                await _configExecutionService.ApplyConfigurationWithOptionsAsync(filteredConfig, selectedSections, importOptions);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying reviewed config: {ex.Message}");
            }
            finally
            {
                _configImportState.IsActive = false;
                _overlayService.HideOverlay();
            }

            // Exit review mode after applying
            _configReviewModeService.ExitReviewMode();

            // Show success message and wait for user dismissal
            await _dialogService.ShowInformationAsync(
                _localizationService.GetString("Config_Import_Success_Message") ?? "Configuration imported successfully.",
                _localizationService.GetString("Config_Import_Success_Title") ?? "Import Successful");

            // Process External Apps installation AFTER success dialog dismissal (needs UI thread)
            // Use captured user selections instead of config section to honor user's review choices
            if (selectedExternalAppIds != null && selectedExternalAppIds.Count > 0)
            {
                await _configAppSelectionService.ProcessExternalAppsFromUserSelectionAsync(selectedExternalAppIds);
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
        await _configAppSelectionService.ClearWindowsAppsSelectionAsync();

        foreach (var item in _externalAppsVM.Items)
            item.IsSelected = false;

        _configReviewModeService.ExitReviewMode();
        _logService.Log(LogLevel.Info, "Review mode cancelled - all selections cleared");
    }

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

    private static FeatureGroupSection FilterFeatureGroupByApprovals(
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
