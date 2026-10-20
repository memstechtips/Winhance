using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.Infrastructure.Features.Optimize.Services;

namespace Winhance.Infrastructure.Tests.Services;

// Computing values and reading options uses none of the services' other dependencies, so those are bare mocks.
internal static class OptionProviderFixtures
{
    public static PowerService PowerService() =>
        new(Mock.Of<ILogService>(), Mock.Of<IPowerSettingsQueryService>(), Mock.Of<IPowerSchemeOperations>());

    public static WindowsThemeService ThemeService(ILocalizationService localization) =>
        new(
            Mock.Of<ILogService>(),
            Mock.Of<IWindowsRegistryService>(),
            Mock.Of<ISystemParametersService>(),
            Mock.Of<IFileSystemService>(),
            Mock.Of<IDesktopSlideshow>(),
            Mock.Of<IInteractiveUserService>(),
            Mock.Of<ISystemDetectionContextFactory>(),
            localization);

    public static OptionProviderRegistry Registry() =>
        new(() => new IOptionProvider[]
        {
            PowerService(),
            new TimeRegionLanguageService(),
            ThemeService(Mock.Of<ILocalizationService>()),
        });
}
