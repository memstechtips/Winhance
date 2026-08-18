using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Models;

// One table instead of the same mode comparison at each call site: "may I do X here?" was answered by "which
// mode am I in?" in a dozen places, so the layer that gates an action and the layer that greys out its control
// could disagree. A pure function of the mode rather than a member on IApplicationModeService, so it cannot be
// stubbed into contradicting CurrentMode. Only distinctions with more than one real reader live here; anything
// derivable stays derived.
public sealed record ModeCapabilities
{
    // True only in Normal: Builder applies nothing, and ConfigReview applies through its own reviewed pipeline.
    public bool AppliesToSystem { get; init; }

    // True only in Builder: the UI holds authored, un-applied intent, so a live refresh is skipped while authoring -
    // re-reading the system would clobber what the user just set. Exiting Builder reloads from live state.
    public bool AuthorsIntent { get; init; }

    // False in ConfigReview: the pending decision is accept/reject, not edit.
    public bool SettingsEditable { get; init; }

    private static readonly ModeCapabilities NormalCapabilities = new()
    {
        AppliesToSystem = true,
        AuthorsIntent = false,
        SettingsEditable = true,
    };

    private static readonly ModeCapabilities BuilderCapabilities = new()
    {
        AppliesToSystem = false,
        AuthorsIntent = true,
        SettingsEditable = true,
    };

    private static readonly ModeCapabilities ConfigReviewCapabilities = new()
    {
        AppliesToSystem = false,
        AuthorsIntent = false,
        SettingsEditable = false,
    };

    // Total over the enum on purpose - a new member must declare what it permits rather than inherit Normal's
    // answers. The throwing arm is a RUNTIME prompt (CS8509 is only a warning here);
    // ModeCapabilitiesTests.EveryDeclaredMode_HasAnExplicitRow catches a missing row before a user does.
    public static ModeCapabilities For(WinhanceMode mode) => mode switch
    {
        WinhanceMode.Normal => NormalCapabilities,
        WinhanceMode.Builder => BuilderCapabilities,
        WinhanceMode.ConfigReview => ConfigReviewCapabilities,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unhandled WinhanceMode."),
    };
}

public static class ApplicationModeServiceCapabilityExtensions
{
    // A null service answers as Normal.
    public static ModeCapabilities Capabilities(this IApplicationModeService? modeService) =>
        ModeCapabilities.For(modeService?.CurrentMode ?? WinhanceMode.Normal);
}
