using Winhance.Core.Features.Common.Catalog;
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

    // A setting's name key is its own fallback, so a setting with no translation shows its key, never "[key]".
    public static string SettingName(this ILocalizationService? localizationService, Setting setting) =>
        localizationService.GetStringOrDefault(setting.Display.Name.Value, setting.Display.Name.Value);

    // The catalog knows which feature owns a setting; the id's prefix does not always name it. Each surface passes the
    // text it shows for a setting no feature owns or whose feature name has no translation.
    public static string FeatureName(this ILocalizationService? localizationService, string settingId, string fallback) =>
        SettingCatalog.FeatureById.TryGetValue(settingId, out var featureId)
            ? localizationService.GetStringOrDefault($"Feature_{featureId}_Name", fallback)
            : fallback;
}
