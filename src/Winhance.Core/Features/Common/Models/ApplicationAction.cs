using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Base class for application actions that can be performed in both Optimization and Customization features.
    /// </summary>
    public partial class ApplicationAction : ObservableObject
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
        
        [ObservableProperty]
        private string _actionType = string.Empty;
        
        [ObservableProperty]
        private string _command = string.Empty;
        
        [ObservableProperty]
        private string _commandAction = string.Empty;
        
        /// <summary>
        /// Gets or sets the registry setting or other action to perform.
        /// </summary>
        public RegistrySetting? RegistrySetting { get; set; }
        
        /// <summary>
        /// Gets or sets optional additional actions to perform.
        /// </summary>
        public Func<Task<bool>>? CustomAction { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether this action supports backup.
        /// </summary>
        public bool SupportsBackup { get; set; }
        
        /// <summary>
        /// Gets or sets optional backup action to perform.
        /// </summary>
        public Func<Task<bool>>? BackupAction { get; set; }
        
        /// <summary>
        /// Gets or sets the parameters for this action.
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }
}
