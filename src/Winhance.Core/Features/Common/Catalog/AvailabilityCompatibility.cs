namespace Winhance.Core.Features.Common.Catalog;

// The 12 merged This PC alias-pair ids are ungated, so no message derives for them on either OS.
// A range Min of exactly (22000, 0) is the OS-boundary clamp, not an interior window bound; (0, 0) is unbounded.
public static class AvailabilityCompatibility
{
    private static readonly WinBuild Windows11Boundary = new(22000);
    private static readonly WinBuild Windows10Ceiling = new(21999, int.MaxValue);

    // The title-bar filter turned off and Builder's "All Windows versions" both come through here, so the two views
    // label settings the same way. Every other scope shows only settings that fit its build, so nothing to say. A
    // setting that fits this PC is still labelled with what it needs, or a Windows 10 card and its Windows 11 twin
    // look identical.
    public static string? ForScope(Availability availability, CatalogScope scope, WinBuild liveBuild)
    {
        if (!scope.IncludeOtherOsVersions)
            return null;

        return DeriveCompatibilityMessage(availability, liveBuild)
            ?? (availability.Builds is [var only] ? DeriveCompatibilityMessage(availability, JustOutside(only)) : null);
    }

    // Past the bound that says the most: above a bounded Max ("26120 or lower"), else below Min ("26100.7171 or higher").
    private static WinBuild JustOutside(BuildRange r)
    {
        if (r.Max.Build != int.MaxValue)
            return r.Max.Revision == int.MaxValue ? new(r.Max.Build + 1) : new(r.Max.Build, r.Max.Revision + 1);

        return r.Min.Revision > 0 ? new(r.Min.Build, r.Min.Revision - 1) : new(r.Min.Build - 1, int.MaxValue);
    }

    public static string? DeriveCompatibilityMessage(Availability availability, WinBuild build)
    {
        var builds = availability.Builds;
        if (builds.Count == 0 || availability.Allows(build))
            return null;

        if (build < Windows11Boundary && builds.All(r => r.Min >= Windows11Boundary))
            return "Compatibility_Windows11Only";

        if (build >= Windows11Boundary && builds.All(r => r.Max <= Windows10Ceiling))
            return "Compatibility_Windows10Only";

        if (builds.Count == 1)
        {
            var r = builds[0];

            // A Min of exactly (22000, 0) is the OS-boundary clamp, not an interior window bound; (0, 0) is
            // unbounded. Only a range bounded on BOTH sides by real interior bounds is a window.
            bool minIsInterior = r.Min > new WinBuild(0) && r.Min != Windows11Boundary;
            bool maxIsBounded = r.Max.Build != int.MaxValue;

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

        return "Compatibility_BuildRange|" + string.Join(" or ", builds.Select(FormatRange));
    }

    private static string FormatRange(BuildRange r) => $"{r.Min.Build}-{r.Max.Build}";
}
