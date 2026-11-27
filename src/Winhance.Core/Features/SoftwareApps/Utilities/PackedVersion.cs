using System.Globalization;

namespace Winhance.Core.Features.SoftwareApps.Utilities;

// The Store catalog writes a version as one 64-bit number: major<<48 | minor<<32 | build<<16 | revision.
public static class PackedVersion
{
    public static bool TryParse(string? decimalText, out ulong packed) =>
        ulong.TryParse(decimalText, NumberStyles.None, CultureInfo.InvariantCulture, out packed);

    public static bool TryFromDotted(string? dotted, out ulong packed)
    {
        packed = 0;
        var parts = dotted?.Trim().Split('.');
        if (parts is not { Length: >= 1 and <= 4 })
            return false;

        foreach (var part in parts)
        {
            if (!ushort.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                return false;
            packed = (packed << 16) | number;
        }

        packed <<= 16 * (4 - parts.Length);
        return true;
    }

    public static ulong FromVersion(Version version) =>
        ((ulong)(ushort)version.Major << 48) | ((ulong)(ushort)version.Minor << 32)
        | ((ulong)(ushort)Math.Max(version.Build, 0) << 16) | (ushort)Math.Max(version.Revision, 0);

    public static string ToDotted(ulong packed) =>
        $"{packed >> 48}.{(packed >> 32) & 0xFFFF}.{(packed >> 16) & 0xFFFF}.{packed & 0xFFFF}";
}
