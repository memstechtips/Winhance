using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Open tag-set. Today: WindowsDefault, Recommended. Future presets add values here
/// with zero schema change.</summary>
public enum RoleKind { None, WindowsDefault, Recommended }

/// <summary>A role tag on a state, scoped to a power context (Always for non-power settings) and optionally to
/// specific build ranges.</summary>
public sealed record StateRole(RoleKind Kind, PowerContext Context = PowerContext.Always)
{
    /// <summary>Build ranges this role applies to (empty = ALL builds / unconditional), mirroring
    /// <see cref="Target.AppliesTo"/>. A role scoped to specific builds is INVISIBLE to the build-unaware
    /// <c>HasRole(kind[, context])</c> query - which asks "is this an UNCONDITIONAL role?" - and matches only the
    /// build-aware <c>HasRole(kind, WinBuild)</c> overload. This lets a merged setting declare an OS-divergent
    /// Windows default (the This PC folders: "Disabled" is the default on Windows 11, "Enabled" on Windows 10)
    /// without a build-unaware reader (UI badges, the validator's one-per-context rule, relationship resolution)
    /// mistaking it for a single unconditional default. Only the OS-aware reset resolver opts into it.</summary>
    public IReadOnlyList<BuildRange> AppliesTo { get; init; } = System.Array.Empty<BuildRange>();

    public static readonly StateRole Recommended = new(RoleKind.Recommended);
    public static readonly StateRole WindowsDefault = new(RoleKind.WindowsDefault);
}
