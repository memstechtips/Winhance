using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;

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

    // The fallback is formatted too, so a miss never shows a raw {0}. A malformed format falls back to the
    // unformatted string rather than throwing, matching the interface's own behaviour.
    public static string GetStringOrDefault(
        this ILocalizationService? localizationService, string key, string fallback, params object[] args)
    {
        var format = localizationService?.TryGetString(key, out var value) == true ? value : fallback;
        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    // A LocKey is checked against en.json at build time and the service already falls back to English, so in the
    // app a miss means there is no service. The key itself is the fallback then, never "[key]".
    public static string GetString(this ILocalizationService? localizationService, LocKey key) =>
        localizationService.GetStringOrDefault(key.Value, key.Value);

    public static string SettingName(this ILocalizationService? localizationService, Setting setting) =>
        localizationService.GetString(setting.Display.Name);

    // The catalog knows which feature owns a setting; the id's prefix does not always name it. Each surface passes the
    // text it shows for a setting no feature owns or whose feature name has no translation.
    public static string FeatureName(this ILocalizationService? localizationService, string settingId, string fallback) =>
        SettingCatalog.FeatureById.TryGetValue(settingId, out var featureId)
            ? localizationService.GetStringOrDefault($"Feature_{featureId}_Name", fallback)
            : fallback;
}
