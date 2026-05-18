namespace Winhance.Core.Features.Common.Enums;

/// <summary>
/// Optional per-mode discriminator for <see cref="SettingBadgeKind"/> pills on
/// PowerCfg AC/DC Separate settings when the system has a battery. Two pills can
/// share the same Kind (e.g. Recommended) and be distinguished by Mode (AC vs DC).
/// </summary>
public enum SettingBadgeMode
{
    None,
    AC,
    DC,
}
