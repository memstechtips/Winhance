using System.Runtime.InteropServices;

namespace Winhance.Core.Features.Common.Native;

public static class User32Api
{
    // Window message constants
    public const int HWND_BROADCAST = 0xffff;
    public const uint WM_SYSCOLORCHANGE = 0x0015;
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint WM_THEMECHANGE = 0x031A;

    /// <summary>
    /// UNDOCUMENTED. Posted to the taskbar window ("Shell_TrayWnd") it asks Explorer to shut the shell
    /// down GRACEFULLY - the same path as the hidden Ctrl+Shift+right-click "Exit Explorer" item on the
    /// taskbar context menu. Explorer flushes and saves its state (desktop icon layout, folder view
    /// preferences) on the way out, which TerminateProcess/Process.Kill does not.
    /// <para>
    /// Reference: ExplorerPatcher's <c>utility.h</c> ExitExplorer(), which posts exactly this message;
    /// the same value appears in long-standing AutoHotkey "restart explorer" samples.
    /// </para>
    /// <para>
    /// Because it is undocumented, every caller must treat it as best-effort and keep the kill as a
    /// fallback - see <c>ExplorerRestartService</c>.
    /// </para>
    /// </summary>
    public const uint WM_SHELL_GRACEFUL_EXIT = 0x5B4;

    // SendMessageTimeout flags
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    // Window management constants
    public const int SW_RESTORE = 9;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    /// <summary>
    /// Posts the message and returns IMMEDIATELY without waiting for any receiver to process it.
    /// Microsoft's guidance for HWND_BROADCAST is "always use SendNotifyMessage or SendMessageTimeout",
    /// and SendMessageTimeout charges its timeout PER TOP-LEVEL WINDOW on a broadcast, so a broadcast that
    /// carries NO POINTER PAYLOAD belongs here instead.
    /// <para>
    /// NEVER use this for a message whose wParam/lParam is a pointer the caller frees: the send returns
    /// before the receivers have read it, so they would read freed memory.
    /// </para>
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SendNotifyMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Posts a message to a window's queue and returns immediately. Same pointer-payload rule as
    /// <see cref="SendNotifyMessage"/>.</summary>
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Finds a top-level window by class name and/or window name. IntPtr.Zero when there is none -
    /// which is how "no taskbar, so no live shell" is detected.</summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int SystemParametersInfo(int uAction, int uParam, string? lpvParam, int fuWinIni);
}
