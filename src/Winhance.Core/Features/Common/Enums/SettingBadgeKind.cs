namespace Winhance.Core.Features.Common.Enums;

/// <summary>
/// The pills on a settings card, which answer "where does this value sit relative to our advice".
///
/// There is deliberately no Custom member. It used to mean two different things at once - "detection
/// could not place this" and "this value is neither Recommended nor Default" - and both are now said
/// better elsewhere: the first by the control's own outcome overlay and banner, which name WHICH kind
/// of problem instead of flattening all three to the word "Custom"; the second by Recommended and
/// Default sitting dim together, which already reads as "at neither".
/// </summary>
public enum SettingBadgeKind
{
    Recommended,
    Default,
    Preference,
}
