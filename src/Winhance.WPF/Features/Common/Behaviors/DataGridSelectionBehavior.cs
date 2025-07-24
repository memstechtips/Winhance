using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Xaml.Behaviors;
using System.Windows.Input;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Winhance.WPF.Features.Common.Behaviors
{
    /// <summary>
    /// Behavior to handle DataGrid selection changes in MVVM-compliant way
    /// Replaces code-behind selection handling logic
    /// </summary>
    public class DataGridSelectionBehavior : Behavior<DataGrid>
    {
        public static readonly DependencyProperty SelectionChangedCommandProperty =
            DependencyProperty.Register(nameof(SelectionChangedCommand), typeof(ICommand), typeof(DataGridSelectionBehavior));

        public static readonly DependencyProperty ColumnHeaderClickCommandProperty =
            DependencyProperty.Register(nameof(ColumnHeaderClickCommand), typeof(ICommand), typeof(DataGridSelectionBehavior));

        /// <summary>
        /// Command to execute when selection changes
        /// </summary>
        public ICommand SelectionChangedCommand
        {
            get => (ICommand)GetValue(SelectionChangedCommandProperty);
            set => SetValue(SelectionChangedCommandProperty, value);
        }

        /// <summary>
        /// Command to execute when column header is clicked for sorting
        /// </summary>
        public ICommand ColumnHeaderClickCommand
        {
            get => (ICommand)GetValue(ColumnHeaderClickCommandProperty);
            set => SetValue(ColumnHeaderClickCommandProperty, value);
        }

        private bool _isHandlingSelection = false;

        protected override void OnAttached()
        {
            base.OnAttached();
            
            if (AssociatedObject != null)
            {
                AssociatedObject.SelectionChanged += OnSelectionChanged;
                AssociatedObject.Loaded += OnDataGridLoaded;
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.SelectionChanged -= OnSelectionChanged;
                AssociatedObject.Loaded -= OnDataGridLoaded;
                
                // Unsubscribe from column header events
                UnsubscribeFromColumnHeaders();
            }
            
            base.OnDetaching();
        }

        private void OnDataGridLoaded(object sender, RoutedEventArgs e)
        {
            // Subscribe to column header click events for sorting
            SubscribeToColumnHeaders();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isHandlingSelection || SelectionChangedCommand == null)
                return;

            try
            {
                _isHandlingSelection = true;
                
                if (SelectionChangedCommand.CanExecute(null))
                {
                    SelectionChangedCommand.Execute(null);
                }
            }
            finally
            {
                _isHandlingSelection = false;
            }
        }

        private void SubscribeToColumnHeaders()
        {
            if (AssociatedObject?.Columns == null) return;

            foreach (var column in AssociatedObject.Columns)
            {
                if (column is DataGridTemplateColumn templateColumn)
                {
                    // Handle template column headers
                    if (templateColumn.HeaderStyle != null)
                    {
                        foreach (var setter in templateColumn.HeaderStyle.Setters)
                        {
                            if (setter is EventSetter eventSetter && eventSetter.Event.Name == "Click")
                            {
                                // Replace with command-based approach
                                eventSetter.Handler = new RoutedEventHandler(OnColumnHeaderClick);
                            }
                        }
                    }
                }
            }
        }

        private void UnsubscribeFromColumnHeaders()
        {
            // Cleanup is handled automatically when the DataGrid is unloaded
        }

        private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            if (sender is DataGridColumnHeader header && ColumnHeaderClickCommand != null)
            {
                string sortProperty = GetSortPropertyFromHeader(header);
                
                if (!string.IsNullOrEmpty(sortProperty) && ColumnHeaderClickCommand.CanExecute(sortProperty))
                {
                    ColumnHeaderClickCommand.Execute(sortProperty);
                }
            }
        }

        private string GetSortPropertyFromHeader(DataGridColumnHeader header)
        {
            return header.Content?.ToString() switch
            {
                "Name" => "Name",
                "Type" => "ItemType",
                "Status" => "IsInstalled",
                "Installable" => "CanBeReinstalled",
                "Package ID" => "PackageName",
                "Category" => "Category",
                "Source" => "Source",
                _ => null
            };
        }
    }
}
