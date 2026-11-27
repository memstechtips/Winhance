using System.Reflection;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

// Winhance.Core's FluentIcons class and the FluentIcons package namespace collide by name, so both
// sides need an alias - unqualified, the compiler picks the namespace and `typeof` stops compiling.
using FluentGlyph = FluentIcons.Common.Icon;
using CatalogFluentIcons = Winhance.Core.Features.Common.Catalog.FluentIcons;

namespace Winhance.UI.Tests.Catalog;

// Winhance.Core cannot reference the icon packages, so nothing there can check glyph names, and a wrong name
// renders an empty glyph with no error; this project references Winhance.UI, where FluentIcons.WinUI comes in.
public class CatalogIconNameTests
{
    public static TheoryData<string, string> FluentAccessors
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var (accessor, icon) in Accessors())
                data.Add(accessor, icon.Glyph);

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(FluentAccessors))]
    public void FluentIcons_EveryGlyphNameResolves(string accessor, string glyph)
    {
        Enum.TryParse<FluentGlyph>(glyph, ignoreCase: false, out _)
            .Should().BeTrue($"FluentIcons.{accessor} names '{glyph}', which is not a FluentIcons.Common.Icon");
    }

    [Fact]
    public void CatalogSettingCards_UseOnlyGeneratedFluentIcons()
    {
        var known = Accessors().Select(entry => entry.Icon).ToHashSet();
        var used = SettingCatalog.All.Where(s => s.Display.Icon is not null)
            .Select(s => (s.Id, Icon: s.Display.Icon!)).ToList();

        used.Should().HaveCountGreaterThan(300);
        used.Where(s => !known.Contains(s.Icon))
            .Select(s => $"{s.Id}: {s.Icon.Pack}/{s.Icon.Glyph}")
            .Should().BeEmpty("settings use Fluent icons; software item and background task logos are not catalog settings");
    }

    [Fact]
    public void BingSearchSetting_UsesGlobeSearch()
    {
        Assert.Equal(new Icon(IconPack.Fluent, nameof(FluentGlyph.GlobeSearch)),
            SettingCatalog.ById["start-disable-bing-search-results"].Display.Icon);
    }

    private static IEnumerable<(string Accessor, Icon Icon)> Accessors() =>
        typeof(CatalogFluentIcons).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (f.Name, Value: f.GetValue(null)))
            .Where(entry => entry.Value is Icon { Pack: IconPack.Fluent })
            .Select(entry => (entry.Name, (Icon)entry.Value!));
}
