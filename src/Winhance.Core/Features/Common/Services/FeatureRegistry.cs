using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Services
{
    public static class FeatureRegistry
    {
        public static readonly Dictionary<string, FeatureInfo[]> Features = new()
        {
            ["Customize"] = new FeatureInfo[]
            {
                new("WindowsTheme", "Windows Theme", "WindowsThemeCustomizationsViewModel", 1),
                new("Taskbar", "Taskbar", "TaskbarCustomizationsViewModel", 2),
                new("StartMenu", "Start Menu", "StartMenuCustomizationsViewModel", 3),
                new("ExplorerCustomization", "File Explorer", "ExplorerCustomizationsViewModel", 4),
            },
            ["Optimize"] = new FeatureInfo[]
            {
                new("Power", "Power", "PowerOptimizationsViewModel", 1),
                new("Privacy", "Privacy & Security", "PrivacyAndSecurityOptimizationsViewModel", 2),
                new("GamingPerformance", "Gaming & Performance", "GamingandPerformanceOptimizationsViewModel", 3),
                new("Notification", "Notifications", "NotificationOptimizationsViewModel", 4),
                new("Sound", "Sound", "SoundOptimizationsViewModel", 5),
                new("Update", "Windows Update", "UpdateOptimizationsViewModel", 6),
            },
            ["SoftwareApps"] = new FeatureInfo[]
            {
                new("WindowsApps", "Windows Apps", "WindowsAppsViewModel", 1),
                new("ExternalApps", "External Apps", "ExternalAppsViewModel", 2),
            },
        };

        public static FeatureInfo[] GetFeaturesForCategory(string category)
        {
            return Features.TryGetValue(category, out var features) ? features : new FeatureInfo[0];
        }

        public static FeatureInfo GetFeature(string moduleId)
        {
            foreach (var categoryFeatures in Features.Values)
            {
                foreach (var feature in categoryFeatures)
                {
                    if (feature.Id == moduleId)
                        return feature;
                }
            }
            return null;
        }
    }

    public record FeatureInfo(
        string Id,
        string DisplayName,
        string ViewModelTypeName,
        int SortOrder
    );
}
