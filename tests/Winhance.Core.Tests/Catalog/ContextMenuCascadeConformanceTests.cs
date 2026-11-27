using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

// Each menu is one cascade on the desktop right-click menu, with its entries as sub-keys under it. Turning the
// menu off deletes the whole key, so its entries go off with it; the last entry going off takes the menu with it.
public class ContextMenuCascadeConformanceTests
{
    private static readonly LocKey On = LocKey.Common.Enabled;
    private static readonly LocKey Off = LocKey.Common.Disabled;

    public static IEnumerable<object[]> Menus() => new[]
    {
        new object[] { "context-menu-repair-menu" },
        new object[] { "context-menu-troubleshooting-menu" },
    };

    private static List<Setting> Entries(string menuId) =>
        SettingCatalog.All.Where(s => s.UiParentId == menuId).ToList();

    [Theory]
    [MemberData(nameof(Menus))]
    public void ResolveForward_MenuTurnedOff_TurnsEveryEntryOff(string menuId)
    {
        var menu = SettingCatalog.Find(menuId)!;
        var entries = Entries(menuId);
        entries.Should().NotBeEmpty();

        var actions = RelationshipResolver.ResolveForward(menu, Off, _ => On);

        actions.Select(a => (a.SettingId, a.StateLabel)).Should()
            .BeEquivalentTo(entries.Select(e => (e.Id, Off)));
    }

    [Theory]
    [MemberData(nameof(Menus))]
    public void ResolveForward_EntryTurnedOn_TurnsItsMenuOn(string menuId)
    {
        foreach (var entry in Entries(menuId))
        {
            var actions = RelationshipResolver.ResolveForward(entry, On, id => id == menuId ? Off : On);

            actions.Should().Contain(a => a.SettingId == menuId && a.StateLabel == On, entry.Id);
        }
    }

    [Theory]
    [MemberData(nameof(Menus))]
    public void ResolveReverseSync_LastEntryTurnedOff_TurnsTheMenuOff(string menuId)
    {
        var menu = SettingCatalog.Find(menuId)!;
        var changed = Entries(menuId)[0].Id;

        var actions = RelationshipResolver.ResolveReverseSync(changed, new[] { menu }, id => id == menuId ? On : Off);

        actions.Should().ContainSingle().Which.Should().Be(new ApplyAction(menuId, Off));
    }

    [Theory]
    [MemberData(nameof(Menus))]
    public void ResolveReverseSync_AnEntryStillOn_KeepsTheMenuOn(string menuId)
    {
        var menu = SettingCatalog.Find(menuId)!;
        var entries = Entries(menuId);

        var actions = RelationshipResolver.ResolveReverseSync(entries[0].Id, new[] { menu },
            id => id == menuId || id == entries[^1].Id ? On : Off);

        actions.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Menus))]
    public void Entries_Always_LiveUnderTheirMenuKey(string menuId)
    {
        var menuPath = SettingCatalog.Find(menuId)!.Targets.OfType<RegTarget>().Single().Paths.Single();

        foreach (var entry in Entries(menuId))
            entry.Targets.OfType<RegTarget>().Single().Paths.Single().Should().StartWith(menuPath + @"\shell\", entry.Id);
    }
}
