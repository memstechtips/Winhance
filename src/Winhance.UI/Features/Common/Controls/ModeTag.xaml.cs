using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winhance.UI.Features.Common.Controls;

// A short badge naming where a change lands. Collapses itself when it has no text, so a bound card needs no
// visibility converter.
public sealed partial class ModeTag : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(ModeTag),
            new PropertyMetadata(string.Empty, OnTextChanged));

    public ModeTag()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var tag = (ModeTag)d;
        var text = (string?)e.NewValue ?? string.Empty;
        tag.Label.Text = text;
        tag.Pill.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
