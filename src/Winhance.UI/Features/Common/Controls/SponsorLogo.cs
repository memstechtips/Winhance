using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Dialogs;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI.Features.Common.Controls;

// A sponsor's logo from the first source that loads; failing all of them, the sponsor's initial in the tier colour.
internal static class SponsorLogo
{
    // The CodeBuiltStyles key for a sponsor's tier: part is Card, Wash, Text or Icon.
    public static string TierStyle(SponsorEntry sponsor, string part) =>
        (SponsorRow.IsEmerald(sponsor) ? "Sponsor.Emerald" : "Sponsor.Gold") + part;

    public static string Initial(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "?" : name.TrimStart().Substring(0, 1).ToUpperInvariant();

    // The rounded tile the sponsor dialog uses (the store wall's logo shape) around BuildImage.
    public static FrameworkElement Build(ISponsorsService sponsorsService, SponsorEntry sponsor, double size)
    {
        double padding = size / 14;
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 4),
            Padding = new Thickness(padding),
            Style = CodeBuiltStyle.Named("CodeBuilt.LogoTile"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = BuildImage(sponsorsService, sponsor, size - 2 * (padding + 1))
        };
    }

    // Walks GetLogoSources in order. `settled` runs once, when a logo has opened or the initial has replaced it.
    public static FrameworkElement BuildImage(ISponsorsService sponsorsService, SponsorEntry sponsor, double size, Action? settled = null)
    {
        var image = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var initial = new TextBlock
        {
            Text = Initial(sponsor.Name),
            FontSize = size / 2.2,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Style = CodeBuiltStyle.Named(TierStyle(sponsor, "Text")),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };

        bool hasSettled = false;
        void Settle()
        {
            if (hasSettled)
                return;
            hasSettled = true;
            settled?.Invoke();
        }

        var sources = sponsorsService.GetLogoSources(sponsor);
        int next = 0;
        void TryNext()
        {
            if (next < sources.Count)
            {
                image.Source = new BitmapImage(new Uri(sources[next++]));
                return;
            }

            image.Visibility = Visibility.Collapsed;
            initial.Visibility = Visibility.Visible;
            Settle();
        }

        image.ImageOpened += (_, _) => Settle();
        image.ImageFailed += (_, _) => TryNext();
        TryNext();

        var layers = new Grid { Width = size, Height = size };
        layers.Children.Add(image);
        layers.Children.Add(initial);
        return layers;
    }
}
