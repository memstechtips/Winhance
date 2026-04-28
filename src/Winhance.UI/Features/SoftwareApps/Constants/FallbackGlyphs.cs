namespace Winhance.UI.Features.SoftwareApps.Constants;

/// <summary>
/// Segoe Fluent Icons codepoints used as fallback when a real app icon
/// cannot be extracted (capabilities, optional features, not-installed AppX).
///
/// Reference: https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-fluent-icons-font
/// </summary>
public static class FallbackGlyphs
{
    /// <summary>Package glyph - used for AppX entries with no resolved icon. Verify codepoint at first run.</summary>
    public const string Package = "\uE7B8";

    /// <summary>Puzzle / Extension glyph - used for legacy capabilities. Verify codepoint at first run.</summary>
    public const string Capability = "\uE9F9";

    /// <summary>Settings gear glyph - used for optional features. Confirmed standard Segoe Fluent code.</summary>
    public const string OptionalFeature = "\uE713";
}
