using FluentAssertions;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Dialogs;
using Xunit;

namespace Winhance.UI.Tests.Dialogs;

public class SponsorRowTests
{
    private static SponsorEntry Sponsor(string name, string tier, string? until = null) =>
        new() { Name = name, Tier = tier, Until = until };

    [Fact]
    public void Pick_EmeraldFirst_GoldKeepsArrayOrder()
    {
        var all = new[] { Sponsor("A", "gold"), Sponsor("B", "gold"), Sponsor("C", "emerald") };

        SponsorRow.Pick(all).Select(s => s.Name).Should().Equal("C", "A", "B");
    }

    [Fact]
    public void Pick_IgnoresLowerTiersAndExpiredListings()
    {
        var all = new[]
        {
            Sponsor("Silver Co", "silver"),
            Sponsor("Bronze Co", "bronze"),
            Sponsor("Gone Co", "gold", until: "2026-01"),
            Sponsor("Live Co", "gold"),
        };

        SponsorRow.Pick(all).Select(s => s.Name).Should().Equal("Live Co");
    }

    [Fact]
    public void Pick_ShowsACardThroughItsEndsDate_ThenDropsIt()
    {
        var all = new[] { new SponsorEntry { Name = "Term Co", Tier = "gold", Ends = "2027-04-06" } };

        SponsorRow.Pick(all, new DateOnly(2027, 4, 6)).Should().ContainSingle();
        SponsorRow.Pick(all, new DateOnly(2027, 4, 7)).Should().BeEmpty();
    }

    [Fact]
    public void Pick_UnreadableEndsDate_KeepsTheCard()
    {
        var all = new[] { new SponsorEntry { Name = "Odd Co", Tier = "gold", Ends = "April" } };

        SponsorRow.Pick(all, new DateOnly(2030, 1, 1)).Should().ContainSingle();
    }

    [Fact]
    public void Pick_NullList_IsEmpty()
    {
        SponsorRow.Pick(null).Should().BeEmpty();
    }
}
