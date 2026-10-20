namespace Winhance.Core.Features.Common.Catalog;

// For AppAsset the glyph is a PNG file name under Winhance.UI's Assets/AppIcons folder.
public enum IconPack { Material, Fluent, AppAsset }

public sealed record Icon(IconPack Pack, string Glyph);
