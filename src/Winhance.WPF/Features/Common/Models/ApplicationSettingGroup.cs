using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Models
{
    /// <summary>
    /// Base class for application setting groups used in both Optimization and Customization features.
    /// </summary>
    public partial class ApplicationSettingGroup : ObservableObject
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
        
        /// <summary>
        /// Gets the collection of settings in this group.
        /// </summary>
        public ObservableCollection<ISettingItem> Settings { get; } = new();
    }
}
