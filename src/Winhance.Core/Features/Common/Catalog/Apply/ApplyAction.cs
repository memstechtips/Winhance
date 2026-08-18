namespace Winhance.Core.Features.Common.Catalog;

// Force: apply even if already in that state. IsReset: a reverse-cascade reset-to-default, so a target with a
// ResetSet override deletes instead of writing its Set value.
public sealed record ApplyAction(string SettingId, string StateLabel, bool Force = false, bool IsReset = false);
