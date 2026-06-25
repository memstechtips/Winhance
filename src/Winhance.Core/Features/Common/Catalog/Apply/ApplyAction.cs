namespace Winhance.Core.Features.Common.Catalog;

/// <summary>A follow-on apply the relationship resolver schedules: put <see cref="SettingId"/> into
/// <see cref="StateLabel"/>. Force means apply even if it is already in that state.</summary>
public sealed record ApplyAction(string SettingId, string StateLabel, bool Force = false);
