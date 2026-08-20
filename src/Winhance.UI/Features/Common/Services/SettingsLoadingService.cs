using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Services;

public class SettingsLoadingService : ISettingsLoadingService
{
    private readonly ICatalogSettingStateProvider _settingStateProvider;
    private readonly ILogService _logService;
    private readonly IInitializationService _initializationService;
    private readonly ICatalogSettingsRegistry _catalogSettingsRegistry;
    private readonly ICatalogScopeProvider _scopeProvider;
    private readonly IWindowsVersionService _windowsVersionService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ISettingViewModelFactory _viewModelFactory;
    private readonly ISettingLocalizationService _settingLocalizationService;
    private readonly ILocalizationService _localization;
    private readonly IApplicationModeService _applicationModeService;
    private readonly ISettingViewModelEnricher _enricher;

    public SettingsLoadingService(
        ICatalogSettingStateProvider settingStateProvider,
        ILogService logService,
        IInitializationService initializationService,
        ICatalogSettingsRegistry catalogSettingsRegistry,
        ICatalogScopeProvider scopeProvider,
        IWindowsVersionService windowsVersionService,
        IUserPreferencesService userPreferencesService,
        ISettingViewModelFactory viewModelFactory,
        ISettingLocalizationService settingLocalizationService,
        ILocalizationService localization,
        IApplicationModeService applicationModeService,
        ISettingViewModelEnricher enricher)
    {
        _settingStateProvider = settingStateProvider;
        _logService = logService;
        _initializationService = initializationService;
        _catalogSettingsRegistry = catalogSettingsRegistry;
        _scopeProvider = scopeProvider;
        _windowsVersionService = windowsVersionService;
        _userPreferencesService = userPreferencesService;
        _viewModelFactory = viewModelFactory;
        _settingLocalizationService = settingLocalizationService;
        _localization = localization;
        _applicationModeService = applicationModeService;
        _enricher = enricher;
    }

    public async Task<ObservableCollection<SettingItemViewModel>> LoadConfiguredSettingsAsync(
        string featureModuleId,
        string progressMessage,
        ISettingsFeatureViewModel? parentViewModel = null)
    {
        try
        {
            _logService.Log(LogLevel.Info, $"[SettingsLoadingService] Starting to load settings for '{featureModuleId}'");
            _initializationService.StartFeatureInitialization(featureModuleId);

            var settingsList = _catalogSettingsRegistry.GetByFeature(featureModuleId, _scopeProvider.Current);
            var settingViewModels = await BuildViewModelsAsync(featureModuleId, settingsList, parentViewModel);

            _logService.Log(LogLevel.Info, $"[SettingsLoadingService] Finished loading {settingViewModels.Count} settings for '{featureModuleId}'");
            _initializationService.CompleteFeatureInitialization(featureModuleId);

            return settingViewModels;
        }
        catch (Exception ex)
        {
            _initializationService.CompleteFeatureInitialization(featureModuleId);
            _logService.Log(LogLevel.Error, $"Error loading settings for {featureModuleId}: {ex.Message}");
            throw;
        }
    }

    public IReadOnlyList<string> GetFeatureSettingIds(string featureModuleId) =>
        _catalogSettingsRegistry.GetByFeature(featureModuleId, _scopeProvider.Current).Select(s => s.Id).ToList();

    // No Start/CompleteFeatureInitialization here: the feature is already initialized, and completing it a
    // second time would re-fire the startup handshake for a handful of cards.
    public async Task<IReadOnlyList<SettingItemViewModel>> LoadSettingsSubsetAsync(
        string featureModuleId,
        IReadOnlyCollection<string> settingIds,
        ISettingsFeatureViewModel? parentViewModel = null)
    {
        if (settingIds.Count == 0)
            return Array.Empty<SettingItemViewModel>();

        var wanted = new HashSet<string>(settingIds, StringComparer.Ordinal);
        var settingsList = _catalogSettingsRegistry
            .GetByFeature(featureModuleId, _scopeProvider.Current)
            .Where(s => wanted.Contains(s.Id))
            .ToList();

        return await BuildViewModelsAsync(featureModuleId, settingsList, parentViewModel);
    }

