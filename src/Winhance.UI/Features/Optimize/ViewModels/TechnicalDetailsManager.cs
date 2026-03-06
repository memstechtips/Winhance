using System;
using System.Collections.ObjectModel;
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
    private readonly Action<ObservableCollection<TechnicalDetailRow>> _setDetails;
    private readonly ILogService _logService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IRegeditLauncher? _regeditLauncher;
    private readonly TechnicalDetailLabels _labels;
    private ISubscriptionToken? _subscription;

    public IRelayCommand<string> OpenRegeditCommand { get; }

    public TechnicalDetailsManager(
        Func<string> getSettingId,
        Action<ObservableCollection<TechnicalDetailRow>> setDetails,
        ILogService logService,
        IDispatcherService dispatcherService,
        IRegeditLauncher? regeditLauncher,
        IEventBus? eventBus,
        TechnicalDetailLabels? labels = null)
    {
        _getSettingId = getSettingId;
        _setDetails = setDetails;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _regeditLauncher = regeditLauncher;
        _labels = labels ?? new TechnicalDetailLabels();

        OpenRegeditCommand = new RelayCommand<string>(OpenRegeditAtPath);

        if (eventBus != null)
            _subscription = eventBus.Subscribe<TooltipUpdatedEvent>(OnTooltipUpdated);
    }

    private void OnTooltipUpdated(TooltipUpdatedEvent evt)
    {
        if (evt.SettingId != _getSettingId()) return;
        // Use Low priority to defer to the next dispatcher cycle, avoiding
        // WinUI COMException when the collection is modified during a layout pass.
        _dispatcherService.RunOnUIThread(DispatcherQueuePriority.Low, () => UpdateTechnicalDetails(evt.TooltipData));
    }

    private void UpdateTechnicalDetails(SettingTooltipData tooltipData)
    {
        try
        {
            // Build the new collection off-screen (no UI bindings yet),
            // then swap it onto the ViewModel via PropertyChanged.
            // This avoids the WinUI COMException that occurs when mutating
            // a bound ObservableCollection during a layout pass or navigation.
            var newDetails = new ObservableCollection<TechnicalDetailRow>();

            // Registry rows
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

                newDetails.Add(new TechnicalDetailRow
                {
                    RowType = DetailRowType.Registry,
                    RegistryPath = reg.KeyPath,
                    ValueName = reg.ValueName ?? "(Default)",
                    ValueType = reg.ValueType.ToString(),
                    CurrentValue = kvp.Value ?? FormatNotExist(reg),
                    RecommendedValue = reg.RecommendedValue?.ToString() ?? FormatNotExist(reg),
                    DefaultValue = reg.DefaultValue?.ToString() ?? FormatNotExist(reg),
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

            // Scheduled task rows
            foreach (var task in tooltipData.ScheduledTaskSettings)
            {
                newDetails.Add(new TechnicalDetailRow
                {
                    RowType = DetailRowType.ScheduledTask,
                    TaskPath = task.TaskPath,
                    RecommendedState = task.RecommendedState == true ? "Enabled" : "Disabled"
                });
            }

            // Power config rows
            foreach (var pcfg in tooltipData.PowerCfgSettings)
            {
                newDetails.Add(new TechnicalDetailRow
                {
                    RowType = DetailRowType.PowerConfig,
                    SubgroupGuid = pcfg.SubgroupGuid,
                    SettingGuid = pcfg.SettingGuid,
                    SubgroupAlias = pcfg.SubgroupGUIDAlias ?? "",
                    SettingAlias = pcfg.SettingGUIDAlias,
                    PowerUnits = pcfg.Units ?? "",
                    RecommendedAC = pcfg.RecommendedValueAC?.ToString() ?? "",
                    RecommendedDC = pcfg.RecommendedValueDC?.ToString() ?? ""
                });
            }

            _setDetails(newDetails);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error,
                $"[TechnicalDetails] UpdateTechnicalDetails failed for '{_getSettingId()}': {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
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
