using System.Runtime.InteropServices;

namespace Winhance.Core.Features.Common.Native;

public static class User32Api
{
    // Window message constants
    public const int HWND_BROADCAST = 0xffff;
    public const uint WM_SYSCOLORCHANGE = 0x0015;
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint WM_THEMECHANGE = 0x031A;

    // UNDOCUMENTED. Posted to the taskbar window ("Shell_TrayWnd") it asks Explorer to shut the shell down
    // GRACEFULLY - the path behind the hidden Ctrl+Shift+right-click "Exit Explorer" item; Explorer saves its state
    // on the way out, which Process.Kill does not. Reference: ExplorerPatcher's utility.h ExitExplorer(); the same
    // value appears in long-standing AutoHotkey samples. Best-effort by construction: callers keep the kill as fallback.
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

    // Returns IMMEDIATELY without waiting for receivers. Microsoft's guidance for HWND_BROADCAST is "always use
    // SendNotifyMessage or SendMessageTimeout", and SendMessageTimeout charges its timeout PER TOP-LEVEL WINDOW - so
    // a broadcast with NO pointer payload belongs here. NEVER use it for a message whose wParam/lParam is a pointer
    // the caller frees: receivers would read freed memory.
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SendNotifyMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    // Same pointer-payload rule as SendNotifyMessage.
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    // IntPtr.Zero when there is none - which is how "no taskbar, so no live shell" is detected.
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
