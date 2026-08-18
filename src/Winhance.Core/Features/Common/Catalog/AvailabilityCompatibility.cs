namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Derives the "show all Windows versions" compatibility-message key for a Setting from its
/// <see cref="Availability"/> build ranges. The 12 merged This PC alias-pair ids are ungated
/// (Availability.Everywhere), so no message derives for them on either OS -- the intended merged-row
/// behaviour.
///
/// Final derivation rules, in order, given a non-empty Builds list that does not allow the build:
///  1. Build below 22000 (a Windows 10 machine) and every range starts at or above (22000, 0)
///     -> "Compatibility_Windows11Only".
///  2. Build at or above 22000 and every range ends at or below (21999, int.MaxValue)
///     -> "Compatibility_Windows10Only".
///  3. Exactly one range R:
///     - A Min of exactly (22000, 0) is the OS-boundary clamp, NOT an interior window bound; a Min of
///       (0, 0) is unbounded.
///     - If R.Min is an interior bound (above (0, 0) and not the 22000 boundary) AND R.Max is bounded
///       (R.Max.Build != int.MaxValue), R is a build WINDOW
///       -> "Compatibility_BuildRange|{Min.Build}-{Max.Build}".
///     - Else if the build fails the lower bound: below R.Min.Build -> "Compatibility_MinBuild|{Min.Build}";
///       equal build (so a lower revision) -> "Compatibility_MinBuild|{Min.Build}.{Min.Revision}".
///     - Else (fails the upper bound): above R.Max.Build -> "Compatibility_MaxBuild|{Max.Build}";
///       equal build (so a higher revision) -> "Compatibility_MaxBuild|{Max.Build}.{Max.Revision}".
///  4. Multiple ranges -> "Compatibility_BuildRange|" + the ranges joined as "{Min.Build}-{Max.Build}" with
///     " or ".
///
/// Pure -- no I/O, no DI.
/// </summary>
public static class AvailabilityCompatibility
{
    private static readonly WinBuild Windows11Boundary = new(22000);
    private static readonly WinBuild Windows10Ceiling = new(21999, int.MaxValue);

    /// <summary>The compatibility-message key for an unavailable setting, or null when the setting has no build
    /// gate or the build is allowed (no message is shown).</summary>
    public static string? DeriveCompatibilityMessage(Availability availability, WinBuild build)
    {
        var builds = availability.Builds;
        if (builds.Count == 0 || availability.Allows(build))
            return null;

        // Rule 1 -- Windows 10 machine, every range requires Windows 11 or later.
        if (build < Windows11Boundary && builds.All(r => r.Min >= Windows11Boundary))
            return "Compatibility_Windows11Only";

        // Rule 2 -- Windows 11 machine, every range ends inside Windows 10.
        if (build >= Windows11Boundary && builds.All(r => r.Max <= Windows10Ceiling))
            return "Compatibility_Windows10Only";

        if (builds.Count == 1)
        {
            var r = builds[0];

            // A Min of exactly (22000, 0) is the OS-boundary clamp, not an interior window bound; (0, 0) is
            // unbounded. Only a range bounded on BOTH sides by real interior bounds is a window.
            bool minIsInterior = r.Min > new WinBuild(0) && r.Min != Windows11Boundary;
            bool maxIsBounded = r.Max.Build != int.MaxValue;

            // Rule 3 window case.
            if (minIsInterior && maxIsBounded)
                return "Compatibility_BuildRange|" + FormatRange(r);

            if (build < r.Min)
            {
                // Fails the lower bound. An equal build implies build.Revision < Min.Revision, so the
                // revision form is well-defined (Min.Revision > 0).
                return build.Build < r.Min.Build
                    ? $"Compatibility_MinBuild|{r.Min.Build}"
                    : $"Compatibility_MinBuild|{r.Min.Build}.{r.Min.Revision}";
            }

            // Fails the upper bound. An equal build implies build.Revision > Max.Revision, so the revision
            // form is well-defined (Max.Revision != int.MaxValue).
            return build.Build > r.Max.Build
                ? $"Compatibility_MaxBuild|{r.Max.Build}"
                : $"Compatibility_MaxBuild|{r.Max.Build}.{r.Max.Revision}";
        }

        // Rule 4 -- multiple ranges join with " or ".
        return "Compatibility_BuildRange|" + string.Join(" or ", builds.Select(FormatRange));
    }

    private static string FormatRange(BuildRange r) => $"{r.Min.Build}-{r.Max.Build}";
}
