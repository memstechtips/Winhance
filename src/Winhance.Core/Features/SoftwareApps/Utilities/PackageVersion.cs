namespace Winhance.Core.Features.SoftwareApps.Utilities;

// Package managers report versions System.Version cannot read: a leading v, a -beta or +build suffix, more than four
// parts, parts too big for an int. Compare is null when either side has no version number to read.
public static class PackageVersion
{
    public static int? Compare(string? left, string? right)
    {
        if (!TryParse(left, out var leftNumbers, out var leftLabel) || !TryParse(right, out var rightNumbers, out var rightLabel))
            return null;

        for (var i = 0; i < Math.Max(leftNumbers.Length, rightNumbers.Length); i++)
        {
            var result = CompareNumbers(
                i < leftNumbers.Length ? leftNumbers[i] : "0",
                i < rightNumbers.Length ? rightNumbers[i] : "0");
            if (result != 0)
                return result;
        }

        return CompareLabels(leftLabel, rightLabel);
    }

    // Only a strictly newer version is an update. When a side cannot be read, the package manager's own verdict
    // stands, since it compared them by its own rules; the same string is never an update.
    public static bool IsUpdate(string? available, string? installed) =>
        Compare(available, installed) is { } result
            ? result > 0
            : !string.IsNullOrWhiteSpace(available)
                && !string.Equals(available.Trim(), installed?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool TryParse(string? text, out string[] numbers, out string label)
    {
        numbers = [];
        label = string.Empty;
        var version = text?.Trim() ?? string.Empty;
        var metadata = version.IndexOf('+');
        if (metadata >= 0)
            version = version[..metadata];
        if (version.Length > 0 && version[0] is 'v' or 'V')
            version = version[1..];

        var end = 0;
        while (end < version.Length
            && (char.IsAsciiDigit(version[end])
                || (version[end] == '.' && end > 0 && end + 1 < version.Length && char.IsAsciiDigit(version[end + 1]))))
            end++;
        if (end == 0)
            return false;

        numbers = version[..end].Split('.');
        label = version[end..].TrimStart('-', '.', '_', ' ');
        return true;
    }

    // Compared as digit strings so a date-stamped part like 20261101 cannot overflow.
    private static int CompareNumbers(string left, string right)
    {
        left = left.TrimStart('0');
        right = right.TrimStart('0');
        return left.Length != right.Length
            ? left.Length.CompareTo(right.Length)
            : Math.Sign(string.CompareOrdinal(left, right));
    }

    // SemVer precedence: a release outranks its pre-releases, numeric parts compare as numbers and rank below words.
    private static int CompareLabels(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
            return right.Length.CompareTo(left.Length);

        var leftParts = left.Split('.', '-');
        var rightParts = right.Split('.', '-');
        for (var i = 0; i < Math.Min(leftParts.Length, rightParts.Length); i++)
        {
            var leftIsNumber = leftParts[i].Length > 0 && leftParts[i].All(char.IsAsciiDigit);
            var rightIsNumber = rightParts[i].Length > 0 && rightParts[i].All(char.IsAsciiDigit);
            var result = (leftIsNumber, rightIsNumber) switch
            {
                (true, true) => CompareNumbers(leftParts[i], rightParts[i]),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.Compare(leftParts[i], rightParts[i], StringComparison.OrdinalIgnoreCase),
            };
            if (result != 0)
                return Math.Sign(result);
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }
}
