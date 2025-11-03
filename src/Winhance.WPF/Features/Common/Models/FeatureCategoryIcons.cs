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

            ["Windows Apps"] = "MicrosoftWindows",
            ["External Apps"] = "PackageDown",

            ["Gaming & Performance"] = "Controller",
            ["Power"] = "Power",
            ["Privacy & Security"] = "Lock",
            ["Windows Update"] = "Sync",
            ["Notifications"] = "BellRing",
            ["Sound"] = "VolumeHigh",

            ["Windows Theme"] = "Brush",
            ["Taskbar"] = "DockBottom",
            ["Start Menu"] = "FileTableBoxOutline",
            ["Explorer"] = "Folder"
        };

        public static string GetIcon(string sectionNameOrKey) =>
            SectionIconMap.TryGetValue(sectionNameOrKey ?? string.Empty, out var icon)
                ? icon
                : "Cog";
    }
}
