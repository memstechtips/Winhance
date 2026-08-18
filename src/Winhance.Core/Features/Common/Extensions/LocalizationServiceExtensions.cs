using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Extensions;

// GetString never returns null - its miss path returns the literal "[key]" - so every `?? fallback` written
// against it was dead, and a dropped key rendered [key] on the button. Routing through TryGetString makes the
// fallback fire; LocalizationFallbackTests keeps the dead form from returning.
public static class LocalizationServiceExtensions
{
    // A null service falls back too, so call sites holding an optional service need no null check.
    public static string GetStringOrDefault(
        this ILocalizationService? localizationService, string key, string fallback) =>
        localizationService?.TryGetString(key, out var value) == true ? value : fallback;

    // A malformed format falls back to the unformatted string rather than throwing, matching the interface's own behaviour.
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
