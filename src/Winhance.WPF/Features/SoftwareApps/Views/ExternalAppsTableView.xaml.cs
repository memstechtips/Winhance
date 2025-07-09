using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Winhance.WPF.Features.SoftwareApps.ViewModels;
using Winhance.WPF.Features.SoftwareApps.Models;

namespace Winhance.WPF.Features.SoftwareApps.Views
{
    /// <summary>
    /// Interaction logic for ExternalAppsTableView.xaml
    /// </summary>
    public partial class ExternalAppsTableView : UserControl
    {
        public ExternalAppsTableView()
        {
            InitializeComponent();
            
            // Add handler for DataGrid selection changed events
            this.Loaded += (s, e) => {
                if (AppsDataGrid != null)
                {
                    AppsDataGrid.SelectionChanged += AppsDataGrid_SelectionChanged;
                    AppsDataGrid.LoadingRow += AppsDataGrid_LoadingRow;
                }
            };
        }

        private void DataGridColumnHeader_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is DataGridColumnHeader header && DataContext is ExternalAppsViewModel viewModel)
            {
                string? sortProperty = null;
                
                switch (header.Content?.ToString())
                {
                    case "Name":
                        sortProperty = "Name";
                        break;
                    case "Package ID":
                        sortProperty = "PackageName";
                        break;
                    case "Category":
                        sortProperty = "Category";
                        break;
                    case "Source":
                        sortProperty = "Source";
                        break;
                }

                if (!string.IsNullOrEmpty(sortProperty) && viewModel.SortByCommand?.CanExecute(sortProperty) == true)
                {
                    viewModel.SortByCommand.Execute(sortProperty);
                }
            }
        }
        
        // Flag to prevent recursive selection handling
        private bool _handlingSelection = false;
        
        private void AppsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Skip if we're already handling a selection change to prevent cycles
            if (_handlingSelection) return;
            
            try
            {
                _handlingSelection = true;
                
                // When selection changes in the DataGrid, invalidate the selection cache in the viewmodel
                if (DataContext is ExternalAppsViewModel viewModel)
                {   
                    // Force a refresh of HasSelectedItems - without changing checkbox states
                    // This ensures the buttons get updated but doesn't interfere with checkbox state
                    viewModel.InvalidateSelectionState();
                }
            }
            finally
            {
                _handlingSelection = false;
            }
        }
        
        private void AppsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // Subscribe to checkbox change events
            if (e.Row?.DataContext != null)
            {
                var checkBox = FindCheckboxInRow(e.Row);
                if (checkBox != null)
                {
                    checkBox.Checked += CheckBox_SelectionChanged;
                    checkBox.Unchecked += CheckBox_SelectionChanged;
                }
            }
        }
        
        private void CheckBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            // Skip if we're already handling a selection change
            if (_handlingSelection) return;
            
            try
            {
                _handlingSelection = true;
                
                // When a checkbox is checked/unchecked, invalidate the selection cache
                if (DataContext is ExternalAppsViewModel viewModel)
                {
                    // This will force a reevaluation of HasSelectedItems
                    viewModel.InvalidateSelectionState();
                    
                    // Log the selection state for debugging
                    if (sender is CheckBox checkBox)
                    {
                        DebugLogger.Log($"[DEBUG] CheckBox_SelectionChanged: IsChecked={checkBox.IsChecked}");
                    }
                }
            }
            finally
            {
                _handlingSelection = false;
            }
        }
        
        private CheckBox FindCheckboxInRow(DataGridRow row)
        {
            if (row == null) return null;
            
            // Find the checkbox in the first cell
            var cell = row.FindName("Cell_0") as DataGridCell ?? 
                       AppsDataGrid.Columns[0]?.GetCellContent(row)?.Parent as DataGridCell;
                       
            if (cell != null)
            {
                return FindVisualChild<CheckBox>(cell);
            }
            
            return null;
        }
        
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T found)
                    return found;
                    
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            
            return null;
        }
    }
}
