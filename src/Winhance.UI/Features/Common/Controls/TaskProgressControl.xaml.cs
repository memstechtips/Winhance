using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// A bottom bar control that displays task progress with app name, terminal output,
/// cancel button, and an indeterminate progress bar.
/// </summary>
public sealed partial class TaskProgressControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty AppNameProperty =
        DependencyProperty.Register(
            nameof(AppName),
            typeof(string),
            typeof(TaskProgressControl),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LastTerminalLineProperty =
        DependencyProperty.Register(
            nameof(LastTerminalLine),
            typeof(string),
            typeof(TaskProgressControl),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsProgressVisibleProperty =
        DependencyProperty.Register(
            nameof(IsProgressVisible),
            typeof(Visibility),
            typeof(TaskProgressControl),
            new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty CanCancelProperty =
        DependencyProperty.Register(
            nameof(CanCancel),
            typeof(Visibility),
            typeof(TaskProgressControl),
            new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty IsTaskRunningProperty =
        DependencyProperty.Register(
            nameof(IsTaskRunning),
            typeof(bool),
            typeof(TaskProgressControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.Register(
            nameof(CancelCommand),
            typeof(ICommand),
            typeof(TaskProgressControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CancelTextProperty =
        DependencyProperty.Register(
            nameof(CancelText),
            typeof(string),
            typeof(TaskProgressControl),
            new PropertyMetadata("Cancel"));

    #endregion

    #region Properties

    public string AppName
    {
        get => (string)GetValue(AppNameProperty);
        set => SetValue(AppNameProperty, value);
    }

    public string LastTerminalLine
    {
        get => (string)GetValue(LastTerminalLineProperty);
        set => SetValue(LastTerminalLineProperty, value);
    }

    public Visibility IsProgressVisible
    {
        get => (Visibility)GetValue(IsProgressVisibleProperty);
        set => SetValue(IsProgressVisibleProperty, value);
    }

    public Visibility CanCancel
    {
        get => (Visibility)GetValue(CanCancelProperty);
        set => SetValue(CanCancelProperty, value);
    }

    public bool IsTaskRunning
    {
        get => (bool)GetValue(IsTaskRunningProperty);
        set => SetValue(IsTaskRunningProperty, value);
    }

    public ICommand CancelCommand
    {
        get => (ICommand)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public string CancelText
    {
        get => (string)GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    #endregion

    public TaskProgressControl()
    {
        this.InitializeComponent();
    }
}
