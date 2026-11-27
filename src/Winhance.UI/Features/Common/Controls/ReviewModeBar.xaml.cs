using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

// Built on entering Config Review and dropped on leaving it, so the other modes carry none of its elements.
public sealed partial class ReviewModeBar : UserControl
{
    public ReviewModeBar(ReviewModeBarViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        // The view model outlives the bar, and its bindings would hold the bar once it is dropped.
        Unloaded += (_, _) => Bindings.StopTracking();
    }

    public ReviewModeBarViewModel ViewModel { get; }

    public void Fit(double available) =>
        ModeBarLayout.Fit(available, BarGrid, ReviewModeDescriptionText, ReviewModeStatusText, 3, new Thickness(0));

    private void ReviewModeApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ApplyReviewedConfigCommand.CanExecute(null))
            ViewModel.ApplyReviewedConfigCommand.Execute(null);
    }

    private void ReviewModeAcceptAllRemainingButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.AcceptAllRemainingCommand.CanExecute(null))
            ViewModel.AcceptAllRemainingCommand.Execute(null);
    }

    private void ReviewModeCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CancelReviewModeCommand.CanExecute(null))
            ViewModel.CancelReviewModeCommand.Execute(null);
    }
}
