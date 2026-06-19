using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>A setting = where/how (Targets, declared once) + what (States) + optional custom Detector.
/// The new unified model (design §2). Phase 1: data shape only; not wired into services.</summary>
public sealed record Setting
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? GroupName { get; init; }
    public string? Icon { get; init; }

    public IReadOnlyList<PowerContext> Contexts { get; init; } =
        new[] { PowerContext.Always };                                   // power settings override with [AC, DC]
    public IReadOnlyList<Target> Targets { get; init; } = System.Array.Empty<Target>();
    public IReadOnlyList<SettingState> States { get; init; } = System.Array.Empty<SettingState>();
    public IStateDetector? Detector { get; init; }                       // Tier-2 escape hatch
}
