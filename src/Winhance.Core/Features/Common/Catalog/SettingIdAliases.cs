namespace Winhance.Core.Features.Common.Catalog;

// Keeps OLD configuration files importing: the 6 This PC folder settings were once split per OS (canonical +
// "-win10"), and the 6 region formats were explorer-customization-* ids until Winhance Release 28. Applied once,
// in ConfigMigrationService.MigrateConfig.
public static class SettingIdAliases
{
    // Retired id -> canonical catalog id. No retired id may also be a live SettingCatalog id.
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
    {
        ["explorer-customization-thispc-folder-desktop-win10"] = "explorer-customization-thispc-folder-desktop",
        ["explorer-customization-thispc-folder-documents-win10"] = "explorer-customization-thispc-folder-documents",
        ["explorer-customization-thispc-folder-downloads-win10"] = "explorer-customization-thispc-folder-downloads",
        ["explorer-customization-thispc-folder-music-win10"] = "explorer-customization-thispc-folder-music",
        ["explorer-customization-thispc-folder-pictures-win10"] = "explorer-customization-thispc-folder-pictures",
        ["explorer-customization-thispc-folder-videos-win10"] = "explorer-customization-thispc-folder-videos",
        ["explorer-customization-short-date"] = "region-short-date",
        ["explorer-customization-first-day-of-week"] = "region-first-day-of-week",
        ["explorer-customization-number-decimal"] = "region-number-decimal",
        ["explorer-customization-list-separator"] = "region-list-separator",
        ["explorer-customization-measurement-system"] = "region-measurement-system",
        ["explorer-customization-currency-decimal"] = "region-currency-decimal",
    };

    public static string Normalize(string id)
        => Aliases.TryGetValue(id, out var canonical) ? canonical : id;
}
