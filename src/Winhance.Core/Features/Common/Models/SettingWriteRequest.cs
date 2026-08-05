namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// One user edit to one setting, described without reference to what the active mode will do with
/// it. A mode that applies reads <see cref="SystemRequest"/>; a mode that authors reads
/// <see cref="AuthoredEdit"/>; a mode that refuses reads neither.
///
/// Both payloads are built by the caller, because the caller is the only thing that knows the
/// input's shape — a toggle, a dropdown, a slider, an AC/DC pair, an action button. Shape knowledge
/// stays at the call site and mode knowledge stays in the strategy, so neither has to enumerate the
/// other. That is the whole point of the split: adding an input shape does not touch any mode, and
/// adding a mode does not touch any input shape.
/// </summary>
public sealed record SettingWriteRequest
{
    /// <summary>
    /// What to send to the machine, minus the confirmation answers — those are not known until the
    /// prompt has been shown, so the applying strategy fills them in. See
    /// <see cref="CheckboxAlsoAppliesRecommended"/>.
    /// </summary>
    public required ApplySettingRequest SystemRequest { get; init; }

    /// <summary>
    /// What to record when the mode authors intent instead of applying, or null when this edit has
    /// no serializable form. Required rather than optional so that a new input shape has to state
    /// an answer; null is a legal one, and the authoring strategy logs it rather than dropping it
    /// in silence.
    /// </summary>
    public required BuilderEdit? AuthoredEdit { get; init; }

    /// <summary>
    /// What the user did, for the log — "toggle to True", "AC=1 DC=2". The payload above is opaque
    /// by design, so a strategy cannot derive a readable description from it, and support logs are
    /// worth more than the small duplication.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Whether to prompt before touching the machine. The caller decides, because the answer is not
    /// purely a property of the setting: a Custom-state pick has already confirmed intent through
    /// its own dialog and must not be asked twice, and the AC/DC inputs have never prompted.
    ///
    /// Only the applying strategy reads this — authoring and refusal have nothing to confirm.
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Whether the confirmation checkbox also means "apply this feature's recommended settings".
    /// True only for action buttons, where one checkbox drives both
    /// <see cref="ApplySettingRequest.CheckboxResult"/> and
    /// <see cref="ApplySettingRequest.ApplyRecommended"/>; everywhere else it is passed through
    /// as the checkbox result alone.
    /// </summary>
    public bool CheckboxAlsoAppliesRecommended { get; init; }
}
