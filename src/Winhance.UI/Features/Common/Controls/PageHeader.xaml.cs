using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class PageHeader : UserControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(FluentIcons.Common.Icon),
        typeof(PageHeader),
        new PropertyMetadata(FluentIcons.Common.Icon.Settings));

    public FluentIcons.Common.Icon Icon
    {
        get => (FluentIcons.Common.Icon)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(PageHeader),
        new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(PageHeader),
        new PropertyMetadata(string.Empty));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    // Right-aligned, bottom-aligned slot for the page's search box.
    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions),
        typeof(object),
        typeof(PageHeader),
        new PropertyMetadata(null));

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public PageHeader()
    {
        InitializeComponent();
    }
}
