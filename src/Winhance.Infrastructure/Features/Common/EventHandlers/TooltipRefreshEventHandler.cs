using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Features;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.EventHandlers;

public class TooltipRefreshEventHandler : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly ITooltipDataService _tooltipDataService;
    private readonly IGlobalSettingsRegistry _settingsRegistry;
    private readonly ILogService _logService;
    private ISubscriptionToken? _settingAppliedSubscriptionToken;
    private ISubscriptionToken? _featureComposedSubscriptionToken;

    public TooltipRefreshEventHandler(
        IEventBus eventBus,
        ITooltipDataService tooltipDataService,
        IGlobalSettingsRegistry settingsRegistry,
        ILogService logService)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _tooltipDataService = tooltipDataService ?? throw new ArgumentNullException(nameof(tooltipDataService));
        _settingsRegistry = settingsRegistry ?? throw new ArgumentNullException(nameof(settingsRegistry));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));

        _settingAppliedSubscriptionToken = eventBus.SubscribeAsync<SettingAppliedEvent>(HandleSettingAppliedAsync);
        _featureComposedSubscriptionToken = eventBus.SubscribeAsync<FeatureComposedEvent>(HandleFeatureComposedAsync);
    }

    private async Task HandleSettingAppliedAsync(SettingAppliedEvent settingAppliedEvent)
    {
        try
        {
            var settingItem = _settingsRegistry.GetSetting(settingAppliedEvent.SettingId);
            
            if (settingItem == null)
            {
                await Task.Delay(100).ConfigureAwait(false);
                settingItem = _settingsRegistry.GetSetting(settingAppliedEvent.SettingId);
            }
            
            if (settingItem is SettingDefinition settingDefinition)
            {
                var tooltipData = await _tooltipDataService.RefreshTooltipDataAsync(settingAppliedEvent.SettingId, settingDefinition).ConfigureAwait(false);
                if (tooltipData != null)
                {
                    _eventBus.Publish(new TooltipUpdatedEvent(settingAppliedEvent.SettingId, tooltipData));
                }

                // Refresh sibling composite-string settings that share the same registry value
                var siblings = FindCompositeStringSiblings(settingDefinition);
                foreach (var sibling in siblings)
                {
                    try
                    {
                        var siblingTooltip = await _tooltipDataService.RefreshTooltipDataAsync(sibling.Id, sibling).ConfigureAwait(false);
                        if (siblingTooltip != null)
                        {
                            _eventBus.Publish(new TooltipUpdatedEvent(sibling.Id, siblingTooltip));
                        }
                    }
                    catch (Exception siblingEx)
                    {
                        _logService.Log(LogLevel.Warning,
                            $"Failed to refresh sibling tooltip for '{sibling.Id}': {siblingEx.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Failed to refresh tooltip for '{settingAppliedEvent.SettingId}': {ex.Message}");
        }
    }

    private async Task HandleFeatureComposedAsync(FeatureComposedEvent featureComposedEvent)
    {
        try
        {
            var settingsList = featureComposedEvent.Settings.ToList();
            if (settingsList.Count == 0) return;

            var tooltipDataCollection = await _tooltipDataService.GetTooltipDataAsync(settingsList).ConfigureAwait(false);

            foreach (var kvp in tooltipDataCollection)
            {
                _eventBus.Publish(new TooltipUpdatedEvent(kvp.Key, kvp.Value));
            }

            _logService.Log(LogLevel.Info, $"Processed tooltip data for {tooltipDataCollection.Count}/{settingsList.Count} settings in {featureComposedEvent.ModuleId}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Failed to initialize tooltips for feature '{featureComposedEvent.ModuleId}': {ex.Message}");
        }
    }

    private List<SettingDefinition> FindCompositeStringSiblings(SettingDefinition appliedSetting)
    {
        var siblings = new List<SettingDefinition>();
        var compositeRegSettings = appliedSetting.RegistrySettings
            .Where(rs => rs.CompositeStringKey != null)
            .ToList();

        if (compositeRegSettings.Count == 0)
            return siblings;

        foreach (var setting in _settingsRegistry.GetAllSettings())
        {
            if (setting is not SettingDefinition def || def.Id == appliedSetting.Id)
                continue;

            foreach (var regSetting in compositeRegSettings)
            {
                if (def.RegistrySettings.Any(rs =>
                    rs.KeyPath == regSetting.KeyPath &&
                    rs.ValueName == regSetting.ValueName &&
                    rs.CompositeStringKey != null))
                {
                    siblings.Add(def);
                    break;
                }
            }
        }

        return siblings;
    }

    public void Dispose()
    {
        _settingAppliedSubscriptionToken?.Dispose();
        _featureComposedSubscriptionToken?.Dispose();
    }
}
