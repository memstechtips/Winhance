using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Customize.ViewModels;
using Winhance.WPF.Features.Optimize.ViewModels;

namespace Winhance.WPF.Features.Common.Factories
{
    /// <summary>
    /// Factory for creating feature ViewModels from feature descriptors.
    /// Maps Core layer descriptors to WPF layer ViewModels.
    /// </summary>
    public class FeatureViewModelFactory : IFeatureViewModelFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogService _logService;

        public FeatureViewModelFactory(IServiceProvider serviceProvider, ILogService logService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Creates a new feature ViewModel instance from a feature descriptor.
        /// </summary>
        /// <param name="descriptor">The feature descriptor containing metadata about the feature.</param>
        /// <returns>A new feature ViewModel instance, or null if the factory cannot create a ViewModel for this descriptor.</returns>
        public async Task<IFeatureViewModel> CreateAsync(IFeatureDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            try
            {
                _logService.Log(LogLevel.Info, $"Creating ViewModel for feature: {descriptor.ModuleId}");

                var viewModel = descriptor.ModuleId switch
                {
                    // Optimization Features
                    "gaming-performance" => CreateGamingPerformanceViewModel(),
                    "privacy" => CreatePrivacyViewModel(),
                    "updates" => CreateUpdateViewModel(),
                    "power" => CreatePowerViewModel(),
                    "security" => CreateSecurityViewModel(),
                    "explorer-optimization" => CreateExplorerOptimizationViewModel(),
                    "notifications" => CreateNotificationViewModel(),
                    "sound" => CreateSoundViewModel(),

                    // Customization Features
                    "windows-theme" => CreateWindowsThemeViewModel(),
                    "start-menu" => CreateStartMenuViewModel(),
                    "taskbar" => CreateTaskbarViewModel(),
                    "explorer-customization" => CreateExplorerCustomizationViewModel(),

                    _ => null
                };

                if (viewModel != null)
                {
                    _logService.Log(LogLevel.Info, $"Successfully created ViewModel for feature: {descriptor.ModuleId}");
                }
                else
                {
                    _logService.Log(LogLevel.Warning, $"No ViewModel implementation found for feature: {descriptor.ModuleId}");
                }

                return viewModel;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error creating ViewModel for feature '{descriptor.ModuleId}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Determines if this factory can create a ViewModel for the given feature descriptor.
        /// </summary>
        /// <param name="descriptor">The feature descriptor to check.</param>
        /// <returns>True if the factory can create a ViewModel for this descriptor; otherwise, false.</returns>
        public bool CanCreate(IFeatureDescriptor descriptor)
        {
            if (descriptor == null)
                return false;

            return descriptor.ModuleId switch
            {
                "gaming-performance" or "privacy" or "updates" or "power" or "security" or 
                "explorer-optimization" or "notifications" or "sound" or
                "windows-theme" or "start-menu" or "taskbar" or "explorer-customization" => true,
                _ => false
            };
        }

        /// <summary>
        /// Gets the supported categories for this factory.
        /// </summary>
        /// <returns>An array of category names that this factory supports.</returns>
        public string[] GetSupportedCategories()
        {
            return new[] { "Optimization", "Customization" };
        }

        #region Private Factory Methods

        private IFeatureViewModel CreateGamingPerformanceViewModel()
        {
            return _serviceProvider.GetRequiredService<GamingandPerformanceOptimizationsViewModel>();
        }

        private IFeatureViewModel CreatePrivacyViewModel()
        {
            return _serviceProvider.GetRequiredService<PrivacyOptimizationsViewModel>();
        }

        private IFeatureViewModel CreateUpdateViewModel()
        {
            return _serviceProvider.GetRequiredService<UpdateOptimizationsViewModel>();
        }

        private IFeatureViewModel CreatePowerViewModel()
        {
            return _serviceProvider.GetRequiredService<PowerOptimizationsViewModel>();
        }

        private IFeatureViewModel CreateSecurityViewModel()
        {
            return _serviceProvider.GetRequiredService<WindowsSecurityOptimizationsViewModel>();
        }

        private IFeatureViewModel CreateExplorerOptimizationViewModel()
        {
            return _serviceProvider.GetRequiredService<ExplorerOptimizationsViewModel>();
        }

        private IFeatureViewModel CreateNotificationViewModel()
        {
            return _serviceProvider.GetRequiredService<NotificationOptimizationsViewModel>();
        }

        private IFeatureViewModel CreateSoundViewModel()
        {
            return _serviceProvider.GetRequiredService<SoundOptimizationsViewModel>();
        }

        private IFeatureViewModel CreateWindowsThemeViewModel()
        {
            return _serviceProvider.GetRequiredService<WindowsThemeCustomizationsViewModel>();
        }

        private IFeatureViewModel CreateStartMenuViewModel()
        {
            return _serviceProvider.GetRequiredService<StartMenuCustomizationsViewModel>();
        }

        private IFeatureViewModel CreateTaskbarViewModel()
        {
            return _serviceProvider.GetRequiredService<TaskbarCustomizationsViewModel>();
        }

        private IFeatureViewModel CreateExplorerCustomizationViewModel()
        {
            return _serviceProvider.GetRequiredService<ExplorerCustomizationsViewModel>();
        }

        #endregion
    }
}
