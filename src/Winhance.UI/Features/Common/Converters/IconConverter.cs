using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Converters;

public sealed partial class IconConverter : IValueConverter
{
    private const string FeatureIconPack = "FeatureIcon";

    // Every card runs this, so the reflection lookup is done once per type. The built element is not cached: an
    // element takes one parent.
    private static readonly ConcurrentDictionary<Type, (PropertyInfo? Icon, PropertyInfo? Pack)> Accessors = new();

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
            "fluent" => CreateFluentIcon(icon.Name, FluentIcons.Common.IconVariant.Regular),
            "fluentcolor" => CreateFluentIcon(icon.Name, FluentIcons.Common.IconVariant.Color),
            "appasset" => new BitmapIcon { UriSource = AppAssetUri(icon.Name), ShowAsMonochrome = true },
            "appfile" => new BitmapIcon { UriSource = new Uri(icon.Name), ShowAsMonochrome = false },
            _ => null
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
            "fluent" => CreateFluentIconSource(icon.Name, FluentIcons.Common.IconVariant.Regular),
            "fluentcolor" => CreateFluentIconSource(icon.Name, FluentIcons.Common.IconVariant.Color),
            "appasset" => new BitmapIconSource { UriSource = AppAssetUri(icon.Name), ShowAsMonochrome = true },
            "appfile" => new BitmapIconSource { UriSource = new Uri(icon.Name), ShowAsMonochrome = false },
            _ => null,
        };
    }

    internal static (string Name, string Pack)? Resolve(object value, object parameter)
    {
        string? iconName = null;
        string iconPack = "Fluent";

        if (value is string strValue)
        {
            iconName = strValue;
            iconPack = parameter?.ToString() ?? "Fluent";
            if (iconPack.StartsWith(FeatureIconPack, StringComparison.OrdinalIgnoreCase))
            {
                return FeatureIcon(strValue, iconPack.EndsWith("Color", StringComparison.OrdinalIgnoreCase));
            }
        }
        else if (value != null)
        {
            var (iconProperty, iconPackProperty) = Accessors.GetOrAdd(
                value.GetType(), t => (t.GetProperty("Icon"), t.GetProperty("IconPack")));

            iconName = iconProperty?.GetValue(value)?.ToString();
            iconPack = iconPackProperty?.GetValue(value)?.ToString() ?? "Fluent";
        }

        return string.IsNullOrEmpty(iconName) ? null : (iconName, iconPack);
    }

    // The value is a FeatureIcons.xaml "...Symbol" key holding a Fluent icon name; "FeatureIconColor" draws it in the
    // Color variant. An unknown key draws no icon rather than throwing: a missing icon is cosmetic, a page that will
    // not load is not.
    private static (string Name, string Pack)? FeatureIcon(string key, bool color) =>
        Application.Current.Resources.TryGetValue(key, out var resource) && resource is string name
            ? (name, color ? "FluentColor" : "Fluent")
            : null;

    private static Uri AppAssetUri(string fileName) => new($"ms-appx:///Assets/AppIcons/{fileName}");

    private static IconElement? CreateFluentIcon(string iconName, FluentIcons.Common.IconVariant variant)
    {
        if (Enum.TryParse<FluentIcons.Common.Icon>(iconName, ignoreCase: true, out var symbol))
        {
            return new FluentIcons.WinUI.FluentIcon
            {
                Icon = symbol,
                IconVariant = variant
            };
        }

        return null;
    }

    private static IconSource? CreateFluentIconSource(string iconName, FluentIcons.Common.IconVariant variant) =>
        Enum.TryParse<FluentIcons.Common.Icon>(iconName, ignoreCase: true, out var symbol)
            ? new FluentIcons.WinUI.FluentIconSource { Icon = symbol, IconVariant = variant }
            : null;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
