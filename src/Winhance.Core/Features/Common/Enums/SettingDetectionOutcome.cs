namespace Winhance.Core.Features.Common.Enums;

/// <summary>
/// The honest result of trying to place a setting on one of its declared states. Replaces the old
/// bool "IsCustomState", which conflated three materially different situations - the axis that
/// matters is whether the user can safely act on the setting.
/// </summary>
public enum SettingDetectionOutcome
{
    /// <summary>Detection placed the setting on a known state. The normal case.</summary>
    Resolved,

    /// <summary>The value is present and readable in the shape the target expects, but its content
    /// matches no declared state. Deliberately NOT subdivided by cause (user / another tool /
    /// legacy) - that would assert something we cannot know. Actionable: choosing a state writes a
    /// known value.</summary>
    Custom,

    /// <summary>The value is present and readable but stored under the WRONG registry type for its
    /// target - e.g. a REG_BINARY bitmask target whose value is REG_SZ, so the bit reduction cannot
    /// run at all. The data is typically intact (a registry string is UTF-16LE, so the original
    /// bytes are still there). Actionable: choosing a state writes a known value AND repairs the
    /// storage type, because every write passes the catalog's declared kind.</summary>
    Malformed,

    /// <summary>Detection threw and we do not know the setting's current value. This is the app
    /// admitting its own failure, not a statement about the user's system. NOT actionable: offering
    /// an apply here would write blind over data we could not read.</summary>
    Undetermined,
}
