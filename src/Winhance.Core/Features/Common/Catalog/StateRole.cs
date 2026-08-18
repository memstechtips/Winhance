namespace Winhance.Core.Features.Common.Catalog;

public enum RoleKind { None, WindowsDefault, Recommended }

public sealed record StateRole(RoleKind Kind, PowerContext Context = PowerContext.Always)
{
    // Empty = unconditional. A build-scoped role is invisible to HasRole(kind) and matches only HasRole(kind, WinBuild):
    // how a merged setting declares an OS-divergent default (This PC folders: Disabled on Win11, Enabled on Win10).
    public IReadOnlyList<BuildRange> AppliesTo { get; init; } = System.Array.Empty<BuildRange>();

    public static readonly StateRole Recommended = new(RoleKind.Recommended);
    public static readonly StateRole WindowsDefault = new(RoleKind.WindowsDefault);
}
