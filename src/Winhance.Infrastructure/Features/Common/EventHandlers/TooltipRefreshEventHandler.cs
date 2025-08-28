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
    /// <summary>
    /// Event handler that refreshes tooltip data when a setting is applied or when a feature is composed.
    /// This handler runs independently from the main setting application flow,
    /// ensuring clean separation of concerns and maintaining tooltip data freshness.
    /// </summary>
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
            
            // Subscribe to SettingAppliedEvent (for individual setting updates)
            _settingAppliedSubscriptionToken = eventBus.Subscribe<SettingAppliedEvent>(HandleSettingApplied);
            _logService.Log(LogLevel.Debug, "TooltipRefreshEventHandler: Subscribed to SettingAppliedEvent");

            // Subscribe to FeatureComposedEvent (for bulk tooltip initialization)
            _featureComposedSubscriptionToken = eventBus.Subscribe<FeatureComposedEvent>(HandleFeatureComposed);
            _logService.Log(LogLevel.Debug, "TooltipRefreshEventHandler: Subscribed to FeatureComposedEvent");
        }

        private async void HandleSettingApplied(SettingAppliedEvent settingAppliedEvent)
        {
            try
            {
                _logService.Log(LogLevel.Debug, $"TooltipRefreshEventHandler: Refreshing tooltip for setting '{settingAppliedEvent.SettingId}'");

                // Get the application setting for the tooltip service
                var settingItem = _settingsRegistry.GetSetting(settingAppliedEvent.SettingId);
                if (settingItem is SettingDefinition SettingDefinition)
                {
                    // Refresh tooltip data asynchronously 
                    var tooltipData = await _tooltipDataService.RefreshTooltipDataAsync(settingAppliedEvent.SettingId, SettingDefinition);
                    if (tooltipData != null)
                    {
                        // Publish TooltipUpdatedEvent so UI can react to the change
                        _eventBus.Publish(new TooltipUpdatedEvent(settingAppliedEvent.SettingId, tooltipData));
                        _logService.Log(LogLevel.Debug, $"TooltipRefreshEventHandler: Published TooltipUpdatedEvent for '{settingAppliedEvent.SettingId}'");
                    }
                    _logService.Log(LogLevel.Debug, $"TooltipRefreshEventHandler: Successfully refreshed tooltip data for '{settingAppliedEvent.SettingId}'");
                }
                else if (settingItem != null)
                {
                    _logService.Log(LogLevel.Warning, $"TooltipRefreshEventHandler: Setting '{settingAppliedEvent.SettingId}' found but is not an SettingDefinition (type: {settingItem.GetType().Name})");
                }
                else
                {
                    _logService.Log(LogLevel.Warning, $"TooltipRefreshEventHandler: Could not find setting for '{settingAppliedEvent.SettingId}'");
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"TooltipRefreshEventHandler: Failed to refresh tooltip for '{settingAppliedEvent.SettingId}': {ex.Message}");
                // Don't rethrow - tooltip refresh failures shouldn't affect other event handlers
            }
        }

        /// <summary>
        /// Handles the FeatureComposedEvent by bulk initializing tooltips for the feature's settings.
        /// </summary>
        private async void HandleFeatureComposed(FeatureComposedEvent featureComposedEvent)
        {
            try
            {
                var settingsList = featureComposedEvent.Settings.ToList();
                _logService.Log(LogLevel.Info, $"TooltipRefreshEventHandler: Initializing tooltips for {settingsList.Count} settings in feature '{featureComposedEvent.ModuleId}'");

                if (settingsList.Count == 0)
                {
                    _logService.Log(LogLevel.Debug, $"TooltipRefreshEventHandler: No settings to initialize tooltips for in feature '{featureComposedEvent.ModuleId}'");
                    return;
                }

                // Bulk load tooltip data for all settings in the feature
                var tooltipDataCollection = await _tooltipDataService.GetTooltipDataAsync(settingsList);

                _logService.Log(LogLevel.Info, $"TooltipRefreshEventHandler: Successfully loaded tooltip data for {tooltipDataCollection.Count} settings in feature '{featureComposedEvent.ModuleId}'");

                // Publish individual TooltipUpdatedEvents for each setting
                foreach (var kvp in tooltipDataCollection)
                {
                    _eventBus.Publish(new TooltipUpdatedEvent(kvp.Key, kvp.Value));
                }

                // Publish bulk loaded event for UI components that need it
                _eventBus.Publish(new TooltipsBulkLoadedEvent(tooltipDataCollection));

                _logService.Log(LogLevel.Info, $"TooltipRefreshEventHandler: Published tooltip events for {tooltipDataCollection.Count} settings in feature '{featureComposedEvent.ModuleId}'");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"TooltipRefreshEventHandler: Failed to initialize tooltips for feature '{featureComposedEvent.ModuleId}': {ex.Message}");
                // Don't rethrow - tooltip initialization failures shouldn't affect other event handlers
            }
        }

        public void Dispose()
        {
            if (_settingAppliedSubscriptionToken != null)
            {
                _logService.Log(LogLevel.Debug, "TooltipRefreshEventHandler: Unsubscribing from SettingAppliedEvent");
                _settingAppliedSubscriptionToken = null;
            }

            if (_featureComposedSubscriptionToken != null)
            {
                _logService.Log(LogLevel.Debug, "TooltipRefreshEventHandler: Unsubscribing from FeatureComposedEvent");
                _featureComposedSubscriptionToken = null;
            }
        }
    }
}
