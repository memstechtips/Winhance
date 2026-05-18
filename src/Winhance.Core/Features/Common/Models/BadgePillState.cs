using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// One pill in a setting's badge row. <paramref name="Kind"/> identifies the pill type;
/// <paramref name="IsHighlighted"/> is true when the current value matches the pill's semantic
/// (or unconditionally true for the Preference pill, which is a setting-level attribute);
/// <paramref name="Label"/> and <paramref name="Tooltip"/> are pre-resolved localized strings
/// the view binds to directly. <paramref name="Mode"/> is None for the usual single-pill case
/// and AC/DC for per-mode pills on PowerCfg AC/DC Separate settings with a battery present.
/// </summary>
public sealed record BadgePillState(
    SettingBadgeKind Kind,
    bool IsHighlighted,
    string Label,
    string Tooltip,
    SettingBadgeMode Mode = SettingBadgeMode.None);
