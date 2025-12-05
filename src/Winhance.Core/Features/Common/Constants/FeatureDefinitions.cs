using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Constants
{
    public static class FeatureDefinitions
    {
        public static readonly List<FeatureDefinition> All = new()
        {
            // Customize
            new(FeatureIds.WindowsTheme, "Windows Theme", "Brush", "Customize", 1),
            new(FeatureIds.Taskbar, "Taskbar", "DockBottom", "Customize", 2),
            new(FeatureIds.StartMenu, "Start Menu", "FileTableBoxOutline", "Customize", 3),
            new(FeatureIds.ExplorerCustomization, "Explorer", "Folder", "Customize", 4),

            // Optimize
            new(FeatureIds.Privacy, "Privacy & Security", "Lock", "Optimize", 1),
            new(FeatureIds.Power, "Power", "Power", "Optimize", 2),
            new(FeatureIds.GamingPerformance, "Gaming & Performance", "Controller", "Optimize", 3),
            new(FeatureIds.Update, "Windows Update", "Sync", "Optimize", 4),
            new(FeatureIds.Notifications, "Notifications", "BellRing", "Optimize", 5),
            new(FeatureIds.Sound, "Sound", "VolumeHigh", "Optimize", 6),

            // SoftwareApps
            new(FeatureIds.WindowsApps, "Windows Apps", "MicrosoftWindows", "SoftwareApps", 1),
            new(FeatureIds.ExternalApps, "External Apps", "PackageDown", "SoftwareApps", 2)
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
