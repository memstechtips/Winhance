using System;

namespace Winhance.Core.Features.Common.TechnicalDetails;

/// <summary>
/// Shortens a registry path for display in the option matrix's spanning group header.
///
/// The header sits directly above the value columns it owns, so its width is charged to those
/// columns: a 68-character HKEY_LOCAL_MACHINE path would stretch the group far wider than its
/// values need and push the table into sideways scrolling for no benefit. Abbreviating the hive
/// is the convention every registry tool uses, and the full path is still what the tooltip shows
/// and what the Registry Editor button opens -- nothing is lost, only shortened.
/// </summary>
public static class RegistryPathFormatter
{
    /// <summary>
    /// Longest first, so HKEY_CURRENT_USER is never mistaken for a prefix of HKEY_CURRENT_CONFIG.
    /// The separator check below makes the order redundant, but reading top-down should not
    /// require noticing that.
    /// </summary>
    private static readonly (string Hive, string Abbreviation)[] Hives =
    [
        ("HKEY_CURRENT_CONFIG", "HKCC"),
        ("HKEY_LOCAL_MACHINE", "HKLM"),
        ("HKEY_CURRENT_USER", "HKCU"),
        ("HKEY_CLASSES_ROOT", "HKCR"),
        ("HKEY_USERS", "HKU"),
    ];

    /// <summary>
    /// Replaces a leading hive name with its short form. Anything else -- a scheduled-task path,
    /// an already-abbreviated path, a relative key -- is returned unchanged.
    /// </summary>
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
