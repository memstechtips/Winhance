using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Provides predefined power setting presets for different power plans.
    /// </summary>
    public static partial class PowerPlanPresets
    {
        /// <summary>
        /// Gets all available power plan presets with their recommended settings.
        /// </summary>
        /// <returns>Dictionary of power plan names to their setting values.</returns>
        public static Dictionary<string, Dictionary<string, object>> GetPresets()
        {
            return new Dictionary<string, Dictionary<string, object>>
            {
                ["Ultimate Performance"] = GetUltimatePerformancePreset(),
                ["High Performance"] = GetHighPerformancePreset(),
                ["Balanced"] = GetBalancedPreset(),
                ["Power Saver"] = GetPowerSaverPreset(),
                ["Gaming"] = GetGamingPreset()
            };
        }

        /// <summary>
        /// Gets the Ultimate Performance preset - maximum performance, no power saving.
        /// </summary>
        private static Dictionary<string, object> GetUltimatePerformancePreset()
        {
            return new Dictionary<string, object>
            {
                // Hard Disk - Never turn off
                ["diskidle"] = 0,
                
                // Sleep - Never sleep or hibernate
                ["standbyidle"] = 0,
                ["hibernateidle"] = 0,
                ["hybridsleep"] = 0,
                ["waketimers"] = 1, // Enable wake timers
                
                // Processor - Maximum performance
                ["procthrottlemin"] = 100, // 100% minimum processor state
                ["procthrottlemax"] = 100, // 100% maximum processor state
                ["syscoolingpolicy"] = 1,  // Active cooling
                
                // Display - Never turn off
                ["videoidle"] = 0,
                ["videoconlock"] = 0,
                
                // USB - Disable power saving
                ["usbselectivesuspend"] = 0,
                ["usbidle"] = 0,
                ["usb3linkpowermgmt"] = 0,
                
                // Wireless - Maximum performance
                ["wirelessidle"] = 0, // Maximum performance
                
                // PCI Express - No power saving
                ["aspm"] = 0, // Off
                
                // Desktop Background - Available
                ["slideshow"] = 1,
                
                // Power Buttons
                ["powerbutton"] = 2, // Do nothing
                ["sleepbutton"] = 2, // Do nothing
                ["lidaction"] = 2,   // Do nothing
            };
        }

        /// <summary>
        /// Gets the High Performance preset - high performance with minimal power saving.
        /// </summary>
        private static Dictionary<string, object> GetHighPerformancePreset()
        {
            return new Dictionary<string, object>
            {
                // Hard Disk - Turn off after 20 minutes
                ["diskidle"] = 20,
                
                // Sleep - Never sleep, hibernate after 3 hours
                ["standbyidle"] = 0,
                ["hibernateidle"] = 180,
                ["hybridsleep"] = 0,
                ["waketimers"] = 1,
                
                // Processor - High performance
                ["procthrottlemin"] = 100,
                ["procthrottlemax"] = 100,
                ["syscoolingpolicy"] = 1,
                
                // Display - Turn off after 20 minutes
                ["videoidle"] = 20,
                ["videoconlock"] = 0,
                
                // USB - Minimal power saving
                ["usbselectivesuspend"] = 0,
                ["usbidle"] = 0,
                ["usb3linkpowermgmt"] = 1,
                
                // Wireless - High performance
                ["wirelessidle"] = 0,
                
                // PCI Express - Moderate power saving
                ["aspm"] = 1, // Moderate power savings
                
                // Desktop Background
                ["slideshow"] = 1,
                
                // Power Buttons
                ["powerbutton"] = 1, // Sleep
                ["sleepbutton"] = 1, // Sleep
                ["lidaction"] = 1,   // Sleep
            };
        }

        /// <summary>
        /// Gets the Balanced preset - balanced performance and power saving.
        /// </summary>
        private static Dictionary<string, object> GetBalancedPreset()
        {
            return new Dictionary<string, object>
            {
                // Hard Disk - Turn off after 20 minutes
                ["diskidle"] = 20,
                
                // Sleep - Sleep after 30 minutes, hibernate after 3 hours
                ["standbyidle"] = 30,
                ["hibernateidle"] = 180,
                ["hybridsleep"] = 1,
                ["waketimers"] = 1,
                
                // Processor - Balanced
                ["procthrottlemin"] = 5,   // 5% minimum
                ["procthrottlemax"] = 100, // 100% maximum
                ["syscoolingpolicy"] = 0,  // Passive cooling
                
                // Display - Turn off after 15 minutes
                ["videoidle"] = 15,
                ["videoconlock"] = 0,
                
                // USB - Enabled power saving
                ["usbselectivesuspend"] = 1,
                ["usbidle"] = 1,
                ["usb3linkpowermgmt"] = 2,
                
                // Wireless - Balanced
                ["wirelessidle"] = 2, // Medium power saving
                
                // PCI Express - Moderate power saving
                ["aspm"] = 1,
                
                // Desktop Background
                ["slideshow"] = 1,
                
                // Power Buttons
                ["powerbutton"] = 1, // Sleep
                ["sleepbutton"] = 1, // Sleep
                ["lidaction"] = 1,   // Sleep
            };
        }

        /// <summary>
        /// Gets the Power Saver preset - maximum power saving.
        /// </summary>
        private static Dictionary<string, object> GetPowerSaverPreset()
        {
            return new Dictionary<string, object>
            {
                // Hard Disk - Turn off after 5 minutes
                ["diskidle"] = 5,
                
                // Sleep - Sleep after 15 minutes, hibernate after 1 hour
                ["standbyidle"] = 15,
                ["hibernateidle"] = 60,
                ["hybridsleep"] = 1,
                ["waketimers"] = 0, // Disable wake timers
                
                // Processor - Power saving
                ["procthrottlemin"] = 5,  // 5% minimum
                ["procthrottlemax"] = 50, // 50% maximum
                ["syscoolingpolicy"] = 0, // Passive cooling
                
                // Display - Turn off after 5 minutes
                ["videoidle"] = 5,
                ["videoconlock"] = 1,
                
                // USB - Maximum power saving
                ["usbselectivesuspend"] = 1,
                ["usbidle"] = 1,
                ["usb3linkpowermgmt"] = 3, // Maximum power savings
                
                // Wireless - Maximum power saving
                ["wirelessidle"] = 3,
                
                // PCI Express - Maximum power saving
                ["aspm"] = 2,
                
                // Desktop Background - Paused
                ["slideshow"] = 0,
                
                // Power Buttons
                ["powerbutton"] = 1, // Sleep
                ["sleepbutton"] = 1, // Sleep
                ["lidaction"] = 1,   // Sleep
            };
        }

        /// <summary>
        /// Gets the Gaming preset - optimized for gaming performance.
        /// </summary>
        private static Dictionary<string, object> GetGamingPreset()
        {
            return new Dictionary<string, object>
            {
                // Hard Disk - Never turn off during gaming
                ["diskidle"] = 0,
                
                // Sleep - Never sleep during gaming
                ["standbyidle"] = 0,
                ["hibernateidle"] = 0,
                ["hybridsleep"] = 0,
                ["waketimers"] = 1,
                
                // Processor - Maximum performance
                ["procthrottlemin"] = 100,
                ["procthrottlemax"] = 100,
                ["syscoolingpolicy"] = 1, // Active cooling for better temps
                
                // Display - Never turn off
                ["videoidle"] = 0,
                ["videoconlock"] = 0,
                
                // USB - Disable power saving for gaming peripherals
                ["usbselectivesuspend"] = 0,
                ["usbidle"] = 0,
                ["usb3linkpowermgmt"] = 0,
                
                // Wireless - Maximum performance for online gaming
                ["wirelessidle"] = 0,
                
                // PCI Express - No power saving for GPU
                ["aspm"] = 0,
                
                // Desktop Background - Available
                ["slideshow"] = 1,
                
                // Power Buttons - Prevent accidental sleep
                ["powerbutton"] = 2, // Do nothing
                ["sleepbutton"] = 2, // Do nothing
                ["lidaction"] = 2,   // Do nothing
            };
        }


        /// <summary>
        /// Gets the list of available preset names.
        /// </summary>
        /// <returns>List of preset names.</returns>
        public static List<string> GetPresetNames()
        {
            return GetPresets().Keys.ToList();
        }
    }
}