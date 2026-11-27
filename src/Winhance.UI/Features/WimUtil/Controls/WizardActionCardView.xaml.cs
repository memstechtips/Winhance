using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.WimUtil.Models;

namespace Winhance.UI.Features.WimUtil.Controls;

public sealed partial class WizardActionCardView : UserControl
{
    public static readonly DependencyProperty CardProperty = DependencyProperty.Register(
        nameof(Card),
        typeof(WizardActionCard),
        typeof(WizardActionCardView),
        new PropertyMetadata(null));

    public WizardActionCard? Card
    {
        get => (WizardActionCard?)GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(FluentIcons.Common.Icon),
        typeof(WizardActionCardView),
        new PropertyMetadata(FluentIcons.Common.Icon.Settings));

    public FluentIcons.Common.Icon Icon
    {
        get => (FluentIcons.Common.Icon)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    // The page squares the bottom corners while a banner is attached; Control.CornerRadius defaults to square, so the
    // card's own 4 is set here.
    public WizardActionCardView()
    {
        InitializeComponent();
        CornerRadius = new CornerRadius(4);
    }
}
