namespace Winhance.Core.Features.Common.Catalog;

public sealed record ApplyBehavior
{
    public bool RequiresConfirmation { get; init; }
    public bool RequiresReboot { get; init; }
    public RestartTarget? Restart { get; init; }

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

    // NEARLY FREE: one SystemParametersInfo makes the shell re-read the desktop background values now, not at next logon.
    Desktop = 1 << 1,

    // FREE: one key press. Windows saves the LIVE Num Lock state at sign-out over whatever was written, so the key
    // has to agree with the saved value or the change is lost.
    NumLock = 1 << 2,
}

// Reboot is separate (RequiresReboot); a setting may need both.
public abstract record RestartTarget
{
    public static bool IsExplorer(RestartTarget? target) =>
        target is RestartProcess process && RestartProcess.NamesExplorer(process.Name);

    // "intl" is not a process: it stands for the regional-settings broadcast.
    public static bool IsRegional(RestartTarget? target) =>
        target is RestartProcess process && process.Name.Equals(RestartProcess.IntlName, StringComparison.OrdinalIgnoreCase);
}

public sealed record RestartProcess(string Name) : RestartTarget   // "Explorer" / "intl" / "StartMenuExperienceHost"
{
    public const string ExplorerName = "Explorer";
    public const string IntlName = "intl";

    public static bool NamesExplorer(string? name) =>
        string.Equals(name, ExplorerName, StringComparison.OrdinalIgnoreCase);
}

public sealed record RestartService(string Name) : RestartTarget;   // e.g. "WpnUserService*"
