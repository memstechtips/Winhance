using Winhance.Core.Features.Common.Selections;

namespace Winhance.Core.Features.Common.Models;

// Described without reference to what the active mode will do: an applying mode reads SystemRequest, an
// authoring mode reads AuthoredEdit, a refusing mode reads neither. Both payloads are built by the caller - the
// only thing that knows the input's shape - so adding an input shape touches no mode and adding a mode touches
// no input shape.
public sealed record SettingWriteRequest
{
    // Minus the confirmation answers - not known until the prompt has been shown, so the applying strategy fills them in.
    public required ApplySettingRequest SystemRequest { get; init; }

    // Required rather than optional so a new input shape has to state an answer; null is legal, and the authoring
    // strategy logs it rather than dropping it in silence.
    public required SettingChoice? AuthoredEdit { get; init; }

    // The payload is opaque by design, so a strategy cannot derive a readable description; support logs are worth
    // the small duplication.
    public required string Description { get; init; }

    // The caller decides: a Custom-state pick has already confirmed intent through its own dialog and must not be
    // asked twice, and the AC/DC inputs have never prompted. Only the applying strategy reads this.
    public bool RequiresConfirmation { get; init; }

    // True only for action buttons, where one checkbox drives both CheckboxResult and ApplyRecommended.
    public bool CheckboxAlsoAppliesRecommended { get; init; }
}
