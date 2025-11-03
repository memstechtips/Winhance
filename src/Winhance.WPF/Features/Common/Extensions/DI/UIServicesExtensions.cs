using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.UI.Interfaces;
using Winhance.Infrastructure.Features.UI.Services;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Common.Services;

namespace Winhance.WPF.Features.Common.Extensions.DI
{
    public static class UIServicesExtensions
    {
        public static IServiceCollection AddUIServices(this IServiceCollection services)
        {
            return services
                .AddUIInfrastructureServices()
                .AddUICoordinationServices()
                .AddDialogServices()
                .CompleteSystemServicesRegistration();
        }

        public static IServiceCollection AddUIInfrastructureServices(this IServiceCollection services)
        {
            services.AddSingleton<IThemeManager>(provider => new ThemeManager(
                provider.GetRequiredService<INavigationService>(),
                provider.GetRequiredService<IWindowsThemeQueryService>()
            ));

            services.AddSingleton<IApplicationCloseService, ApplicationCloseService>();

            services.AddSingleton<WindowInitializationService>();
            services.AddSingleton<IWindowManagementService, WindowManagementService>();
            services.AddSingleton<IFlyoutManagementService, FlyoutManagementService>();

            services.AddSingleton<IWinhanceNotificationService, WinhanceNotificationService>();

            services.AddSingleton<IStartupNotificationService, StartupNotificationService>();

            services.AddSingleton<UserPreferencesService>(provider => new UserPreferencesService(
                provider.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IUserPreferencesService>(provider =>
                provider.GetRequiredService<UserPreferencesService>()
            );


            return services;
        }

        public static IServiceCollection AddUICoordinationServices(this IServiceCollection services)
        {
            services.AddTransient<ISettingsConfirmationService, SettingsConfirmationService>();

            services.AddSingleton<IConfigurationService, ConfigurationService>();

            services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IAutounattendXmlGeneratorService,
                Winhance.WPF.Features.AdvancedTools.Services.AutounattendXmlGeneratorService>();

            services.AddSingleton<Infrastructure.Features.Common.EventHandlers.TooltipRefreshEventHandler>();

            services.AddScoped<ISettingApplicationService>(sp =>
                new Infrastructure.Features.Common.Services.SettingApplicationService(
                    sp.GetRequiredService<IDomainServiceRouter>(),
                    sp.GetRequiredService<IWindowsRegistryService>(),
                    sp.GetRequiredService<IComboBoxResolver>(),
                    sp.GetRequiredService<ICommandService>(),
                    sp.GetRequiredService<ILogService>(),
                    sp.GetRequiredService<IDependencyManager>(),
                    sp.GetRequiredService<IGlobalSettingsRegistry>(),
                    sp.GetRequiredService<IEventBus>(),
                    sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                    sp.GetRequiredService<IRecommendedSettingsService>(),
                    sp.GetRequiredService<IWindowsUIManagementService>(),
                    sp.GetRequiredService<IPowerCfgQueryService>(),
                    sp.GetRequiredService<IHardwareDetectionService>(),
                    sp.GetRequiredService<IPowerShellExecutionService>(),
                    sp.GetRequiredService<IWindowsCompatibilityFilter>()

                ));

            return services;
        }

        public static IServiceCollection AddDialogServices(this IServiceCollection services)
        {
            services.AddTransient<IDialogService, DialogService>();
            services.AddTransient<ISettingsConfirmationService, SettingsConfirmationService>();

            return services;
        }

        public static IServiceCollection AddNavigationServices(this IServiceCollection services)
        {
            return services;
        }
    }
}
