using System.Xml.Linq;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Controls;

// A horizontal StackPanel measures its children at infinite width, so a long title (French WIMUtil at 640 px) ran off
// the edge instead of wrapping. The title sits in a star column, wraps between words, trims past two lines and
// shows the full text as a tooltip only when it is trimmed, as Files does for its trimmed names.
public class PageHeaderTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string Source(string file) =>
        Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI", "Features", "Common", "Controls", file);

    [Fact]
    public void Title_WrapsAndTrimsInsideTheWidthItIsGiven()
    {
        var title = XDocument.Load(Source("PageHeader.xaml")).Descendants()
            .Single(e => (string?)e.Attribute(Xaml + "Name") == "TitleText");

        title.Ancestors().Should().NotContain(a => a.Name.LocalName == "StackPanel" && (string?)a.Attribute("Orientation") == "Horizontal");
        ((string?)title.Attribute("TextWrapping")).Should().Be("WrapWholeWords");
        ((string?)title.Attribute("TextTrimming")).Should().Be("CharacterEllipsis");
        ((string?)title.Attribute("MaxLines")).Should().Be("2");
        ((string?)title.Attribute("IsTextTrimmedChanged")).Should().Be("TitleText_IsTextTrimmedChanged");
    }

    [Fact]
    public void TrimmedTitle_ShowsTheFullTextAsATooltip()
    {
        File.ReadAllText(Source("PageHeader.xaml.cs"))
            .Should().Contain("ToolTipService.SetToolTip(sender, sender.IsTextTrimmed ? sender.Text : null);");
    }
}
