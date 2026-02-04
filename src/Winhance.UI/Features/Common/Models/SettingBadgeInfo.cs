namespace Winhance.UI.Features.Common.Models;

/// <summary>
/// Types of badges that can be displayed on settings.
/// </summary>
public enum BadgeType
{
    /// <summary>
    /// Setting requires Windows 11.
    /// </summary>
    Win11,

    /// <summary>
    /// Setting requires Windows 10.
    /// </summary>
    Win10,

    /// <summary>
    /// Setting requires a specific Windows build range.
    /// </summary>
    WinBuild
}

/// <summary>
/// Information about a badge to display on a setting.
/// </summary>
public record SettingBadgeInfo
{
    /// <summary>
    /// The type of badge (determines styling).
    /// </summary>
    public BadgeType Type { get; init; }

    /// <summary>
    /// The short text to display on the badge (e.g., "10", "11", "10/11").
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// The tooltip text shown when hovering over the badge (full localized message).
    /// </summary>
    public string Tooltip { get; init; } = string.Empty;

    public SettingBadgeInfo(BadgeType type, string text, string tooltip)
    {
        Type = type;
        Text = text;
        Tooltip = tooltip;
    }
}
