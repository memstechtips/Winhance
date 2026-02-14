using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;
using System.Windows.Input;

namespace Winhance.UI.Features.Common.Behaviors;

/// <summary>
/// Behavior to handle DataGrid selection changes in MVVM-compliant way.
/// Replaces code-behind selection handling logic.
/// Uses CommunityToolkit.WinUI.UI.Controls.DataGrid for WinUI 3.
/// </summary>
public class DataGridSelectionBehavior : Behavior<DataGrid>
{
    public static readonly DependencyProperty SelectionChangedCommandProperty =
        DependencyProperty.Register(
            nameof(SelectionChangedCommand),
            typeof(ICommand),
            typeof(DataGridSelectionBehavior),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ColumnHeaderClickCommandProperty =
        DependencyProperty.Register(
            nameof(ColumnHeaderClickCommand),
            typeof(ICommand),
            typeof(DataGridSelectionBehavior),
            new PropertyMetadata(null));

    /// <summary>
    /// Command to execute when selection changes.
    /// </summary>
    public ICommand? SelectionChangedCommand
    {
        get => (ICommand?)GetValue(SelectionChangedCommandProperty);
        set => SetValue(SelectionChangedCommandProperty, value);
    }

    /// <summary>
    /// Command to execute when column header is clicked for sorting.
    /// </summary>
    public ICommand? ColumnHeaderClickCommand
    {
        get => (ICommand?)GetValue(ColumnHeaderClickCommandProperty);
        set => SetValue(ColumnHeaderClickCommandProperty, value);
    }

    private bool _isHandlingSelection = false;

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject != null)
        {
            AssociatedObject.SelectionChanged += OnSelectionChanged;
            AssociatedObject.Sorting += OnSorting;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.SelectionChanged -= OnSelectionChanged;
            AssociatedObject.Sorting -= OnSorting;
        }

        base.OnDetaching();
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

    /// <summary>
    /// Handles the Sorting event from the CommunityToolkit DataGrid.
    /// Use the column's Tag property to specify the sort member path.
    /// </summary>
    private void OnSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (ColumnHeaderClickCommand == null)
            return;

        // Get the sort property from the column's Tag or derive from header content
        // Set Tag="PropertyName" on each DataGridColumn to specify the sort property
        string? sortProperty = e.Column.Tag?.ToString()
            ?? GetSortPropertyFromHeader(e.Column.Header?.ToString());

        if (!string.IsNullOrEmpty(sortProperty) && ColumnHeaderClickCommand.CanExecute(sortProperty))
        {
            ColumnHeaderClickCommand.Execute(sortProperty);
        }
    }

    private static string? GetSortPropertyFromHeader(string? headerContent)
    {
        return headerContent switch
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
