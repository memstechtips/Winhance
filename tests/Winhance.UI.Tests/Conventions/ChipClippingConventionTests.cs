using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;

namespace Winhance.UI.Tests.Conventions;

// A status chip is a 20 px capsule, so any container that pins its height or its row to a number cuts the chip off the
// moment its neighbours grow: a description that wraps to two lines, a longer language, larger Windows text.
// Heights that hold a chip are Auto, or a star row that the chip row never has to share.
public class ChipClippingConventionTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly Regex ChipStyle = new(@"^\{StaticResource (?:Badge\w+Style|NewBadgeBorderStyle)\}$");

    [Fact]
    public void Chips_NeverSitInsideAFixedHeight()
    {
        var offenders = new List<string>();
        var styles = XamlUi.SelectMany(f => XDocument.Parse(Text(f)).Descendants().Where(e => e.Name.LocalName == "Style"))
            .Where(e => e.Attribute(Xaml + "Key") is not null)
            .ToDictionary(e => (string)e.Attribute(Xaml + "Key")!);
        foreach (var file in XamlUi)
        {
            var root = XDocument.Parse(Text(file)).Root!;
            foreach (var chip in root.Descendants().Where(IsChip))
            {
                foreach (var name in new[] { "Height", "MaxHeight" })
                {
                    if ((string?)chip.Attribute(name) is { } own && own != "Auto")
                        offenders.Add($"{file}: a chip sets {name}=\"{own}\"");
                }

                foreach (var style in StyleChain(styles, (string)chip.Attribute("Style")!))
                {
                    foreach (var setter in style.Descendants().Where(e => e.Name.LocalName == "Setter"
                        && (string?)e.Attribute("Property") is "Height" or "MaxHeight"))
                        offenders.Add($"{(string?)style.Attribute(Xaml + "Key")}: a chip style sets {(string?)setter.Attribute("Property")}");
                }

                for (var child = chip; child.Parent is { } parent; child = parent)
                {
                    foreach (var name in new[] { "Height", "MaxHeight" })
                    {
                        if ((string?)parent.Attribute(name) is { } value && value != "Auto")
                            offenders.Add($"{file}: <{parent.Name.LocalName} {name}=\"{value}\"> holds a chip");
                    }

                    if (parent.Name.LocalName == "Grid" && RowHeight(parent, child) is { } row && row != "Auto" && !row.EndsWith('*'))
                        offenders.Add($"{file}: a Grid row of fixed height {row} holds a chip");
                }
            }
        }

        offenders.Distinct().Should().BeEmpty(
            "give the container Auto height (MinHeight to keep a floor) or a star row, so the chip is never cut off");
    }

    [Fact]
    public void AppCard_ChipsWrapInAnAutoRowAndTheDescriptionAbsorbsAnyShortage()
    {
        var card = XDocument.Parse(Text(Ui + "Features/SoftwareApps/SoftwareAppsPage.xaml")).Root!.Descendants()
            .Single(e => (string?)e.Attribute(Xaml + "Key") == "AppCardTemplate");
        var description = card.Descendants().Single(e => (string?)e.Attribute(Xaml + "Name") == "DescriptionText");
        var rows = description.Parent!;
        var chipRow = rows.Elements().Single(e => e.Descendants().Any(IsChip));

        RowHeight(rows, description).Should().Be("*", "a taller description must take its space from the description, not the chips");
        RowHeight(rows, chipRow).Should().Be("Auto");
        chipRow.Name.LocalName.Should().Be("WrapPanel", "a horizontal StackPanel clips the last chip when the card is narrow");
    }

    private static IEnumerable<XElement> StyleChain(Dictionary<string, XElement> styles, string reference)
    {
        var key = Regex.Match(reference, @"\{StaticResource (\w+)\}").Groups[1].Value;
        while (styles.TryGetValue(key, out var style))
        {
            yield return style;
            key = Regex.Match((string?)style.Attribute("BasedOn") ?? "", @"\{StaticResource (\w+)\}").Groups[1].Value;
        }
    }

    private static bool IsChip(XElement e) =>
        e.Name.LocalName == "Border" && ChipStyle.IsMatch((string?)e.Attribute("Style") ?? "");

    private static string? RowHeight(XElement grid, XElement child)
    {
        var rows = grid.Element(grid.Name.Namespace + "Grid.RowDefinitions")?.Elements().ToList();
        var index = (int?)child.Attribute("Grid.Row") ?? 0;
        return rows is null || index >= rows.Count ? null : (string?)rows[index].Attribute("Height") ?? "1*";
    }
}
