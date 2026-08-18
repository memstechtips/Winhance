namespace Winhance.Core.Features.Common.Catalog;

public enum IconPack { Material, Fluent }

public sealed record Icon(IconPack Pack, string Glyph);
