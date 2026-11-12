using System.Globalization;

namespace Winhance.Core.Features.Autounattend;

// One answer file can set up many PCs, so each gets the typed name plus a dash and four characters of its own. The BIOS
// serial survives a disk swap; a board that leaves it blank or at its factory text falls back to the first physical
// network card, then to random hex. The rename script in the answer file follows the same rules.
public static class UniqueComputerName
{
    public const string SettingId = "autounattend-computer-name-unique";

    public const string NameSettingId = "autounattend-computer-name";

    public const string DefaultPrefix = "WINHANCE";

    public const int EndingLength = 4;

    // NetBIOS caps a computer name at 15 characters.
    private const int MaxLength = 15;

    // Compared after Clean: what board makers leave in a serial field nobody filled in.
    public static IReadOnlyList<string> PlaceholderSerials { get; } =
    [
        "TOBEFILLEDBYOEM", "DEFAULTSTRING", "SYSTEMSERIALNUMBER", "CHASSISSERIALNUMBER", "NONE",
        "NOTSPECIFIED", "NOTAPPLICABLE", "123456789", "0123456789", "1234567890",
    ];

    public static string Prefix(string? typed)
    {
        var kept = new string((typed ?? "").Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').ToArray()).TrimStart('-');
        if (kept.Length == 0)
            return DefaultPrefix;

        return kept[..Math.Min(kept.Length, MaxLength - EndingLength - 1)].TrimEnd('-');
    }

    public static string? Ending(string? serial, string? networkCardAddress)
    {
        var cleaned = Clean(serial, char.IsAsciiLetterOrDigit);
        if (cleaned.Length >= EndingLength && cleaned.Distinct().Count() > 1 && !PlaceholderSerials.Contains(cleaned))
            return cleaned[^EndingLength..];

        var hex = Clean(networkCardAddress, char.IsAsciiHexDigit);
        return hex.Length >= EndingLength ? hex[^EndingLength..] : null;
    }

    public static string RandomEnding() => Random.Shared.Next(0x10000).ToString("X4", CultureInfo.InvariantCulture);

    public static string Name(string? typed, string ending) => $"{Prefix(typed)}-{ending}";

    private static string Clean(string? text, Func<char, bool> keep) =>
        new string((text ?? "").Where(keep).ToArray()).ToUpperInvariant();
}
