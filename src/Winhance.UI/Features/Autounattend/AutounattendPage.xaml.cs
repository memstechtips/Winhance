using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Winhance.UI.Features.Autounattend.ViewModels;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Autounattend;

public sealed partial class AutounattendPage : SectionPage
{
    public AutounattendPageViewModel ViewModel { get; }

    protected override ISectionPageViewModel PageViewModel => ViewModel;

    public AutounattendPage()
    {
        ViewModel = App.Services.GetRequiredService<AutounattendPageViewModel>();

        this.InitializeComponent();

        InitializeSectionPage(Shell);

        SettingsView.SetBinding(AutounattendSettingsView.HasNoSearchResultsProperty, new Binding
        {
            Source = ViewModel,
            Path = new PropertyPath(nameof(ViewModel.HasNoSearchResults)),
            Mode = BindingMode.OneWay,
        });
    }
}
