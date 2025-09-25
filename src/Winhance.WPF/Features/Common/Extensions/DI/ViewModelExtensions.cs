using Microsoft.Extensions.DependencyInjection;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Customize.ViewModels;
using Winhance.WPF.Features.Optimize.ViewModels;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.Common.Extensions.DI
{
    public static class ViewModelExtensions
    {
        public static IServiceCollection AddViewModels(this IServiceCollection services)
        {
            return services
                .AddMainViewModels()
                .AddOptimizationViewModels()
                .AddCustomizationViewModels()
                .AddSoftwareAppViewModels();
        }

        public static IServiceCollection AddMainViewModels(this IServiceCollection services)
        {
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MoreMenuViewModel>();
            services.AddTransient<LoadingWindowViewModel>();
            services.AddSingleton<UpdateNotificationViewModel>();
            return services;
        }

        public static IServiceCollection AddOptimizationViewModels(this IServiceCollection services)
        {
            services.AddTransient<OptimizeViewModel>();
            services.AddSingleton<PowerOptimizationsViewModel>();
            services.AddSingleton<PrivacyOptimizationsViewModel>();
            services.AddSingleton<WindowsSecurityOptimizationsViewModel>();
            services.AddSingleton<GamingandPerformanceOptimizationsViewModel>();
            services.AddSingleton<NotificationOptimizationsViewModel>();
            services.AddSingleton<SoundOptimizationsViewModel>();
            services.AddSingleton<UpdateOptimizationsViewModel>();
            services.AddSingleton<ExplorerOptimizationsViewModel>();
            return services;
        }

        public static IServiceCollection AddCustomizationViewModels(this IServiceCollection services)
        {
            services.AddTransient<CustomizeViewModel>();
            services.AddSingleton<WindowsThemeCustomizationsViewModel>();
            services.AddSingleton<StartMenuCustomizationsViewModel>();
            services.AddSingleton<TaskbarCustomizationsViewModel>();
            services.AddSingleton<ExplorerCustomizationsViewModel>();
            return services;
        }

        public static IServiceCollection AddSoftwareAppViewModels(this IServiceCollection services)
        {
            services.AddSingleton<SoftwareAppsViewModel>();
            services.AddSingleton<WindowsAppsViewModel>();
            services.AddSingleton<ExternalAppsViewModel>();
            services.AddTransient<RemovalStatusContainerViewModel>();
            services.AddTransient<RemovalStatusViewModel>();
            services.AddTransient<ExternalAppsHelpViewModel>();
            services.AddTransient<WindowsAppsHelpContentViewModel>();
            return services;
        }

        public static IServiceCollection AddSpecializedViewModels(this IServiceCollection services)
        {
            return services;
        }
    }
}
