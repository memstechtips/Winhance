using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Customize.Models;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// Base class for customization view models.
    /// </summary>
    public abstract class BaseCustomizationsViewModel : BaseSettingsViewModel<CustomizationSettingItem>
    {
        /// <summary>
        /// Gets the category name.
        /// </summary>
        public abstract string CategoryName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseCustomizationsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        protected BaseCustomizationsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService)
            : base(progressService, registryService, logService)
        {
        }
    }
}