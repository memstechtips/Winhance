using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

/// <summary>The answer to one <see cref="SettingWriteRequest"/>.</summary>
public sealed record SettingWriteResult
{
    public required SettingWriteOutcome Outcome { get; init; }

    /// <summary>
    /// How the confirmation checkbox was left, for the one caller that needs it: an action whose
    /// checkbox applied a whole feature's recommended settings has changed the settings this card
    /// sits beside, so that list has to be reloaded. Always false when no prompt was shown.
    /// </summary>
    public bool ConfirmationCheckboxChecked { get; init; }

    /// <summary>Nothing happened; the caller must put the card back the way it was.</summary>
    public static SettingWriteResult Rejected { get; } =
        new() { Outcome = SettingWriteOutcome.Rejected };

    /// <summary>The edit was stored as authored intent and not applied.</summary>
    public static SettingWriteResult Recorded { get; } =
        new() { Outcome = SettingWriteOutcome.Recorded };
}
