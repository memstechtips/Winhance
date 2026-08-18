namespace Winhance.Core.Features.Common.Interfaces;

public interface IPowerSchemeOperations
{
    // Safe on a non-existent scheme: returns an error code, does not throw.
    uint DeleteScheme(Guid schemeGuid);

    uint DuplicateScheme(Guid sourceGuid, out Guid destinationGuid);

    uint SetActiveScheme(Guid schemeGuid);

    uint WriteFriendlyName(Guid schemeGuid, string name);

    uint WriteDescription(Guid schemeGuid, string description);
}
