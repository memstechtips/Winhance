using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;

namespace Winhance.UI.Tests.Conventions;

// Census rows 3, 9, 10, 11, 14 and 18: how markup binds, sizes images, draws icons, shares converters and styles,
// and lists items.
public class XamlConventionTests
{
    private const string IconConverter = Ui + "Features/Common/Converters/IconConverter.cs";

    private static readonly Regex ConverterClass = new(@"\bclass\s+(\w+)\b[^{;]*?:[^{;]*\bIValueConverter\b");

    private static readonly Regex StyleLiteral =
        new(@"(?<![\w.:])(Margin|Padding|Spacing|RowSpacing|ColumnSpacing|FontSize|CornerRadius)=""(?!\{)([^""]*)""");

    private static readonly Regex NonZero = new(@"[^0,\s]");

    private static readonly Regex LayoutResource =
        new(@"<(x:Double|Thickness|CornerRadius|x:Int32|GridLength)\b[^>]*\bx:Key=""([^""]+)""[^>]*>([^<]*)</");

    // The WinUI type-ramp sizes: Caption 12, Body 14, BodyLarge 18, Subtitle 20, Title 28.
    private static readonly string[] TypeRamp = ["12", "14", "18", "20", "28"];

    [Fact]
    public void ReflectionBinding_MatchesTheLedger()
    {
        Ledger.AssertMatches("ReflectionBinding", Count(XamlAll, @"\{Binding\b"),
            "use x:Bind with x:DataType instead of {Binding}");
    }

    [Fact]
    public void UnsizedImageDecode_MatchesTheLedger()
    {
        // C#: new BitmapImage with no DecodePixel* in its object initialiser or in the statement right after it.
        // XAML: an Image with a literal Source (it decodes at full size), or a BitmapImage without DecodePixelWidth
        // or DecodePixelHeight. An Image bound to a source is sized where that source is built.
        var actual = Merge(
            Count(CsAll, @"\bnew\s+(?:[\w.]+\.)?BitmapImage\b(?!\s*(?:\((?:[^()]|\([^()]*\))*\))?\s*(?:\{[^}]*\bDecodePixel|;\s*\w+\.DecodePixel))"),
            Count(XamlAll, @"<Image\b(?=[^>]*\bSource=""(?!\{))"),
            Count(XamlAll, @"<BitmapImage\b(?![^>]*\bDecodePixel(?:Width|Height)=)"));

        Ledger.AssertMatches("UnsizedImageDecode", actual, "set DecodePixelWidth or DecodePixelHeight to the displayed size");
    }

    [Fact]
    public void NonFluentIcon_MatchesTheLedger()
    {
        // A FontIcon, PathIcon, SymbolIcon, BitmapIcon or ImageIcon (or its *Source) in UI markup, a raw Path drawing
        // an *IconPath geometry, or one of those constructed in UI code outside the catalog icon converter.
        var actual = Merge(
            Count(XamlUi, @"<(?:FontIcon|PathIcon|SymbolIcon|BitmapIcon|ImageIcon)(?:Source)?\b"),
            Count(XamlUi, @"<Path\b[^>]*Data=""\{StaticResource \w*IconPath\}"""),
            Count(CsUi, @"\bnew\s+(?:[\w.]+\.)?(?:FontIcon|PathIcon|SymbolIcon|BitmapIcon|ImageIcon)(?:Source)?\b", IconConverter));

        Ledger.AssertMatches("NonFluentIcon", actual, "use a FluentIcon");
    }

    [Fact]
    public void IconPacks_AreFluentAndAppLogosOnly()
    {
        // Marco's call (2026-11-16): Fluent icons only, plus app logos from the icon cache with a Fluent fallback.
        var material = Merge(Count(CsAll, @"\bMaterial\.Icons\b|\bMaterialIcon\w*|\bIconPack\.Material\b"),
            Count(XamlAll, @"\bMaterial\.Icons\b|\bMaterialIcon\w*"));
        material.Should().BeEmpty("use a FluentIcon, or an IconPack.App icon with a Fluent fallback");

        File.ReadAllText(Path.Combine(Root, Ui, "Winhance.UI.csproj"))
            .Should().NotContain("Material.Icons", "the Material icon package is gone");
    }

