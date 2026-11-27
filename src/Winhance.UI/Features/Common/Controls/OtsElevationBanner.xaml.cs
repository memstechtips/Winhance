using Microsoft.UI.Xaml.Controls;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

// Built only while the banner is open; most runs never elevate with another account's credentials.
public sealed partial class OtsElevationBanner : UserControl
{
    public OtsElevationBanner(MainWindowViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        // The view model outlives the banner, and its bindings would hold the banner once it is dropped.
        Unloaded += (_, _) => Bindings.StopTracking();
    }

    public MainWindowViewModel ViewModel { get; }

    private void OtsElevationInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args) => ViewModel.DismissOtsInfoBar();
}
