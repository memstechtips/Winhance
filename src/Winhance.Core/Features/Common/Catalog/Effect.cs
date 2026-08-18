using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>An apply-only side-effect a state runs when applied. Fire-and-forget — NEVER participates in
/// detection. Each state owns the concrete effect for that state; there is no shared enabled/disabled pair.</summary>
public abstract record Effect
{
    /// <summary>Build ranges this effect applies to (empty = every build), reusing the same scoping primitive
    /// as Target.AppliesTo / StateRole.AppliesTo. Only WallpaperEffect uses it today (the default wallpaper is
    /// OS-divergent); every other effect leaves it empty (unconditional).</summary>
    public IReadOnlyList<BuildRange> AppliesTo { get; init; } = System.Array.Empty<BuildRange>();

    /// <summary>True when carrying this effect out launches a PROCESS and waits for it, rather than making
    /// a blocking OS call the way the registry and native-power effects do. <see cref="ApplyPlan"/> routes
    /// these to IAsyncEffectRunner; this is the one place that classification lives.</summary>
    public bool IsAsyncIo => this is ScriptEffect or RegContentEffect;
}

/// <summary>PowerShell script this state runs on apply. Detection comes from an accompanying Target in the state's Set.</summary>
public sealed record ScriptEffect(string Script, RunContext Run) : Effect;

/// <summary>.reg content this state imports on apply.</summary>
public sealed record RegContentEffect(string Content) : Effect;

/// <summary>Native power API write (CallNtPowerInformation) this state performs on apply. Apply-only; no read path exists.</summary>
public sealed record NativePowerEffect(int InformationLevel, byte Value) : Effect;

/// <summary>Writes a registry value on apply. Apply-only - an Action is never detected, so this never
/// participates in detection. Mirrors what the old enabled-branch ApplySetting writes for this value.</summary>
public sealed record RegistryWriteEffect(string Path, string ValueName, RegistryValueKind Kind, object Value) : Effect
{
    public bool IsGroupPolicy { get; init; }
}

/// <summary>The default Windows wallpaper this state applies when the user opts into "also change the
/// wallpaper" on a theme switch (theme-mode-windows Light/Dark). OS-divergent, so authored per-OS via
/// Effect.AppliesTo. Apply-only + INERT in the apply engine (WindowsStateWriter.RunEffect's default no-op) -
/// ThemeWallpaperApplier reads it directly. Surfaced in the Technical Details Effects section.</summary>
public sealed record WallpaperEffect(string Path) : Effect;
