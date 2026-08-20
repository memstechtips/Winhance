using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.TechnicalDetails;

namespace Winhance.UI.Features.Common.Controls;

// One control for all three card shapes: plain card, parent expander, child row.
public sealed partial class TechnicalDetailsPanel : UserControl
{
    public TechnicalDetailsPanel()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty MatrixProperty =
        DependencyProperty.Register(
            nameof(Matrix),
            typeof(OptionMatrix),
            typeof(TechnicalDetailsPanel),
            new PropertyMetadata(null));

    public OptionMatrix? Matrix
    {
        get => (OptionMatrix?)GetValue(MatrixProperty);
        set => SetValue(MatrixProperty, value);
    }

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(TechnicalDetailsPanel),
            new PropertyMetadata(false));

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public static readonly DependencyProperty IsBarVisibleProperty =
        DependencyProperty.Register(
            nameof(IsBarVisible),
            typeof(bool),
            typeof(TechnicalDetailsPanel),
            new PropertyMetadata(false));

    public bool IsBarVisible
    {
        get => (bool)GetValue(IsBarVisibleProperty);
        set => SetValue(IsBarVisibleProperty, value);
    }

    public static readonly DependencyProperty HeaderLabelProperty =
        DependencyProperty.Register(
            nameof(HeaderLabel),
            typeof(string),
            typeof(TechnicalDetailsPanel),
            new PropertyMetadata(string.Empty));

    public string HeaderLabel
    {
        get => (string)GetValue(HeaderLabelProperty);
        set => SetValue(HeaderLabelProperty, value);
    }

    public static readonly DependencyProperty BarCornerRadiusProperty =
        DependencyProperty.Register(
            nameof(BarCornerRadius),
            typeof(CornerRadius),
            typeof(TechnicalDetailsPanel),
            new PropertyMetadata(default(CornerRadius)));

    public CornerRadius BarCornerRadius
    {
        get => (CornerRadius)GetValue(BarCornerRadiusProperty);
        set => SetValue(BarCornerRadiusProperty, value);
    }

    public static readonly DependencyProperty ToggleCommandProperty =
        DependencyProperty.Register(
            nameof(ToggleCommand),
            typeof(ICommand),
            typeof(TechnicalDetailsPanel),
            new PropertyMetadata(null));

    public ICommand? ToggleCommand
    {
        get => (ICommand?)GetValue(ToggleCommandProperty);
        set => SetValue(ToggleCommandProperty, value);
    }

    // Forwarded to the OptionMatrixView inside: the regedit buttons live down there, but the panel is what the
    // three card templates bind to.
    public static readonly DependencyProperty RegeditCommandProperty =
        DependencyProperty.Register(
            nameof(RegeditCommand),
            typeof(ICommand),
            typeof(TechnicalDetailsPanel),
            new PropertyMetadata(null));

    public ICommand? RegeditCommand
    {
        get => (ICommand?)GetValue(RegeditCommandProperty);
        set => SetValue(RegeditCommandProperty, value);
    }
}
