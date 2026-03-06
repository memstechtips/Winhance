using Material.Icons;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts icon names and icon packs to IconElement controls.
/// Supports Material, MaterialDesign, and Fluent icon packs.
/// Returns IconElement types (PathIcon, FontIcon, SymbolIcon) that are compatible with SettingsCard.HeaderIcon.
/// </summary>
public partial class IconConverter : IValueConverter
{

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
                        Data = geometry,
                        Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 1)
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
    /// Creates a SymbolIcon from FluentIcons.WinUI for Fluent icons.
    /// </summary>
    private static IconElement? CreateFluentIcon(string iconName)
    {
        if (Enum.TryParse<FluentIcons.Common.Symbol>(iconName, ignoreCase: true, out var symbol))
        {
            return new FluentIcons.WinUI.SymbolIcon
            {
                Symbol = symbol,
                IconVariant = FluentIcons.Common.IconVariant.Regular
            };
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
