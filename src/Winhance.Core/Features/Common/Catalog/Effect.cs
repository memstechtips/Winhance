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

// Activates the scheme, importing a predefined one that is not installed.
public sealed record PowerPlanEffect(string Guid) : Effect;

public sealed record RegistryWriteEffect(string Path, string ValueName, RegistryValueKind Kind, object Value) : Effect
{
    public bool IsGroupPolicy { get; init; }
}

// A RunSynchronous command in the answer file. Inert in the live apply engine.
public sealed record AutounattendCommand(string Pass, string Component, string Command, string? Description = null) : Effect;

// A FirstLogonCommands entry, which only oobeSystem Shell-Setup carries. Inert in the live apply engine.
public sealed record AutounattendFirstLogonCommand(string Command, string? Description = null) : Effect;
