using Microsoft.Extensions.DependencyInjection;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.Customize.Views;
using Winhance.WPF.Features.Optimize.Views;
using Winhance.WPF.Features.SoftwareApps.Views;

namespace Winhance.WPF.Features.Common.Extensions.DI
{
    /// <summary>
    /// Extension methods for registering Views.
    /// Views are registered as Transient since they should be created fresh
    /// when needed and disposed properly.
    /// </summary>
    public static class ViewExtensions
    {
        /// <summary>
        /// Registers all Views for the Winhance application.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddViews(this IServiceCollection services)
        {
            return services
                .AddMainViews()
                .AddOptimizationViews()
                .AddCustomizationViews()
                .AddSoftwareAppViews()
                .AddDialogViews();
        }

        /// <summary>
        /// Registers main application Views.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddMainViews(this IServiceCollection services)
        {
            // Main Window (Transient - Should be created fresh)
            services.AddTransient<MainWindow>();

            // Loading Window (Transient - Created per startup)
            services.AddTransient<LoadingWindow>();

            return services;
        }

        /// <summary>
        /// Registers optimization feature Views.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddOptimizationViews(this IServiceCollection services)
        {
            // Main Optimization View (Transient)
            services.AddTransient<OptimizeView>();

            // Feature-specific Views (Transient)
            services.AddTransient<PowerOptimizationsView>();
            services.AddTransient<PrivacyOptimizationsView>();
            services.AddTransient<WindowsSecurityOptimizationsView>();
            services.AddTransient<GamingandPerformanceOptimizationsView>();
            services.AddTransient<NotificationOptimizationsView>();
            services.AddTransient<SoundOptimizationsView>();
            services.AddTransient<UpdateOptimizationsView>();
            services.AddTransient<ExplorerOptimizationsView>();

            return services;
        }

        /// <summary>
        /// Registers customization feature Views.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddCustomizationViews(this IServiceCollection services)
        {
            // Main Customization View (Transient)
            services.AddTransient<CustomizeView>();

            // Feature-specific Views (Transient)
            services.AddTransient<WindowsThemeCustomizationsView>();
            services.AddTransient<StartMenuCustomizationsView>();
            services.AddTransient<TaskbarCustomizationsView>();
            services.AddTransient<ExplorerCustomizationsView>();

            return services;
        }

        /// <summary>
        /// Registers software applications Views.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddSoftwareAppViews(this IServiceCollection services)
        {
            // Main Software Apps Views (Transient)
            services.AddTransient<SoftwareAppsView>();
            services.AddTransient<WindowsAppsView>();
            services.AddTransient<ExternalAppsView>();

            // Table Views (Transient)
            services.AddTransient<WindowsAppsTableView>();
            services.AddTransient<ExternalAppsTableView>();

            // Help Content Views (Transient)
            services.AddTransient<WindowsAppsHelpContent>();
            services.AddTransient<ExternalAppsHelpContent>();

            return services;
        }

        /// <summary>
        /// Registers dialog and modal Views.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddDialogViews(this IServiceCollection services)
        {
            // Dialog Views (Transient - Created per dialog)
            services.AddTransient<CustomDialog>();
            services.AddTransient<ModalDialog>();
            services.AddTransient<UnifiedConfigurationDialog>();
            services.AddTransient<UpdateDialog>();
            services.AddTransient<UpdateNotificationDialog>();
            services.AddTransient<ConfigImportOptionsDialog>();
            services.AddTransient<DonationDialog>();

            return services;
        }
    }
}
