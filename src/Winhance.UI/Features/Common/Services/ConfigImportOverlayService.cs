using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Views;

namespace Winhance.UI.Features.Common.Services;

public class ConfigImportOverlayService : IConfigImportOverlayService
{
    private readonly IDispatcherService _dispatcherService;
    private ConfigImportOverlayWindow? _overlayWindow;

    public ConfigImportOverlayService(IDispatcherService dispatcherService)
    {
        _dispatcherService = dispatcherService;
    }

    public void ShowOverlay(string statusText, string? detailText = null)
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            _overlayWindow = new ConfigImportOverlayWindow(statusText, detailText);
            _overlayWindow.Activate();
        });
    }

    public void UpdateStatus(string statusText, string? detailText = null)
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            _overlayWindow?.UpdateStatus(statusText, detailText);
        });
    }

    public void HideOverlay()
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            try
            {
                _overlayWindow?.Close();
            }
            catch
            {
                // Window may already be closed
            }
            _overlayWindow = null;
        });
    }
}
