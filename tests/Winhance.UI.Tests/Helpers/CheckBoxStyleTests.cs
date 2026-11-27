using System.Xml.Linq;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

// Every CheckBox takes the implicit style in CheckBoxStyle.xaml: WinUI's template with the check drawn as still Paths.
// An AnimatedIcon check costs an animation player per box and draws a ghost check inside a hovered SettingsCard.
public class CheckBoxStyleTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static IEnumerable<string> XamlFiles() => RepoPaths.SourceFiles("*.xaml");

    private static XDocument StyleFile() => XDocument.Load(XamlFiles().Single(f => Path.GetFileName(f) == "CheckBoxStyle.xaml"));

    [Fact]
    public void CheckBoxStyle_DrawsTheCheckAndDashAsStillPaths()
    {
        var doc = StyleFile();

        doc.Descendants().Should().NotContain(e => e.Name.LocalName == "AnimatedIcon");
        doc.Descendants(Presentation + "Path").Select(p => (string?)p.Attribute(Xaml + "Name"))
            .Should().BeEquivalentTo("CheckGlyph", "IndeterminateGlyph");
    }

    [Fact]
    public void App_MergesTheCheckBoxStyle()
    {
        var app = XamlFiles().Single(f => Path.GetFileName(f) == "App.xaml");

        File.ReadAllText(app).Should().Contain("ms-appx:///Features/Common/Resources/CheckBoxStyle.xaml");
    }

    [Fact]
    public void XamlFiles_Always_DefineCheckBoxStylesOnlyInCheckBoxStyle()
    {
        var offenders = XamlFiles()
            .Where(f => Path.GetFileName(f) != "CheckBoxStyle.xaml")
            .SelectMany(f => XDocument.Load(f).Descendants()
                .Where(e => (e.Name.LocalName is "Style" or "ControlTemplate") && (string?)e.Attribute("TargetType") == "CheckBox")
                .Select(_ => Path.GetFileName(f)))
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void XamlFiles_Always_HaveNoCheckBoxSettingItsOwnStyle()
    {
        var offenders = XamlFiles()
            .SelectMany(f => XDocument.Load(f).Descendants(Presentation + "CheckBox")
                .Where(c => c.Attribute("Style") is not null || c.Attribute("Template") is not null)
                .Select(_ => Path.GetFileName(f)))
            .ToList();

        offenders.Should().BeEmpty();
    }
}
