using System.Collections.Generic;

namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Represents a PowerCfg command setting.
    /// </summary>
    public class PowerCfgSetting
    {
        /// <summary>
        /// Gets or sets the command to execute.
        /// </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the command.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the value to use when the setting is enabled.
        /// </summary>
        public string EnabledValue { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the value to use when the setting is disabled.
        /// </summary>
        public string DisabledValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// Provides access to all available power plans.
    /// </summary>
    public static class PowerPlans
    {
        /// <summary>
        /// The Balanced power plan.
        /// </summary>
        public static readonly PowerPlan Balanced = new PowerPlan
        {
            Name = "Balanced",
            Guid = "381b4222-f694-41f0-9685-ff5bb260df2e",
            Description = "Automatically balances performance with energy consumption on capable hardware.",
        };

        /// <summary>
        /// The High Performance power plan.
        /// </summary>
        public static readonly PowerPlan HighPerformance = new PowerPlan
        {
            Name = "High Performance",
            Guid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
            Description = "Favors performance, but may use more energy.",
        };

        /// <summary>
        /// The Ultimate Performance power plan.
        /// </summary>
        public static readonly PowerPlan UltimatePerformance = new PowerPlan
        {
            Name = "Ultimate Performance",
            // This GUID is a placeholder and will be updated at runtime by PowerService
            Guid = "e9a42b02-d5df-448d-aa00-03f14749eb61",
            Description = "Provides ultimate performance on Windows.",
        };

        /// <summary>
        /// Gets a list of all available power plans.
        /// </summary>
        /// <returns>A list of all power plans.</returns>
        public static List<PowerPlan> GetAllPowerPlans()
        {
            return new List<PowerPlan> { Balanced, HighPerformance, UltimatePerformance };
        }
    }

    /// <summary>
    /// Provides power optimization settings and power setting catalog definitions.
    /// Main entry point for power optimizations - implemented as partial class.
    /// Catalog definitions: PowerOptimizations.Catalog.cs
    /// Factory methods: PowerOptimizations.Factory.cs  
    /// Preset logic: PowerOptimizations.Presets.cs
    /// </summary>
    public static partial class PowerOptimizations
    {
        // Note: All implementation moved to partial class files:
        // - PowerOptimizations.Catalog.cs (power setting definitions)
        // - PowerOptimizations.Factory.cs (factory and conversion methods)
        // - PowerOptimizations.Presets.cs (preset definitions and application)

        // This main file maintains backwards compatibility while enabling
        // proper separation of concerns through partial classes.
    }
}