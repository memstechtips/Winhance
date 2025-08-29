using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Winhance.WPF.Features.Common.Views
{
    /// <summary>
    /// Reusable Modal Dialog Window that follows Material Design principles
    /// </summary>
    public partial class ModalDialog : Window
    {
        public ModalDialog()
        {
            InitializeComponent();
            ApplyModalDialogStyle();
        }

        public ModalDialog(string title, object content, double width = 650, double height = 500)
        {
            InitializeComponent();
            ApplyModalDialogStyle();

            // Set window properties
            Width = width;
            Height = height;

            // Set title and content in the XAML template
            SetTitleAndContent(title, content);
        }

        private void SetTitleAndContent(string title, object content)
        {
            // Find the title text block and content presenter in the XAML
            var titleTextBlock = FindName("PART_TitleText") as TextBlock;
            var contentPresenter = FindName("PART_ContentPresenter") as ContentPresenter;

            if (titleTextBlock != null)
            {
                titleTextBlock.Text = title;
            }

            if (contentPresenter != null)
            {
                contentPresenter.Content = content;
            }
        }

        private void ApplyModalDialogStyle()
        {
            // Apply style programmatically to avoid resource loading issues
            try
            {
                var style = FindResource("ModalDialogWindowStyle") as Style;
                if (style != null)
                {
                    Style = style;
                    return; // Style applied successfully
                }
            }
            catch
            {
                // Style not found, continue to fallback
            }

            // Fallback: Apply basic styling manually with proper window layering
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = true;  // Show in taskbar to avoid getting stuck
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            // Rely on Owner relationship instead of Topmost for proper app-level layering
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Find template parts and wire up events (for style-based templates)
            var titleBar = GetTemplateChild("PART_TitleBar") as Grid;
            var closeButton = GetTemplateChild("PART_CloseButton") as Button;

            if (titleBar != null)
            {
                titleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
            }

            if (closeButton != null)
            {
                closeButton.Click += CloseButton_Click;
            }
        }

        // Event handler for title bar dragging (used by XAML)
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // Event handler for close button (used by XAML)
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
