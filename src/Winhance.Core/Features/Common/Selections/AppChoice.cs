namespace Winhance.Core.Features.Common.Selections;

public sealed record AppChoice(
    string Id,
    string Name,
    string[]? AppxPackageName,
    string? CapabilityName,
    string? OptionalFeatureName,
    string? WinGetPackageId);
