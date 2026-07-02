namespace Winhance.Core.Features.Common.Enums;

/// <summary>
/// Display mode for the sponsors dialog.
/// <see cref="Normal"/> shows a simple Close button. <see cref="Exit"/> adds a
/// "don't show again" checkbox and a countdown-gated close button (used when the
/// dialog is shown on app exit so the supporter list is actually read).
/// </summary>
public enum SponsorsDialogMode
{
    Normal,
    Exit
}
