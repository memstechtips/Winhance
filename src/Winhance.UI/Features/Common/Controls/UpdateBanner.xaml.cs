using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

// Built only while an update message shows, and dropped when it closes.
public sealed partial class UpdateBanner : UserControl
{
    private readonly IDispatcherService _dispatcherService = App.Services.GetRequiredService<IDispatcherService>();

    public UpdateBanner(UpdateCheckViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        UpdateActionButton();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        // The view model outlives the banner, and its event and bindings would hold the banner once it is dropped.
        Unloaded += (_, _) =>
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            Bindings.StopTracking();
        };
    }

    public UpdateCheckViewModel ViewModel { get; }

    // InfoBar.ActionButton takes a ButtonBase instance, so the button cannot be swapped by a binding.
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UpdateCheckViewModel.IsUpdateActionButtonVisible)
            or nameof(UpdateCheckViewModel.InstallNowButtonText)
            or nameof(UpdateCheckViewModel.IsRelaunchButtonVisible)
            or nameof(UpdateCheckViewModel.RelaunchButtonText))
        {
            _dispatcherService.RunOnUIThread(UpdateActionButton);
        }
    }

    private void UpdateActionButton()
    {
        UpdateInfoBar.ActionButton = ViewModel switch
        {
            { IsRelaunchButtonVisible: true } => new Button
            {
                Content = ViewModel.RelaunchButtonText,
                Command = ViewModel.RelaunchCommand,
                Style = CodeBuiltStyle.Named("AccentButtonStyle"),
            },
            { IsUpdateActionButtonVisible: true } => new Button
            {
                Content = ViewModel.InstallNowButtonText,
                Command = ViewModel.InstallUpdateCommand,
                Style = CodeBuiltStyle.Named("AccentButtonStyle"),
            },
            _ => null,
        };
    }

    private void UpdateInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args) => ViewModel.DismissUpdateInfoBar();
}
