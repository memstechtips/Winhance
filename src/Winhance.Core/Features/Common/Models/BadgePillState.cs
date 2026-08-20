using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

// IsHighlighted is unconditionally true for the Preference pill, which is a setting-level attribute.
public sealed record BadgePillState(
    SettingBadgeKind Kind,
    bool IsHighlighted,
    string Label,
    string Tooltip,
    SettingBadgeMode Mode = SettingBadgeMode.None);
