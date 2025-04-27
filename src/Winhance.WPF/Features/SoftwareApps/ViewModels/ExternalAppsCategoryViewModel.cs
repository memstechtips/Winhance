using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.WPF.Features.SoftwareApps.Models;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    public partial class ExternalAppsCategoryViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private ObservableCollection<ExternalApp> _apps = new();

        [ObservableProperty]
        private bool _isExpanded = true;

        public ExternalAppsCategoryViewModel(string name, ObservableCollection<ExternalApp> apps)
        {
            _name = name;
            _apps = apps;
        }

        public int AppCount => Apps.Count;
    }
}