    // Two card fields are read off the catalog SCOPE rather than off the setting, so a scope change leaves
    // them stale on every card that survived it: HasBattery (authoring for other hardware shows the DC half
    // on a machine that has none) and the cross-group info message (its child list is scope-filtered, and two
    // of those children are Windows-10-only). Everything else on a card derives from the setting or from live
    // system state, and a scope change moves neither.
    public async Task RefreshScopeDerivedStateAsync(IEnumerable<SettingItemViewModel> settings)
    {
        foreach (var viewModel in settings)
        {
            if (viewModel.SupportsSeparateACDC)
            {
                bool hadBattery = viewModel.HasBattery;
                await _enricher.DetectBatteryAsync(viewModel);
                if (viewModel.HasBattery != hadBattery)
                {
                    viewModel.ComputeBadgeState();
                    viewModel.RefreshTechnicalDetails();
                }
            }

            if (viewModel.Setting is not { } setting)
                continue;

            var crossGroupInfoMessage = _settingLocalizationService.BuildCrossGroupInfoMessage(setting);
            if (crossGroupInfoMessage != viewModel.CrossGroupInfoMessage)
            {
                viewModel.CrossGroupInfoMessage = crossGroupInfoMessage;
                viewModel.UpdateStatusBanner(viewModel.SelectedValue);
            }
        }
    }

    // The card build shared by the full feature load and the incremental subset load.
    private async Task<ObservableCollection<SettingItemViewModel>> BuildViewModelsAsync(
        string featureModuleId,
        IReadOnlyList<Setting> settingsList,
        ISettingsFeatureViewModel? parentViewModel)
    {
        var settingViewModels = new ObservableCollection<SettingItemViewModel>();

        var showTechnicalDetails = await _userPreferencesService.GetPreferenceAsync(
            Core.Features.Common.Constants.UserPreferenceKeys.ShowTechnicalDetails, false);

        _logService.Log(LogLevel.Debug, $"Getting batch states for {settingsList.Count} settings in {featureModuleId}");
        // PERF-TRACE (temporary, 2026-08-20): remove with the commit that added it.
        var traceStart = System.Diagnostics.Stopwatch.GetTimestamp();
        var traceThreadIn = Environment.CurrentManagedThreadId;
        var batchStates = await _settingStateProvider.GetStatesAsync(settingsList);
        var traceStated = System.Diagnostics.Stopwatch.GetTimestamp();

        var liveBuild = LiveBuild();

        foreach (var setting in settingsList)
        {
            if (batchStates.TryGetValue(setting.Id, out var settingState) && !settingState.Success)
            {
                _logService.Log(LogLevel.Debug, $"Skipping setting '{setting.Id}': {settingState.ErrorMessage}");
                continue;
            }

            var currentState = batchStates.TryGetValue(setting.Id, out var s) ? s : new SettingStateResult();

            var crossGroupInfoMessage = _settingLocalizationService.BuildCrossGroupInfoMessage(setting);

            // Builder mode keeps the index-valued power-plan dropdown; the recorded choice reads the plan GUID
            // off the option's Tag.
            // Build it here from the DynamicOptions (the same runtime options the live GUID-valued
            // dropdown uses), index-valued + the rich PowerPlanComboBoxOption Tag the bespoke control reads.
            // The factory's builder block localizes the PowerPlan_ DisplayText, so this service passes the raw loc key.
            ComboBoxSetupResult? builderComboBoxOptions =
                (_applicationModeService.Capabilities().AuthorsIntent && setting.OptionSource is not null)
                    ? BuildBuilderPowerPlanOptions(currentState)
                    : null;

            var viewModel = await _viewModelFactory.CreateAsync(setting, currentState, parentViewModel, crossGroupInfoMessage, builderComboBoxOptions, LocalizeCompatibilityMessage(AvailabilityCompatibility.DeriveCompatibilityMessage(setting.Availability, liveBuild)), liveBuild);
            viewModel.IsTechnicalDetailsGloballyVisible = showTechnicalDetails;
            settingViewModels.Add(viewModel);
        }

        var traceBuilt = System.Diagnostics.Stopwatch.GetTimestamp();
        long freq = System.Diagnostics.Stopwatch.Frequency;
        _logService.Log(LogLevel.Info, $"PERF-TRACE load '{featureModuleId}' n={settingsList.Count} states={(traceStated - traceStart) * 1000 / freq}ms create={(traceBuilt - traceStated) * 1000 / freq}ms threadIn={traceThreadIn} threadOut={Environment.CurrentManagedThreadId}");

        return settingViewModels;
    }

