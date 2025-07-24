using System.Windows;
using System.Windows.Controls;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Controls
{
    /// <summary>
    /// Interaction logic for MoreMenu.xaml
    /// </summary>
    public partial class MoreMenu : UserControl
    {
        public MoreMenu()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows the context menu at the specified placement target
        /// </summary>
        /// <param name="placementTarget">The UI element that the context menu should be positioned relative to</param>
        public void ShowMenu(UIElement placementTarget)
        {
            var contextMenu = Resources["MoreButtonContextMenu"] as ContextMenu;
            if (contextMenu != null)
            {
                contextMenu.DataContext = DataContext;
                contextMenu.PlacementTarget = placementTarget;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
                contextMenu.HorizontalOffset = 0;
                contextMenu.VerticalOffset = 0;
                contextMenu.Closed += ContextMenu_Closed;
                contextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// Handles the context menu closing event to reset the navigation button selection
        /// </summary>
        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            var parentWindow = Window.GetWindow(this);
            if (parentWindow?.DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.SelectedNavigationItem = mainViewModel.CurrentViewName;
            }
        }
    }
}
