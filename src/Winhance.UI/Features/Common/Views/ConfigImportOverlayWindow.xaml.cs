using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Media.Imaging;
using Winhance.Core.Features.Common.Interfaces;
using WinRT.Interop;

namespace Winhance.UI.Features.Common.Views;

/// <summary>
/// A fullscreen overlay window shown during config application.
/// Uses Win32 interop for semi-transparency and borderless maximized presentation.
/// </summary>
public sealed partial class ConfigImportOverlayWindow : Window
{
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const uint LWA_ALPHA = 0x2;

    private bool _configured;

    public ConfigImportOverlayWindow(string statusText, string? detailText = null)
    {
        this.InitializeComponent();

        // Set initial text
        OverlayStatusText.Text = statusText;
        OverlayDetailText.Text = detailText ?? string.Empty;

        // Set branding
        OverlayLogo.Source = new BitmapImage(
            new Uri("ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png"));

        try
        {
            var localizationService = App.Services.GetService<ILocalizationService>();
            OverlayTitleText.Text = localizationService?.GetString("App_Title") ?? "Winhance";
            OverlayTaglineText.Text = localizationService?.GetString("App_Tagline") ?? "";
        }
        catch
        {
            OverlayTitleText.Text = "Winhance";
            OverlayTaglineText.Text = "";
        }

        // Configure window after activation
        this.Activated += OnActivated;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_configured) return;
        _configured = true;

        try
        {
            // Remove title bar
            ExtendsContentIntoTitleBar = true;

            var hwnd = WindowNative.GetWindowHandle(this);

            // Add WS_EX_LAYERED (for opacity) and WS_EX_TOOLWINDOW (hides from taskbar)
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TOOLWINDOW);

            // Set window opacity to ~90% (0xE6 = 230) — matches WPF's #E6000000
            SetLayeredWindowAttributes(hwnd, 0, 230, LWA_ALPHA);

            // Borderless, always-on-top, maximized
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsAlwaysOnTop = true;
                presenter.Maximize();
            }
        }
        catch (Exception ex)
        {
            try
            {
                App.Services.GetService<ILogService>()?.LogDebug($"Failed to configure overlay window: {ex.Message}");
            }
            catch { }
        }

        // Announce initial status text to Narrator
        AnnounceStatus(OverlayStatusText.Text);
    }

    /// <summary>
    /// Updates the status and detail text on the overlay.
    /// </summary>
    public void UpdateStatus(string status, string? detail = null)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            OverlayStatusText.Text = status;
            OverlayDetailText.Text = detail ?? string.Empty;
            AnnounceStatus(status);
        });
    }

    private void AnnounceStatus(string text)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(OverlayStatusText)
                   ?? FrameworkElementAutomationPeer.CreatePeerForElement(OverlayStatusText);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent,
            text,
            "OverlayStatus");
    }
}
