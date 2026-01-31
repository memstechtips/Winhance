using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml.Controls;

namespace Winhance.UI.Features.AdvancedTools;

/// <summary>
/// Page displaying advanced tools like WIM Utility.
/// </summary>
public sealed partial class AdvancedToolsPage : Page
{
    public AdvancedToolsPage()
    {
        this.InitializeComponent();
    }

    private void WimUtilCard_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // TODO: Navigate to WIM Utility wizard page when implemented
        // For now, show a message that it's coming soon
        Frame?.Navigate(typeof(WimUtilPage));
    }
}
