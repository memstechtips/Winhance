using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Utilities;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// Manages the technical details panel: tooltip event subscription,
/// row population (registry, scheduled tasks, power config), and regedit launching.
/// </summary>
internal sealed class TechnicalDetailsManager : IDisposable
{
    private readonly Func<string> _getSettingId;
    private readonly Action<IReadOnlyList<TechnicalDetailSection>> _setSections;
    private readonly ILogService _logService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IRegeditLauncher? _regeditLauncher;
    private readonly ILocalizationService _localizationService;
    private readonly TechnicalDetailLabels _labels;
    private ISubscriptionToken? _subscription;

    public IRelayCommand<string> OpenRegeditCommand { get; }

    public TechnicalDetailsManager(
        Func<string> getSettingId,
        Action<IReadOnlyList<TechnicalDetailSection>> setSections,
        ILogService logService,
        IDispatcherService dispatcherService,
        IRegeditLauncher? regeditLauncher,
        IEventBus? eventBus,
        ILocalizationService localizationService,
        TechnicalDetailLabels? labels = null)
    {
        _getSettingId = getSettingId;
        _setSections = setSections;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _regeditLauncher = regeditLauncher;
        _localizationService = localizationService;
        _labels = labels ?? new TechnicalDetailLabels();

        OpenRegeditCommand = new RelayCommand<string>(OpenRegeditAtPath);

        if (eventBus != null)
            _subscription = eventBus.Subscribe<TooltipUpdatedEvent>(OnTooltipUpdated);
    }

    private void OnTooltipUpdated(TooltipUpdatedEvent evt)
    {
        if (evt.SettingId != _getSettingId()) return;
        _dispatcherService.RunOnUIThread(DispatcherQueuePriority.Low,
            () => UpdateTechnicalDetails(evt.TooltipData));
    }

