using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winhance.UI.Features.Common.Controls;

// Built only when the offline snapshot has gold or emerald sponsors to show on the splash.
public sealed partial class SplashSponsors : UserControl
{
    public SplashSponsors(string heading, IEnumerable<UIElement> pills)
    {
        InitializeComponent();
        SponsorsHeading.Text = heading;
        foreach (var pill in pills)
            SponsorPills.Children.Add(pill);
    }
}
