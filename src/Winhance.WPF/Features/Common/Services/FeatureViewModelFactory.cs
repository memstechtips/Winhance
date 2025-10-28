using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Services;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Customize.ViewModels;
using Winhance.WPF.Features.Customize.Views;
using Winhance.WPF.Features.Optimize.ViewModels;
using Winhance.WPF.Features.Optimize.Views;
using Winhance.WPF.Features.SoftwareApps.ViewModels;
using Winhance.WPF.Features.SoftwareApps.Views;

namespace Winhance.WPF.Features.Common.Services
{
    public static class FeatureViewModelFactory
    {
        public static async Task<UserControl> CreateFeatureAsync(
            FeatureInfo feature,
            IServiceProvider serviceProvider
        )
        {
            if (feature == null || serviceProvider == null)
                return null;

            try
            {
                object viewModel = feature.ViewModelTypeName switch
                {
                    "WindowsThemeCustomizationsViewModel" => serviceProvider.GetRequiredService<WindowsThemeCustomizationsViewModel>(),
                    "TaskbarCustomizationsViewModel" => serviceProvider.GetRequiredService<TaskbarCustomizationsViewModel>(),
                    "StartMenuCustomizationsViewModel" => serviceProvider.GetRequiredService<StartMenuCustomizationsViewModel>(),
                    "ExplorerCustomizationsViewModel" => serviceProvider.GetRequiredService<ExplorerCustomizationsViewModel>(),
                    "PowerOptimizationsViewModel" => serviceProvider.GetRequiredService<PowerOptimizationsViewModel>(),
                    "PrivacyAndSecurityOptimizationsViewModel" => serviceProvider.GetRequiredService<PrivacyAndSecurityOptimizationsViewModel>(),
                    "GamingandPerformanceOptimizationsViewModel" => serviceProvider.GetRequiredService<GamingandPerformanceOptimizationsViewModel>(),
                    "NotificationOptimizationsViewModel" => serviceProvider.GetRequiredService<NotificationOptimizationsViewModel>(),
                    "SoundOptimizationsViewModel" => serviceProvider.GetRequiredService<SoundOptimizationsViewModel>(),
                    "UpdateOptimizationsViewModel" => serviceProvider.GetRequiredService<UpdateOptimizationsViewModel>(),
                    "WindowsAppsViewModel" => serviceProvider.GetRequiredService<WindowsAppsViewModel>(),
                    "ExternalAppsViewModel" => serviceProvider.GetRequiredService<ExternalAppsViewModel>(),
                    _ => null
                };

                UserControl view = feature.ViewModelTypeName switch
                {
                    "WindowsThemeCustomizationsViewModel" => serviceProvider.GetRequiredService<WindowsThemeCustomizationsView>(),
                    "TaskbarCustomizationsViewModel" => serviceProvider.GetRequiredService<TaskbarCustomizationsView>(),
                    "StartMenuCustomizationsViewModel" => serviceProvider.GetRequiredService<StartMenuCustomizationsView>(),
                    "ExplorerCustomizationsViewModel" => serviceProvider.GetRequiredService<ExplorerCustomizationsView>(),
                    "PowerOptimizationsViewModel" => serviceProvider.GetRequiredService<PowerOptimizationsView>(),
                    "PrivacyAndSecurityOptimizationsViewModel" => serviceProvider.GetRequiredService<PrivacyAndSecurityOptimizationsView>(),
                    "GamingandPerformanceOptimizationsViewModel" => serviceProvider.GetRequiredService<GamingandPerformanceOptimizationsView>(),
                    "NotificationOptimizationsViewModel" => serviceProvider.GetRequiredService<NotificationOptimizationsView>(),
                    "SoundOptimizationsViewModel" => serviceProvider.GetRequiredService<SoundOptimizationsView>(),
                    "UpdateOptimizationsViewModel" => serviceProvider.GetRequiredService<UpdateOptimizationsView>(),
                    "WindowsAppsViewModel" => serviceProvider.GetRequiredService<WindowsAppsView>(),
                    "ExternalAppsViewModel" => serviceProvider.GetRequiredService<ExternalAppsView>(),
                    _ => null
                };

                if (viewModel == null || view == null)
                    return null;

                if (viewModel is ISettingsFeatureViewModel settingsVm)
                {
                    await settingsVm.LoadSettingsAsync();
                }
                else if (viewModel is IAppFeatureViewModel appVm)
                {
                    await appVm.LoadItemsAsync();
                }

                view.DataContext = viewModel;
                return view;
            }
            catch
            {
                return null;
            }
        }
    }
}
