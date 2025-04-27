using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Messages;
using Winhance.WPF.Features.Optimize.ViewModels;

namespace Winhance.WPF.Features.Optimize.Views
{
    public partial class OptimizeView : UserControl
    {
        private IMessengerService? _messengerService; // Changed from readonly to allow assignment
        
        // References to the child views
        private UIElement _windowsSecurityOptimizationsView;
        private UIElement _privacySettingsView;
        private UIElement _gamingandPerformanceOptimizationsView;
        private UIElement _updateOptimizationsView;
        private UIElement _powerSettingsView;
        private UIElement _explorerOptimizationsView;
        private UIElement _notificationOptimizationsView;
        private UIElement _soundOptimizationsView;

        public OptimizeView()
        {
            InitializeComponent();
            Loaded += OptimizeView_Loaded;

            // Initialize section visibility and click handlers - all visible by default
            // WindowsSecurityContent will be initialized in the Loaded event

            // Add click handlers to the header border elements
            WindowsSecurityHeaderBorder.MouseDown += WindowsSecurityHeader_MouseDown;
            PrivacyHeaderBorder.MouseDown += PrivacyHeader_MouseDown;
            GamingHeaderBorder.MouseDown += GamingHeader_MouseDown;
            UpdatesHeaderBorder.MouseDown += UpdatesHeader_MouseDown;
            PowerHeaderBorder.MouseDown += PowerHeader_MouseDown;
            ExplorerHeaderBorder.MouseDown += ExplorerHeader_MouseDown;
            NotificationHeaderBorder.MouseDown += NotificationHeader_MouseDown;
            SoundHeaderBorder.MouseDown += SoundHeader_MouseDown;
            
            // Subscribe to DataContextChanged event to get the messenger service
            DataContextChanged += OptimizeView_DataContextChanged;
        }
        
        private void OptimizeView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unregister from old messenger service if exists
            if (_messengerService != null)
            {
                _messengerService.Unregister(this);
                _messengerService = null;
            }

            // Register with new messenger service if available
            if (e.NewValue is OptimizeViewModel viewModel &&
                viewModel.MessengerService is IMessengerService messengerService)
            {
                _messengerService = messengerService;
                
                // Subscribe to the message that signals resetting expansion states
                _messengerService.Register<ResetExpansionStateMessage>(this, OnResetExpansionState);
            }
        }
        
        private void OnResetExpansionState(ResetExpansionStateMessage message)
        {
            // Reset all section expansion states to expanded
            if (_windowsSecurityOptimizationsView != null)
            {
                _windowsSecurityOptimizationsView.Visibility = Visibility.Visible;
                WindowsSecurityHeaderIcon.Kind = PackIconKind.ChevronUp;
            }
            
            if (_privacySettingsView != null)
            {
                _privacySettingsView.Visibility = Visibility.Visible;
                PrivacyHeaderIcon.Kind = PackIconKind.ChevronUp;
            }
            
            if (_gamingandPerformanceOptimizationsView != null)
            {
                _gamingandPerformanceOptimizationsView.Visibility = Visibility.Visible;
                GamingHeaderIcon.Kind = PackIconKind.ChevronUp;
            }
            
            if (_updateOptimizationsView != null)
            {
                _updateOptimizationsView.Visibility = Visibility.Visible;
                UpdatesHeaderIcon.Kind = PackIconKind.ChevronUp;
            }
            
            if (_powerSettingsView != null)
            {
                _powerSettingsView.Visibility = Visibility.Visible;
                PowerHeaderIcon.Kind = PackIconKind.ChevronUp;
            }
            
            if (_explorerOptimizationsView != null)
            {
                _explorerOptimizationsView.Visibility = Visibility.Visible;
                ExplorerHeaderIcon.Kind = PackIconKind.ChevronUp;
            }
            
            if (_notificationOptimizationsView != null)
            {
                _notificationOptimizationsView.Visibility = Visibility.Visible;
                NotificationHeaderIcon.Kind = PackIconKind.ChevronUp;
            }
            
            if (_soundOptimizationsView != null)
            {
                _soundOptimizationsView.Visibility = Visibility.Visible;
                SoundHeaderIcon.Kind = PackIconKind.ChevronUp;
            }
        }