    [Fact]
    public void DeadConverters_MatchTheLedger()
    {
        // A class implementing IValueConverter is used when some XAML resource of that type (prefix:Name x:Key="k")
        // is referenced as {StaticResource k} or {ThemeResource k}. Code-only use does not count.
        var xaml = XamlAll.Select(Text).ToList();
        var converters = 0;
        var actual = new Dictionary<string, int>();
        foreach (var file in CsAll)
        {
            foreach (Match m in ConverterClass.Matches(Text(file)))
            {
                converters++;
                var keyed = new Regex(@"<\w+:" + m.Groups[1].Value + @"\b[^>]*\bx:Key=""([^""]+)""");
                var keys = xaml.SelectMany(x => keyed.Matches(x).Select(k => k.Groups[1].Value)).ToList();
                var used = keys.Any(k => xaml.Any(x =>
                    Regex.IsMatch(x, @"\{(?:StaticResource|ThemeResource)\s+" + Regex.Escape(k) + @"\s*\}")));
                if (!used)
                    actual[file] = actual.GetValueOrDefault(file) + 1;
            }
        }

        converters.Should().BePositive();
        Ledger.AssertMatches("DeadConverter", actual, "use the converter from XAML or delete it");
    }

    [Fact]
    public void BooleanToVisibilityConverter_IsNeverReferenced()
    {
        // x:Bind converts a bool to Visibility itself, and an element that is only sometimes shown uses x:Load.
        Merge(Count(XamlAll, @"\bBooleanToVisibilityConverter\b"), Count(CsAll, @"\bBooleanToVisibilityConverter\b"))
            .Should().BeEmpty("bind the bool directly, or use x:Load when the element is only sometimes shown");
    }

    [Fact]
    public void ItemsRepeaters_EachOwnTheirItemTemplate()
    {
        // An ItemsRepeater recycles through a pool kept on its DataTemplate. In WinAppSDK 1.8.7, a new ItemsSource
        // recycles the realized rows with the repeater recorded as their owner and then empties its Children, so a
        // second repeater on the same template can take a row whose owner no longer holds it, and throws
        // "ItemsRepeater's child not found in its Children collection" (the RecycleWithoutOwner guard first appears
        // in 1.8.12). A template written inside the repeater is a new instance, with its own pool, per repeater.
        Count(XamlAll, @"<ItemsRepeater\b").Should().NotBeEmpty();
        Count(XamlAll, @"<ItemsRepeater\b[^>]*\bItemTemplate=""\{(?:Static|Theme)Resource\b")
            .Should().BeEmpty("write the repeater's DataTemplate inside <ItemsRepeater.ItemTemplate>, so no two "
                + "repeaters share a recycle pool");
    }

    [Fact]
    public void StyleLiterals_StayUnderTheRepeatBudget()
    {
        // A Margin, Padding, Spacing, RowSpacing, ColumnSpacing, FontSize or CornerRadius literal may appear twice per
        // property and value across the UI. A third use means the value has a role, so it gets a token named for that
        // role in LayoutTokens.xaml (or reuses a key that already holds it).
        var literals = StyleLiterals().ToList();
        literals.Should().HaveCountGreaterThan(50, "the scan must see the tree's literals");

        var repeated = literals
            .GroupBy(l => (l.Property, l.Value))
            .Where(g => g.Count() >= 3)
            .Select(g => $"{g.Key.Property}=\"{g.Key.Value}\" x{g.Count()}: {string.Join(", ", g.Select(l => l.Site))}");

        repeated.Should().BeEmpty("a literal used 3 or more times becomes a role token in LayoutTokens.xaml");
    }

