using Material.Icons;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI.Features.Common.Converters;

public sealed partial class IconConverter : IValueConverter
{

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        string? iconName = null;
        string iconPack = "Material";

        if (value is string strValue)
        {
            iconName = strValue;
            iconPack = parameter?.ToString() ?? "Material";
        }
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
            return null;
        }

        return iconPack.ToLowerInvariant() switch
        {
            "material" or "materialdesign" => CreateMaterialPathIcon(iconName),
            "fluent" => CreateFluentIcon(iconName),
            _ => CreateMaterialPathIcon(iconName)
        };
    }

    private static IconElement? CreateMaterialPathIcon(string iconName)
    {
        if (Enum.TryParse<MaterialIconKind>(iconName, ignoreCase: true, out var iconKind))
        {
            var pathData = MaterialIconDataProvider.GetData(iconKind);

            if (!string.IsNullOrEmpty(pathData))
            {
                try
                {
                    return new PathIcon
                    {
                        Data = GeometryHelper.FromPathData(pathData),
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

    private static IconElement? CreateFluentIcon(string iconName)
    {
        if (Enum.TryParse<FluentIcons.Common.Icon>(iconName, ignoreCase: true, out var symbol))
        {
            return new FluentIcons.WinUI.FluentIcon
            {
                Icon = symbol,
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
