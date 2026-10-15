using System.Globalization;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Dialogs;

// Which sponsor cards a surface shows: gold and emerald only, emerald first. A card leaves once it carries
// Until, or the day after its Ends date, so a term ends on time even before the data is edited.
internal static class SponsorRow
{
    public static List<SponsorEntry> Pick(IEnumerable<SponsorEntry>? sponsors) =>
        Pick(sponsors, DateOnly.FromDateTime(DateTime.Now));

    public static List<SponsorEntry> Pick(IEnumerable<SponsorEntry>? sponsors, DateOnly today) =>
        (sponsors ?? Enumerable.Empty<SponsorEntry>())
            .Where(s => s.Until == null && !HasEnded(s, today) && (IsEmerald(s) || IsGold(s)))
            .OrderBy(s => IsEmerald(s) ? 0 : 1) // OrderBy is stable in .NET
            .ToList();

    public static bool IsEmerald(SponsorEntry sponsor) =>
        string.Equals(sponsor.Tier, "emerald", StringComparison.OrdinalIgnoreCase);

    private static bool HasEnded(SponsorEntry sponsor, DateOnly today) =>
        DateOnly.TryParseExact(sponsor.Ends, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ends)
        && today > ends;

    private static bool IsGold(SponsorEntry sponsor) =>
        string.Equals(sponsor.Tier, "gold", StringComparison.OrdinalIgnoreCase);
}
