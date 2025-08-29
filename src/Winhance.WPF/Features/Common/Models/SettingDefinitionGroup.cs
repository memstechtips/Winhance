using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Models
{

    public partial class SettingDefinitionGroup : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isGroupHeader;

        [ObservableProperty]
        private string _groupName = string.Empty;

        [ObservableProperty]
        private bool _isVisible = true;

        public ObservableCollection<ISettingItem> Settings { get; } = new();
    }
}
