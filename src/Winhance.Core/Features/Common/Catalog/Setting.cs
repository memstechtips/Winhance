using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>A setting: identity (Id) + what the user sees (Display) + where/how (Targets) + what (States) +
/// optional custom Detector, gating (Availability), apply behaviour (Apply), and relationships. The new
/// unified model. Pure data; not yet wired into live services.</summary>
public sealed record Setting
{
    public required string Id { get; init; }                         // the contract: configs + loc keys key off this
    public required Display Display { get; init; }                   // everything the user sees

    public IReadOnlyList<PowerContext> Contexts { get; init; } = new[] { PowerContext.Always };
    public IReadOnlyList<Target> Targets { get; init; } = System.Array.Empty<Target>();
    public IReadOnlyList<SettingState> States { get; init; } = System.Array.Empty<SettingState>();
    public IStateDetector? Detector { get; init; }

    public Availability Availability { get; init; } = Availability.Everywhere;   // gating
    public ApplyBehavior Apply { get; init; } = ApplyBehavior.None;              // confirmation + restart

    public IReadOnlyList<Link> Links { get; init; } = System.Array.Empty<Link>();

    /// <summary>Presentation only: nest this setting under the parent in the UI and disable its control when
    /// the parent is off. No apply behaviour. Null = top-level.</summary>
    public string? UiParentId { get; init; }
}
