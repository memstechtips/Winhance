namespace Winhance.Core.Features.Common.Enums;

// Deliberately no Custom member: "detection could not place this" is said by the control's outcome overlay,
// and "neither Recommended nor Default" by both pills sitting dim.
public enum SettingBadgeKind
{
    Recommended,
    Default,
    Preference,
}
