using System;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Features;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.EventHandlers
{
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

            _settingAppliedSubscriptionToken = eventBus.Subscribe<SettingAppliedEvent>(HandleSettingApplied);
            _featureComposedSubscriptionToken = eventBus.Subscribe<FeatureComposedEvent>(HandleFeatureComposed);
        }

        private async void HandleSettingApplied(SettingAppliedEvent settingAppliedEvent)
        {
            try
            {
                var settingItem = _settingsRegistry.GetSetting(settingAppliedEvent.SettingId);
                
                if (settingItem == null)
                {
                    await Task.Delay(100);
                    settingItem = _settingsRegistry.GetSetting(settingAppliedEvent.SettingId);
                }
                
                if (settingItem is SettingDefinition settingDefinition)
                {
                    var tooltipData = await _tooltipDataService.RefreshTooltipDataAsync(settingAppliedEvent.SettingId, settingDefinition);
                    if (tooltipData != null)
                    {
                        _eventBus.Publish(new TooltipUpdatedEvent(settingAppliedEvent.SettingId, tooltipData));
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to refresh tooltip for '{settingAppliedEvent.SettingId}': {ex.Message}");
            }
        }

        private async void HandleFeatureComposed(FeatureComposedEvent featureComposedEvent)
        {
            try
            {
                var settingsList = featureComposedEvent.Settings.ToList();
                if (settingsList.Count == 0) return;

                var tooltipDataCollection = await _tooltipDataService.GetTooltipDataAsync(settingsList);

                foreach (var kvp in tooltipDataCollection)
                {
                    _eventBus.Publish(new TooltipUpdatedEvent(kvp.Key, kvp.Value));
                }

                _eventBus.Publish(new TooltipsBulkLoadedEvent(tooltipDataCollection));
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to initialize tooltips for feature '{featureComposedEvent.ModuleId}': {ex.Message}");
            }
        }

        public void Dispose()
        {
            _settingAppliedSubscriptionToken?.Dispose();
            _featureComposedSubscriptionToken?.Dispose();
        }
    }
}
