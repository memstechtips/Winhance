namespace Winhance.Core.Features.Common.TechnicalDetails;

// A 68-character HKEY_LOCAL_MACHINE path in the group header would stretch the group far wider than its values
// need; the full path is still what the tooltip shows and regedit opens.
public static class RegistryPathFormatter
{
    // Longest first, so HKEY_CURRENT_USER is never mistaken for a prefix of HKEY_CURRENT_CONFIG.
    private static readonly (string Hive, string Abbreviation)[] Hives =
    [
        ("HKEY_CURRENT_CONFIG", "HKCC"),
        ("HKEY_LOCAL_MACHINE", "HKLM"),
        ("HKEY_CURRENT_USER", "HKCU"),
        ("HKEY_CLASSES_ROOT", "HKCR"),
        ("HKEY_USERS", "HKU"),
    ];

    public static string Abbreviate(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        foreach (var (hive, abbreviation) in Hives)
        {
            if (!path.StartsWith(hive, StringComparison.OrdinalIgnoreCase)) continue;

            // Only a whole segment counts. Without this, a key literally named HKEY_USERS_BACKUP
            // would be rewritten to HKU_BACKUP and point somewhere that does not exist.
            if (path.Length > hive.Length && path[hive.Length] != '\\') continue;

            return string.Concat(abbreviation, path.AsSpan(hive.Length));
        }

        return path;
    }
}
