using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>An apply-only side-effect a state runs when applied. Fire-and-forget — NEVER participates in
/// detection. Each state owns the concrete effect for that state; there is no shared enabled/disabled pair.</summary>
public abstract record Effect;

/// <summary>PowerShell script this state runs on apply. Detection comes from an accompanying Target in the state's Set.</summary>
public sealed record ScriptEffect(string Script, RunContext Run) : Effect;

/// <summary>.reg content this state imports on apply.</summary>
public sealed record RegContentEffect(string Content) : Effect;

/// <summary>Native power API write (CallNtPowerInformation) this state performs on apply. Apply-only; no read path exists.</summary>
public sealed record NativePowerEffect(int InformationLevel, byte Value) : Effect;
