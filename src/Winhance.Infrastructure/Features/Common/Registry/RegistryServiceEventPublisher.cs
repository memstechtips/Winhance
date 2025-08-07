using System;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Registry;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    /// <summary>
    /// Partial class for RegistryService implementing event publishing
    /// </summary>
    public partial class RegistryService : IRegistryEventPublisher
    {

        /// <summary>
        /// Publishes a registry value changed event
        /// </summary>
        /// <param name="registrySetting">The registry setting that changed</param>
        /// <param name="oldValue">The old value</param>
        /// <param name="newValue">The new value</param>
        public void PublishRegistryValueChanged(RegistrySetting registrySetting, object? oldValue, object? newValue)
        {
            try
            {
                var registryEvent = new RegistryValueChangedEvent(registrySetting, oldValue, newValue);
                
                _logService.Log(
                    LogLevel.Info, 
                    $"Registry value changed: {registrySetting.Hive}\\{registrySetting.SubKey}\\{registrySetting.Name} " +
                    $"from {oldValue ?? "null"} to {newValue ?? "null"}"
                );
                
                // Publish the event through the event bus
                _eventBus.Publish(registryEvent);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error publishing registry change event: {ex.Message}");
            }
        }

        /// <summary>
        /// Internal method to publish registry changes from within registry operations
        /// </summary>
        /// <param name="registrySetting">The registry setting</param>
        /// <param name="oldValue">The old value</param>
        /// <param name="newValue">The new value</param>
        internal void OnRegistryValueChanged(RegistrySetting registrySetting, object? oldValue, object? newValue)
        {
            // Clear caches for this specific registry path
            ClearRegistryCachesForPath(registrySetting);
            
            // Publish the change event
            PublishRegistryValueChanged(registrySetting, oldValue, newValue);
        }

        /// <summary>
        /// Previously cleared registry caches for a specific registry setting
        /// Now just logs the registry setting access since caching has been removed
        /// </summary>
        /// <param name="registrySetting">The registry setting that was accessed</param>
        private void ClearRegistryCachesForPath(RegistrySetting registrySetting)
        {
            string path = $"{registrySetting.Hive}\\{registrySetting.SubKey}\\{registrySetting.Name}";
            _logService.Log(LogLevel.Debug, $"Registry setting accessed: {path}");
            // No caching - direct registry access only
        }
    }
}
