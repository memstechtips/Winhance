namespace Winhance.UI.Features.Common.Services;

// The notification-area icon. Attach subclasses the window that receives the icon's messages, so every event arrives
// on that window's thread. MenuItemInvoked carries the index into the list given to SetMenu.
public interface ISystemTrayIcon : IDisposable
{
    event EventHandler? Invoked;

    event EventHandler? NotificationClicked;

    event EventHandler<int>? MenuItemInvoked;

    void Attach(nint windowHandle);

    void Show(string tooltip);

    void Hide();

    void SetMenu(IReadOnlyList<string> items);

    // Shows the icon first when it is hidden: Windows only shows a notification for an icon in the tray.
    void ShowNotification(string title, string message);
}