    [Fact]
    public void FontSizes_OnTheTypeRamp_UseAResource()
    {
        var onRamp = StyleLiterals()
            .Where(l => l.Property == "FontSize" && TypeRamp.Contains(l.Value))
            .Select(l => $"FontSize=\"{l.Value}\": {l.Site}");

        onRamp.Should().BeEmpty("12, 14 and 20 use CaptionTextBlockFontSize, BodyTextBlockFontSize and SubtitleTextBlockFontSize, "
            + "and 18 and 28 a token in LayoutTokens.xaml or IconSizes.xaml");
    }

    [Fact]
    public void LayoutResources_AreDefinedOnce()
    {
        // The same key with the same value in two dictionaries is a copy that drifts: define it once and merge that
        // dictionary. A key redefined with a different value is a local override and stays.
        var resources = XamlUi
            .SelectMany(file => LayoutResource.Matches(Text(file)).Select(m =>
                (Key: m.Groups[2].Value, Type: m.Groups[1].Value, Value: m.Groups[3].Value.Trim(), Site: Site(file, m.Index))))
            .ToList();
        resources.Should().HaveCountGreaterThan(50, "the scan must see the layout dictionaries");

        var copies = resources
            .GroupBy(r => (r.Key, r.Type, r.Value))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Key} = {g.Key.Value}: {string.Join(", ", g.Select(r => r.Site))}");

