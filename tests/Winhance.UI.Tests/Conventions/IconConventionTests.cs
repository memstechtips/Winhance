using System.Buffers.Binary;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;
using CatalogFluentIcons = Winhance.Core.Features.Common.Catalog.FluentIcons;
using CatalogIcon = Winhance.Core.Features.Common.Catalog.Icon;
using FluentGlyph = FluentIcons.Common.Icon;

namespace Winhance.UI.Tests.Conventions;

// FluentIcon draws from SeagullFluentIcons.otf unless IconSize is set, and Seagull leaves out the icons FluentIcons
// marks [NonResizable] in its source (the attribute does not survive into the shipped DLL, so the font is the check).
// A missing one renders an empty box with no error. Seagull puts an icon's Regular glyph at U+F0000 + (int)icon * 4.
public class IconConventionTests
{
    private static readonly Regex[] UiIconNames =
    [
        new(@"\bIcon\.([A-Z]\w*)"),
        new(@"\b(?:Icon|IconSymbol|PageIcon)=""([A-Z]\w*)"""),
        new(@"<x:String x:Key=""\w*Icon\w*"">([A-Z]\w*)</x:String>"),
    ];

    private static readonly Lazy<HashSet<int>> SeagullCodepoints = new(ReadSeagullCodepoints);

    [Fact]
    public void SeagullLookup_FindsResizableIconsAndMissesTheOthers()
    {
        InSeagull(nameof(FluentGlyph.Target)).Should().BeTrue();
        InSeagull(nameof(FluentGlyph.Cd)).Should().BeFalse();
    }

    [Fact]
    public void EveryIconTheAppUses_IsInTheResizableFont()
    {
        var catalog = typeof(CatalogFluentIcons).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => ((CatalogIcon)f.GetValue(null)!).Glyph);
        var ui = CsUi.Concat(XamlUi)
            .SelectMany(file => UiIconNames.SelectMany(r => r.Matches(Text(file))))
            .Select(m => m.Groups[1].Value);

        var used = catalog.Concat(ui).Where(n => Enum.IsDefined(typeof(FluentGlyph), n)).Distinct().ToList();

        used.Count.Should().BeGreaterThan(250);
        used.Where(n => !InSeagull(n)).Should().BeEmpty("these render blank in FluentIcon's default font");
    }

    private static bool InSeagull(string name) =>
        SeagullCodepoints.Value.Contains(0xF0000 + (int)Enum.Parse<FluentGlyph>(name) * 4);

    // The cmap's format 12 subtable (Windows, full Unicode) lists the font's codepoints as runs.
    private static HashSet<int> ReadSeagullCodepoints()
    {
        var font = File.ReadAllBytes(Directory.EnumerateFiles(
            AppContext.BaseDirectory, "SeagullFluentIcons.otf", SearchOption.AllDirectories).First());
        int U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(at));
        int U32(int at) => (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(at));

        var cmap = Enumerable.Range(0, U16(4)).Select(i => 12 + (i * 16))
            .Where(entry => U32(entry) == 0x636D6170).Select(entry => U32(entry + 8)).Single();
        var table = Enumerable.Range(0, U16(cmap + 2)).Select(i => cmap + 4 + (i * 8))
            .Where(record => U16(record) == 3 && U16(record + 2) == 10).Select(record => cmap + U32(record + 4)).Single();

        var codepoints = new HashSet<int>();
        for (var group = table + 16; group < table + 16 + (U32(table + 12) * 12); group += 12)
        {
            for (var cp = U32(group); cp <= U32(group + 4); cp++)
                codepoints.Add(cp);
        }

        return codepoints;
    }
}
