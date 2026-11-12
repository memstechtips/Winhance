using System.Xml.Linq;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

// The page hosts the whole settings view in the shell Optimize and Customize use; WIMUtil hosts only its included cards.
public class AutounattendPageShellTests
{
    private static readonly XNamespace Controls = "using:Winhance.UI.Features.Common.Controls";
    private static readonly XNamespace Autounattend = "using:Winhance.UI.Features.Autounattend";
    private static readonly string FeaturesDir = Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI", "Features");

    [Fact]
    public void AutounattendPage_Always_IsTheSharedShellWithTheSettingsViewInItsContentSlot()
    {
        var page = XDocument.Load(Path.Combine(FeaturesDir, "Autounattend", "AutounattendPage.xaml")).Root!;

        page.Name.Should().Be(Controls + "SectionPage");
        var shell = page.Elements().Should().ContainSingle().Subject;
        shell.Name.Should().Be(Controls + "SectionPageShell");
        shell.Element(Controls + "SectionPageShell.SectionContent")!.Elements()
            .Should().ContainSingle().Which.Name.Should().Be(Autounattend + "AutounattendSettingsView");
    }

    [Fact]
    public void WimUtilPage_Always_HostsTheIncludedCardsInStepThreeAndNotTheWholeSettingsView()
    {
        var page = XDocument.Load(Path.Combine(FeaturesDir, "WimUtil", "WimUtilPage.xaml"));

        page.Descendants(Autounattend + "AutounattendSettingsView").Should().BeEmpty();
        var included = page.Descendants(Autounattend + "IncludedFromFeaturesView").Should().ContainSingle().Subject;
        included.Attribute("LoadsItself")!.Value.Should().Be("True");
        included.Attribute("ShowsModeTags")!.Value.Should().Be("True");
    }

    [Fact]
    public void AutounattendSettingsView_Always_DrawsItsIncludedCardsWithTheSharedView()
    {
        var view = XDocument.Load(Path.Combine(FeaturesDir, "Autounattend", "AutounattendSettingsView.xaml"));

        view.Descendants(Autounattend + "IncludedFromFeaturesView").Should().ContainSingle();
    }

    // Step 2 holds the answer-file choice, step 3 shows its summary above the drivers and updates, and the Generate card is gone.
    [Fact]
    public void WimUtilPage_Always_PutsTheChangesAboveTheDriversAndUpdates()
    {
        var elements = XDocument.Load(Path.Combine(FeaturesDir, "WimUtil", "WimUtilPage.xaml")).Descendants().ToList();
        var drivers = elements.FindIndex(e => (string?)e.Attribute("IsChecked") == "{x:Bind ViewModel.Step3.AddSystemDrivers, Mode=TwoWay}");
        int Index(string attribute, string binding) =>
            elements.FindIndex(e => (string?)e.Attribute(attribute) == $"{{x:Bind ViewModel.{binding}}}");

        drivers.Should().BeGreaterThan(0);
        Index("IsChecked", "Step2.IsBuilderChosen, Mode=OneWay").Should().BeInRange(0, drivers);
        elements.FindIndex(e => e.Name == Autounattend + "IncludedFromFeaturesView").Should().BeInRange(0, drivers);
        Index("Command", "OpenAutounattendCommand").Should().BeInRange(0, drivers);
        Index("Command", "GenerateWinhanceXmlCommand").Should().Be(-1);
        Index("Command", "StartAutounattendSessionCommand").Should().Be(-1);
    }
}
