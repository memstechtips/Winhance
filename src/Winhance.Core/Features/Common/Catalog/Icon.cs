namespace Winhance.Core.Features.Common.Catalog;

// AppAsset: the glyph is a PNG file name under Winhance.UI's Assets/AppIcons folder. App: the glyph is a Windows Apps
// catalog id whose logo Software & Apps has cached; Fallback is the Fluent icon shown until it has.
public enum IconPack { Fluent, AppAsset, App }

public sealed record Icon(IconPack Pack, string Glyph, Icon? Fallback = null);
