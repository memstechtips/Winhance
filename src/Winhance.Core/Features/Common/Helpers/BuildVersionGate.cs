namespace Winhance.Core.Features.Common.Helpers;

// When a setting has both a build bound and a revision bound, the revision is only compared when the current
// build equals the boundary. A null revision bound accepts any revision of the boundary build.
public static class BuildVersionGate
{
    public static bool IsCompatible(
        int currentBuild,
        int currentRevision,
        int? minBuild,
        int? minRevision,
        int? maxBuild,
        int? maxRevision)
    {
        if (minBuild.HasValue)
        {
            if (currentBuild < minBuild.Value) return false;
            if (currentBuild == minBuild.Value
                && minRevision.HasValue
                && currentRevision < minRevision.Value)
            {
                return false;
            }
        }

        if (maxBuild.HasValue)
        {
            if (currentBuild > maxBuild.Value) return false;
            if (currentBuild == maxBuild.Value
                && maxRevision.HasValue
                && currentRevision > maxRevision.Value)
            {
                return false;
            }
        }

        return true;
    }
}
