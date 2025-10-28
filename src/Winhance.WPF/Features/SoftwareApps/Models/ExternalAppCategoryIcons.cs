using System.Collections.Generic;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    public static class ExternalAppCategoryIcons
    {
        private static readonly Dictionary<string, string> CategoryIconMap = new()
        {
            ["Browsers"] = "Web",
            ["Compression"] = "ZipBox",
            ["Customization Utilities"] = "Palette",
            ["Development"] = "CodeBraces",
            ["Document Viewers"] = "FileDocument",
            ["File & Disk Management"] = "Folder",
            ["Gaming"] = "GamepadVariant",
            ["Imaging"] = "Image",
            ["Messaging, Email & Calendar"] = "Message",
            ["Privacy & Security"] = "Shield",
            ["Multimedia (Audio & Video)"] = "Play",
            ["Online Storage"] = "Cloud",
            ["Optical Disc Tools"] = "Disc",
            ["Other Utilities"] = "Tools",
            ["Remote Access"] = "Remote"
        };

        public static string GetIcon(string categoryName) =>
            CategoryIconMap.TryGetValue(categoryName, out var icon) ? icon : "Apps";
    }
}
