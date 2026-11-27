using System.Xml.Linq;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

// x:Load on the direct content of a toolkit SettingsExpander stopped the Settings page from opening, with no exception
// logged and nothing caught by the build. A panel child is the shape every working x:Load in the app uses.
// A Window has no FindName, so x:Load there never builds the element: the literal form leaves it null, which crashed
// the app with nothing logged, and the x:Bind form does not compile. Build such a part as a UserControl in code.
public class XLoadParentTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly HashSet<string> Panels =
        ["StackPanel", "Grid", "RelativePanel", "Canvas", "VariableSizedWrapGrid", "WrapPanel"];

    [Fact]
    public void DeferredElements_Always_SitInAPanel()
    {
        var deferred = RepoPaths.SourceFiles("*.xaml")
            .SelectMany(f => XDocument.Load(f).Descendants().Select(e => (File: Path.GetFileName(f), Element: e)))
            .Where(x => x.Element.Attribute(Xaml + "Load") is not null)
            .ToList();

        deferred.Should().NotBeEmpty();
        deferred.Where(x => x.Element.Document!.Root!.Name.LocalName == "Window")
            .Select(x => $"{x.File}: {(string?)x.Element.Attribute(Xaml + "Name")}")
            .Should().BeEmpty();
        deferred.Where(x => !Panels.Contains(x.Element.Parent!.Name.LocalName))
            .Select(x => $"{x.File}: {x.Element.Parent!.Name.LocalName} > {x.Element.Name.LocalName} {(string?)x.Element.Attribute(Xaml + "Name")}")
            .Should().BeEmpty();
    }
}
