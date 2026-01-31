using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts icon names to Segoe MDL2 Assets glyphs for use with FontIcon.
/// Replaces the WPF MahApps.Metro.IconPacks which are not available in WinUI 3.
/// </summary>
public class FluentIconConverter : IValueConverter
{
    private static readonly Dictionary<string, string> IconMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Navigation
        ["Apps"] = "\uE71D",
        ["RocketLaunch"] = "\uE7C1",
        ["Palette"] = "\uE771",
        ["Wrench"] = "\uE90F",
        ["Cog"] = "\uE713",
        ["Settings"] = "\uE713",
        ["DotsHorizontal"] = "\uE712",
        ["More"] = "\uE712",

        // Actions
        ["Save"] = "\uE74E",
        ["Open"] = "\uE8E5",
        ["Import"] = "\uE8B5",
        ["Export"] = "\uEDE1",
        ["Delete"] = "\uE74D",
        ["Trash"] = "\uE74D",
        ["Edit"] = "\uE70F",
        ["Copy"] = "\uE8C8",
        ["Paste"] = "\uE77F",
        ["Cut"] = "\uE8C6",
        ["Undo"] = "\uE7A7",
        ["Redo"] = "\uE7A6",
        ["Refresh"] = "\uE72C",
        ["Search"] = "\uE721",
        ["Filter"] = "\uE71C",
        ["Sort"] = "\uE8CB",

        // Status
        ["Check"] = "\uE73E",
        ["CheckMark"] = "\uE73E",
        ["Close"] = "\uE711",
        ["Cancel"] = "\uE711",
        ["Error"] = "\uE783",
        ["Warning"] = "\uE7BA",
        ["Info"] = "\uE946",
        ["Information"] = "\uE946",
        ["Question"] = "\uE897",
        ["Help"] = "\uE897",

        // Window Controls
        ["Minimize"] = "\uE921",
        ["Maximize"] = "\uE922",
        ["Restore"] = "\uE923",
        ["ChromeClose"] = "\uE8BB",

        // Arrows/Chevrons
        ["ChevronUp"] = "\uE70E",
        ["ChevronDown"] = "\uE70D",
        ["ChevronLeft"] = "\uE76B",
        ["ChevronRight"] = "\uE76C",
        ["ArrowUp"] = "\uE74A",
        ["ArrowDown"] = "\uE74B",
        ["ArrowLeft"] = "\uE72B",
        ["ArrowRight"] = "\uE72A",
        ["Back"] = "\uE72B",
        ["Forward"] = "\uE72A",

        // Common UI
        ["Home"] = "\uE80F",
        ["Star"] = "\uE734",
        ["Heart"] = "\uEB51",
        ["Favorite"] = "\uEB51",
        ["Pin"] = "\uE718",
        ["Unpin"] = "\uE77A",
        ["Lock"] = "\uE72E",
        ["Unlock"] = "\uE785",
        ["Eye"] = "\uE7B3",
        ["EyeOff"] = "\uED1A",

        // Files/Folders
        ["Folder"] = "\uE8B7",
        ["FolderOpen"] = "\uE838",
        ["File"] = "\uE7C3",
        ["Document"] = "\uE8A5",
        ["NewFile"] = "\uE8A5",

        // System
        ["Computer"] = "\uE7F4",
        ["Desktop"] = "\uE7F4",
        ["Power"] = "\uE7E8",
        ["PowerOff"] = "\uE7E8",
        ["Restart"] = "\uE72C",
        ["Shield"] = "\uE83D",
        ["Security"] = "\uE83D",
        ["Privacy"] = "\uE72E",
        ["Update"] = "\uE777",
        ["Download"] = "\uE896",
        ["Upload"] = "\uE898",
        ["Install"] = "\uE896",
        ["Uninstall"] = "\uE74D",

        // Apps
        ["App"] = "\uE71D",
        ["WindowsApps"] = "\uE71D",
        ["Store"] = "\uE719",

        // Optimize
        ["Performance"] = "\uE9D9",
        ["Speed"] = "\uEC4A",
        ["Battery"] = "\uE83F",
        ["Memory"] = "\uE7F4",
        ["Disk"] = "\uEDA2",
        ["Network"] = "\uE839",

        // Misc
        ["Bug"] = "\uEBE8",
        ["Report"] = "\uE9D9",
        ["Log"] = "\uE81C",
        ["Terminal"] = "\uE756",
        ["Code"] = "\uE943",
        ["Link"] = "\uE71B",
        ["ExternalLink"] = "\uE8A7",
        ["Mail"] = "\uE715",
        ["Send"] = "\uE724",
        ["Share"] = "\uE72D",
        ["Print"] = "\uE749",
        ["Calendar"] = "\uE787",
        ["Clock"] = "\uE823",
        ["Alarm"] = "\uE7ED",
        ["Location"] = "\uE81D",
        ["Map"] = "\uE826",
        ["Globe"] = "\uE774",
        ["Language"] = "\uE775",
        ["Theme"] = "\uE771",
        ["Color"] = "\uE790",
        ["Font"] = "\uE8D2",
        ["TextSize"] = "\uE8E9",

        // Toggle states
        ["ToggleOn"] = "\uEC12",
        ["ToggleOff"] = "\uEC11",
        ["RadioOn"] = "\uECCA",
        ["RadioOff"] = "\uECCB",
        ["CheckboxOn"] = "\uE73A",
        ["CheckboxOff"] = "\uE739",

        // User
        ["User"] = "\uE77B",
        ["People"] = "\uE716",
        ["Contact"] = "\uE77B",
        ["Admin"] = "\uE7EF",

        // Donation/Support
        ["Donate"] = "\uEB51",
        ["Gift"] = "\uEC9F",
        ["Coffee"] = "\uEC32"
    };

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var iconName = value?.ToString() ?? parameter?.ToString() ?? string.Empty;

        if (IconMap.TryGetValue(iconName, out var glyph))
        {
            return new FontIcon
            {
                Glyph = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets")
            };
        }

        // Return a default icon if not found
        return new FontIcon
        {
            Glyph = "\uE946", // Info icon as fallback
            FontFamily = new FontFamily("Segoe MDL2 Assets")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets a glyph string for the specified icon name.
    /// Useful for direct use in code-behind.
    /// </summary>
    public static string GetGlyph(string iconName)
    {
        return IconMap.TryGetValue(iconName, out var glyph) ? glyph : "\uE946";
    }
}
