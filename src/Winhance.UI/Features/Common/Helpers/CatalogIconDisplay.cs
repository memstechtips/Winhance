using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.UI.Features.Common.Helpers;

// The (glyph, pack) pair IconConverter draws for a catalog icon. An app logo shows only once Software & Apps has
// cached it; until then the Fluent fallback shows.
public static class CatalogIconDisplay
{
    public static (string Glyph, string Pack) For(Icon? icon, IAppIconResolver appIcons, bool lightTheme) => icon switch
    {
        null => (string.Empty, nameof(IconPack.Fluent)),
        { Pack: IconPack.App } when appIcons.CachedIconPath(icon.Glyph, lightTheme) is { } path => (path, AppFilePack),
        { Pack: IconPack.App, Fallback: { } fallback } => (fallback.Glyph, fallback.Pack.ToString()),
        _ => (icon.Glyph, icon.Pack.ToString()),
    };

    public const string AppFilePack = "AppFile";
}
