namespace Winhance.Core.Features.Common.Interfaces;

public interface IPowerSchemeOperations
{
    // Safe on a non-existent scheme: returns an error code, does not throw.
    uint DeleteScheme(Guid schemeGuid);

    // desiredGuid null lets the API pick one. Supplying it is what powercfg.exe /duplicatescheme does;
    // destinationGuid always reports what was actually created, which can differ from what was asked for.
    uint DuplicateScheme(Guid sourceGuid, Guid? desiredGuid, out Guid destinationGuid);

    uint SetActiveScheme(Guid schemeGuid);

    uint WriteFriendlyName(Guid schemeGuid, string name);

    uint WriteDescription(Guid schemeGuid, string description);
}
