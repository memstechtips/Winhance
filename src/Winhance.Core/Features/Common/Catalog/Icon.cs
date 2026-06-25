namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Icon font a glyph belongs to. Default authored pack is Material; some settings use Fluent.</summary>
public enum IconPack { Material, Fluent }

/// <summary>A UI glyph: its font pack and glyph name in one value. Authored via the generated
/// MaterialIcons / FluentIcons accessors so the glyph name is compile-checked.</summary>
public sealed record Icon(IconPack Pack, string Glyph);
