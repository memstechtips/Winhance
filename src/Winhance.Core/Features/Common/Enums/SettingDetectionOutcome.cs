namespace Winhance.Core.Features.Common.Enums;

public enum SettingDetectionOutcome
{
    Resolved,

    // Deliberately NOT subdivided by cause (user / another tool / legacy) - that would assert something we cannot
    // know. Actionable: choosing a state writes a known value.
    Custom,

    // e.g. a REG_BINARY bitmask target whose value is REG_SZ. The data is typically intact (a registry string is
    // UTF-16LE); choosing a state writes a known value AND repairs the storage type.
    Malformed,

    // The app admitting its own failure, not a statement about the user's system. NOT actionable: applying would
    // write blind over data we could not read.
    Undetermined,
}
