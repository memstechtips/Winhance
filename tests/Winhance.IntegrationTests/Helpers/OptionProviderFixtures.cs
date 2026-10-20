using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.Infrastructure.Features.Optimize.Services;

namespace Winhance.IntegrationTests.Helpers;

// Turning a key into the values a script writes needs none of the services' dependencies, so those are bare mocks.
internal static class OptionProviderFixtures
{
    public static OptionProviderRegistry Registry() =>
        new(() => new IOptionProvider[]
        {
            new PowerService(Mock.Of<ILogService>(), Mock.Of<IPowerSettingsQueryService>(), Mock.Of<IPowerSchemeOperations>()),
            new TimeRegionLanguageService(),
            new WindowsThemeService(
                Mock.Of<ILogService>(),
                Mock.Of<IWindowsRegistryService>(),
                Mock.Of<ISystemParametersService>(),
                Mock.Of<IFileSystemService>(),
                Mock.Of<IDesktopSlideshow>(),
                Mock.Of<IInteractiveUserService>(),
                Mock.Of<ISystemDetectionContextFactory>(),
                Mock.Of<ILocalizationService>()),
        });
}
