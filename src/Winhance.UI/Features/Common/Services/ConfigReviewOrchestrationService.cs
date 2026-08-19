using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.Common.Services;

public class ConfigReviewOrchestrationService : IConfigReviewOrchestrationService, IDisposable
{
    private bool _disposed;
    private WinhanceMode _previousMode = WinhanceMode.Normal;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IApplicationModeService _applicationModeService;
    private readonly IConfigReviewModeService _configReviewModeService;
    private readonly IConfigReviewDiffService _configReviewDiffService;
    private readonly IConfigImportOverlayService _overlayService;
    private readonly IConfigImportState _configImportState;
    private readonly IConfigAppSelectionService _configAppSelectionService;
    private readonly IConfigApplicationExecutionService _configExecutionService;
    private readonly IConfigLoadService _configLoadService;
    private readonly IEventBus _eventBus;
    private readonly IReviewModeViewModelCoordinator _vmCoordinator;
    private readonly IPolicyCleanupService _policyCleanupService;
    private readonly IChangeHistoryService _changeHistoryService;

    public ConfigReviewOrchestrationService(
        ILogService logService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IApplicationModeService applicationModeService,
        IConfigReviewModeService configReviewModeService,
        IConfigReviewDiffService configReviewDiffService,
        IConfigImportOverlayService overlayService,
        IConfigImportState configImportState,
        IConfigAppSelectionService configAppSelectionService,
        IConfigApplicationExecutionService configExecutionService,
        IConfigLoadService configLoadService,
        IEventBus eventBus,
        IReviewModeViewModelCoordinator vmCoordinator,
        IPolicyCleanupService policyCleanupService,
        IChangeHistoryService changeHistoryService)
    {
        _logService = logService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _applicationModeService = applicationModeService;
        _configReviewModeService = configReviewModeService;
        _configReviewDiffService = configReviewDiffService;
        _overlayService = overlayService;
        _configImportState = configImportState;
        _configAppSelectionService = configAppSelectionService;
        _configExecutionService = configExecutionService;
        _configLoadService = configLoadService;
        _eventBus = eventBus;
        _vmCoordinator = vmCoordinator;
        _policyCleanupService = policyCleanupService;
        _changeHistoryService = changeHistoryService;

        _configReviewModeService.ReviewModeChanged += OnReviewModeChanged;

        _previousMode = _applicationModeService.CurrentMode;
        _applicationModeService.ModeChanged += OnApplicationModeChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _configReviewModeService.ReviewModeChanged -= OnReviewModeChanged;
        _applicationModeService.ModeChanged -= OnApplicationModeChanged;
        GC.SuppressFinalize(this);
    }

    private void OnApplicationModeChanged(object? sender, EventArgs e)
    {
        var previous = _previousMode;
        var current = _applicationModeService.CurrentMode;
        _previousMode = current;

        // A mode that authors intent moves toggle/selection state on the shared settings VMs
        // without applying it, so any transition out of one leaves those positions stale and the
        // settings must be reloaded from live system state. Safe during review entry too: the
        // recreated ViewModels get their review decoration from SettingViewModelFactory, which
        // applies the eagerly computed diffs against fresh discovery state.
        //
        // Asked as a capability rather than as "was that Builder?" so a second authoring mode
        // inherits the reload by declaring its capabilities. Naming Builder here is how a future
        // mode would silently keep showing values it never applied.
        bool leftAnAuthoringMode =
            ModeCapabilities.For(previous).AuthorsIntent && !ModeCapabilities.For(current).AuthorsIntent;

        if (leftAnAuthoringMode)
        {
            _eventBus.Publish(new AuthoringModeExitedEvent());
            _logService.Log(LogLevel.Info, "Published AuthoringModeExitedEvent to reload settings from system state");
        }
    }

