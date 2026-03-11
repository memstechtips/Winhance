using System;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Wraps plan-level P/Invoke operations on power schemes for testability.
/// </summary>
public interface IPowerSchemeOperations
{
    /// <summary>
    /// Deletes a power scheme by GUID. Safe to call on non-existent schemes
    /// (returns an error code but does not throw).
    /// </summary>
    uint DeleteScheme(Guid schemeGuid);

    /// <summary>
    /// Duplicates a power scheme, returning a new GUID assigned by Windows.
    /// </summary>
    uint DuplicateScheme(Guid sourceGuid, out Guid destinationGuid);

    /// <summary>
    /// Sets the active power scheme.
    /// </summary>
    uint SetActiveScheme(Guid schemeGuid);

    /// <summary>
    /// Writes the friendly name for a power scheme.
    /// </summary>
    uint WriteFriendlyName(Guid schemeGuid, string name);

    /// <summary>
    /// Writes the description for a power scheme.
    /// </summary>
    uint WriteDescription(Guid schemeGuid, string description);
}
