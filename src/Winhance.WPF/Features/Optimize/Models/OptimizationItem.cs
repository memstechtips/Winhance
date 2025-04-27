using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.WPF.Features.Optimize.Models
{
    public partial class OptimizationItem : ObservableObject
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
        
        /// <summary>
        /// Gets or sets a value indicating whether the item is visible.
        /// </summary>
        [ObservableProperty]
        private bool _isVisible = true;
        
        public ObservableCollection<OptimizationSettingItem> Settings { get; } = new();
    }
}