using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.Customize.ViewModels;

namespace Winhance.WPF.Features.Common.Services.Configuration
{
    /// <summary>
    /// Service for applying configuration to the Customize section.
    /// </summary>
    public class CustomizeConfigurationApplier : ISectionConfigurationApplier
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogService _logService;
        private readonly IViewModelRefresher _viewModelRefresher;
        private readonly IConfigurationPropertyUpdater _propertyUpdater;
        private readonly IDomainService _windowsThemeService;
        private readonly IDialogService _dialogService;

        /// <summary>
        /// Gets the section name that this applier handles.
        /// </summary>
        public string SectionName => "Customize";

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomizeConfigurationApplier"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="viewModelRefresher">The view model refresher.</param>
        /// <param name="propertyUpdater">The property updater.</param>
        /// <param name="windowsThemeService">The Windows theme service.</param>
        /// <param name="dialogService">The dialog service.</param>
        public CustomizeConfigurationApplier(
            IServiceProvider serviceProvider,
            ILogService logService,
            IViewModelRefresher viewModelRefresher,
            IConfigurationPropertyUpdater propertyUpdater,
            IDomainService windowsThemeService,
            IDialogService dialogService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _viewModelRefresher = viewModelRefresher ?? throw new ArgumentNullException(nameof(viewModelRefresher));
            _propertyUpdater = propertyUpdater ?? throw new ArgumentNullException(nameof(propertyUpdater));
            _windowsThemeService = windowsThemeService ?? throw new ArgumentNullException(nameof(windowsThemeService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        /// <summary>
        /// Applies the configuration to the Customize section.
        /// </summary>
        /// <param name="configFile">The configuration file to apply.</param>
        /// <returns>True if any items were updated, false otherwise.</returns>
        public async Task<bool> ApplyConfigAsync(ConfigurationFile configFile)
        {
            try
            {
                _logService.Log(LogLevel.Info, "Applying configuration to CustomizeViewModel");

                var viewModel = _serviceProvider.GetService<CustomizeViewModel>();
                if (viewModel == null)
                {
                    _logService.Log(LogLevel.Warning, "CustomizeViewModel not available");
                    return false;
                }

                // Skip handling for CustomizeViewModel as it uses composition pattern 
                // which is incompatible with the legacy configuration system
                _logService.Log(LogLevel.Info, "CustomizeViewModel uses composition pattern which is incompatible with the legacy configuration system");

                // Return early as we can't apply configuration to the composition-based ViewModel
                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying Customize configuration: {ex.Message}");
                return false;
            }
        }

    }
}