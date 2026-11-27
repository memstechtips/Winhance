using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

// Built on entering Builder and dropped on leaving it, so the other modes carry none of its elements.
public sealed partial class BuilderModeBar : UserControl
{
    private readonly ILogService _logService;

    public BuilderModeBar(BuilderModeBarViewModel viewModel, ILogService logService)
    {
        ViewModel = viewModel;
        _logService = logService;
        InitializeComponent();
        // The view model outlives the bar, and its bindings would hold the bar once it is dropped.
        Unloaded += (_, _) => Bindings.StopTracking();
    }

    public BuilderModeBarViewModel ViewModel { get; }

    public void Fit(double available) =>
        ModeBarLayout.Fit(available, BarGrid, BuilderModeDescriptionText, BuilderTargetPanel, 4, new Thickness(0, 0, 12, 0));

    private void BuilderSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SaveCommand.CanExecute(null))
            ViewModel.SaveCommand.Execute(null);
    }

    private void BuilderCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CancelCommand.CanExecute(null))
            ViewModel.CancelCommand.Execute(null);
    }

    private void BuilderConfigRadio_Checked(object sender, RoutedEventArgs e) => ViewModel.SelectConfigTarget();

    private void BuilderAutounattendRadio_Checked(object sender, RoutedEventArgs e) => ViewModel.SelectAutounattendTarget();

    private void BuilderOtherHardwareCheck_Changed(object sender, RoutedEventArgs e) =>
        ViewModel.SetShowOtherHardwareAsync(BuilderOtherHardwareCheck.IsChecked == true).FireAndForget(_logService);

    private void BuilderTargetWindowsFlyout_Opening(object sender, object e)
    {
        if (sender is not MenuFlyout flyout)
            return;

        flyout.Items.Clear();
        foreach (var release in ViewModel.TargetReleases)
            flyout.Items.Add(TargetWindowsItem(release.Name, release.Build));
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(TargetWindowsItem(ViewModel.AllVersionsText, null));
    }

    private RadioMenuFlyoutItem TargetWindowsItem(string text, int? build)
    {
        var item = new RadioMenuFlyoutItem { Text = text, GroupName = "BuilderTargetWindows", IsChecked = ViewModel.TargetBuild == build };
        item.Click += (_, _) => ViewModel.SetTargetWindowsAsync(build).FireAndForget(_logService);
        return item;
    }
}
