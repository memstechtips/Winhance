using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly ObservableCollection<TechnicalDetailRow> _details;
    private readonly Action _onDetailsChanged;
    private readonly ILogService _logService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IRegeditLauncher? _regeditLauncher;
    private ISubscriptionToken? _subscription;

    public IRelayCommand<string> OpenRegeditCommand { get; }

    public TechnicalDetailsManager(
        Func<string> getSettingId,
        ObservableCollection<TechnicalDetailRow> details,
        Action onDetailsChanged,
        ILogService logService,
        IDispatcherService dispatcherService,
        IRegeditLauncher? regeditLauncher,
        IEventBus? eventBus)
    {
        _getSettingId = getSettingId;
        _details = details;
        _onDetailsChanged = onDetailsChanged;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _regeditLauncher = regeditLauncher;

        OpenRegeditCommand = new RelayCommand<string>(OpenRegeditAtPath);

        if (eventBus != null)
            _subscription = eventBus.Subscribe<TooltipUpdatedEvent>(OnTooltipUpdated);
    }

    private void OnTooltipUpdated(TooltipUpdatedEvent evt)
    {
        if (evt.SettingId != _getSettingId()) return;
        _dispatcherService.RunOnUIThread(() => UpdateTechnicalDetails(evt.TooltipData));
    }

    private void UpdateTechnicalDetails(SettingTooltipData tooltipData)
    {
        try
        {
            _details.Clear();

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

                _details.Add(new TechnicalDetailRow
                {
                    RowType = DetailRowType.Registry,
                    RegistryPath = reg.KeyPath,
                    ValueName = reg.ValueName ?? "(Default)",
                    ValueType = reg.ValueType.ToString(),
                    CurrentValue = kvp.Value ?? "(not set)",
                    RecommendedValue = reg.RecommendedValue?.ToString() ?? "",
                    OpenRegeditCommand = OpenRegeditCommand,
                    RegeditIconSource = RegeditIconProvider.CachedIcon,
                    CanOpenRegedit = keyExists
                });
            }

            // Scheduled task rows
            foreach (var task in tooltipData.ScheduledTaskSettings)
            {
                _details.Add(new TechnicalDetailRow
                {
                    RowType = DetailRowType.ScheduledTask,
                    TaskPath = task.TaskPath,
                    RecommendedState = task.RecommendedState == true ? "Enabled" : "Disabled"
                });
            }

            // Power config rows
            foreach (var pcfg in tooltipData.PowerCfgSettings)
            {
                _details.Add(new TechnicalDetailRow
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

            _onDetailsChanged();
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error,
                $"[TechnicalDetails] UpdateTechnicalDetails failed for '{_getSettingId()}': {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
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
