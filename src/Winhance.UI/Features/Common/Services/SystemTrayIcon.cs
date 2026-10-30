using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

// A notification-area icon on the main window, the way PowerToys Command Palette does it. It lives in the UI project
// because NOTIFYICONDATAW's layout differs by CPU, and CsWin32 only generates it for a project that targets one. A balloon (NIF_INFO) and
// not an app notification, because Windows App SDK notifications do not reach an elevated app; Windows 10 and 11 show
// the balloon as an ordinary toast.
internal sealed class SystemTrayIcon(ILogService logService) : ISystemTrayIcon
{
    private const uint IconId = 1;
    private const uint CallbackMessage = PInvoke.WM_APP + 1;
    private const int FirstMenuCommand = 1;
    // NIN_SELECT | NINF_KEY: Enter or Space on the icon. NINF_KEY is not in the Win32 metadata.
    private const uint KeySelect = PInvoke.NIN_SELECT | 1;

    // Broadcast to every top-level window when Explorer restarts; the icon has to be added again.
    private readonly uint _taskbarCreatedMessage = PInvoke.RegisterWindowMessage("TaskbarCreated");
    private HWND _hwnd;
    // Held in a field: Windows keeps only a pointer to the delegate, and a collected delegate crashes the window.
    private SUBCLASSPROC? _subclassProc;
    private HICON _smallIcon;
    private HICON _largeIcon;
    private string _tooltip = string.Empty;
    private IReadOnlyList<string> _menu = [];
    private bool _isVisible;

    public event EventHandler? Invoked;

    public event EventHandler? NotificationClicked;

    public event EventHandler<int>? MenuItemInvoked;

    public void Attach(nint windowHandle)
    {
        if (!_hwnd.IsNull)
            return;

        _hwnd = new HWND(windowHandle);
        _subclassProc = WindowProc;
        if (!PInvoke.SetWindowSubclass(_hwnd, _subclassProc, IconId, 0))
            logService.LogWarning($"Could not subclass the main window for the tray icon (error {Marshal.GetLastWin32Error()})");
        LoadIcons();
    }

    public void Show(string tooltip)
    {
        if (_hwnd.IsNull)
            return;

        _tooltip = tooltip;
        var data = NewData(NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP);
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = _smallIcon;
        data.szTip = Fit(tooltip, 128).AsSpan();
        if (_isVisible)
        {
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in data);
            return;
        }

        if (!PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in data))
        {
            logService.LogWarning("Could not add the tray icon");
            return;
        }

        // With the legacy messages a left click on the icon in the Windows 11 overflow flyout did nothing (26H2 VM);
        // version 4 reports it as NIN_SELECT.
        var version = NewData(0);
        version.Anonymous.uVersion = PInvoke.NOTIFYICON_VERSION_4;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, in version);
        _isVisible = true;
    }

    public void Hide()
    {
        if (!_isVisible)
            return;

        var data = NewData(0);
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in data);
        _isVisible = false;
    }

    public void SetMenu(IReadOnlyList<string> items) => _menu = items;

    public void ShowNotification(string title, string message)
    {
        if (!_isVisible)
            Show(_tooltip);
        if (!_isVisible)
            return;

        var data = NewData(NOTIFY_ICON_DATA_FLAGS.NIF_INFO);
        data.szInfoTitle = Fit(title, 64).AsSpan();
        data.szInfo = Fit(message, 256).AsSpan();
        data.dwInfoFlags = NOTIFY_ICON_INFOTIP_FLAGS.NIIF_USER | NOTIFY_ICON_INFOTIP_FLAGS.NIIF_LARGE_ICON;
        data.hBalloonIcon = _largeIcon;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in data);
    }

    public void Dispose()
    {
        Hide();
        if (!_hwnd.IsNull && _subclassProc is not null)
            PInvoke.RemoveWindowSubclass(_hwnd, _subclassProc, IconId);
        if (!_smallIcon.IsNull)
            PInvoke.DestroyIcon(_smallIcon);
        if (!_largeIcon.IsNull)
            PInvoke.DestroyIcon(_largeIcon);
        _smallIcon = default;
        _largeIcon = default;
    }

    // The fixed buffers hold a terminating null, so the text gets one character less than the buffer.
    internal static string Fit(string text, int bufferLength) =>
        text.Length < bufferLength ? text : text[..(bufferLength - 1)];

    private NOTIFYICONDATAW NewData(NOTIFY_ICON_DATA_FLAGS flags) => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd = _hwnd,
        uID = IconId,
        uFlags = flags,
    };

    private unsafe void LoadIcons()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is null)
            return;

        HICON large = default;
        HICON small = default;
        fixed (char* path = exePath)
        {
            if (PInvoke.ExtractIconEx(path, 0, &large, &small, 1) == 0)
                logService.LogWarning($"No icon in {exePath} for the tray");
        }
        _largeIcon = large;
        _smallIcon = small;
    }

    private LRESULT WindowProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam, nuint subclassId, nuint refData)
    {
        if (message == CallbackMessage)
        {
            // Version 4 puts the event in the low word of lParam.
            switch ((uint)lParam.Value & 0xFFFF)
            {
                case PInvoke.NIN_SELECT:
                case KeySelect:
                    Invoked?.Invoke(this, EventArgs.Empty);
                    break;
                case PInvoke.WM_CONTEXTMENU:
                    ShowMenu();
                    break;
                case PInvoke.NIN_BALLOONUSERCLICK:
                    NotificationClicked?.Invoke(this, EventArgs.Empty);
                    break;
            }
            return new LRESULT(0);
        }

        if (message == _taskbarCreatedMessage && _isVisible)
        {
            _isVisible = false;
            Show(_tooltip);
        }

        return PInvoke.DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private unsafe void ShowMenu()
    {
        if (_menu.Count == 0)
            return;

        var menu = PInvoke.CreatePopupMenu();
        try
        {
            for (var i = 0; i < _menu.Count; i++)
            {
                fixed (char* text = _menu[i])
                {
                    PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, (nuint)(FirstMenuCommand + i), text);
                }
            }

            PInvoke.GetCursorPos(out var point);
            // Without the foreground call the menu stays open when the user clicks elsewhere, and without the WM_NULL
            // it can fail to show the next time (KB135788).
            PInvoke.SetForegroundWindow(_hwnd);
            var command = PInvoke.TrackPopupMenuEx(
                menu,
                (uint)(TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD | TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON | TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN),
                point.X,
                point.Y,
                _hwnd,
                null);
            PInvoke.PostMessage(_hwnd, PInvoke.WM_NULL, default, default);

            if (command.Value >= FirstMenuCommand)
                MenuItemInvoked?.Invoke(this, command.Value - FirstMenuCommand);
        }
        finally
        {
            PInvoke.DestroyMenu(menu);
        }
    }
}
