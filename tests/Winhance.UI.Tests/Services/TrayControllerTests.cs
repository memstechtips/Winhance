using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class TrayControllerTests
{
    [Theory]
    [InlineData(true, false, 3600, false)]
    [InlineData(false, false, 3600, true)]
    [InlineData(false, false, 5, false)]
    [InlineData(false, false, 11, true)]
    [InlineData(false, true, 300, false)]
    [InlineData(false, true, 601, true)]
    public void CanLeave_Always_LeavesOnceIdleAndNotificationHasHadItsTime(bool busy, bool waitsForClick, int secondsSinceNotice, bool leaves) =>
        TrayController.CanLeave(busy, waitsForClick, TimeSpan.FromSeconds(secondsSinceNotice)).Should().Be(leaves);

    [Fact]
    public void BuildMenu_WithActiveProfileWhileBusy_TicksTheActiveProfileAndGreysProfiles()
    {
        var localization = new Mock<ILocalizationService>();
        localization.Setup(l => l.GetString(It.IsAny<string>())).Returns<string>(k => k);
        IReadOnlyList<SettingsProfile> profiles =
        [
            new("builtin:focus", "Winhance Focus", ProfileKind.Focus, null),
            new("Gaming", "Gaming", ProfileKind.User, @"C:\Profiles\Gaming.winhance"),
        ];

        var menu = TrayController.BuildMenu(localization.Object, profiles, activeId: "Gaming", busy: true, new TrayStatus());

        menu.Select(i => i.Command).Should().Equal(TrayController.OpenItem, -1, TrayController.CheckItem, TrayMenuItem.Separator.Command, TrayController.ExitItem);
        menu[1].Children!.Select(i => (i.Command, i.Checked, i.Enabled)).Should().Equal(
            (TrayController.FirstProfileItem, false, false),
            (TrayController.FirstProfileItem + 1, true, false));
    }

    private static Mock<ILocalizationService> Localization()
    {
        var localization = new Mock<ILocalizationService>();
        localization.Setup(l => l.GetString(It.IsAny<string>())).Returns<string>(k => k);
        localization.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns<string, object[]>((key, args) => $"{key}: {string.Join(", ", args)}");
        return localization;
    }

    [Fact]
    public void BuildMenu_WhenIdle_HasNoStateLineAndNoUpdateItem()
    {
        var menu = TrayController.BuildMenu(Localization().Object, [], null, busy: false, new TrayStatus());

        menu.Select(i => i.Command).Should().Equal(TrayController.OpenItem, -1, TrayController.CheckItem, TrayMenuItem.Separator.Command, TrayController.ExitItem);
    }

    [Theory]
    [InlineData(3, null, false, "Tray_State_AppUpdates: 3", true)]
    [InlineData(0, "v26.11.20", false, "Tray_State_WinhanceUpdate: v26.11.20", false)]
    [InlineData(0, null, true, "Tray_State_Attention", false)]
    public void BuildMenu_WithActiveState_LeadsTheMenuAndOnlyWaitingAppUpdatesAddTheUpdateItem(int apps, string? version, bool attention, string line, bool updateItem)
    {
        var menu = TrayController.BuildMenu(Localization().Object, [], null, busy: false, new TrayStatus(apps, version, attention));

        menu[0].Should().Be(new TrayMenuItem(line, TrayController.StateItem));
        menu[1].Should().Be(TrayMenuItem.Separator);
        menu.Any(i => i.Command == TrayController.UpdateAppsItem).Should().Be(updateItem);
        if (updateItem)
            menu.Single(i => i.Command == TrayController.UpdateAppsItem).Text.Should().Be("Tray_Menu_UpdateApps: 3");
    }
}
