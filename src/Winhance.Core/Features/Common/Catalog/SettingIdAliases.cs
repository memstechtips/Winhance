using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Maps a retired config-item setting id to its current canonical <see cref="SettingCatalog"/> id, so OLD
/// configuration files keep importing after the old <c>SettingDefinition</c> variants are retired at teardown.
///
/// Phase 6.5 case: the 6 "This PC folder" settings were split per-OS in the old model (a Windows-11 canonical def
/// plus a "-win10" Windows-10 variant). The new catalog MERGES each pair into ONE build-gated <see cref="Setting"/>
/// under the canonical id (Windows-11 and Windows-10 targets gated by <c>AppliesTo</c>), so the "-win10" ids are
/// unpaired. Normalizing them to the canonical id lets an old config resolve, gate (via <see cref="Availability"/>),
/// and apply through the merged setting on either OS. Applied once in <c>ConfigMigrationService.MigrateConfig</c>.
/// </summary>
public static class SettingIdAliases
{
    // Retired id -> canonical catalog id. Verified 2026-06-26: these 6 "-win10" ids are absent from SettingCatalog;
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

    /// <summary>Returns the canonical catalog id for a (possibly retired) config id, or the input unchanged when it
    /// is not an alias.</summary>
    public static string Normalize(string id)
        => Aliases.TryGetValue(id, out var canonical) ? canonical : id;
}
