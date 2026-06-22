namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Apply-time behaviour around a setting: a confirmation gate, a recommended reboot, and/or a
/// process/service to restart for the change to take effect.</summary>
public sealed record ApplyBehavior
{
    public bool RequiresConfirmation { get; init; }
    public bool RequiresReboot { get; init; }            // old RequiresRestart (system)
    public RestartTarget? Restart { get; init; }         // old RestartProcess / RestartService, unified
    public static readonly ApplyBehavior None = new();
}

/// <summary>What to restart for a setting to take effect. Reboot is separate (ApplyBehavior.RequiresReboot)
/// since a setting may need both.</summary>
public abstract record RestartTarget;
public sealed record RestartProcess(string Name) : RestartTarget;   // "Explorer" / "intl" / "StartMenuExperienceHost"
public sealed record RestartService(string Name) : RestartTarget;   // e.g. "WpnUserService*"
