using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Winhance.Infrastructure.Tests.Docs;

internal sealed record ThemeExport(
    int SchemaVersion,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Themes,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Aliases,
    IReadOnlyDictionary<string, StyleExport> Styles,
    IReadOnlyDictionary<string, GeometryExport> Geometries);

internal sealed record StyleExport(string Target, string? BasedOn, IReadOnlyDictionary<string, string> Setters);

internal sealed record GeometryExport(string Data, int ViewBox);

// Parses the app's own WinUI resource dictionaries so the winhance.net docs can render Winhance's actual
// colours, table metrics and pill geometries instead of website-authored approximations. Exports everything
// the three dictionaries contain rather than a whitelist - the website picks what it needs.
internal static class XamlTokens
{
    public const int SchemaVersion = 1;

    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    // The XAML declares no viewbox - PathIcon centres the geometry in its 12x12 box. These are the
    // coordinate spaces the paths were drawn in: the two Fluent icons are 12x12 upstream, the Windows
    // logo is redrawn at 11x11. A geometry with no entry here fails the test rather than being guessed at.
    private static readonly Dictionary<string, int> ViewBoxes = new(StringComparer.Ordinal)
    {
        ["BadgeRecommendedIconPath"] = 12,
        ["BadgeDefaultIconPath"] = 11,
        ["BadgePreferenceIconPath"] = 12,
    };

    public static ThemeExport Extract(string solutionDir)
    {
        var dir = Path.Combine(solutionDir, "src", "Winhance.UI", "Features", "Common", "Resources");
        var badge = XDocument.Load(Path.Combine(dir, "BadgeStyles.xaml")).Root!;
        var techDetails = XDocument.Load(Path.Combine(dir, "TechnicalDetailsStyles.xaml")).Root!;
        var featureIcons = XDocument.Load(Path.Combine(dir, "FeatureIcons.xaml")).Root!;

        var themes = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["light"] = new(StringComparer.Ordinal),
            ["dark"] = new(StringComparer.Ordinal),
        };
        var aliases = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["light"] = new(StringComparer.Ordinal),
            ["dark"] = new(StringComparer.Ordinal),
        };

        ReadThemeDictionaries(badge, themes, aliases);
        ReadThemeDictionaries(techDetails, themes, aliases);

        var styles = new Dictionary<string, StyleExport>(StringComparer.Ordinal);
        ReadStyles(badge, styles);
        ReadStyles(techDetails, styles);

        return new ThemeExport(
            SchemaVersion,
            themes.ToDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<string, string>)kv.Value, StringComparer.Ordinal),
            aliases.ToDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<string, string>)kv.Value, StringComparer.Ordinal),
            styles,
            ReadGeometries(featureIcons));
    }

    private static void ReadThemeDictionaries(
        XElement root,
        Dictionary<string, Dictionary<string, string>> themes,
        Dictionary<string, Dictionary<string, string>> aliases)
    {
        var themeDictionaries = root.Element(Presentation + "ResourceDictionary.ThemeDictionaries");
        if (themeDictionaries is null)
            return;

        foreach (var themeDict in themeDictionaries.Elements(Presentation + "ResourceDictionary"))
        {
            var themeKey = (string)themeDict.Attribute(Xaml + "Key")!;
            if (themeKey is not ("Light" or "Dark"))
                continue; // HighContrast is not a website theme

            var theme = themeKey.ToLowerInvariant();
            var (colors, resourceAliases) = ReadDictionary(themeDict);
            foreach (var (key, value) in colors)
                themes[theme][key] = value;
            foreach (var (key, value) in resourceAliases)
                aliases[theme][key] = value;
        }
    }

    // One ResourceDictionary's direct brush/alias children: <SolidColorBrush x:Key Color> becomes a colour,
    // <StaticResource x:Key ResourceKey> becomes an alias to a different (usually WinUI-owned) resource key.
    private static (Dictionary<string, string> Colors, Dictionary<string, string> Aliases) ReadDictionary(XElement dictionary)
    {
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var el in dictionary.Elements())
        {
            var key = (string?)el.Attribute(Xaml + "Key");
            if (key is null)
                continue;

            if (el.Name.LocalName == "SolidColorBrush")
                colors[key] = (string)el.Attribute("Color")!;
            else if (el.Name.LocalName == "StaticResource")
                aliases[key] = (string)el.Attribute("ResourceKey")!;
        }

        return (colors, aliases);
    }

    private static void ReadStyles(XElement root, Dictionary<string, StyleExport> styles)
    {
        foreach (var style in root.Elements(Presentation + "Style"))
        {
            var key = (string)style.Attribute(Xaml + "Key")!;
            var target = (string)style.Attribute("TargetType")!;
            var basedOn = (string?)style.Attribute("BasedOn");

            var setters = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var setter in style.Elements(Presentation + "Setter"))
                setters[(string)setter.Attribute("Property")!] = (string)setter.Attribute("Value")!;

            styles[key] = new StyleExport(target, StripStaticResource(basedOn), setters);
        }
    }

    // BasedOn="{StaticResource BadgePillBase}" -> "BadgePillBase". Setter values are left verbatim - a
    // "{ThemeResource X}" string stays exactly that, because the website resolves it, not this parser.
    private static string? StripStaticResource(string? value) =>
        value is null ? null : Regex.Match(value, @"^\{StaticResource\s+(.+)\}$").Groups[1].Value;

    private static Dictionary<string, GeometryExport> ReadGeometries(XElement root)
    {
        var geometries = new Dictionary<string, GeometryExport>(StringComparer.Ordinal);

        foreach (var el in root.Elements(Xaml + "String"))
        {
            var key = (string)el.Attribute(Xaml + "Key")!;
            if (!key.StartsWith("Badge", StringComparison.Ordinal) || !key.EndsWith("IconPath", StringComparison.Ordinal))
                continue;

            // Missing ViewBoxes entry throws here rather than guessing a box for a geometry nobody sized yet.
            geometries[key] = new GeometryExport(el.Value, ViewBoxes[key]);
        }

        return geometries;
    }
}
