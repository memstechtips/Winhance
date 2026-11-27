using System.Globalization;
using System.Xml.Linq;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Controls;

// The working-directory card holds a checkbox and a button side by side. At the toolkit's default wrap width (476)
// they keep their row and squeeze the header to one letter per line, so this card moves them under the header sooner.
public class WorkingDirectoryCardTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Toolkit = "using:CommunityToolkit.WinUI.Controls";

    [Fact]
    public void WorkingDirectoryCard_WrapsItsControlsBelowTheHeaderBeforeTheHeaderIsSqueezed()
    {
        var card = XDocument.Load(Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI", "Features", "WimUtil", "WimUtilPage.xaml"))
            .Descendants(Toolkit + "SettingsCard")
            .Single(c => (string?)c.Attribute("Header") == "{x:Bind ViewModel.Step1.SelectDirectoryCard.Title}");

        var threshold = card.Elements(Toolkit + "SettingsCard.Resources").Elements(Xaml + "Double")
            .SingleOrDefault(d => (string?)d.Attribute(Xaml + "Key") == "SettingsCardWrapThreshold");

        threshold.Should().NotBeNull();
        double.Parse(threshold!.Value, CultureInfo.InvariantCulture).Should().BeGreaterThanOrEqualTo(700);
    }

    // Under the header the row gets only the card's width. German needs about 430, so a fixed row clipped the button;
    // a wrap panel moves the button under the checkbox instead.
    [Fact]
    public void WorkingDirectoryCard_WrapsTheButtonUnderTheCheckboxWhenTheRowIsTooNarrow()
    {
        var card = XDocument.Load(Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI", "Features", "WimUtil", "WimUtilPage.xaml"))
            .Descendants(Toolkit + "SettingsCard")
            .Single(c => (string?)c.Attribute("Header") == "{x:Bind ViewModel.Step1.SelectDirectoryCard.Title}");

        var row = card.Elements().Single(e => !e.Name.LocalName.StartsWith("SettingsCard.", StringComparison.Ordinal));

        row.Name.Should().Be(Toolkit + "WrapPanel");
        row.Elements().Select(e => e.Name.LocalName).Should().Equal("CheckBox", "Button");
    }
}
