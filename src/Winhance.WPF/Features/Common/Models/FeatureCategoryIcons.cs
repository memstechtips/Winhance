using System.Collections.Generic;

namespace Winhance.WPF.Features.Common.Models
{
    public static class FeatureCategoryIcons
    {
        private static readonly Dictionary<string, string> SectionIconMap = new()
        {
            ["Software & Apps"] = "PackageVariant",
            ["Optimization Settings"] = "RocketLaunch",
            ["Customization Settings"] = "Palette",

            ["WindowsApps"] = "MicrosoftWindows",
            ["Windows Apps"] = "MicrosoftWindows",
            ["ExternalApps"] = "ApplicationCog",
            ["External Apps"] = "ApplicationCog",

            ["GamingPerformance"] = "Controller",
            ["Gaming and Performance"] = "Controller",
            ["PowerSettings"] = "Power",
            ["Power Settings"] = "Power",
            ["WindowsSecurity"] = "Security",
            ["Windows Security Settings"] = "Security",
            ["PrivacySettings"] = "Lock",
            ["Privacy Settings"] = "Lock",
            ["WindowsUpdates"] = "Update",
            ["Windows Updates"] = "Update",
            ["Explorer"] = "Folder",
            ["Notifications"] = "BellRing",
            ["Sound"] = "VolumeHigh",

            ["WindowsTheme"] = "Brush",
            ["Windows Theme"] = "Brush",
            ["Taskbar"] = "DockBottom",
            ["StartMenu"] = "FileTableBoxOutline",
            ["Start Menu"] = "FileTableBoxOutline",
            ["Customize_Explorer"] = "Folder"
        };

        public static string GetIcon(string sectionNameOrKey) =>
            SectionIconMap.TryGetValue(sectionNameOrKey ?? string.Empty, out var icon)
                ? icon
                : "Cog";
    }
}
