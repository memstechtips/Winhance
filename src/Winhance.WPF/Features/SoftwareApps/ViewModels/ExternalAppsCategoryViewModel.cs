using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels;

public partial class ExternalAppsCategoryViewModel(string name, ObservableCollection<AppItemViewModel> apps) : ObservableObject
{
    [ObservableProperty]
    private string _name = name;

    [ObservableProperty]
    private ObservableCollection<AppItemViewModel> _apps = apps;

    [ObservableProperty]
    private bool _isExpanded = true;

    public int AppCount => Apps.Count;
}
