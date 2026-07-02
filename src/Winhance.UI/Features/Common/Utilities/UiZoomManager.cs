using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Utilities;

/// <summary>
/// Manages app-local UI zoom for the main content area. Emulates a LayoutTransform
/// (absent in WinUI 3) by inverse-sizing the zoom host (Width = viewport / factor)
/// and applying a ScaleTransform(factor), which produces DPI-like reflow without
/// nested scrollbars. Persists the factor via IUserPreferencesService, mirroring
/// WindowSizeManager.
/// </summary>
public class UiZoomManager
{
    private readonly FrameworkElement _viewport; // measures available space (row-4 cell)
    private readonly FrameworkElement _zoomHost; // inverse-sized + scaled content host
    private readonly ScaleTransform _scale;
    private readonly IUserPreferencesService _preferences;
    private readonly ILogService _logService;

    private double _factor = ZoomLevels.Default;

    public UiZoomManager(
        FrameworkElement viewport,
        FrameworkElement zoomHost,
        IUserPreferencesService preferences,
        ILogService logService)
    {
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _zoomHost = zoomHost ?? throw new ArgumentNullException(nameof(zoomHost));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));

        _scale = new ScaleTransform();
        _zoomHost.RenderTransform = _scale;
        _zoomHost.RenderTransformOrigin = new Point(0, 0);
        _zoomHost.HorizontalAlignment = HorizontalAlignment.Left;
        _zoomHost.VerticalAlignment = VerticalAlignment.Top;

        _viewport.SizeChanged += (_, _) => Apply();
    }

    public double Factor => _factor;

    /// <summary>Reads the persisted factor (sync, startup-safe) and applies it.</summary>
    public void Initialize()
    {
        try
        {
            var saved = _preferences.GetPreference(UserPreferenceKeys.UiZoomFactor, ZoomLevels.Default);
            _factor = ZoomLevels.SnapToStep(saved);
        }
        catch (Exception ex)
        {
            _logService.LogDebug($"UiZoomManager: failed to load saved zoom, using default. {ex.Message}");
            _factor = ZoomLevels.Default;
        }
        Apply();
    }

    public void StepUp() => SetFactor(ZoomLevels.Next(_factor));
    public void StepDown() => SetFactor(ZoomLevels.Previous(_factor));
    public void Reset() => SetFactor(ZoomLevels.Default);

    private void SetFactor(double factor)
    {
        var snapped = ZoomLevels.SnapToStep(factor);
        if (Math.Abs(snapped - _factor) < 0.0001)
        {
            Apply(); // no change in level, but keep transform/size correct
            return;
        }
        _factor = snapped;
        Apply();
        Persist();
    }

    /// <summary>Inverse-sizes the host to the current viewport and applies the scale.</summary>
    public void Apply()
    {
        var w = _viewport.ActualWidth;
        var h = _viewport.ActualHeight;
        if (w <= 0 || h <= 0)
            return; // not measured yet; SizeChanged re-runs this once laid out

        _zoomHost.Width = w / _factor;
        _zoomHost.Height = h / _factor;
        _scale.ScaleX = _factor;
        _scale.ScaleY = _factor;
    }

    private async void Persist()
    {
        try
        {
            await _preferences.SetPreferenceAsync(UserPreferenceKeys.UiZoomFactor, _factor);
        }
        catch (Exception ex)
        {
            _logService.LogDebug($"UiZoomManager: failed to persist zoom. {ex.Message}");
        }
    }
}
