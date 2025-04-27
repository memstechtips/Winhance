using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Messages;
using Winhance.WPF.Features.Customize.ViewModels;

namespace Winhance.WPF.Features.Customize.Views
{
    /// <summary>
    /// Interaction logic for CustomizeView.xaml
    /// </summary>
    public partial class CustomizeView : UserControl
    {
        private IMessengerService? _messengerService; // Changed from readonly to allow assignment
        
        public CustomizeView()
        {
            InitializeComponent();
            Loaded += CustomizeView_Loaded;
            
            // Get messenger service from DataContext when it's set
            DataContextChanged += (s, e) => 
            {
                if (e.NewValue is CustomizeViewModel viewModel && 
                    viewModel.MessengerService is IMessengerService messengerService)
                {
                    _messengerService = messengerService;
                    SubscribeToMessages();
                }
            };
        }
        
        private void SubscribeToMessages()
        {
            if (_messengerService == null)
                return;
                
            // Subscribe to the message that signals resetting expansion states
            _messengerService.Register<ResetExpansionStateMessage>(this, OnResetExpansionState);
        }
        
        private void OnResetExpansionState(ResetExpansionStateMessage message)
        {
            // Reset all sections to be expanded
            ResetAllExpansionStates();
        }
        
        /// <summary>
        /// Resets all section expansion states to expanded
        /// </summary>
        private void ResetAllExpansionStates()
        {
            // Ensure all content is visible
            TaskbarContent.Visibility = Visibility.Visible;
            StartMenuContent.Visibility = Visibility.Visible;
            ExplorerContent.Visibility = Visibility.Visible;
            WindowsThemeContent.Visibility = Visibility.Visible;
            
            // Set all arrow icons to up (expanded state)
            TaskbarHeaderIcon.Kind = PackIconKind.ChevronUp;
            StartMenuHeaderIcon.Kind = PackIconKind.ChevronUp;
            ExplorerHeaderIcon.Kind = PackIconKind.ChevronUp;
            WindowsThemeHeaderIcon.Kind = PackIconKind.ChevronUp;
        }

        private void CustomizeView_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize sections to be expanded by default
            TaskbarContent.Visibility = Visibility.Visible;
            StartMenuContent.Visibility = Visibility.Visible;
            ExplorerContent.Visibility = Visibility.Visible;
            WindowsThemeContent.Visibility = Visibility.Visible;

            // Set arrow icons to up arrow
            TaskbarHeaderIcon.Kind = PackIconKind.ChevronUp; // Up arrow
            StartMenuHeaderIcon.Kind = PackIconKind.ChevronUp; // Up arrow
            ExplorerHeaderIcon.Kind = PackIconKind.ChevronUp; // Up arrow
            WindowsThemeHeaderIcon.Kind = PackIconKind.ChevronUp; // Up arrow

            // Remove all existing event handlers from all border elements
            foreach (var element in this.FindVisualChildren<Border>(this))
            {
                if (element?.Tag != null && element.Tag is string tag)
                {
                    // Remove any existing MouseLeftButtonDown handlers
                    element.MouseLeftButtonDown -= HeaderBorder_MouseLeftButtonDown;

                    // Add our new handler
                    element.PreviewMouseDown += Element_PreviewMouseDown;
                }
            }
        }

        private void Element_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // If we clicked on a Button, don't handle it here
            if (e.OriginalSource is Button)
            {
                return;
            }

            if (sender is Border border && border.Tag is string tag)
            {
                try
                {
                    // Toggle visibility and selection based on tag
                    switch (tag)
                    {
                        case "0":
                            ToggleSectionVisibility(TaskbarContent, TaskbarHeaderIcon);
                            TaskbarToggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            break;
                        case "1":
                            ToggleSectionVisibility(StartMenuContent, StartMenuHeaderIcon);
                            StartMenuToggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            break;
                        case "2":
                            ToggleSectionVisibility(ExplorerContent, ExplorerHeaderIcon);
                            ExplorerToggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            break;
                        case "3":
                            ToggleSectionVisibility(WindowsThemeContent, WindowsThemeHeaderIcon);
                            WindowsThemeToggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            break;
                    }

                    // Mark event as handled so it won't bubble up
                    e.Handled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error handling header click: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // This method is no longer needed since we're not checking for checkboxes anymore

        private void ToggleSectionVisibility(UIElement content, PackIcon icon)
        {
            if (content.Visibility == Visibility.Collapsed)
            {
                content.Visibility = Visibility.Visible;
                icon.Kind = PackIconKind.ChevronUp; // Upward arrow for expanded state
            }
            else
            {
                content.Visibility = Visibility.Collapsed;
                icon.Kind = PackIconKind.ChevronDown; // Downward arrow for collapsed state
            }
        }

        // This is defined in the XAML, so we need to keep it to avoid errors
        private void HeaderBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // This no longer does anything because we're using PreviewMouseDown now
            // We'll just mark it as handled to prevent unexpected behavior
            e.Handled = true;
        }

        // Helper method to find visual children of a specific type
        private System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T childOfType)
                    yield return childOfType;

                foreach (T childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }
    }
}
