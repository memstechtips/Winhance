using System.Xml.Linq;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

// The page and WIMUtil host the same view; only the page wraps it in the shell Optimize and Customize use.
public class AutounattendPageShellTests
{
    private static readonly XNamespace Controls = "using:Winhance.UI.Features.Common.Controls";
    private static readonly XNamespace Autounattend = "using:Winhance.UI.Features.Autounattend";
    private static readonly string FeaturesDir = Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI", "Features");

    [Fact]
    public void The_page_is_the_shared_shell_with_the_settings_view_in_its_content_slot()
    {
        var page = XDocument.Load(Path.Combine(FeaturesDir, "Autounattend", "AutounattendPage.xaml")).Root!;

        page.Name.Should().Be(Controls + "SectionPage");
        var shell = page.Elements().Should().ContainSingle().Subject;
        shell.Name.Should().Be(Controls + "SectionPageShell");
        shell.Element(Controls + "SectionPageShell.SectionContent")!.Elements()
            .Should().ContainSingle().Which.Name.Should().Be(Autounattend + "AutounattendSettingsView");
    }

    [Fact]
    public void WIMUtil_still_embeds_the_settings_view_itself()
    {
        var page = XDocument.Load(Path.Combine(FeaturesDir, "WimUtil", "WimUtilPage.xaml"));

        page.Descendants(Autounattend + "AutounattendSettingsView").Should().ContainSingle();
    }
}
