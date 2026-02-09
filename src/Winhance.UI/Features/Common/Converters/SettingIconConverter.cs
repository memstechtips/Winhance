using Material.Icons;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts icon names and icon packs to IconElement controls.
/// Supports Material, MaterialDesign, and Fluent icon packs.
/// Returns IconElement types (PathIcon, FontIcon) that are compatible with SettingsCard.HeaderIcon.
/// </summary>
public class SettingIconConverter : IValueConverter
{
    /// <summary>
    /// Fallback Segoe MDL2 glyph mappings for Fluent icons.
    /// </summary>
    private static readonly Dictionary<string, string> FluentIconMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Navigation
        ["Navigation"] = "\uE700",
        ["PanelLeft"] = "\uE89F",
        ["PanelTop"] = "\uE745",
        ["WindowNew"] = "\uE8A7",
        ["Window"] = "\uE737",

        // Files/Documents
        ["DocumentText"] = "\uE8A5",
        ["Document"] = "\uE8A5",
        ["DocumentClock"] = "\uE823",
        ["DocumentNumber"] = "\uE8A5",
        ["DocumentLock"] = "\uE8A7",
        ["File"] = "\uE7C3",

        // Folders
        ["Folder"] = "\uE8B7",
        ["FolderOpen"] = "\uE838",
        ["FolderTree"] = "\uE8D4",

        // Cursors/Pointers
        ["CursorClick"] = "\uE8B0",
        ["SelectObject"] = "\uEF20",

        // Boxes/Containers
        ["BoxMultiple"] = "\uF133",

        // Media
        ["Video"] = "\uE714",
        ["Image"] = "\uE8B9",
        ["ImageMultiple"] = "\uE8B9",

        // AI/Bot
        ["Bot"] = "\uE99A",

        // Store
        ["StoreMicrosoft"] = "\uE719",

        // Alerts
        ["AlertBadge"] = "\uE7BA",
        ["ShieldError"] = "\uEA39",

        // Battery/Power
        ["BatteryCharge"] = "\uE83E",
        ["DesktopOff"] = "\uE7F4",
        ["DesktopPulse"] = "\uE7F4",

        // Sharing
        ["Share"] = "\uE72D",

        // Misc
        ["Bug"] = "\uEBE8",
        ["Wallpaper"] = "\uE771",
        ["BookOpen"] = "\uE82D",
        ["PulseSquare"] = "\uE9D9",
        ["Pen"] = "\uE70F",
        ["Pin"] = "\uE718",

        // Default
        ["Info"] = "\uE946",
    };

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        string? iconName = null;
        string iconPack = "Material";

        // Check if value is a string (direct icon name)
        if (value is string strValue)
        {
            iconName = strValue;
            iconPack = parameter?.ToString() ?? "Material";
        }
        // Check if value is an object with Icon and IconPack properties (like SettingItemViewModel)
        else if (value != null)
        {
            var type = value.GetType();
            var iconProperty = type.GetProperty("Icon");
            var iconPackProperty = type.GetProperty("IconPack");

            iconName = iconProperty?.GetValue(value)?.ToString();
            iconPack = iconPackProperty?.GetValue(value)?.ToString() ?? "Material";
        }

        if (string.IsNullOrEmpty(iconName))
        {
            return null; // Return null so no icon is shown
        }

        return iconPack.ToLowerInvariant() switch
        {
            "material" or "materialdesign" => CreateMaterialPathIcon(iconName),
            "fluent" => CreateFluentIcon(iconName),
            _ => CreateMaterialPathIcon(iconName)
        };
    }

    /// <summary>
    /// Creates a PathIcon from Material icon path data.
    /// PathIcon is an IconElement, compatible with SettingsCard.HeaderIcon.
    /// </summary>
    private static IconElement? CreateMaterialPathIcon(string iconName)
    {
        // Try to parse the icon name as MaterialIconKind enum
        if (Enum.TryParse<MaterialIconKind>(iconName, ignoreCase: true, out var iconKind))
        {
            // Get the SVG path data for this icon
            var pathData = MaterialIconDataProvider.GetData(iconKind);

            if (!string.IsNullOrEmpty(pathData))
            {
                try
                {
                    // Parse the path data into a Geometry
                    var geometry = (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                        typeof(Geometry), pathData);

                    return new PathIcon
                    {
                        Data = geometry
                    };
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a FontIcon from Segoe MDL2 Assets for Fluent icons.
    /// </summary>
    private static IconElement? CreateFluentIcon(string iconName)
    {
        if (FluentIconMap.TryGetValue(iconName, out var glyph))
        {
            return new FontIcon
            {
                Glyph = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets")
            };
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
