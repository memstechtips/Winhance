using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class SelectAllBar : UserControl
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(
            nameof(Source),
            typeof(ISelectAllSource),
            typeof(SelectAllBar),
            new PropertyMetadata(null));

    public SelectAllBar()
    {
        InitializeComponent();
    }

    public ISelectAllSource? Source
    {
        get => (ISelectAllSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }
}