    public async Task<Dictionary<string, SettingStateResult>> RefreshSettingStatesAsync(
        IEnumerable<SettingItemViewModel> settings)
    {
        var settingsList = settings.ToList();

        // Re-source the catalog Settings for this refresh from the catalog registry, keyed by each VM's owning
        // feature module and filtered to the VMs on screen - the same registry + scope as the initial load, so
        // the settings are identical.
        var wantedIds = new HashSet<string>(settingsList.Select(s => s.SettingId));
        var catalogSettings = settingsList
            .Select(s => s.ParentFeatureViewModel?.ModuleId)
            .Where(m => !string.IsNullOrEmpty(m))
            .Distinct()
            .SelectMany(m => _catalogSettingsRegistry.GetByFeature(m!, _scopeProvider.Current))
            .Where(c => wantedIds.Contains(c.Id))
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .ToList();

        if (catalogSettings.Count == 0)
            return new Dictionary<string, SettingStateResult>();

        var batchStates = await _settingStateProvider.GetStatesAsync(catalogSettings);

        return batchStates;
    }

    // The compatibility message arrives raw from the catalog derivation
    // (AvailabilityCompatibility.DeriveCompatibilityMessage; format: "Compatibility_Key|Arg1|Arg2..."). Localize it
    // here before passing it to the factory. A non-Compatibility_
    // message (incl. null) is returned unchanged.
    private string? LocalizeCompatibilityMessage(string? message)
    {
        if (message is { } compatKey && compatKey.StartsWith("Compatibility_"))
        {
            var parts = compatKey.Split('|');
            var key = parts[0];
            if (parts.Length > 1)
            {
                var args = parts.Skip(1).ToArray();
                try
                {
                    var format = _localization.GetString(key);
                    return string.Format(format, args);
                }
                catch
                {
                    return _localization.GetString(key);
                }
            }
            return _localization.GetString(key);
        }
        return message;
    }

    // Read ONCE per load (cached before the VM loop), not per setting.
    private WinBuild LiveBuild() =>
        new(_windowsVersionService.GetWindowsBuildNumber(), _windowsVersionService.GetWindowsBuildRevision());

    // INDEX-valued: the dropdown binds on the list index, but the recorded choice is a ChoiceValue.PowerPlan built
    // from Tag.Guid, so the index never leaves the UI. The Tag mirrors SettingItemViewModel.TryApplyDynamicPowerPlanOptions
    // (what the PowerPlanComboBox control reads): ExistsOnSystem/IsActive drive the visuals, SystemPlan.Guid is the
    // delete target, DisplayName is the raw PowerPlan_ key the delete dialog re-localizes. Empty (non-null) when
    // there are no runtime options.
    private static ComboBoxSetupResult BuildBuilderPowerPlanOptions(SettingStateResult state)
    {
        var result = new ComboBoxSetupResult { Success = true };
        if (state.DynamicOptions is not { } dynamicOptions)
            return result;

        int activeIndex = 0;
        bool foundActive = false;
        for (int i = 0; i < dynamicOptions.Count; i++)
        {
            var opt = dynamicOptions[i];
            var isActive = state.DynamicSelection != null
                && string.Equals(opt.Value, state.DynamicSelection, StringComparison.OrdinalIgnoreCase);
            // FIRST match wins (each option's Tag still carries its own per-option isActive
            // below).
            if (isActive && !foundActive)
            {
                activeIndex = i;
                foundActive = true;
            }

            var tag = new PowerPlanComboBoxOption
            {
                DisplayName = opt.Label,
                Guid = opt.Value,
                ExistsOnSystem = opt.ExistsOnSystem,
                IsActive = isActive,
                SystemPlan = opt.ExistsOnSystem
                    ? new Winhance.Core.Features.Optimize.Models.PowerPlan { Guid = opt.Value, Name = opt.Label, IsActive = isActive }
                    : null,
            };

            // Value = the option index (what the dropdown binds on); DisplayText = the raw PowerPlan_ loc key.
            result.Options.Add(new ComboBoxDisplayOption(
                opt.Label,
                i,
                opt.ExistsOnSystem ? "Installed on system" : "Not installed",
                tag));
        }

        result.SelectedValue = activeIndex;
        return result;
    }
}
