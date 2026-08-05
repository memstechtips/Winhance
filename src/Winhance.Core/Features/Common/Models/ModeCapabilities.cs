using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// What a <see cref="WinhanceMode"/> permits, expressed as data in one table instead of as the
/// same comparison repeated at each call site.
///
/// The problem this solves: "may I do X here?" was being answered by asking "which mode am I in?"
/// in a dozen places, so the answer could differ between the layer that gates an action and the
/// layer that greys out the control for it. Naming the capability makes the two read the same fact.
///
/// Deliberately a pure function of the mode rather than a member on
/// <see cref="IApplicationModeService"/>: a capability that could be stubbed independently of
/// <see cref="IApplicationModeService.CurrentMode"/> could contradict it, and every existing test
/// that already arranges a mode keeps working with no extra setup.
///
/// Only distinctions with more than one real reader live here. Anything derivable is left derived —
/// e.g. the restart banner is shown exactly when <see cref="AppliesToSystem"/> is true, so it is not
/// a separate flag.
/// </summary>
public sealed record ModeCapabilities
{
    /// <summary>
    /// Changes reach the live machine. True only in Normal.
    ///
    /// Builder authors a file and applies nothing; ConfigReview applies through its own reviewed
    /// pipeline rather than through the per-setting write path, so neither grants this.
    /// </summary>
    public bool AppliesToSystem { get; init; }

    /// <summary>
    /// The UI holds authored, un-applied intent that must be recorded rather than applied — and
    /// must not be overwritten by a re-read of live system state. True only in Builder.
    ///
    /// This is the reason a live refresh is skipped while authoring: re-reading the system would
    /// clobber the values the user just set. Exiting Builder reloads from live state, which is the
    /// intended behaviour (the UI must show current system values again) and is unaffected by this.
    /// </summary>
    public bool AuthorsIntent { get; init; }

    /// <summary>
    /// The user may change setting values. False in ConfigReview, where the cards are read-only
    /// because the pending decision is accept/reject, not edit.
    /// </summary>
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

    /// <summary>
    /// The capabilities of <paramref name="mode"/>. Total over the enum on purpose — a new member
    /// must declare what it permits rather than silently inheriting Normal's answers.
    ///
    /// <para>The throwing arm is what makes that a prompt, and it is a <b>runtime</b> one: an unhandled
    /// member in a switch expression is CS8509, a warning, and this repo does not treat warnings as
    /// errors. <c>ModeCapabilitiesTests.EveryDeclaredMode_HasAnExplicitRow</c> maps every declared mode
    /// through here, so a new one that forgets its row fails there rather than reaching a user.</para>
    /// </summary>
    public static ModeCapabilities For(WinhanceMode mode) => mode switch
    {
        WinhanceMode.Normal => NormalCapabilities,
        WinhanceMode.Builder => BuilderCapabilities,
        WinhanceMode.ConfigReview => ConfigReviewCapabilities,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unhandled WinhanceMode."),
    };
}

/// <summary>
/// Reads <see cref="ModeCapabilities"/> off the mode service at a call site.
/// </summary>
public static class ApplicationModeServiceCapabilityExtensions
{
    /// <summary>
    /// The capabilities of the service's current mode. A null service answers as Normal, matching
    /// the pre-existing <c>_service?.CurrentMode == WinhanceMode.Builder</c> null handling at the
    /// sites this replaces.
    /// </summary>
    public static ModeCapabilities Capabilities(this IApplicationModeService? modeService) =>
        ModeCapabilities.For(modeService?.CurrentMode ?? WinhanceMode.Normal);
}
