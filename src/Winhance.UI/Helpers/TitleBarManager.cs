using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Winhance.Core.Features.Common.Interfaces;
using System;
using System.Collections.Generic;
using Windows.Graphics;

namespace Winhance.UI.Helpers;

/// <summary>
/// Manages title bar passthrough regions, padding, and caption button theming.
/// Extracted from MainWindow to reduce code-behind complexity.
/// </summary>
internal sealed class TitleBarManager
{
    private readonly AppWindow _appWindow;
    private readonly ILogService? _logService;

    public TitleBarManager(AppWindow appWindow, ILogService? logService)
    {
        _appWindow = appWindow;
        _logService = logService;
    }

    /// <summary>
    /// Sets up passthrough regions for interactive elements in the title bar.
    /// This prevents double-clicks on buttons from maximizing the window.
    /// </summary>
    public void SetPassthroughRegions(
        FrameworkElement titleBar,
        FrameworkElement paneToggleButton,
        FrameworkElement titleBarButtons)
    {
        try
        {
            var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(_appWindow.Id);
            var scale = titleBar.XamlRoot?.RasterizationScale ?? 1.0;

            var passthroughRects = new List<RectInt32>();

            // Add passthrough region for the pane toggle button
            AddElementPassthroughRect(paneToggleButton, scale, passthroughRects);

            // Add passthrough region for the entire title bar buttons container
            AddElementPassthroughRect(titleBarButtons, scale, passthroughRects);

            if (passthroughRects.Count > 0)
            {
                nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, passthroughRects.ToArray());
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to set title bar passthrough regions: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds an element's bounds to the passthrough rectangles list.
    /// </summary>
    private void AddElementPassthroughRect(FrameworkElement element, double scale, List<RectInt32> rects)
    {
        try
        {
            var transform = element.TransformToVisual(null);
            var bounds = transform.TransformBounds(
                new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));

            rects.Add(new RectInt32(
                _X: (int)Math.Round(bounds.X * scale),
                _Y: (int)Math.Round(bounds.Y * scale),
                _Width: (int)Math.Round(bounds.Width * scale),
                _Height: (int)Math.Round(bounds.Height * scale)
            ));
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to add passthrough rect for {element.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets the padding columns to account for system caption buttons.
    /// Handles RTL layout by swapping inset assignments.
    /// </summary>
    public void SetTitleBarPadding(
        ColumnDefinition leftPaddingColumn,
        ColumnDefinition rightPaddingColumn,
        FrameworkElement titleBar,
        FlowDirection flowDirection)
    {
        try
        {
            var appTitleBar = _appWindow.TitleBar;
            var scale = titleBar.XamlRoot?.RasterizationScale ?? 1.0;

            // When FlowDirection is RTL, the grid columns are visually mirrored:
            // LeftPaddingColumn (Column 0) renders on the physical right,
            // RightPaddingColumn (last column) renders on the physical left.
            // The system caption buttons remain physically on the right regardless of FlowDirection,
            // so we swap the inset assignments to keep padding aligned with the caption buttons.
            if (flowDirection == FlowDirection.RightToLeft)
            {
                leftPaddingColumn.Width = new GridLength(appTitleBar.RightInset / scale);
                rightPaddingColumn.Width = new GridLength(appTitleBar.LeftInset / scale);
            }
            else
            {
                rightPaddingColumn.Width = new GridLength(appTitleBar.RightInset / scale);
                leftPaddingColumn.Width = new GridLength(appTitleBar.LeftInset / scale);
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to set title bar padding: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies theme-aware colors to the caption buttons (minimize, maximize, close).
    /// </summary>
    public void ApplyThemeToCaptionButtons(ElementTheme currentTheme)
    {
        try
        {
            var titleBar = _appWindow.TitleBar;

            // Set foreground colors based on theme
            var foregroundColor = currentTheme == ElementTheme.Dark
                ? Microsoft.UI.Colors.White
                : Microsoft.UI.Colors.Black;

            titleBar.ButtonForegroundColor = foregroundColor;
            titleBar.ButtonHoverForegroundColor = foregroundColor;

            // Set hover background to subtle theme-aware color (~9% opacity)
            var hoverBackgroundColor = currentTheme == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(24, 255, 255, 255)  // Subtle white
                : Windows.UI.Color.FromArgb(24, 0, 0, 0);        // Subtle black

            titleBar.ButtonHoverBackgroundColor = hoverBackgroundColor;

            // Set other backgrounds to transparent
            titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to apply caption button colors: {ex.Message}");
        }
    }
}
