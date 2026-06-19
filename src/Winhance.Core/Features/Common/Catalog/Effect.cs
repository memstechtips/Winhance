using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>An apply-only side-effect. Fire-and-forget — NEVER participates in detection.</summary>
public abstract record Effect;

/// <summary>PowerShell run on enable/disable. Detection comes from an accompanying Target in the state's Set.</summary>
public sealed record ScriptEffect(string EnabledScript, string DisabledScript, RunContext Run) : Effect;

/// <summary>.reg content imported on enable/disable.</summary>
public sealed record RegContentEffect(string EnabledContent, string DisabledContent) : Effect;

/// <summary>Native power API write (CallNtPowerInformation). Apply-only; no read path exists.</summary>
public sealed record NativePowerEffect(int InformationLevel, byte EnabledValue, byte DisabledValue) : Effect;
