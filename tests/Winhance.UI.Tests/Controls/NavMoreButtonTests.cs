using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Controls;

// The More flyout's light-dismiss layer covers the button while it is open, so the button never sees PointerExited
// and keeps its hover fill once the flyout closes. The sidebar clears it before MainWindow moves the selection back.
public class NavMoreButtonTests
{
    private static string Read(string file) =>
        File.ReadAllText(Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI", "Features", "Common", "Controls", file));

    [Fact]
    public void MoreFlyoutClosed_ClearsTheHoverBeforeTheSelectionMovesBack()
    {
        var handler = Regex.Match(Read("NavSidebar.xaml.cs"), @"void MoreMenuFlyout_Closed\([^)]*\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline)
            .Groups["body"].Value;

        handler.Should().Contain("MoreButton.ClearPointerOver();");
        handler.IndexOf("MoreButton.ClearPointerOver();", StringComparison.Ordinal)
            .Should().BeLessThan(handler.IndexOf("MoreMenuClosed?.Invoke", StringComparison.Ordinal));
    }

    [Fact]
    public void ClearPointerOver_DropsTheHoverFlagAndRedrawsTheState()
    {
        var method = Regex.Match(Read("NavButton.xaml.cs"), @"void ClearPointerOver\(\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline)
            .Groups["body"].Value;

        method.Should().Contain("_isPointerOver = false;").And.Contain("UpdateVisualState();");
    }

    // The flyout hands focus back to More when Esc closes it. Like NavigationViewItem, focus shows only as the system
    // focus rect (drawn for keyboard focus), never as the hover fill, so the button goes back to Normal.
    [Fact]
    public void Focus_ShowsAsTheSystemFocusRect_NotTheHoverFill()
    {
        var code = Read("NavButton.xaml.cs");
        var update = Regex.Match(code, @"void UpdateVisualState\(\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline)
            .Groups["body"].Value;

        update.Should().Contain("\"PointerOver\"");
        update.Should().NotContainAny("Focus", "focus");
        code.Should().NotContain("GotFocus").And.NotContain("LostFocus");
        Read("NavButton.xaml").Should().Contain("UseSystemFocusVisuals=\"True\"");
    }

    // Button takes focus on a pointer press, so a flyout it opens hands focus back to it. NavButton is a UserControl,
    // which does not, so Esc sent focus to the window's first tab stop (Back) instead of More.
    [Fact]
    public void PointerPress_FocusesTheButtonLikeAButton()
    {
        var pressed = Regex.Match(Read("NavButton.xaml.cs"), @"void RootGrid_PointerPressed\([^)]*\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline)
            .Groups["body"].Value;

        pressed.Should().Contain("Focus(FocusState.Pointer);");
    }

    // Closing a popup replays the last pointer update (WinAppSDK 2.x), so PointerEntered can fire on More under a
    // pointer that never moved and leave the hover fill on. After the flyout closes, only a real move or a press over
    // the button brings the hover back.
    [Fact]
    public void AfterTheFlyoutCloses_HoverComesBackOnlyFromAMoveOrAPress()
    {
        var code = Read("NavButton.xaml.cs");
        string Body(string name) => Regex.Match(code, @"void " + name + @"\([^)]*\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline)
            .Groups["body"].Value;

        Read("NavButton.xaml").Should().Contain("PointerMoved=\"RootGrid_PointerMoved\"");
        Body("ClearPointerOver").Should().Contain("_hoverWaitsForMove = true;");
        Body("RootGrid_PointerEntered").Should().Contain("if (_hoverWaitsForMove)");
        Body("RootGrid_PointerMoved").Should().Contain("_hoverWaitsForMove = false;").And.Contain("_isPointerOver = true;");
        Body("RootGrid_PointerPressed").Should().Contain("_hoverWaitsForMove = false;").And.Contain("_isPointerOver = true;");
        Body("RootGrid_PointerExited").Should().Contain("_hoverWaitsForMove = false;");
    }
}
