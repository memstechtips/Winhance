using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for publishing registry change events
    /// </summary>
    public interface IRegistryEventPublisher
    {
        /// <summary>
        /// Publishes a registry value changed event
        /// </summary>
        /// <param name="registrySetting">The registry setting that changed</param>
        /// <param name="oldValue">The old value</param>
        /// <param name="newValue">The new value</param>
        void PublishRegistryValueChanged(RegistrySetting registrySetting, object? oldValue, object? newValue);
    }
}
