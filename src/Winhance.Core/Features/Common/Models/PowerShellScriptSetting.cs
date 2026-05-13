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

    /// <summary>
    /// Read-only PowerShell script that returns the current enabled state for the setting.
    /// Used by <see cref="Winhance.Core.Features.Common.Interfaces.IPowerShellDetectionService"/>
    /// when <see cref="SettingDefinition.DetectionType"/> is <see cref="DetectionType.PowerShellScript"/>.
    ///
    /// Contract:
    /// - Script MUST be read-only. No state-changing cmdlets.
    /// - Script MUST terminate by emitting <c>$true</c> or <c>$false</c> on stdout
    ///   (or the invariant literals <c>1</c>/<c>0</c>). Do not pipe cmdlet output
    ///   whose string form may be localized.
    /// - Wrap risky calls in <c>try { ... } catch { $false }</c>; if the script throws,
    ///   the host treats the setting as Disabled.
    /// - Runs in-process, so a script that calls <c>exit</c> will tear down the host —
    ///   never call <c>exit</c>.
    /// </summary>
    public string? DetectionScript { get; init; }
}
