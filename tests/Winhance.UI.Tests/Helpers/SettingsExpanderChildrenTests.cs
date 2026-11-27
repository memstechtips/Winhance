using System.Xml.Linq;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

// SettingsExpander gives every child without a local Style its SettingsCard style, and WinUI throws (exit
// 0xc000027b) when that style meets another element type, e.g. an InfoBar.
public class SettingsExpanderChildrenTests
{
    private static readonly XNamespace Controls = "using:CommunityToolkit.WinUI.Controls";
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static IEnumerable<XDocument> Pages() => RepoPaths.SourceFiles("*.xaml").Select(f => XDocument.Load(f));

    private static bool IsSafe(XElement child) =>
        child.Name == Controls + "SettingsCard" || child.Attribute("Style") is not null;

    [Theory]
    [InlineData("ActivePill", "ScriptPresentPill")]
    [InlineData("InactivePill", "ScriptMissingPill")]
    public void BackgroundTaskScriptPills_MatchTheCorrespondingTaskStyles(string taskPill, string scriptPill)
    {
        var page = XDocument.Load(RepoPaths.SourceFiles("SettingsPage.xaml").Single());
        var badges = page.Descendants(Presentation + "Border");
        var task = badges.Single(badge => (string?)badge.Attribute(Xaml + "Name") == taskPill);
        var script = badges.Single(badge => (string?)badge.Attribute(Xaml + "Name") == scriptPill);

        ((string?)script.Attribute("Style")).Should().Be((string?)task.Attribute("Style"));
        ((string?)script.Element(Presentation + "TextBlock")!.Attribute("Foreground"))
            .Should().Be((string?)task.Element(Presentation + "TextBlock")!.Attribute("Foreground"));
    }

    [Fact]
    public void SettingsExpanderItems_Always_AreASettingsCardOrHaveTheirOwnStyle()
    {
        var offenders = Pages()
            .SelectMany(page => page.Descendants(Controls + "SettingsExpander.Items"))
            .SelectMany(items => items.Elements())
            .Where(child => !IsSafe(child))
            .Select(child => child.Name.LocalName)
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void SettingsExpanderItemTemplates_Always_HaveASettingsCardRootOrTheirOwnStyle()
    {
        var templates = Pages().SelectMany(page => page.Descendants(Controls + "SettingsExpander.ItemTemplate"))
            .SelectMany(holder => holder.Descendants(Presentation + "DataTemplate"))
            .Concat(Pages().SelectMany(page => page.Descendants()
                .Where(e => e.Name.LocalName.StartsWith("BackgroundRowTemplateSelector.", StringComparison.Ordinal))
                .SelectMany(holder => holder.Elements(Presentation + "DataTemplate"))));

        var offenders = templates
            .Select(template => template.Elements().First())
            .Where(root => !IsSafe(root))
            .Select(root => root.Name.LocalName)
            .ToList();

        offenders.Should().BeEmpty();
    }
}
