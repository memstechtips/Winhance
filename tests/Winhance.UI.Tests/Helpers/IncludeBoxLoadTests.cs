using System.Xml.Linq;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

// The include boxes exist only while authoring. Behind Visibility, every card on the cached Optimize and Customize
// pages would build its box, check glyphs and bindings in Normal mode too, and keep them updating on every change.
public class IncludeBoxLoadTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string[] BuilderOnlyProperties = ["ShowIncludeBox", "ShowIncludeCheckbox"];

    private static List<XAttribute> BuilderOnlyBindings() => RepoPaths.SourceFiles("*.xaml")
        .SelectMany(f => XDocument.Load(f).Descendants())
        .SelectMany(e => e.Attributes())
        .Where(a => BuilderOnlyProperties.Any(p => a.Value.StartsWith("{x:Bind " + p, StringComparison.Ordinal)))
        .ToList();

    [Fact]
    public void IncludeBoxes_Always_LoadOnlyWhileAuthoring()
    {
        var bindings = BuilderOnlyBindings();

        bindings.Select(a => a.Value).Should().Contain(v => v.Contains("ShowIncludeBox"))
            .And.Contain(v => v.Contains("ShowIncludeCheckbox"));
        bindings.Where(a => a.Name != Xaml + "Load")
            .Select(a => $"{a.Parent!.Name.LocalName}.{a.Name.LocalName}")
            .Should().BeEmpty();
    }
}
