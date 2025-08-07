using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Registry;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Registry;
using Winhance.WPF.Features.Common.Events;
using Winhance.WPF.Features.Common.Services.Configuration;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Reactive tooltip data service that responds to registry change events
    /// </summary>
    public class ReactiveTooltipDataService : EventHandlerBase, IDisposable
    {
        private readonly IRegistryService _registryService;
        private readonly ILogService _logService;
        private readonly Dictionary<string, object?> _lastKnownValues = new Dictionary<string, object?>();

        /// <summary>
        /// Initializes a new instance of the ReactiveTooltipDataService
        /// </summary>
        /// <param name="registryService">The registry service</param>
        /// <param name="logService">The log service</param>
        /// <param name="eventBus">The event bus for domain events</param>
        public ReactiveTooltipDataService(
            IRegistryService registryService,
            ILogService logService,
            IEventBus eventBus)
            : base(eventBus, logService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            // Subscribe to registry change events
            SubscribeToRegistryEvents();
        }

        /// <summary>
        /// Gets tooltip data for the specified settings
        /// </summary>
        /// <param name="settings">The settings to get tooltip data for</param>
        /// <returns>A dictionary mapping setting IDs to tooltip data</returns>
        public async Task<Dictionary<string, SettingTooltipData>> GetTooltipDataAsync(IEnumerable<ApplicationSetting> settings)
        {
            var tooltipData = new Dictionary<string, SettingTooltipData>();

            try
            {
                foreach (var setting in settings)
                {
                    var data = await GetTooltipDataForSettingAsync(setting);
                    if (data != null)
                    {
                        tooltipData[setting.Id] = data;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting tooltip data: {ex.Message}");
            }

            return tooltipData;
        }

        /// <summary>
        /// Gets tooltip data for a single setting
        /// </summary>
        /// <param name="setting">The setting to get tooltip data for</param>
        /// <returns>The tooltip data</returns>
        private async Task<SettingTooltipData?> GetTooltipDataForSettingAsync(ApplicationSetting setting)
        {
            if (setting.RegistrySettings == null || !setting.RegistrySettings.Any())
                return null;

            try
            {
                var registrySetting = setting.RegistrySettings.First();
                
                // Get current registry value
                var hiveString = registrySetting.Hive switch
                {
                    Microsoft.Win32.RegistryHive.LocalMachine => "HKEY_LOCAL_MACHINE",
                    Microsoft.Win32.RegistryHive.CurrentUser => "HKEY_CURRENT_USER",
                    Microsoft.Win32.RegistryHive.ClassesRoot => "HKEY_CLASSES_ROOT",
                    Microsoft.Win32.RegistryHive.Users => "HKEY_USERS",
                    Microsoft.Win32.RegistryHive.CurrentConfig => "HKEY_CURRENT_CONFIG",
                    _ => registrySetting.Hive.ToString()
                };
                var keyPath = $"{hiveString}\\{registrySetting.SubKey}";
                var currentValue = _registryService.GetValue(keyPath, registrySetting.Name);

                var valueExists = _registryService.ValueExists(keyPath, registrySetting.Name);

                var keyExists = _registryService.KeyExists(keyPath);

                return new SettingTooltipData
                {
                    SettingId = setting.Id,
                    IndividualRegistryValues = new Dictionary<RegistrySetting, object?>
                    {
                        [registrySetting] = currentValue
                    },
                    CommandSettings = setting.CommandSettings
                };
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting tooltip data for setting {setting.Id}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Subscribes to registry change events
        /// </summary>
        private void SubscribeToRegistryEvents()
        {
            // Subscribe to registry value changed events using the event bus
            Subscribe<RegistryValueChangedEvent>(OnRegistryValueChanged);
        }

        /// <summary>
        /// Handles registry value change events
        /// </summary>
        /// <param name="event">The registry change event</param>
        private async void OnRegistryValueChanged(RegistryValueChangedEvent @event)
        {
            try
            {
                _logService.Log(
                    LogLevel.Info, 
                    $"ReactiveTooltipDataService received registry change for {@event.ValuePath}"
                );

                // Find affected settings and refresh their tooltip data
                await RefreshAffectedTooltipsAsync(@event.RegistrySetting);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error handling registry change: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes tooltip data for settings affected by a registry change
        /// </summary>
        /// <param name="changedSetting">The registry setting that changed</param>
        private async Task RefreshAffectedTooltipsAsync(RegistrySetting changedSetting)
        {
            try
            {
                // Registry cache is already cleared in the RegistryService.SetValue/DeleteValue methods
                // We just need to get the fresh value and update our internal state
                var keyPath = $"{RegistryExtensions.GetRegistryHiveString(changedSetting.Hive)}\\{changedSetting.SubKey}";
                
                // Get the fresh value from registry (cache is already cleared)
                var freshValue = _registryService.GetValue(keyPath, changedSetting.Name);
                _logService.Log(LogLevel.Info, $"Fresh registry value for tooltip: {keyPath}\\{changedSetting.Name} = {freshValue}");
                
                // Update our last known values cache
                string valuePath = $"{keyPath}\\{changedSetting.Name}";
                _lastKnownValues[valuePath] = freshValue;
                
                // Notify UI components that tooltip data has been updated by publishing an event
                var tooltipRefreshedEvent = new TooltipDataRefreshedEvent(
                    changedSetting.Hive, 
                    changedSetting.SubKey, 
                    changedSetting.Name
                );
                
                _logService.Log(LogLevel.Info, $"Publishing TooltipDataRefreshedEvent for {tooltipRefreshedEvent.RegistryPath}");
                PublishEvent(tooltipRefreshedEvent);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error refreshing tooltips: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes resources and unsubscribes from events
        /// </summary>
        public override void Dispose()
        {
            base.Dispose(); // This will unsubscribe all event handlers
            _lastKnownValues.Clear();
        }
    }


}
