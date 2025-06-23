using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Service for displaying help content in a popup
    /// </summary>
    public class HelpService
    {
        private static Popup _currentPopup;
        private static SoftwareAppsViewModel _currentViewModel;

        /// <summary>
        /// Shows help content in a popup attached to the specified element
        /// </summary>
        /// <param name="helpContent">The UserControl containing the help content</param>
        /// <param name="attachedElement">The element to attach the popup to</param>
        public static void ShowHelp(UserControl helpContent, FrameworkElement attachedElement)
        {
            // Close any existing popup
            CloseCurrentPopup();

            // Create new popup
            _currentPopup = new Popup
            {
                Child = helpContent,
                StaysOpen = false,
                Placement = PlacementMode.Bottom,
                PlacementTarget = attachedElement,
                IsOpen = true,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade
            };

            // Add a drop shadow effect
            var border = new Border
            {
                Child = helpContent,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 315,
                    ShadowDepth = 5,
                    BlurRadius = 10,
                    Opacity = 0.5
                }
            };

            _currentPopup.Child = border;

            // Store the view model reference if available
            if (attachedElement.DataContext is SoftwareAppsViewModel viewModel)
            {
                _currentViewModel = viewModel;
            }

            // Close popup when clicking outside
            MouseButtonEventHandler clickHandler = null;
            clickHandler = (s, e) =>
            {
                CloseCurrentPopup();
                Mouse.RemovePreviewMouseDownOutsideCapturedElementHandler(
                    _currentPopup, clickHandler);
            };

            Mouse.AddPreviewMouseDownOutsideCapturedElementHandler(
                _currentPopup, clickHandler);
        }

        /// <summary>
        /// Closes the currently open popup if any
        /// </summary>
        public static void CloseCurrentPopup()
        {
            if (_currentPopup != null && _currentPopup.IsOpen)
            {
                _currentPopup.IsOpen = false;
                _currentPopup = null;

                // Update the view model if available
                if (_currentViewModel != null)
                {
                    _currentViewModel.IsHelpVisible = false;
                    _currentViewModel = null;
                }
            }
        }
    }
}
