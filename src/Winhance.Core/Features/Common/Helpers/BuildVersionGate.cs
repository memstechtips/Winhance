namespace Winhance.Core.Features.Common.Helpers;

/// <summary>
/// Centralized build/revision compatibility check used by every service that gates
/// settings by Windows build. Mirrors the semantics of <c>WindowsCompatibilityFilter</c>:
/// when a setting has both a major-build bound and a revision bound, the revision is
/// only compared when the current major build equals the boundary.
/// </summary>
/// <remarks>
/// A null revision bound means "no revision constraint at this boundary" — i.e. any
/// revision of the boundary build is accepted.
/// </remarks>
public static class BuildVersionGate
{
    /// <summary>
    /// Returns true when (currentBuild, currentRevision) falls inside the bounds
    /// described by the four nullable bound values. Bounds that are null are ignored.
    /// </summary>
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
