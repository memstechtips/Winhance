using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.WPF.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Models
{
    public partial class QuickNavItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public string DisplayName { get; set; } = string.Empty;
        public Type ViewModelType { get; set; } = typeof(object);
        public UserControl? TargetView { get; set; }
        public ISettingsFeatureViewModel? ViewModel { get; set; }
        public int SortOrder { get; set; }
        
        public string IconTemplate => IsSelected ? "Selected" : "Normal";
    }
}