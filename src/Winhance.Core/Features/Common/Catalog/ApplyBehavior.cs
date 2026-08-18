namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Apply-time behaviour around a setting: a confirmation gate, a recommended reboot, a
/// process/service to restart for the change to take effect, and what the rest of Windows has to be
/// told once the setting has been applied.</summary>
public sealed record ApplyBehavior
{
    public bool RequiresConfirmation { get; init; }
    public bool RequiresReboot { get; init; }            // old RequiresRestart (system)
    public RestartTarget? Restart { get; init; }         // old RestartProcess / RestartService, unified

    /// <summary>What the rest of Windows has to be told once this setting has been applied, so running
    /// apps can pick the change up live instead of waiting for a restart. The default
    /// (<see cref="WindowsChange.None"/>) is right for almost every setting - see the enum for what each
    /// member costs.</summary>
    public WindowsChange NotifyWindows { get; init; }

    public static readonly ApplyBehavior None = new();
}

/// <summary>
/// What the rest of Windows has to be TOLD about a setting once it has been applied. A fact about the
/// setting, authored next to its confirmation gate and its restart - not reverse-engineered elsewhere
/// from the registry paths the setting happens to write.
///
/// Flags, because a setting may one day need to announce more than one kind of change. Each member
/// documents its COST, which is the entire reason this is declared per setting rather than sent for
/// everything.
/// </summary>
[Flags]
public enum WindowsChange
{
    /// <summary>Nothing beyond the generic "a setting changed" notice a restart-carrying setting already
    /// gets. FREE: that notice carries no payload, so it is posted and returns immediately however many
    /// windows are open. Correct for almost every setting.</summary>
    None = 0,

    /// <summary>The desktop APPEARANCE changed - light/dark mode, colours, transparency. Tells open windows
    /// to repaint in the new theme, which is what makes a mode switch show up without restarting the shell.
    /// EXPENSIVE: this notice carries a payload, so it must be SENT rather than posted, and the send is
    /// charged its full timeout PER TOP-LEVEL WINDOW on the desktop - seconds on a busy machine. Declare it
    /// only where applying the setting genuinely changes how Windows looks.</summary>
    Appearance = 1 << 0,
}

/// <summary>What to restart for a setting to take effect. Reboot is separate (ApplyBehavior.RequiresReboot)
/// since a setting may need both.</summary>
public abstract record RestartTarget;
public sealed record RestartProcess(string Name) : RestartTarget;   // "Explorer" / "intl" / "StartMenuExperienceHost"
public sealed record RestartService(string Name) : RestartTarget;   // e.g. "WpnUserService*"
