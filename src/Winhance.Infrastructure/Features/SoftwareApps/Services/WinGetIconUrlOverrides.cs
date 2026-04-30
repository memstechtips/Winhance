using System;
using System.Collections.Generic;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Static fallback lookup used by <see cref="WinGetIconSource"/> when the local
/// WinGet COM catalog has no <c>Icons:</c> entry for a package (typically older
/// manifests authored before the schema added the field).
///
/// Each entry is a <c>WinGetPackageId → vendor icon URL</c> mapping, fetched at
/// runtime from the vendor's own canonical CDN — no bundled bytes, no
/// third-party dependency, no GitHub-API rate limit. Lookups are case-insensitive
/// because WinGet package IDs are themselves matched case-insensitively.
///
/// Adding an entry: when an external app has no icon at runtime, find the
/// vendor's official icon URL (favicon, brand-asset page, GitHub release
/// artifact, etc.) and add it below. Keep entries sorted by key for readability.
/// </summary>
internal static class WinGetIconUrlOverrides
{
    public static IReadOnlyDictionary<string, string> Map { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Populated incrementally as gaps are observed at runtime.
            // Example:
            //   ["Mozilla.Firefox"] = "https://www.mozilla.org/media/img/logos/firefox/logo-quantum.png",
        };

    public static bool TryGet(string winGetPackageId, out string url)
    {
        if (string.IsNullOrWhiteSpace(winGetPackageId))
        {
            url = string.Empty;
            return false;
        }

        if (Map.TryGetValue(winGetPackageId, out var found))
        {
            url = found;
            return true;
        }

        url = string.Empty;
        return false;
    }
}
