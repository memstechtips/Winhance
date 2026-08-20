using FluentAssertions;
using Windows.Graphics;
using Winhance.UI.Features.Common.Utilities;
using Xunit;

namespace Winhance.UI.Tests.Utilities;

public class WindowSizeManagerTests
{
    private static RectInt32 R(int x, int y, int w, int h) => new(x, y, w, h);

    // A single 1920x1080 primary display with a standard taskbar reserved.
    private static readonly RectInt32[] SingleDisplay = { R(0, 0, 1920, 1032) };

    // Two displays side-by-side: primary (1920x1080) + secondary to the right (1920x1080).
    private static readonly RectInt32[] DualDisplay = { R(0, 0, 1920, 1032), R(1920, 0, 1920, 1032) };

    [Fact]
    public void IsWindowRectVisible_FullyOnPrimary_ReturnsTrue()
    {
        WindowSizeManager.IsWindowRectVisible(R(100, 100, 1280, 800), SingleDisplay)
            .Should().BeTrue();
    }

    [Fact]
    public void IsWindowRectVisible_PartiallyOffRight_StillVisibleEnough_ReturnsTrue()
    {
        // Window mostly off the right edge but with 300px still visible.
        WindowSizeManager.IsWindowRectVisible(R(1620, 100, 1280, 800), SingleDisplay)
            .Should().BeTrue();
    }

    [Fact]
    public void IsWindowRectVisible_OffScreenNegative_FromDisconnectedMonitor_ReturnsFalse()
    {
        // Reproduces issue #585: secondary monitor was to the left, saved Left=-1524.
        // After disconnect, only primary (0..1920) remains — window is entirely off.
        WindowSizeManager.IsWindowRectVisible(R(-1524, 200, 1280, 800), SingleDisplay)
            .Should().BeFalse();
    }

    [Fact]
    public void IsWindowRectVisible_OnSecondaryDisplay_ReturnsTrue()
    {
        WindowSizeManager.IsWindowRectVisible(R(2500, 100, 1280, 800), DualDisplay)
            .Should().BeTrue();
    }

    [Fact]
    public void IsWindowRectVisible_OnSecondary_AfterItDisconnects_ReturnsFalse()
    {
        WindowSizeManager.IsWindowRectVisible(R(2500, 100, 1280, 800), SingleDisplay)
            .Should().BeFalse();
    }

    [Fact]
    public void IsWindowRectVisible_TinySliverOverlap_ReturnsFalse()
    {
        // Only 10 pixels of the window's right edge are on the primary — below threshold.
        WindowSizeManager.IsWindowRectVisible(R(-1270, 100, 1280, 800), SingleDisplay)
            .Should().BeFalse();
    }

    [Fact]
    public void IsWindowRectVisible_OverlapMeetsMinimumExactly_ReturnsTrue()
    {
        int x = 1920 - WindowSizeManager.MinVisibleWidth;
        WindowSizeManager.IsWindowRectVisible(R(x, 100, 1280, 800), SingleDisplay)
            .Should().BeTrue();
    }

    [Fact]
    public void IsWindowRectVisible_OverlapOnePixelShy_ReturnsFalse()
    {
        int x = 1920 - WindowSizeManager.MinVisibleWidth + 1;
        WindowSizeManager.IsWindowRectVisible(R(x, 100, 1280, 800), SingleDisplay)
            .Should().BeFalse();
    }

    [Fact]
    public void IsWindowRectVisible_EmptyDisplayList_ReturnsFalse()
    {
        WindowSizeManager.IsWindowRectVisible(R(0, 0, 1280, 800), System.Array.Empty<RectInt32>())
            .Should().BeFalse();
    }

    [Fact]
    public void IsWindowRectVisible_BelowBottomEdge_ReturnsFalse()
    {
        // Window positioned below the work area (e.g. tall monitor removed).
        WindowSizeManager.IsWindowRectVisible(R(100, 2000, 1280, 800), SingleDisplay)
            .Should().BeFalse();
    }
}