    private void UpdateTechnicalDetails(SettingTooltipData tooltipData)
    {
        try
        {
            var registryRows  = BuildRegistryRows(tooltipData);
            var taskRows      = BuildScheduledTaskRows(tooltipData);
            var powerRows     = BuildPowerCfgRows(tooltipData);
            var scriptRows    = BuildPowerShellScriptRows(tooltipData);
            var regContentRows = BuildRegContentRows(tooltipData);
            var dependencyRows = BuildDependencyRows(tooltipData);

            var sections = new List<TechnicalDetailSection>();
            if (registryRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.Registry,         _labels.SectionRegistry,       true,  registryRows));
            if (taskRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.ScheduledTask,    _labels.SectionScheduledTasks, false, taskRows));
            if (powerRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.PowerConfig,      _labels.SectionPowerSettings,  false, powerRows));
            if (scriptRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.PowerShellScript, _labels.SectionScripts,        false, scriptRows));
            if (regContentRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.RegContent,       _labels.SectionRegContent,     false, regContentRows));
            if (dependencyRows.Count > 0)
                sections.Add(new TechnicalDetailSection(DetailRowType.Dependency,       _labels.SectionDependencies,   false, dependencyRows));

            _setSections(sections);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error,
                $"[TechnicalDetails] UpdateTechnicalDetails failed for '{_getSettingId()}': {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private List<TechnicalDetailRow> BuildRegistryRows(SettingTooltipData tooltipData)
    {
        var rows = new List<TechnicalDetailRow>();
        var setting = tooltipData.SettingDefinition;
        var isSelection = setting?.InputType == InputType.Selection;
        foreach (var kvp in tooltipData.IndividualRegistryValues)
        {
            var reg = kvp.Key;
            var keyExists = false;
            try
            {
                keyExists = _regeditLauncher?.KeyExists(reg.KeyPath) ?? false;
            }
            catch (Exception kex)
            {
                _logService.Log(LogLevel.Warning,
                    $"[TechnicalDetails] KeyExists failed for '{reg.KeyPath}': {kex.GetType().Name}: {kex.Message}");
            }

            // For Selection settings the per-entry RecommendedValue / DefaultValue are null;
            // the state lives on the ComboBoxOption whose IsRecommended / IsDefault flag is set.
            string recommendedColumn;
            string defaultColumn;
            if (isSelection && setting is not null)
            {
                recommendedColumn = ResolveSelectionColumnValue(setting, reg, wantRecommended: true, _labels);
                defaultColumn = ResolveSelectionColumnValue(setting, reg, wantRecommended: false, _labels);
            }
            else
            {
                recommendedColumn = ResolveRecommendedColumn(setting, reg);
                defaultColumn = reg.DefaultValue?.ToString() ?? FormatNotExist(reg);
            }

            rows.Add(new TechnicalDetailRow
            {
                RowType = DetailRowType.Registry,
                RegistryPath = reg.KeyPath,
                ValueName = reg.ValueName ?? "(Default)",
                ValueType = reg.ValueType.ToString(),
                CurrentValue = kvp.Value ?? FormatNotExist(reg),
                RecommendedValue = recommendedColumn,
                DefaultValue = defaultColumn,
                PathLabel = _labels.Path,
                ValueLabel = _labels.Value,
                CurrentLabel = _labels.Current,
                RecommendedLabel = _labels.Recommended,
                DefaultLabel = _labels.Default,
                OpenRegeditCommand = OpenRegeditCommand,
                RegeditIconSource = RegeditIconProvider.CachedIcon,
                CanOpenRegedit = keyExists
            });
        }
        return rows;
    }

    private List<TechnicalDetailRow> BuildScheduledTaskRows(SettingTooltipData tooltipData)
    {
        var rows = new List<TechnicalDetailRow>();
        foreach (var task in tooltipData.ScheduledTaskSettings)
        {
            rows.Add(new TechnicalDetailRow
            {
                RowType = DetailRowType.ScheduledTask,
                TaskPath = task.TaskPath,
                PathLabel = _labels.Path,
                CurrentLabel = _labels.Current,
                RecommendedLabel = _labels.Recommended,
                DefaultLabel = _labels.Default,
                CurrentState = tooltipData.CurrentSettingState switch
                {
                    true  => _labels.On,
                    false => _labels.Off,
                    _     => string.Empty
                },
                RecommendedState = task.RecommendedState switch
                {
                    true  => _labels.On,
                    false => _labels.Off,
                    _     => string.Empty
                },
                DefaultState = task.DefaultState switch
                {
                    true  => _labels.On,
                    false => _labels.Off,
                    _     => string.Empty
                }
            });
        }
        return rows;
    }

    private List<TechnicalDetailRow> BuildPowerCfgRows(SettingTooltipData tooltipData)
    {
        var rows = new List<TechnicalDetailRow>();
        foreach (var pcfg in tooltipData.PowerCfgSettings)
        {
            tooltipData.CurrentPowerValues.TryGetValue(pcfg, out var current);
            rows.Add(new TechnicalDetailRow
            {
                RowType       = DetailRowType.PowerConfig,
                CurrentLabel = _labels.Current,
                RecommendedLabel = _labels.Recommended,
                DefaultLabel = _labels.Default,
                SubgroupLabel = _labels.PowerCfgSubgroup,
                SettingLabel  = _labels.PowerCfgSetting,
                SubgroupGuid  = pcfg.SubgroupGuid,
                SettingGuid   = pcfg.SettingGuid,
                SubgroupAlias = pcfg.SubgroupGUIDAlias ?? "",
                SettingAlias  = pcfg.SettingGUIDAlias,
                PowerUnits    = pcfg.Units ?? "",
                CurrentAC     = current.AC?.ToString() ?? "",
                CurrentDC     = current.DC?.ToString() ?? "",
                RecommendedAC = pcfg.RecommendedValueAC?.ToString() ?? "",
                RecommendedDC = pcfg.RecommendedValueDC?.ToString() ?? "",
                DefaultAC     = pcfg.DefaultValueAC?.ToString() ?? "",
                DefaultDC     = pcfg.DefaultValueDC?.ToString() ?? "",
            });
        }
        return rows;
    }

    private List<TechnicalDetailRow> BuildPowerShellScriptRows(SettingTooltipData tooltipData)
    {
        var rows = new List<TechnicalDetailRow>();
        foreach (var s in tooltipData.PowerShellScripts)
        {
            if (!string.IsNullOrWhiteSpace(s.EnabledScript))
                rows.Add(new TechnicalDetailRow
                {
                    RowType     = DetailRowType.PowerShellScript,
                    ScriptLabel = _labels.ScriptOnEnable,
                    ScriptBody  = s.EnabledScript
                });
            if (!string.IsNullOrWhiteSpace(s.DisabledScript))
                rows.Add(new TechnicalDetailRow
                {
                    RowType     = DetailRowType.PowerShellScript,
                    ScriptLabel = _labels.ScriptOnDisable,
                    ScriptBody  = s.DisabledScript
                });
        }
        return rows;
    }

    private List<TechnicalDetailRow> BuildRegContentRows(SettingTooltipData tooltipData)
    {
        var rows = new List<TechnicalDetailRow>();
        foreach (var r in tooltipData.RegContents)
        {
            if (!string.IsNullOrWhiteSpace(r.EnabledContent))
                rows.Add(new TechnicalDetailRow
                {
                    RowType      = DetailRowType.RegContent,
                    ContentLabel = _labels.RegContentOnEnable,
                    ContentBody  = r.EnabledContent
                });
            if (!string.IsNullOrWhiteSpace(r.DisabledContent))
                rows.Add(new TechnicalDetailRow
                {
                    RowType      = DetailRowType.RegContent,
                    ContentLabel = _labels.RegContentOnDisable,
                    ContentBody  = r.DisabledContent
                });
        }
        return rows;
    }

    private List<TechnicalDetailRow> BuildDependencyRows(SettingTooltipData tooltipData)
    {
        var rows = new List<TechnicalDetailRow>();
        foreach (var dep in tooltipData.Dependencies)
        {
            var name = _localizationService.GetString($"Setting_{dep.RequiredSettingId}_Name");
            if (string.IsNullOrEmpty(name) || name == $"Setting_{dep.RequiredSettingId}_Name")
                name = dep.RequiredSettingId;  // fall back to id when no translation
            var relation = dep.DependencyType switch
            {
                SettingDependencyType.RequiresEnabled              => $"{_labels.DependencyEquals} {_labels.On}",
                SettingDependencyType.RequiresDisabled             => $"{_labels.DependencyEquals} {_labels.Off}",
                SettingDependencyType.RequiresSpecificValue        => $"{_labels.DependencyEquals} {dep.RequiredValue ?? string.Empty}",
                SettingDependencyType.RequiresValueBeforeAnyChange => $"{_labels.DependencyEquals} {dep.RequiredValue ?? string.Empty}",
                _ => string.Empty
            };
            rows.Add(new TechnicalDetailRow
            {
                RowType            = DetailRowType.Dependency,
                DependencyLabel    = name,
                DependencyRelation = relation
            });
        }
        return rows;
    }

    /// <summary>
    /// Resolves the per-registry-entry Recommended or Default column value for a Selection setting,
    /// sourced from the ComboBoxOption whose IsRecommended / IsDefault flag is set. The value itself
    /// lives in that option's ValueMappings keyed by the registry value name.
    /// </summary>
    /// <remarks>
    /// Returns <c>labels.ValueNotExist</c> in three distinct "absent" cases (the current
    /// TechnicalDetailLabels set doesn't have separate strings for each — this is intentional
    /// per Task A9's note about TODO label additions):
    /// <list type="bullet">
    ///   <item>ComboBox.Options is null or empty (malformed Selection setting).</item>
    ///   <item>No option has the requested flag set (e.g. no IsRecommended option).</item>
    ///   <item>The target option's ValueMappings doesn't contain the registry value name,
    ///     or maps it to an explicit null (meaning the key is deleted under that option).</item>
    /// </list>
    /// </remarks>
    private static string ResolveSelectionColumnValue(
        SettingDefinition setting,
        RegistrySetting reg,
        bool wantRecommended,
        TechnicalDetailLabels labels)
    {
        var options = setting.ComboBox?.Options;
        if (options is null || options.Count == 0) return labels.ValueNotExist;

        var target = options.FirstOrDefault(o => wantRecommended ? o.IsRecommended : o.IsDefault);
        if (target is null) return labels.ValueNotExist;

        var valueName = reg.ValueName ?? "KeyExists";
        if (target.ValueMappings is null || !target.ValueMappings.ContainsKey(valueName))
        {
            // Key absent from the target option's mapping: this entry is "unchanged" under that
            // option. Distinct label not yet added to TechnicalDetailLabels — use ValueNotExist.
            return labels.ValueNotExist;
        }

        var v = target.ValueMappings[valueName];
        if (v is null)
        {
            // Mapping is explicitly null: key would be deleted under this option.
            return labels.ValueNotExist;
        }
        return v.ToString()!;
    }

    private string ResolveRecommendedColumn(SettingDefinition? setting, RegistrySetting reg)
    {
        // Toggle-like settings with an explicit RecommendedToggleState render through
        // this branch regardless of reg.RecommendedValue, so the user always sees the
        // human-readable "(On)" / "(Off)" suffix. Maps the target state to the concrete
        // value via EnabledValue / DisabledValue, falling back to the null-sentinel
        // "doesn't exist" form when the target array carries the null sentinel.
        if (setting is not null
            && (setting.InputType == InputType.Toggle || setting.InputType == InputType.CheckBox)
            && setting.RecommendedToggleState.HasValue)
        {
            bool targetState = setting.RecommendedToggleState.Value;
            var targetArray = targetState ? reg.EnabledValue : reg.DisabledValue;
            var concreteValue = targetArray?.FirstOrDefault(v => v is not null);
            string stateLabel = targetState ? _labels.On : _labels.Off;
            if (concreteValue is not null)
                return $"{concreteValue} ({stateLabel})";
            return $"{_labels.ValueNotExist} ({stateLabel})";
        }
        return reg.RecommendedValue?.ToString() ?? FormatNotExist(reg);
    }

    private string FormatNotExist(RegistrySetting reg)
    {
        if (reg.EnabledValue?.Contains(null) == true)
            return $"{_labels.ValueNotExist} ({_labels.On})";
        if (reg.DisabledValue?.Contains(null) == true)
            return $"{_labels.ValueNotExist} ({_labels.Off})";
        return _labels.ValueNotExist;
    }

    private void OpenRegeditAtPath(string? path)
    {
        if (!string.IsNullOrEmpty(path))
            _regeditLauncher?.OpenAtPath(path);
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
    }
}
