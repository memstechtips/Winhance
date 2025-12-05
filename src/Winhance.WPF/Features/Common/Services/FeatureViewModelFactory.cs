using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Services
{
    public static class FeatureViewModelFactory
    {
        public static async Task<UserControl> CreateFeatureAsync(
            FeatureMetadata feature,
            IServiceProvider serviceProvider,
            IViewPoolService viewPoolService = null
        )
        {

            if (feature == null || serviceProvider == null)
                return null;

            try
            {
                object viewModel = serviceProvider.GetRequiredService(feature.ViewModelType);

                UserControl view = null;

                if (viewPoolService != null)
                {
                    view = viewPoolService.GetOrCreateView(feature.ViewType, serviceProvider) as UserControl;
                }
                else
                {
                    view = serviceProvider.GetRequiredService(feature.ViewType) as UserControl;
                }

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
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
