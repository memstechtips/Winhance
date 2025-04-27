using System.Collections.Generic;

namespace Winhance.WPF.Features.Common.Resources
{
    /// <summary>
    /// Provides access to Material Symbols icon Unicode values.
    /// </summary>
    public static class MaterialSymbols
    {
        private static readonly Dictionary<string, string> _iconMap = new Dictionary<string, string>
        {
            // Navigation icons
            { "RocketLaunch", "\uEB9B" },
            { "Rocket", "\uE837" },
            { "DeployedCode", "\uE8A7" },
            { "DeployedCodeUpdate", "\uE8A8" },
            { "CodeBraces", "\uE86F" },
            { "Routine", "\uEBB9" },
            { "ThemeLightDark", "\uE51C" },
            { "Palette", "\uE40A" },
            { "Information", "\uE88E" },
            { "MicrosoftWindows", "\uE950" },

            // Other common icons
            { "Apps", "\uE5C3" },
            { "Settings", "\uE8B8" },
            { "Close", "\uE5CD" },
            { "Menu", "\uE5D2" },
            { "Search", "\uE8B6" },
            { "Add", "\uE145" },
            { "Delete", "\uE872" },
            { "Edit", "\uE3C9" },
            { "Save", "\uE161" },
            { "Download", "\uE2C4" },
            { "Upload", "\uE2C6" },
            { "Refresh", "\uE5D5" },
            { "ArrowBack", "\uE5C4" },
            { "ArrowForward", "\uE5C8" },
            { "ChevronDown", "\uE5CF" },
            { "ChevronUp", "\uE5CE" },
            { "ChevronLeft", "\uE5CB" },
            { "ChevronRight", "\uE5CC" },
            { "ExpandMore", "\uE5CF" },
            { "ExpandLess", "\uE5CE" },
            { "MoreVert", "\uE5D4" },
            { "MoreHoriz", "\uE5D3" },
            { "Check", "\uE5CA" },
            { "Clear", "\uE14C" },
            { "Error", "\uE000" },
            { "Warning", "\uE002" },
            { "Info", "\uE88E" },
            { "Help", "\uE887" },
            { "HelpOutline", "\uE8FD" },
            { "Sync", "\uE627" },
            { "SyncDisabled", "\uE628" },
            { "SyncProblem", "\uE629" },
            { "Visibility", "\uE8F4" },
            { "VisibilityOff", "\uE8F5" },
            { "Lock", "\uE897" },
            { "LockOpen", "\uE898" },
            { "Star", "\uE838" },
            { "StarBorder", "\uE83A" },
            { "Favorite", "\uE87D" },
            { "FavoriteBorder", "\uE87E" },
            { "ThumbUp", "\uE8DC" },
            { "ThumbDown", "\uE8DB" },
            { "MicrosoftWindows", "\uE9AA" }
        };

        /// <summary>
        /// Gets the Unicode character for the specified icon name.
        /// </summary>
        /// <param name="iconName">The name of the icon.</param>
        /// <returns>The Unicode character for the icon, or a question mark if not found.</returns>
        public static string GetIcon(string iconName)
        {
            if (_iconMap.TryGetValue(iconName, out string iconChar))
            {
                return iconChar;
            }

            return "?"; // Return a question mark if the icon is not found
        }
    }
}
