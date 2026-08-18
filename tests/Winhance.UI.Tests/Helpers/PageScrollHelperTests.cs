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
}
