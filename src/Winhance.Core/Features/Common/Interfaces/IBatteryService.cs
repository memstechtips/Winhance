using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for detecting and querying battery information.
    /// </summary>
    public interface IBatteryService
    {
        /// <summary>
        /// Checks if the system has a battery.
        /// </summary>
        /// <returns>True if a battery is present, false otherwise.</returns>
        Task<bool> HasBatteryAsync();

        /// <summary>
        /// Gets the current battery charge percentage if available.
        /// </summary>
        /// <returns>Battery charge percentage (0-100) or null if no battery is present.</returns>
        Task<int?> GetBatteryPercentageAsync();

        /// <summary>
        /// Gets the current power source.
        /// </summary>
        /// <returns>True if running on battery power, false if on AC power.</returns>
        Task<bool> IsRunningOnBatteryAsync();
        
        /// <summary>
        /// Checks if the system is a laptop/portable device with a lid.
        /// </summary>
        /// <returns>True if the device has a lid (is a laptop), false otherwise.</returns>
        /// <remarks>
        /// This is useful for determining if lid-related settings should be shown,
        /// even if the laptop doesn't currently have a battery installed.
        /// </remarks>
        Task<bool> HasLidAsync();
    }
}
