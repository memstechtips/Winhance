using System;
using System.Windows;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Services
{
    public class FlyoutManagementService(ILogService logService) : IFlyoutManagementService
    {
        public void ShowMoreMenuFlyout()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    logService?.LogInformation("MainWindow found, showing flyout overlay");

                    var overlay = mainWindow.FindName("MoreMenuOverlay") as FrameworkElement;
                    var flyoutContent = mainWindow.FindName("MoreMenuFlyoutContent") as FrameworkElement;
                    var moreButton = mainWindow.FindName("MoreButton") as FrameworkElement;

                    logService?.LogInformation($"Elements found - Overlay: {overlay != null}, FlyoutContent: {flyoutContent != null}, MoreButton: {moreButton != null}");

                    if (overlay != null && flyoutContent != null && moreButton != null)
                    {
                        var buttonPosition = moreButton.TransformToAncestor(mainWindow).Transform(new Point(0, 0));

                        logService?.LogInformation($"Button position: X={buttonPosition.X}, Y={buttonPosition.Y}, Button size: {moreButton.ActualWidth}x{moreButton.ActualHeight}");

                        var flyoutMargin = new Thickness(
                            buttonPosition.X + moreButton.ActualWidth + 5,
                            buttonPosition.Y - (moreButton.ActualHeight * 2) - 45,
                            0,
                            0
                        );

                        logService?.LogInformation($"Setting flyout margin: Left={flyoutMargin.Left}, Top={flyoutMargin.Top}");

                        flyoutContent.Margin = flyoutMargin;
                        overlay.Visibility = Visibility.Visible;

                        logService?.LogInformation($"Overlay visibility set to: {overlay.Visibility}");

                        overlay.Focus();
                        logService?.LogInformation("MoreMenu flyout shown successfully");
                    }
                    else
                    {
                        logService?.LogWarning($"Could not find required flyout elements - Overlay: {overlay != null}, FlyoutContent: {flyoutContent != null}, MoreButton: {moreButton != null}");
                    }
                }
                else
                {
                    logService?.LogWarning("MainWindow is null, cannot show flyout");
                }
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error showing MoreMenu flyout: {ex.Message}", ex);
            }
        }

        public void CloseMoreMenuFlyout()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    var overlay = mainWindow.FindName("MoreMenuOverlay") as FrameworkElement;
                    if (overlay != null)
                    {
                        overlay.Visibility = Visibility.Collapsed;
                        logService?.LogInformation("MoreMenu flyout closed");
                    }
                }
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error closing MoreMenu flyout: {ex.Message}", ex);
            }
        }
    }
}