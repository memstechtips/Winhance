using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.WPF.Features.SoftwareApps.Models;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels;

public partial class ExternalAppsCategoryViewModel(string name, ObservableCollection<AppItemViewModel> apps, string? displayName = null) : ObservableObject
{
    [ObservableProperty]
    private string _name = name;

    [ObservableProperty]
    private string _displayName = displayName ?? name;

    [ObservableProperty]
    private string _icon = ExternalAppCategoryIcons.GetIcon(name);

    [ObservableProperty]
    private ObservableCollection<AppItemViewModel> _apps = apps;

    [ObservableProperty]
    private bool _isExpanded = true;

    public int AppCount => Apps.Count;
}
