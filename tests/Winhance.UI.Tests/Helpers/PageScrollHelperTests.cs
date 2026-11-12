using FluentAssertions;
using Windows.System;
using Winhance.UI.Features.Common.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

// ShouldSkipForFocusedElement walks the visual tree and cannot be exercised without a live XAML island.
public class PageScrollHelperTests
{
    [Theory]
    [InlineData(VirtualKey.PageUp)]
    [InlineData(VirtualKey.PageDown)]
    [InlineData(VirtualKey.Home)]
    [InlineData(VirtualKey.End)]
    public void IsPagingKey_ForInterceptedKeys_ReturnsTrue(VirtualKey key)
    {
        PageScrollHelper.IsPagingKey(key).Should().BeTrue();
    }

    [Theory]
    [InlineData(VirtualKey.Up)]
    [InlineData(VirtualKey.Down)]
    [InlineData(VirtualKey.Left)]
    [InlineData(VirtualKey.Right)]
    [InlineData(VirtualKey.Tab)]
    [InlineData(VirtualKey.Enter)]
    [InlineData(VirtualKey.Space)]
    [InlineData(VirtualKey.Escape)]
    [InlineData(VirtualKey.A)]
    public void IsPagingKey_ForOtherKeys_ReturnsFalse(VirtualKey key)
    {
        PageScrollHelper.IsPagingKey(key).Should().BeFalse();
    }

    [Theory]
    [InlineData(VirtualKey.PageDown, 100, 1000, 5000, 250)]
    [InlineData(VirtualKey.PageUp, 400, 1000, 5000, 250)]
    [InlineData(VirtualKey.Home, 400, 1000, 5000, 0)]
    [InlineData(VirtualKey.End, 400, 1000, 5000, 5000)]
    public void TargetOffset_StepsAFractionOfTheViewport(VirtualKey key, double offset, double viewport, double scrollable, double expected)
    {
        PageScrollHelper.TargetOffset(key, offset, viewport, scrollable).Should().Be(expected);
    }

    [Theory]
    [InlineData(VirtualKey.PageUp, 50, 0)]
    [InlineData(VirtualKey.PageDown, 4950, 5000)]
    public void TargetOffset_StaysInsideTheScrollableRange(VirtualKey key, double offset, double expected)
    {
        PageScrollHelper.TargetOffset(key, offset, 1000, 5000).Should().Be(expected);
    }

    [Theory]
    [InlineData(VirtualKey.PageDown, 0)]
    [InlineData(VirtualKey.End, -1)]
    [InlineData(VirtualKey.Down, 5000)]
    public void TargetOffset_WithNothingToScrollOrNoPagingKey_IsNull(VirtualKey key, double scrollable)
    {
        PageScrollHelper.TargetOffset(key, 0, 1000, scrollable).Should().BeNull();
    }
}
