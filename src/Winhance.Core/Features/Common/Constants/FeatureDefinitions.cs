using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Constants
{
    public static class FeatureDefinitions
    {
        public static readonly IReadOnlyList<FeatureDefinition> All = new List<FeatureDefinition>()
        {
            // Customize
            new(FeatureIds.WindowsTheme, "Windows Theme", "Customize"),
            new(FeatureIds.Taskbar, "Taskbar", "Customize"),
            new(FeatureIds.StartMenu, "Start Menu", "Customize"),
            new(FeatureIds.ExplorerCustomization, "Explorer", "Customize"),

            // Optimize
            new(FeatureIds.Privacy, "Privacy & Security", "Optimize"),
            new(FeatureIds.Power, "Power", "Optimize"),
            new(FeatureIds.GamingPerformance, "Gaming & Performance", "Optimize"),
            new(FeatureIds.Update, "Windows Update", "Optimize"),
            new(FeatureIds.Notifications, "Notifications", "Optimize"),
            new(FeatureIds.Sound, "Sound", "Optimize"),

            // SoftwareApps
            new(FeatureIds.WindowsApps, "Windows Apps", "SoftwareApps"),
            new(FeatureIds.ExternalApps, "External Apps", "SoftwareApps")
        };

        public static readonly HashSet<string> OptimizeFeatures = All
            .Where(f => f.Category == "Optimize")
            .Select(f => f.Id)
            .ToHashSet();

        public static readonly HashSet<string> CustomizeFeatures = All
            .Where(f => f.Category == "Customize")
            .Select(f => f.Id)
            .ToHashSet();

        public static FeatureDefinition? Get(string id) => All.FirstOrDefault(f => f.Id == id);
    }
}
