using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Optimize.Models
{
    public partial class OptimizationAction : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;
        
        [ObservableProperty]
        private string _name = string.Empty;
        
        [ObservableProperty]
        private string _description = string.Empty;
        
        [ObservableProperty]
        private string _groupName = string.Empty;
        
        [ObservableProperty]
        private string _confirmationMessage = string.Empty;
        
        // The registry setting or other action to perform
        public RegistrySetting? RegistrySetting { get; set; }
        
        // Optional: Additional actions to perform
        public Func<Task<bool>>? CustomAction { get; set; }
        
        // Optional: Backup functionality
        public bool SupportsBackup { get; set; }
        public Func<Task<bool>>? BackupAction { get; set; }
    }
}