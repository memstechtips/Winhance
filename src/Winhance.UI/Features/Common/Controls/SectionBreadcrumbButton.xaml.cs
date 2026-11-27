using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Controls;

// Its own control so the shell can x:Load it: a DropDownButton declared inline there failed to come back after
// its second unload. A DependencyProperty so the x:Bind inside follows the assignment.
public sealed partial class SectionBreadcrumbButton : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ISectionPageViewModel),
        typeof(SectionBreadcrumbButton),
        new PropertyMetadata(null));

    public ISectionPageViewModel? ViewModel
    {
        get => (ISectionPageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Sender is the button; its Tag is the section key.
    public event RoutedEventHandler? ItemClicked;

    public SectionBreadcrumbButton() => InitializeComponent();

    private void SectionFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        SectionButton.Flyout?.Hide();
        ItemClicked?.Invoke(sender, e);
    }
}