        copies.Should().BeEmpty("a layout resource is defined in one dictionary and merged where it is used");
    }

    // Literal style values, skipping values made only of zeros, which need no token.
    private static IEnumerable<(string Property, string Value, string Site)> StyleLiterals() =>
        XamlUi
            .SelectMany(file => StyleLiteral.Matches(Text(file)).Select(m =>
                (Property: m.Groups[1].Value, Value: m.Groups[2].Value, Site: Site(file, m.Index))))
            .Where(l => NonZero.IsMatch(l.Value));

    private static string Site(string file, int at) => $"{file}:{Text(file).AsSpan(0, at).Count('\n') + 1}";

    [Fact]
    public void ItemsControls_MatchTheLedger()
    {
        // An ItemsControl realizes every item, so one that stays says why directly above it: <!-- Bounded: ... --> for
        // a short fixed set, <!-- Nested: ... --> inside a virtualizing list.
        const string element = @"<ItemsControl(?![\w.])";
        var unmarked = new List<string>();
        foreach (var file in XamlAll)
        {
            var raw = File.ReadAllText(Path.Combine(Root, file)).Replace("\r\n", "\n", StringComparison.Ordinal);
            unmarked.AddRange(Regex.Matches(Text(file), element).Where(m => !HasReasonComment(raw, m.Index))
                .Select(m => $"{file}:{raw.AsSpan(0, m.Index).Count('\n') + 1}"));
        }

        unmarked.Should().BeEmpty("an ItemsControl needs a <!-- Bounded: ... --> or <!-- Nested: ... --> comment directly above it, or a virtualizing ListView");
        Ledger.AssertMatches("ItemsControl", Count(XamlAll, element),
            "use a virtualizing ListView, or ledger the ItemsControl as bounded-set or nested-in-list under its comment");
    }

    [Fact]
    public void SettingCardBanners_HangUnderTheCardFromOneTemplate()
    {
        // A card's state banners come from SettingCardBannersTemplate, joined under the card with
        // JoinedSettingCardBannerStyle. The password note is the one banner inside a card, because it belongs to the rows.
        const string file = Ui + "Features/Common/Resources/SettingTemplates.xaml";
        var text = Text(file);
        var template = Regex.Match(text, @"<DataTemplate x:Key=""SettingCardBannersTemplate"".*?</DataTemplate>",
            RegexOptions.Singleline);
        template.Success.Should().BeTrue("the card banners template must exist");

        var banners = Regex.Matches(text, @"<(?:\w+:)?(?:Quiet)?InfoBar(?![\w.])[^>]*>").ToList();
        bool InTemplate(Match b) => b.Index > template.Index && b.Index < template.Index + template.Length;
        banners.Count(InTemplate).Should().Be(4, "the template holds the status, export note and both review banners");

        banners.Where(b => InTemplate(b) != b.Value.Contains(@"Style=""{StaticResource JoinedSettingCardBannerStyle}""", StringComparison.Ordinal))
            .Select(b => Site(file, b.Index))
            .Should().BeEmpty("only the template's banners are joined under the card");
        banners.Where(b => !InTemplate(b)
                && !(b.Value.Contains("ShowPasswordBanner", StringComparison.Ordinal)
                    && b.Value.Contains(@"Style=""{StaticResource SettingCardBannerStyle}""", StringComparison.Ordinal)))
            .Select(b => Site(file, b.Index))
            .Should().BeEmpty("a card state banner goes in SettingCardBannersTemplate");
        Regex.Count(text, @"ContentTemplate=""\{StaticResource SettingCardBannersTemplate\}""")
            .Should().Be(3, "the plain card, the parent card and each child row place the template under the card");
    }

    [Fact]
    public void LinksInText_AreBuiltByInlineLink()
    {
        Count(CsUi, @"\bnew\s+(?:[\w.]+\.)?Hyperlink\b", Ui + "Features/Common/Helpers/InlineLink.cs")
            .Should().BeEmpty("a link inside running text comes from InlineLink.Create, so every one looks and reads the same");
        Count(XamlUi, @"<Hyperlink\b").Should().BeEmpty("build a link inside text with InlineLink.Create");
        Count(CsUi.Concat(XamlUi), @"UnderlineStyle(\.|="")Single").Should().BeEmpty("links in Winhance carry no underline");
    }

    // Keys the WinUI and toolkit dictionaries define. A {StaticResource} key nothing defines compiles, then throws
    // XamlParseException when the page loads.
    private static readonly HashSet<string> FrameworkKeys =
    [
        "AccentButtonStyle", "AttentionValueInfoBadgeStyle", "BodyStrongTextBlockStyle", "BodyTextBlockFontSize",
        "BodyTextBlockStyle", "CaptionTextBlockFontSize", "CaptionTextBlockStyle", "CheckBoxBorderThickness",
        "CheckBoxFocusVisualMargin", "CheckBoxHeight", "CheckBoxMinWidth", "CheckBoxPadding", "CheckBoxSize",
        "ControlCornerRadius", "DefaultComboBoxStyle", "SubtitleTextBlockStyle", "SuccessIconInfoBadgeStyle",
        "TitleTextBlockStyle", "UseSystemFocusVisuals",
    ];

    [Fact]
    public void StaticResourceKeys_AreDefined()
    {
        var defined = XamlAll.SelectMany(f => Regex.Matches(Text(f), @"x:Key=""([^""]+)""").Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        var used = XamlAll.SelectMany(f => Regex.Matches(Text(f), @"\{StaticResource\s+([^\s}]+)\s*\}")
            .Select(m => (Key: m.Groups[1].Value, Site: Site(f, m.Index)))).ToList();

        used.Should().HaveCountGreaterThan(100);
        used.Where(u => !defined.Contains(u.Key) && !FrameworkKeys.Contains(u.Key)).Select(u => $"{u.Key}: {u.Site}")
            .Should().BeEmpty("every {StaticResource} key is defined in a Winhance dictionary or is a WinUI key in FrameworkKeys");
    }

    private static bool HasReasonComment(string raw, int at)
    {
        var before = raw[..at].TrimEnd();
        var open = before.LastIndexOf("<!--", StringComparison.Ordinal);
        if (open < 0 || !before.EndsWith("-->", StringComparison.Ordinal))
            return false;
        var comment = before[(open + 4)..].TrimStart();
        return comment.StartsWith("Bounded:", StringComparison.Ordinal) || comment.StartsWith("Nested:", StringComparison.Ordinal);
    }
}
