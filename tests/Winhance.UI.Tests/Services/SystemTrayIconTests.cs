using FluentAssertions;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class SystemTrayIconTests
{
    [Theory]
    [InlineData(TrayMenuTheme.System, 1)]
    [InlineData(TrayMenuTheme.Dark, 2)]
    [InlineData(TrayMenuTheme.Light, 3)]
    public void PreferredAppMode_EachMenuTheme_MapsToItsAppMode(TrayMenuTheme theme, int mode) =>
        SystemTrayIcon.PreferredAppMode(theme).Should().Be(mode);

    [Theory]
    [InlineData(TrayBadge.None, null)]
    [InlineData(TrayBadge.AppUpdates, "tray-updates.ico")]
    [InlineData(TrayBadge.WinhanceUpdate, "tray-winhance-update.ico")]
    [InlineData(TrayBadge.Attention, "tray-attention.ico")]
    public void BadgeIconFile_EachBadge_ReturnsItsIconFile(TrayBadge badge, string? file) =>
        SystemTrayIcon.BadgeIconFile(badge).Should().Be(file);

    [Theory]
    [InlineData("R&D", "R&&D")]
    [InlineData("Gaming", "Gaming")]
    [InlineData("A && B", "A &&&& B")]
    public void MenuText_KeepsAnAmpersandVisible(string name, string expected) =>
        SystemTrayIcon.MenuText(name).Should().Be(expected);
}
