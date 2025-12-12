using System;
using System.Windows;
using System.Windows.Media;
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
                    
                    // Use VisualTreeHelper to find the button reliably, bypassing FindName scope issues
                    var moreButton = FindChild<FrameworkElement>(mainWindow, "MoreButton");

                    logService?.LogInformation($"Elements found - Overlay: {overlay != null}, FlyoutContent: {flyoutContent != null}, MoreButton: {moreButton != null}");

                    if (overlay != null && flyoutContent != null && moreButton != null)
                    {
                        moreButton.UpdateLayout();
                        var buttonPosition = moreButton.TransformToAncestor(mainWindow).Transform(new Point(0, 0));

                        logService?.LogInformation($"Button position: X={buttonPosition.X}, Y={buttonPosition.Y}, Button size: {moreButton.ActualWidth}x{moreButton.ActualHeight}");

                        // Reset margin before measuring to ensure DesiredSize doesn't include the previous margin
                        flyoutContent.Margin = new Thickness(0);

                        // Measure the flyout content to get its height
                        flyoutContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        var flyoutHeight = flyoutContent.DesiredSize.Height;
                        
                        // Calculate Top position to align the bottom of the flyout with the bottom of the button
                        // Default behavior for bottom-left menus
                        var topPosition = (buttonPosition.Y + moreButton.ActualHeight) - flyoutHeight;
                        
                        // Ensure we don't go off the top edge of the window
                        if (topPosition < 10)
                        {
                            topPosition = 10;
                        }

                        var flyoutMargin = new Thickness(
                            buttonPosition.X + moreButton.ActualWidth + 5,
                            topPosition,
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

        public void ShowAdvancedToolsFlyout()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    logService?.LogInformation("MainWindow found, showing AdvancedTools flyout overlay");

                    var overlay = mainWindow.FindName("AdvancedToolsOverlay") as FrameworkElement;
                    var flyoutContent = mainWindow.FindName("AdvancedToolsFlyoutContent") as FrameworkElement;
                    var advancedButton = mainWindow.FindName("AdvancedToolsButton") as FrameworkElement;

                    logService?.LogInformation($"Elements found - Overlay: {overlay != null}, FlyoutContent: {flyoutContent != null}, AdvancedButton: {advancedButton != null}");

                    if (overlay != null && flyoutContent != null && advancedButton != null)
                    {
                        advancedButton.UpdateLayout();
                        var buttonPosition = advancedButton.TransformToAncestor(mainWindow).Transform(new Point(0, 0));

                        logService?.LogInformation($"Button position: X={buttonPosition.X}, Y={buttonPosition.Y}, Button size: {advancedButton.ActualWidth}x{advancedButton.ActualHeight}");

                        var flyoutMargin = new Thickness(
                            buttonPosition.X + advancedButton.ActualWidth + 5,
                            buttonPosition.Y - 10,
                            0,
                            0
                        );

                        logService?.LogInformation($"Setting flyout margin: Left={flyoutMargin.Left}, Top={flyoutMargin.Top}");

                        flyoutContent.Margin = flyoutMargin;
                        overlay.Visibility = Visibility.Visible;

                        logService?.LogInformation($"Overlay visibility set to: {overlay.Visibility}");

                        overlay.Focus();
                        logService?.LogInformation("AdvancedTools flyout shown successfully");
                    }
                    else
                    {
                        logService?.LogWarning($"Could not find required flyout elements - Overlay: {overlay != null}, FlyoutContent: {flyoutContent != null}, AdvancedButton: {advancedButton != null}");
                    }
                }
                else
                {
                    logService?.LogWarning("MainWindow is null, cannot show flyout");
                }
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error showing AdvancedTools flyout: {ex.Message}", ex);
            }
        }

        public void CloseAdvancedToolsFlyout()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    var overlay = mainWindow.FindName("AdvancedToolsOverlay") as FrameworkElement;
                    if (overlay != null)
                    {
                        overlay.Visibility = Visibility.Collapsed;
                        logService?.LogInformation("AdvancedTools flyout closed");
                    }
                }
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error closing AdvancedTools flyout: {ex.Message}", ex);
            }
        }

        private T FindChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
        {
            if (parent == null) return null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T frameworkElement && frameworkElement.Name == childName)
                {
                    return frameworkElement;
                }

                var childOfChild = FindChild<T>(child, childName);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }
    }
}
