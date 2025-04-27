using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.ViewModels
{
    /// <summary>
    /// Base class for settings view models.
    /// </summary>
    /// <typeparam name="T">The type of settings.</typeparam>
    public abstract partial class BaseSettingsViewModel<T> : ObservableObject where T : class, ISettingItem
    {
        protected readonly ITaskProgressService _progressService;
        protected readonly IRegistryService _registryService;
        protected readonly ILogService _logService;

        /// <summary>
        /// Gets the collection of settings.
        /// </summary>
        public ObservableCollection<T> Settings { get; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether the settings are being loaded.
        /// </summary>
        [ObservableProperty]
        private bool _isLoading;

        /// <summary>
        /// Gets or sets a value indicating whether all settings are selected.
        /// </summary>
        [ObservableProperty]
        private bool _isSelected;

        /// <summary>
        /// Gets or sets a value indicating whether the view model has visible settings.
        /// </summary>
        [ObservableProperty]
        private bool _hasVisibleSettings = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseSettingsViewModel{T}"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        protected BaseSettingsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService)
        {
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Loads the settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task LoadSettingsAsync();

        /// <summary>
        /// Checks the status of all settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task CheckSettingStatusesAsync();

        /// <summary>
        /// Applies all selected settings.
        /// </summary>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task ApplySettingsAsync(IProgress<TaskProgressDetail> progress);

        /// <summary>
        /// Restores all selected settings to their default values.
        /// </summary>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task RestoreDefaultsAsync(IProgress<TaskProgressDetail> progress);
    }
}
