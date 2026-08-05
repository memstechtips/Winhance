using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Extensions;

/// <summary>
/// Localization lookups that fall back for real.
///
/// <para><b>Why this exists.</b> ~290 call sites read
/// <c>_localizationService.GetStringOrDefault("Button_Cancel", "Cancel")</c>. Every one of those <c>??</c>
/// operands was unreachable: <see cref="ILocalizationService.GetString(string)"/> returns
/// non-nullable <c>string</c> and its miss path returns the literal <c>"[Button_Cancel]"</c>, never
/// null. So a dropped key rendered <c>[Button_Cancel]</c> on the button — precisely the outcome
/// each author wrote the <c>??</c> to prevent. The compiler stayed quiet only because the operand
/// was dead rather than wrong.</para>
///
/// <para>Routing through <see cref="ILocalizationService.TryGetString"/> makes the fallback fire.
/// A guard test keeps the dead form from returning; see <c>LocalizationFallbackTests</c>.</para>
/// </summary>
public static class LocalizationServiceExtensions
{
    /// <summary>
    /// The localized string for <paramref name="key"/>, or <paramref name="fallback"/> when the key
    /// is missing. A null service falls back too, so call sites that hold an optional service do not
    /// need their own null check.
    /// </summary>
    public static string GetStringOrDefault(
        this ILocalizationService? localizationService, string key, string fallback) =>
        localizationService?.TryGetString(key, out var value) == true ? value : fallback;

    /// <summary>
    /// Formatting overload, mirroring <see cref="ILocalizationService.GetString(string, object[])"/>:
    /// the resolved string is used as a format. A malformed format falls back to the unformatted
    /// string rather than throwing, matching the interface's own behaviour.
    /// </summary>
    public static string GetStringOrDefault(
        this ILocalizationService? localizationService, string key, string fallback, params object[] args)
    {
        if (localizationService?.TryGetString(key, out var format) != true)
        {
            return fallback;
        }

        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }
}
