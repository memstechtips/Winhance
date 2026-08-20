using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Catalog;

// Fire-and-forget: NEVER participates in detection.
public abstract record Effect
{
    public IReadOnlyList<BuildRange> AppliesTo { get; init; } = System.Array.Empty<BuildRange>();

    // True when carrying the effect out launches a PROCESS and waits for it, as opposed to a blocking OS call.
    // ApplyPlan routes these to IAsyncEffectRunner; this is the one place that classification lives.
    public bool IsAsyncIo => this is ScriptEffect or RegContentEffect;
}

// Detection comes from an accompanying Target in the state's Set.
public sealed record ScriptEffect(string Script, RunContext Run) : Effect;

public sealed record RegContentEffect(string Content) : Effect;

// Apply-only; no read path exists.
public sealed record NativePowerEffect(int InformationLevel, byte Value) : Effect;

public sealed record RegistryWriteEffect(string Path, string ValueName, RegistryValueKind Kind, object Value) : Effect
{
    public bool IsGroupPolicy { get; init; }
}

// Applied only when the user opts into "also change the wallpaper" on a theme switch. INERT in the apply engine
// (WindowsStateWriter.RunEffect's default no-op) - ThemeWallpaperApplier reads it directly.
public sealed record WallpaperEffect(string Path) : Effect;
