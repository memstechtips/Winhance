using Material.Icons;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI.Features.Common.Converters;

public sealed partial class IconConverter : IValueConverter
{
    // IconSource form: an IconSourceElement the template declares can bind Opacity; an element a converter returns cannot.
    public object? Convert(object value, Type targetType, object parameter, string language) =>
        typeof(IconSource).IsAssignableFrom(targetType) ? BuildSource(value, parameter) : Build(value, parameter);

    internal static IconElement? Build(object value, object? parameter)
    {
        if (Resolve(value, parameter!) is not { } icon)
        {
            return null;
        }

        return icon.Pack.ToLowerInvariant() switch
        {
            "material" or "materialdesign" => CreatePathIcon(MaterialPathData(icon.Name)),
            "fluent" => CreateFluentIcon(icon.Name),
            "appasset" => new BitmapIcon { UriSource = AppAssetUri(icon.Name), ShowAsMonochrome = true },
            _ => CreatePathIcon(MaterialPathData(icon.Name))
        };
    }

    private static IconSource? BuildSource(object value, object parameter)
    {
        if (Resolve(value, parameter) is not { } icon)
        {
            return null;
        }

        return icon.Pack.ToLowerInvariant() switch
        {
            "fluent" => CreateFluentIconSource(icon.Name),
            "appasset" => new BitmapIconSource { UriSource = AppAssetUri(icon.Name), ShowAsMonochrome = true },
            _ => CreatePathIconSource(MaterialPathData(icon.Name)),
        };
    }

    internal static (string Name, string Pack)? Resolve(object value, object parameter)
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

        return string.IsNullOrEmpty(iconName) ? null : (iconName, iconPack);
    }

    private static string? MaterialPathData(string iconName) =>
        Enum.TryParse<MaterialIconKind>(iconName, ignoreCase: true, out var iconKind)
            ? MaterialIconDataProvider.GetData(iconKind)
            : null;

    private static Uri AppAssetUri(string fileName) => new($"ms-appx:///Assets/AppIcons/{fileName}");

    private static IconElement? CreatePathIcon(string? pathData)
    {
        if (pathData is { Length: > 0 })
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

        return null;
    }

    private static IconSource? CreatePathIconSource(string? pathData)
    {
        if (pathData is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return new PathIconSource { Data = GeometryHelper.FromPathData(pathData) };
        }
        catch
        {
            return null;
        }
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

    private static IconSource? CreateFluentIconSource(string iconName) =>
        Enum.TryParse<FluentIcons.Common.Icon>(iconName, ignoreCase: true, out var symbol)
            ? new FluentIcons.WinUI.FluentIconSource { Icon = symbol, IconVariant = FluentIcons.Common.IconVariant.Regular }
            : null;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
