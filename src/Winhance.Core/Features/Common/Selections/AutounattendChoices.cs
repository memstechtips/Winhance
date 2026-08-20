namespace Winhance.Core.Features.Common.Selections;

// Choices that exist only in an autounattend (no live-machine counterpart, no place in .winhance) - the slot for
// the Builder+Autounattend-only page. Add members here, never on SettingChoice.
public sealed record AutounattendChoices
{
    public static readonly AutounattendChoices None = new();
}
