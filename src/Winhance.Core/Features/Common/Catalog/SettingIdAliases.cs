namespace Winhance.Core.Features.Common.Catalog;

// Keeps OLD configuration files importing: the 6 This PC folder settings were once split per OS (canonical +
// "-win10"), the 6 region formats were explorer-customization-* ids until Winhance Release 28, and the 11
// context menu settings were explorer-* ids on the Explorer page until they got their own Context Menu page.
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
        ["explorer-customization-context-menu"] = "context-menu-classic",
        ["explorer-take-ownership"] = "context-menu-take-ownership",
        ["explorer-context-menu-firewall"] = "context-menu-firewall",
        ["explorer-context-menu-toggle-extensions"] = "context-menu-toggle-extensions",
        ["explorer-context-menu-windows-terminal"] = "context-menu-windows-terminal",
        ["explorer-context-menu-ask-copilot"] = "context-menu-ask-copilot",
        ["explorer-context-menu-sfc"] = "context-menu-sfc",
        ["explorer-context-menu-dism"] = "context-menu-dism",
        ["explorer-context-menu-chkdsk"] = "context-menu-chkdsk",
        ["explorer-context-menu-ps1-edit-run"] = "context-menu-ps1-edit-run",
        ["explorer-context-menu-compress-to"] = "context-menu-compress-to",
    };

    public static string Normalize(string id)
        => Aliases.TryGetValue(id, out var canonical) ? canonical : id;
}
