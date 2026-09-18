using System.Text.RegularExpressions;
using FluentAssertions;
using Windows.System;
using Winhance.TestSupport;
using Winhance.UI.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class NavAcceleratorsTests
{
    [Fact]
    public void Tags_follow_the_sidebar_order()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI", "Features", "Common", "Controls", "NavSidebar.xaml"));
        var sidebarTags = Regex.Matches(xaml, "NavigationTag=\"([^\"]+)\"").Select(m => m.Groups[1].Value).Where(t => t != "More").ToList();

        NavAccelerators.Tags.Should().Equal(sidebarTags);
    }

    [Fact]
    public void MainWindow_declares_one_navigate_accelerator_per_tag_and_More_on_the_next_digit()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI", "MainWindow.xaml"));
        var navigateKeys = Regex.Matches(xaml, "Key=\"Number(\\d)\" Modifiers=\"Control\" Invoked=\"NavigateAccelerator_Invoked\"").Select(m => int.Parse(m.Groups[1].Value)).ToList();
        var moreKeys = Regex.Matches(xaml, "Key=\"Number(\\d)\" Modifiers=\"Control\" Invoked=\"MoreMenuAccelerator_Invoked\"").Select(m => int.Parse(m.Groups[1].Value)).ToList();

        navigateKeys.Should().Equal(Enumerable.Range(1, NavAccelerators.Tags.Count));
        moreKeys.Should().Equal(NavAccelerators.Tags.Count + 1);
    }

    [Fact]
    public void Every_tag_resolves_to_a_page()
    {
        foreach (var tag in NavAccelerators.Tags)
            NavigationRouter.TagToPageType.Should().ContainKey(tag);
    }

    [Theory]
    [InlineData(VirtualKey.Number1, "SoftwareApps")]
    [InlineData(VirtualKey.Number6, "Settings")]
    [InlineData(VirtualKey.Number7, null)]
    [InlineData(VirtualKey.A, null)]
    public void TagFor_maps_digits_positionally(VirtualKey key, string? expected)
    {
        NavAccelerators.TagFor(key).Should().Be(expected);
    }
}