    private void OnReviewModeChanged(object? sender, EventArgs e)
    {
        if (_configReviewModeService.IsInReviewMode)
        {
            // Entering review straight from a mode that authored intent: skip the in-place reapply.
            // Those VMs still show authored, un-applied positions, and the applier's fallback diff
            // would read them as system truth and register false diffs. ReviewModeChanged fires
            // before ModeChanged, so _previousMode still holds the authoring mode here; the
            // ModeChanged handler then publishes AuthoringModeExitedEvent, and the reloaded
            // ViewModels get their review decoration from SettingViewModelFactory.
            //
            // The capability, not the mode name: what makes the reapply wrong is that the values on
            // screen were never applied, which is precisely what AuthorsIntent declares.
            if (ModeCapabilities.For(_previousMode).AuthorsIntent)
            {
                return;
            }

            _vmCoordinator.ReapplyReviewDiffsToExistingSettings();
            return;
        }

        ClearReviewStateFromAllSettings();
    }

    private void ClearReviewStateFromAllSettings()
    {
        _eventBus.Publish(new ReviewModeExitedEvent());
        _logService.Log(LogLevel.Info, "Published ReviewModeExitedEvent to clear review state from all loaded settings");
    }


    public async Task EnterReviewModeAsync(WinhanceConfigFile config, bool isWindowsDefaults = false)
    {
        try
        {
            var incompatibleSettings = _configLoadService.DetectIncompatibleSettings(config);
            if (incompatibleSettings.Count > 0)
            {
                config = _configLoadService.FilterConfigForCurrentSystem(config);
                _logService.Log(LogLevel.Info, $"Silently filtered {incompatibleSettings.Count} incompatible settings from config");
            }

            // Review-entry filter forcing is carried solely by the async ForceFilterOn chain
            // (MainWindowViewModel forces the filter on when the review bar flips). The brief
            // mid-entry window before that chain lands is accepted and self-healing.

            await _configReviewModeService.EnterReviewModeAsync(config, isWindowsDefaults);

            if (config.WindowsApps.Items.Count > 0)
            {
                await _configAppSelectionService.SelectWindowsAppsFromConfigAsync(config.WindowsApps);
                _logService.Log(LogLevel.Info, $"Pre-selected {config.WindowsApps.Items.Count} Windows Apps for review");
            }

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
            _dialogService.ShowMessage(_localizationService.GetString("Config_Review_EnterError", ex.Message), _localizationService.GetString("Dialog_Error"));
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
            var selectedSections = new List<string>();

            bool hasWindowsApps = _vmCoordinator.HasSelectedWindowsApps;
            bool windowsAppsInstall = _vmCoordinator.IsWindowsAppsInstallAction;
            bool windowsAppsRemove = _vmCoordinator.IsWindowsAppsRemoveAction;
            if (hasWindowsApps && (windowsAppsInstall || windowsAppsRemove))
                selectedSections.Add("WindowsApps");

            bool hasExternalApps = _vmCoordinator.HasSelectedExternalApps;
            bool externalAppsInstall = _vmCoordinator.IsExternalAppsInstallAction;
            bool externalAppsRemove = _vmCoordinator.IsExternalAppsRemoveAction;
            if (hasExternalApps && (externalAppsInstall || externalAppsRemove))
                selectedSections.Add("ExternalApps");

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

            var importOptions = new ImportOptions
            {
                ProcessWindowsAppsRemoval = hasWindowsApps && windowsAppsRemove,
                ProcessWindowsAppsInstallation = hasWindowsApps && windowsAppsInstall,
                ProcessExternalAppsInstallation = hasExternalApps && externalAppsInstall,
                ProcessExternalAppsRemoval = hasExternalApps && externalAppsRemove,
                ApplyThemeWallpaper = approvedDiffs.Any(d => d.SettingId == SettingIds.ThemeModeWindows && d.IsActionApproved),
                ApplyCleanTaskbar = approvedSettingIds.Contains(SettingIds.TaskbarClean),
                ApplyCleanStartMenu = approvedSettingIds.Contains(SettingIds.StartMenuCleanWin10) || approvedSettingIds.Contains(SettingIds.StartMenuCleanWin11),
            };

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

            if (selectedSections.Count == 0)
            {
                _dialogService.ShowMessage(
                    _localizationService.GetStringOrDefault("Config_Import_Error_NoSelection", "No changes to apply."),
                    _localizationService.GetStringOrDefault("Config_Import_Error_NoSelection_Title", "No Changes"));
                return;
            }

            var filteredConfig = BuildFilteredConfigFromApprovals(config, approvedSettingIds);

            // Capture current external app UI selections BEFORE exiting review mode
            // This preserves user's checkbox changes made during review
            List<string>? selectedExternalAppIds = null;
            if (hasExternalApps)
            {
                selectedExternalAppIds = _vmCoordinator.GetSelectedExternalAppIds();
            }

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

            var overlayStatus = _localizationService.GetStringOrDefault("Config_Import_Status_Applying", "Sit back, relax and watch while Winhance enhances Windows with your desired settings...");
            _overlayService.ShowOverlay(overlayStatus);

            _configImportState.IsActive = true;
            _configImportState.ImportSuppliesPowerValues = false;
            var changeBatch = _changeHistoryService.BeginBatch(BuildImportBatchHeader());

            try
            {
                await _configExecutionService.ApplyConfigurationWithOptionsAsync(filteredConfig, selectedSections, importOptions);

                // When restoring Windows defaults, clean up all policy registry keys AFTER
                // applying settings, because applying "disabled" values can re-create the keys
                if (_configReviewModeService.IsWindowsDefaults)
                {
                    _logService.Log(LogLevel.Info, "Windows Defaults import (review mode): cleaning up policy registry keys");
                    await Task.Run(() => _policyCleanupService.CleanupPolicyKeys());
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying reviewed config: {ex.Message}");
            }
            finally
            {
                _configImportState.IsActive = false;
                _configImportState.ImportSuppliesPowerValues = false;
                changeBatch.Dispose();
                _overlayService.HideOverlay();
            }

            _configReviewModeService.ExitReviewMode();

            await _dialogService.ShowInformationAsync(
                _localizationService.GetStringOrDefault("Config_Import_Success_Message", "Configuration imported successfully."),
                _localizationService.GetStringOrDefault("Config_Import_Success_Title", "Import Successful"));

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
            _dialogService.ShowMessage(_localizationService.GetString("Config_Apply_Error", ex.Message), _localizationService.GetString("Dialog_Error"));
        }
    }

    public async Task CancelReviewModeAsync()
    {
        if (!_configReviewModeService.IsInReviewMode) return;

        // Preserve app selections on cancel — only exit review mode.
        // Clearing selections was destructive: users who imported a config and clicked
        // Cancel would lose all their carefully chosen checkboxes in Software & Apps.
        // Cancel means "cancel the review operation", not "discard my selections".
        // Review diffs and badges are still cleaned up via ReviewModeExitedEvent
        // fired by ExitReviewMode() through OnReviewModeChanged.

        _configReviewModeService.ExitReviewMode();
        _logService.Log(LogLevel.Info, "Review mode cancelled - selections preserved");
    }

    private string BuildImportBatchHeader()
    {
        var label = _localizationService.GetString("ChangeHistory_ConfigImport");
        var source = _configImportState.SourceName;
        return string.IsNullOrEmpty(source) ? label : $"{label} ({source})";
    }

    private WinhanceConfigFile BuildFilteredConfigFromApprovals(
        WinhanceConfigFile original,
        HashSet<string> approvedSettingIds)
    {
        var filtered = new WinhanceConfigFile
        {
            Version = original.Version,
            CreatedAt = original.CreatedAt,
            WindowsApps = original.WindowsApps,   // Apps are filtered by checkbox selection, not diffs
            ExternalApps = original.ExternalApps,
        };

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

            if (approvedItems.Count > 0)
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
