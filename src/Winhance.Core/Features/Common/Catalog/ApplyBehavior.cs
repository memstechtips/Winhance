namespace Winhance.Core.Features.Common.Catalog;

public sealed record ApplyBehavior
{
    public bool RequiresConfirmation { get; init; }
    public bool RequiresReboot { get; init; }            // old RequiresRestart (system)
    public RestartTarget? Restart { get; init; }         // old RestartProcess / RestartService, unified

    public WindowsChange NotifyWindows { get; init; }

    public static readonly ApplyBehavior None = new();
}

// Declared per setting because each member has a COST (see the members); flags so a setting can announce more than one kind.
[Flags]
public enum WindowsChange
{
    // FREE: no payload, so it is posted and returns immediately however many windows are open.
    None = 0,

    // EXPENSIVE: carries a payload, so it must be SENT and is charged its full timeout per top-level window - seconds
    // on a busy machine. Declare it only where applying the setting genuinely changes how Windows looks.
    Appearance = 1 << 0,
}

// Reboot is separate (RequiresReboot); a setting may need both.
public abstract record RestartTarget;
public sealed record RestartProcess(string Name) : RestartTarget;   // "Explorer" / "intl" / "StartMenuExperienceHost"
public sealed record RestartService(string Name) : RestartTarget;   // e.g. "WpnUserService*"
