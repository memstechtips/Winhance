namespace Winhance.Core.Features.Common.Catalog;

// Keeps OLD configuration files importing: the 6 This PC folder settings were once split per OS (canonical +
// "-win10"); the catalog merges each pair into one build-gated Setting, so the -win10 ids normalize to the
// canonical id. Applied once, in ConfigMigrationService.MigrateConfig.
public static class SettingIdAliases
{
    // Retired id -> canonical catalog id. These 6 "-win10" ids are absent from SettingCatalog;
    // their canonical (no-suffix) peers ARE present (ExplorerCustomizationsCatalog). No "-win11" variant exists.
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
    {
        ["explorer-customization-thispc-folder-desktop-win10"] = "explorer-customization-thispc-folder-desktop",
        ["explorer-customization-thispc-folder-documents-win10"] = "explorer-customization-thispc-folder-documents",
        ["explorer-customization-thispc-folder-downloads-win10"] = "explorer-customization-thispc-folder-downloads",
        ["explorer-customization-thispc-folder-music-win10"] = "explorer-customization-thispc-folder-music",
        ["explorer-customization-thispc-folder-pictures-win10"] = "explorer-customization-thispc-folder-pictures",
        ["explorer-customization-thispc-folder-videos-win10"] = "explorer-customization-thispc-folder-videos",
    };

    public static string Normalize(string id)
        => Aliases.TryGetValue(id, out var canonical) ? canonical : id;
}
