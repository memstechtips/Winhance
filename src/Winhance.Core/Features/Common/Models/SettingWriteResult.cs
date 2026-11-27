using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

public sealed record SettingWriteResult
{
    public required SettingWriteOutcome Outcome { get; init; }

    // For the one caller that needs it: an action whose checkbox applied a feature's recommended settings has
    // changed the settings beside this card, so that list is reloaded. Always false when no prompt was shown.
    public bool ConfirmationCheckboxChecked { get; init; }

    // The caller must put the card back the way it was.
    public static SettingWriteResult Rejected { get; } =
        new() { Outcome = SettingWriteOutcome.Rejected };

    public static SettingWriteResult Recorded { get; } =
        new() { Outcome = SettingWriteOutcome.Recorded };
}
