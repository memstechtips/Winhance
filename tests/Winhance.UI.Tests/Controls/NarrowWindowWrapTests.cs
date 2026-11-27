using System.Xml.Linq;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Controls;

// At the 640 px minimum width a fixed horizontal row clips its last control in the longer locales (French cut the
// Windows 11 button and "Afficher les parametres..."). A wrap panel moves that control to the next line instead.
public class NarrowWindowWrapTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Toolkit = "using:CommunityToolkit.WinUI.Controls";

    private static XDocument Load(params string[] path) =>
        XDocument.Load(Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI", Path.Combine(path)));

    [Fact]
    public void WimUtilDownloadRow_WrapsTheButtonsUnderTheLabel()
    {
        var row = Load("Features", "WimUtil", "WimUtilPage.xaml").Descendants()
            .Single(e => e.Elements().Any(c => (string?)c.Attribute("Command") == "{x:Bind ViewModel.Step1.OpenWindows11DownloadCommand}"));

        row.Name.Should().Be(Toolkit + "WrapPanel");
        row.Elements().Select(e => e.Name.LocalName).Should().Equal("TextBlock", "Button", "Button");
    }

    [Fact]
    public void BuilderBarTargetGroup_WrapsWhenItHasARowOfItsOwn()
    {
        var group = Load("Features", "Common", "Controls", "BuilderModeBar.xaml").Descendants()
            .Single(e => (string?)e.Attribute(Xaml + "Name") == "BuilderTargetPanel");

        group.Name.Should().Be(Toolkit + "WrapPanel");
        group.Elements().Select(e => (string?)e.Attribute(Xaml + "Name"))
            .Should().Equal("BuilderConfigRadio", "BuilderAutounattendRadio", "BuilderTargetWindowsButton", "BuilderOtherHardwareCheck");
    }
}
