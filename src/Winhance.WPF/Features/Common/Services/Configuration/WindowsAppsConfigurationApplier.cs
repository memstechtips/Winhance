using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.Common.Services.Configuration
{
    /// <summary>
    /// Service for applying configuration to the WindowsApps section.
    /// </summary>
    public class WindowsAppsConfigurationApplier : ISectionConfigurationApplier
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogService _logService;
        private readonly IViewModelRefresher _viewModelRefresher;
        private readonly IConfigurationPropertyUpdater _propertyUpdater;

        /// <summary>
        /// Gets the section name that this applier handles.
        /// </summary>
        public string SectionName => "WindowsApps";

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsAppsConfigurationApplier"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="viewModelRefresher">The view model refresher.</param>
        /// <param name="propertyUpdater">The property updater.</param>
        public WindowsAppsConfigurationApplier(
            IServiceProvider serviceProvider,
            ILogService logService,
            IViewModelRefresher viewModelRefresher,
            IConfigurationPropertyUpdater propertyUpdater)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _viewModelRefresher = viewModelRefresher ?? throw new ArgumentNullException(nameof(viewModelRefresher));
            _propertyUpdater = propertyUpdater ?? throw new ArgumentNullException(nameof(propertyUpdater));
        }

        /// <summary>
        /// Applies the configuration to the WindowsApps section.
        /// </summary>
        /// <param name="configFile">The configuration file to apply.</param>
        /// <returns>True if any items were updated, false otherwise.</returns>
        public async Task<bool> ApplyConfigAsync(ConfigurationFile configFile)
        {
            try
            {
                _logService.Log(LogLevel.Info, "Applying configuration to WindowsAppsViewModel");

                var viewModel = _serviceProvider.GetService<WindowsAppsViewModel>();
                if (viewModel == null)
                {
                    _logService.Log(LogLevel.Warning, "WindowsAppsViewModel not available");
                    return false;
                }

                // Ensure the view model is initialized
                if (!viewModel.IsInitialized)
                {
                    _logService.Log(LogLevel.Info, "WindowsAppsViewModel not initialized, initializing now");
                    await viewModel.LoadItemsAsync();
                }

                // Apply the configuration directly to the view model's items
                int updatedCount = await _propertyUpdater.UpdateItemsAsync(viewModel.Items, configFile);

                _logService.Log(LogLevel.Info, $"Updated {updatedCount} items in WindowsAppsViewModel");

                // Refresh the UI
                await _viewModelRefresher.RefreshViewModelAsync(viewModel);

                return updatedCount > 0;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying WindowsApps configuration: {ex.Message}");
                return false;
            }
        }
    }
}