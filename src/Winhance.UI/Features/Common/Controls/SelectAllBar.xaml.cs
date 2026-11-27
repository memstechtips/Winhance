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

    private bool _stoppedTracking;

    public SelectAllBar()
    {
        InitializeComponent();
        // Source is a view model that outlives the bar, and its bindings would hold the bar and the view around it,
        // so a bar leaving the tree lets go of it.
        Unloaded += (_, _) =>
        {
            if (IsLoaded)
                return;
            Bindings.StopTracking();
            _stoppedTracking = true;
        };
        Loaded += (_, _) =>
        {
            if (!_stoppedTracking)
                return;
            _stoppedTracking = false;
            Bindings.Update();
        };
    }

    public ISelectAllSource? Source
    {
        get => (ISelectAllSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }
}
