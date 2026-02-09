using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;
using System.Windows.Input;

namespace Winhance.UI.Features.Common.Behaviors;

/// <summary>
/// Behavior to handle CheckBox selection changes in MVVM-compliant way.
/// Replaces code-behind checkbox event handling.
/// </summary>
public class CheckBoxSelectionBehavior : Behavior<CheckBox>
{
    public static readonly DependencyProperty SelectionChangedCommandProperty =
        DependencyProperty.Register(
            nameof(SelectionChangedCommand),
            typeof(ICommand),
            typeof(CheckBoxSelectionBehavior),
            new PropertyMetadata(null));

    /// <summary>
    /// Command to execute when checkbox selection changes.
    /// </summary>
    public ICommand? SelectionChangedCommand
    {
        get => (ICommand?)GetValue(SelectionChangedCommandProperty);
        set => SetValue(SelectionChangedCommandProperty, value);
    }

    private bool _isHandlingSelection = false;

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject != null)
        {
            AssociatedObject.Checked += OnSelectionChanged;
            AssociatedObject.Unchecked += OnSelectionChanged;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.Checked -= OnSelectionChanged;
            AssociatedObject.Unchecked -= OnSelectionChanged;
        }

        base.OnDetaching();
    }

    private void OnSelectionChanged(object sender, RoutedEventArgs e)
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
}
