namespace Winhance.Core.Features.Common.Catalog;

/// <summary>A follow-on apply the relationship resolver schedules: put <see cref="SettingId"/> into
/// <see cref="StateLabel"/>. Force means apply even if it is already in that state. IsReset means the follow-on is a
/// reset-to-default (the reverse-cascade): the funnel applies it with ResetToDefault=true, so a target carrying a
/// ResetSet override (the [1,null] Explorer settings) DELETEs instead of writing its normal Set value - matching the
/// old DependencyManager cascade.</summary>
public sealed record ApplyAction(string SettingId, string StateLabel, bool Force = false, bool IsReset = false);
