using Microsoft.Extensions.DependencyInjection;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Customize.ViewModels;
using Winhance.WPF.Features.Optimize.ViewModels;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.Common.Extensions.DI
{
    /// <summary>
    /// Extension methods for registering ViewModels.
    /// ViewModels are registered as Transient to ensure clean state
    /// and avoid data pollution between instances.
    /// </summary>
    public static class ViewModelExtensions
    {
        /// <summary>
        /// Registers all ViewModels for the Winhance application.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddViewModels(this IServiceCollection services)
        {
            return services
                .AddMainViewModels()
                .AddOptimizationViewModels()
                .AddCustomizationViewModels()
                .AddSoftwareAppViewModels();
        }

        /// <summary>
        /// Registers main application ViewModels.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddMainViewModels(this IServiceCollection services)
        {
            // Main ViewModel (Singleton - Application root)
            services.AddSingleton<MainViewModel>();

            // Loading Window ViewModel (Transient - Created per startup)
            services.AddTransient<LoadingWindowViewModel>();

            // Update Notification ViewModel (Singleton - Application-wide notifications)
            services.AddSingleton<UpdateNotificationViewModel>();

            return services;
        }

        /// <summary>
        /// Registers optimization feature ViewModels.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddOptimizationViewModels(this IServiceCollection services)
        {
            // Main Optimization ViewModel (Transient - Clean state per navigation)
            services.AddTransient<OptimizeViewModel>();

            // Feature-specific ViewModels (Transient - Clean state per use)
            services.AddTransient<PowerOptimizationsViewModel>();
            services.AddTransient<PrivacyOptimizationsViewModel>();
            services.AddTransient<WindowsSecurityOptimizationsViewModel>();
            services.AddTransient<GamingandPerformanceOptimizationsViewModel>();
            services.AddTransient<NotificationOptimizationsViewModel>();
            services.AddTransient<SoundOptimizationsViewModel>();
            services.AddTransient<UpdateOptimizationsViewModel>();
            services.AddTransient<ExplorerOptimizationsViewModel>();

            return services;
        }

        /// <summary>
        /// Registers customization feature ViewModels.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddCustomizationViewModels(
            this IServiceCollection services
        )
        {
            // Main Customization ViewModel (Transient - Clean state per navigation)
            services.AddTransient<CustomizeViewModel>();

            // Feature-specific ViewModels (Transient - Clean state per use)
            services.AddTransient<WindowsThemeCustomizationsViewModel>();
            services.AddTransient<StartMenuCustomizationsViewModel>();
            services.AddTransient<TaskbarCustomizationsViewModel>();
            services.AddTransient<ExplorerCustomizationsViewModel>();

            return services;
        }

        /// <summary>
        /// Registers software applications ViewModels.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddSoftwareAppViewModels(this IServiceCollection services)
        {
            // Main Software Apps ViewModel (Singleton - Expensive to initialize)
            services.AddSingleton<SoftwareAppsViewModel>();

            // Feature-specific ViewModels (Singleton - Heavy data loading)
            services.AddSingleton<WindowsAppsViewModel>();
            services.AddSingleton<ExternalAppsViewModel>();

            // Helper ViewModels (Transient - Lightweight)
            services.AddTransient<RemovalStatusContainerViewModel>();
            services.AddTransient<RemovalStatusViewModel>();
            services.AddTransient<ExternalAppsHelpViewModel>();
            services.AddTransient<WindowsAppsHelpContentViewModel>();

            return services;
        }

        /// <summary>
        /// Registers ViewModels that require special configuration or complex dependencies.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddSpecializedViewModels(this IServiceCollection services)
        {
            // ViewModels with complex initialization can be registered with factories
            // if constructor injection becomes too complex

            return services;
        }
    }
}
