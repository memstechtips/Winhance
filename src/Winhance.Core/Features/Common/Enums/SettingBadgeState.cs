namespace Winhance.Core.Features.Common.Enums;

public enum SettingBadgeState
{
    Recommended, // Green - all entries match recommended
    Default,     // Grey  - all entries match default
    Custom,      // Amber - anything else (user set a value not in the known set)
    Preference   // Blue  - subjective setting; any known value is "on the user"
}
