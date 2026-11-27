using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;

namespace Winhance.UI.Tests.Conventions;

// Every scrollable page opens where the user left it, for the session, through ScrollMemory. Both cases rebuild the
// scroller, which then opens at the top: an uncached page on every visit, an x:Load part each time it shows.
public class ScrollMemoryConventionTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] Scrollers = ["ListView", "GridView", "ScrollViewer", "DataGrid"];
    private static readonly string[] Templates = ["DataTemplate", "ControlTemplate", "ItemsPanelTemplate"];

    [Fact]
    public void UncachedPages_KeepTheirScrollPosition()
    {
        var pages = XamlUi.Where(f => Regex.IsMatch(Text(f), @"\bNavigationCacheMode=""Disabled""")).ToList();
        pages.Should().NotBeEmpty();

        // A hosted control counts only when it is given a key itself: SettingsListView calls ScrollMemory, but only
        // for a host that sets its ScrollMemoryKey.
        var missing = pages.Where(p => !UsesScrollMemory(p) && !HostedControls(p).Any(SetsScrollMemoryKey)).ToList();

        missing.Should().BeEmpty(
            "call ScrollMemory.Keep on the page's scroller in its code-behind, or set ScrollMemoryKey on the SettingsListView it hosts");
    }

    [Fact]
    public void ScrollersBuiltByXLoad_KeepTheirScrollPosition()
    {
        var missing = new List<string>();
        foreach (var file in XamlUi)
        {
            var code = CodeBehind(file);
            foreach (var scroller in Parse(file).Descendants().Where(IsBuiltByXLoad))
            {
                var name = (string?)scroller.Attribute(X + "Name");
                if (name is null || !(PassedToScrollMemory(code, name) || LoadedHandlerUsesScrollMemory(code, (string?)scroller.Attribute("Loaded"))))
                    missing.Add($"{file}: {name ?? scroller.Name.LocalName}");
            }
        }

        missing.Should().BeEmpty(
            "pass the scroller's x:Name to a ScrollMemory call in the code-behind, or give it a Loaded handler that does");
    }

    private static bool IsBuiltByXLoad(XElement element)
    {
        if (!Scrollers.Contains(element.Name.LocalName))
            return false;
        var self = element.AncestorsAndSelf().ToList();
        return !self.Any(e => Templates.Contains(e.Name.LocalName)) && self.Any(e => e.Attribute(X + "Load") is not null);
    }

    private static bool PassedToScrollMemory(string code, string name) =>
        Regex.IsMatch(code, @"\bScrollMemory\.\w+\([^;]*\b" + Regex.Escape(name) + @"\b");

    private static bool LoadedHandlerUsesScrollMemory(string code, string? handler)
    {
        if (handler is null || Regex.Match(code, @"\bvoid\s+" + Regex.Escape(handler) + @"\s*\(") is not { Success: true } m)
            return false;
        var open = m.Index + m.Length - 1;
        return StatementAfter(code, CloseParen(code, open)).Contains("ScrollMemory.", StringComparison.Ordinal);
    }

    private static bool UsesScrollMemory(string xaml) =>
        SetsScrollMemoryKey(xaml) || Regex.IsMatch(CodeBehind(xaml), @"\bScrollMemory\.\w+\(");

    private static bool SetsScrollMemoryKey(string xaml) =>
        Regex.IsMatch(Text(xaml), @"\bScrollMemoryKey=""") || Regex.IsMatch(CodeBehind(xaml), @"\bScrollMemoryKey\s*=(?!=)");

    private static IEnumerable<string> HostedControls(string xaml)
    {
        var names = Parse(xaml).Descendants()
            .Where(e => e.Name.NamespaceName.StartsWith("using:Winhance.", StringComparison.Ordinal))
            .Select(e => "/" + e.Name.LocalName + ".xaml")
            .ToHashSet();
        return XamlUi.Where(f => names.Any(n => f.EndsWith(n, StringComparison.Ordinal)));
    }

    private static string CodeBehind(string xaml) =>
        File.Exists(Path.Combine(Root, xaml + ".cs")) ? Text(xaml + ".cs") : string.Empty;

    private static XDocument Parse(string xaml) => XDocument.Load(Path.Combine(Root, xaml));
}
