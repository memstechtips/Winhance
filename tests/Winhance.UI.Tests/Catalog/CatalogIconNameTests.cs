using System.Reflection;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.TestSupport;
using Xunit;

// Winhance.Core's FluentIcons class and the FluentIcons package namespace collide by name, so both
// sides need an alias - unqualified, the compiler picks the namespace and `typeof` stops compiling.
using FluentGlyph = FluentIcons.Common.Icon;
using MaterialGlyph = Material.Icons.MaterialIconKind;
using CatalogFluentIcons = Winhance.Core.Features.Common.Catalog.FluentIcons;
using CatalogMaterialIcons = Winhance.Core.Features.Common.Catalog.MaterialIcons;

namespace Winhance.UI.Tests.Catalog;

// Winhance.Core cannot reference the icon packages, so nothing there can check glyph names, and a wrong name
// renders an empty glyph with no error; this project references Winhance.UI, where FluentIcons.WinUI and
// Material.Icons.WinUI3 come in.
public class CatalogIconNameTests
{
    public static TheoryData<string, string> FluentAccessors => Data(IconPack.Fluent);

    public static TheoryData<string, string> MaterialAccessors => Data(IconPack.Material);

    public static TheoryData<string, string> AppAssetIcons
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var setting in SettingCatalog.All.Where(s => s.Display.Icon?.Pack == IconPack.AppAsset))
                data.Add(setting.Id, setting.Display.Icon!.Glyph);

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

    [Theory]
    [MemberData(nameof(MaterialAccessors))]
    public void MaterialIcons_EveryGlyphNameResolves(string accessor, string glyph)
    {
        Enum.TryParse<MaterialGlyph>(glyph, ignoreCase: false, out _)
            .Should().BeTrue($"MaterialIcons.{accessor} names '{glyph}', which is not a MaterialIconKind");
    }

    [Theory]
    [MemberData(nameof(AppAssetIcons))]
    public void AppAssetIcons_EveryFileExists(string settingId, string fileName)
    {
        File.Exists(Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI", "Assets", "AppIcons", fileName))
            .Should().BeTrue($"{settingId} names '{fileName}', which is not in src/Winhance.UI/Assets/AppIcons");
    }

    [Fact]
    public void EveryGlyphIconTheCatalogUsesComesFromAGeneratedAccessor()
    {
        // Without this, a hand-written `new Icon(IconPack.Fluent, "Typo")` on a Setting would sit
        // outside the theories entirely and stay unchecked, and so would a real glyph under the wrong pack.
        var known = new[] { IconPack.Fluent, IconPack.Material }
            .SelectMany(pack => Accessors(pack).Select(a => (pack, a.Glyph)))
            .ToHashSet();

        var used = SettingCatalog.All
            .Select(s => s.Display.Icon)
            .Where(icon => icon is not null && icon.Pack != IconPack.AppAsset)
            .Select(icon => (icon!.Pack, icon.Glyph))
            .Distinct();

        used.Where(icon => !known.Contains(icon)).Should().BeEmpty();
    }

    private static TheoryData<string, string> Data(IconPack pack)
    {
        var data = new TheoryData<string, string>();
        foreach (var (accessor, glyph) in Accessors(pack))
            data.Add(accessor, glyph);

        return data;
    }

    private static List<(string Accessor, string Glyph)> Accessors(IconPack pack)
    {
        var owner = pack switch
        {
            IconPack.Fluent => typeof(CatalogFluentIcons),
            _ => typeof(CatalogMaterialIcons),
        };

        return owner.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (f.Name, Value: f.GetValue(null) as Icon))
            .Where(x => x.Value is not null && x.Value.Pack == pack)
            .Select(x => (x.Name, x.Value!.Glyph))
            .ToList();
    }
}
