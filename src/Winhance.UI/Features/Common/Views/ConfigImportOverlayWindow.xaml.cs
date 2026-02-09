using System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Winhance.UI.Features.Common.Views;

/// <summary>
/// A fullscreen overlay window shown during config application.
/// Uses FullScreenPresenter for clean fullscreen coverage with a solid dark background.
/// </summary>
public sealed partial class ConfigImportOverlayWindow : Window
{
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
            var localizationService = App.Services.GetService(typeof(Winhance.Core.Features.Common.Interfaces.ILocalizationService))
                as Winhance.Core.Features.Common.Interfaces.ILocalizationService;
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

    private bool _configured;

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_configured) return;
        _configured = true;

        try
        {
            // Remove title bar
            ExtendsContentIntoTitleBar = true;

            // Use FullScreenPresenter for clean fullscreen coverage
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to configure overlay window: {ex.Message}");
        }
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
        });
    }
}
