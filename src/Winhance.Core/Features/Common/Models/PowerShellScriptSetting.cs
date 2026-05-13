using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

public sealed record PowerShellScriptSetting
{
    public string? Id { get; init; }
    public string? Script { get; init; }
    public string? EnabledScript { get; init; }
    public string? DisabledScript { get; init; }
    public string? Purpose { get; init; }
    public bool RequiresElevation { get; init; } = true;

    /// <summary>
    /// Autounattend pass this script must run in. Defaults to System (specialize pass as SYSTEM).
    /// Set to User for scripts that touch HKCU, per-user adapter state, or anything that needs
    /// the interactive user's session — those only work in the FirstLogon bridge.
    /// </summary>
    public RunContext RunContext { get; init; } = RunContext.System;
}
