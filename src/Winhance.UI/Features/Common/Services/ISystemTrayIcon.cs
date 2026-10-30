namespace Winhance.UI.Features.Common.Services;

// The notification-area icon. Attach subclasses the window that receives the icon's messages, so every event arrives
// on that window's thread. MenuItemInvoked carries the Command of the item picked.
public interface ISystemTrayIcon : IDisposable
{
    event EventHandler? Invoked;

    event EventHandler? NotificationClicked;

    event EventHandler<int>? MenuItemInvoked;

    void Attach(nint windowHandle);

    void Show(string tooltip);

    void Hide();

    void SetMenu(IReadOnlyList<TrayMenuItem> items);

    // Shows the icon first when it is hidden: Windows only shows a notification for an icon in the tray.
    void ShowNotification(string title, string message);
}

// An item with Children opens them as a submenu and reports no Command of its own.
public sealed record TrayMenuItem(string Text, int Command, bool Checked = false, bool Enabled = true, IReadOnlyList<TrayMenuItem>? Children = null)
{
    public static TrayMenuItem Separator { get; } = new(string.Empty, -1);
}