        private async void OptimizeView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is Winhance.WPF.Features.Optimize.ViewModels.OptimizeViewModel viewModel)
            {
                try
                {
                    // Find the child views
                    _windowsSecurityOptimizationsView = FindChildByType<WindowsSecurityOptimizationsView>(this);
                    _privacySettingsView = FindChildByType<PrivacyOptimizationsView>(this);
                    _gamingandPerformanceOptimizationsView = FindChildByType<GamingandPerformanceOptimizationsView>(this);
                    _updateOptimizationsView = FindChildByType<UpdateOptimizationsView>(this);
                    _powerSettingsView = FindChildByType<PowerOptimizationsView>(this);
                    _explorerOptimizationsView = FindChildByType<ExplorerOptimizationsView>(this);
                    _notificationOptimizationsView = FindChildByType<NotificationOptimizationsView>(this);
                    _soundOptimizationsView = FindChildByType<SoundOptimizationsView>(this);

                    // Set initial visibility
                    if (_windowsSecurityOptimizationsView != null) _windowsSecurityOptimizationsView.Visibility = Visibility.Visible;
                    if (_privacySettingsView != null) _privacySettingsView.Visibility = Visibility.Visible;
                    if (_gamingandPerformanceOptimizationsView != null) _gamingandPerformanceOptimizationsView.Visibility = Visibility.Visible;
                    if (_updateOptimizationsView != null) _updateOptimizationsView.Visibility = Visibility.Visible;
                    if (_powerSettingsView != null) _powerSettingsView.Visibility = Visibility.Visible;
                    if (_explorerOptimizationsView != null) _explorerOptimizationsView.Visibility = Visibility.Visible;
                    if (_notificationOptimizationsView != null) _notificationOptimizationsView.Visibility = Visibility.Visible;
                    if (_soundOptimizationsView != null) _soundOptimizationsView.Visibility = Visibility.Visible;

                    // Set header icons
                    WindowsSecurityHeaderIcon.Kind = PackIconKind.ChevronUp; // Upward arrow for expanded state
                    PrivacyHeaderIcon.Kind = PackIconKind.ChevronUp; // Upward arrow for expanded state
                    GamingHeaderIcon.Kind = PackIconKind.ChevronUp; // Upward arrow for expanded state
                    UpdatesHeaderIcon.Kind = PackIconKind.ChevronUp; // Upward arrow for expanded state
                    PowerHeaderIcon.Kind = PackIconKind.ChevronUp; // Upward arrow for expanded state
                    ExplorerHeaderIcon.Kind = PackIconKind.ChevronUp; // Upward arrow for expanded state
                    NotificationHeaderIcon.Kind = PackIconKind.ChevronUp; // Upward arrow for expanded state
                    SoundHeaderIcon.Kind = PackIconKind.ChevronUp; // Upward arrow for expanded state

                    // Initialize data if needed
                    if (viewModel.InitializeCommand != null)
                    {
                        Console.WriteLine("OptimizeView: Executing InitializeCommand");
                        await viewModel.InitializeCommand.ExecuteAsync(null);
                        Console.WriteLine("OptimizeView: InitializeCommand completed successfully");
                    }
                    else
                    {
                        MessageBox.Show("InitializeCommand is null in OptimizeViewModel",
                            "Initialization Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"OptimizeView: Error initializing - {ex.Message}");
                    MessageBox.Show($"Error initializing Optimize view: {ex.Message}",
                        "Initialization Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            else
            {
                Console.WriteLine("OptimizeView: DataContext is not OptimizeViewModel");
                MessageBox.Show("DataContext is not OptimizeViewModel",
                    "Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void WindowsSecurityHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Toggle section visibility
                ToggleSectionVisibility(_windowsSecurityOptimizationsView, WindowsSecurityHeaderIcon);

                // Toggle the selection state by clicking the hidden button
                if (e.OriginalSource is not Button) // Don't trigger if we clicked the hidden button
                {
                    WindowsSecurityToggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }

                e.Handled = true;  // Mark as handled to prevent the event from bubbling up
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling Windows Security Settings section: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrivacyHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Toggle section visibility
                ToggleSectionVisibility(_privacySettingsView, PrivacyHeaderIcon);

                // Toggle the selection state by clicking the hidden button
                if (e.OriginalSource is not Button) // Don't trigger if we clicked the hidden button
                {
                    PrivacyToggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }

                e.Handled = true;  // Mark as handled to prevent the event from bubbling up
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling Privacy Settings section: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GamingHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Toggle section visibility
            ToggleSectionVisibility(_gamingandPerformanceOptimizationsView, GamingHeaderIcon);

            // Toggle the selection state by clicking the hidden button
            if (e.OriginalSource is not Button) // Don't trigger if we clicked the hidden button
            {
                GamingToggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }

            e.Handled = true;
        }

        private void UpdatesHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Toggle section visibility
            ToggleSectionVisibility(_updateOptimizationsView, UpdatesHeaderIcon);

            // Toggle the selection state by clicking the hidden button
            if (e.OriginalSource is not Button) // Don't trigger if we clicked the hidden button
            {
                UpdatesToggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }

            e.Handled = true;
        }

        private void PowerHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Toggle section visibility
            ToggleSectionVisibility(_powerSettingsView, PowerHeaderIcon);

            // Toggle the selection state by clicking the hidden button
            if (e.OriginalSource is not Button) // Don't trigger if we clicked the hidden button
            {
                PowerToggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }

            e.Handled = true;
        }

        private void ExplorerHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Toggle section visibility
            ToggleSectionVisibility(_explorerOptimizationsView, ExplorerHeaderIcon);

            // Toggle the selection state by clicking the hidden button
            if (e.OriginalSource is not Button) // Don't trigger if we clicked the hidden button
            {
                ExplorerToggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }

            e.Handled = true;
        }

        private void NotificationHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Toggle section visibility
            ToggleSectionVisibility(_notificationOptimizationsView, NotificationHeaderIcon);

            // Toggle the selection state by clicking the hidden button
            if (e.OriginalSource is not Button) // Don't trigger if we clicked the hidden button
            {
                NotificationToggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }

            e.Handled = true;
        }

        private void SoundHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Toggle section visibility
            ToggleSectionVisibility(_soundOptimizationsView, SoundHeaderIcon);

            // Toggle the selection state by clicking the hidden button
            if (e.OriginalSource is not Button) // Don't trigger if we clicked the hidden button
            {
                SoundToggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }

            e.Handled = true;
        }

        private void ToggleSectionVisibility(UIElement content, PackIcon icon)
        {
            if (content == null) return;

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

        /// <summary>
        /// Finds a child element of the specified type in the visual tree.
        /// </summary>
        /// <typeparam name="T">The type of element to find.</typeparam>
        /// <param name="parent">The parent element to search in.</param>
        /// <returns>The first child element of the specified type, or null if not found.</returns>
        private T FindChildByType<T>(DependencyObject parent) where T : DependencyObject
        {
            // Confirm parent and type are valid
            if (parent == null) return null;

            // Get child count
            int childCount = VisualTreeHelper.GetChildrenCount(parent);

            // Check all children
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                // If the child is the type we're looking for, return it
                if (child is T typedChild)
                {
                    return typedChild;
                }

                // Otherwise, recursively check this child's children
                var result = FindChildByType<T>(child);
                if (result != null)
                {
                    return result;
                }
            }

            // If we get here, we didn't find a match
            return null;
        }
    }

    // Extension method to check if a visual element is a descendant of another
    public static class VisualExtensions
    {
        public static bool IsDescendantOf(this System.Windows.DependencyObject element, System.Windows.DependencyObject parent)
        {
            if (element == parent)
                return true;

            var currentParent = System.Windows.Media.VisualTreeHelper.GetParent(element);
            while (currentParent != null)
            {
                if (currentParent == parent)
                    return true;
                currentParent = System.Windows.Media.VisualTreeHelper.GetParent(currentParent);
            }

            return false;
        }
    }
}
