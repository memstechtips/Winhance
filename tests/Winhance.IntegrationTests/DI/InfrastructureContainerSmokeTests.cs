using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Extensions.DI;
using Xunit;

namespace Winhance.IntegrationTests.DI;

[Trait("Category", "Integration")]
public class InfrastructureContainerSmokeTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddInfrastructureServices();
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(typeof(ILogService))]
    [InlineData(typeof(IWindowsRegistryService))]
    [InlineData(typeof(IFileSystemService))]
    [InlineData(typeof(IWindowsVersionService))]
    [InlineData(typeof(IEventBus))]
    [InlineData(typeof(ILocalizationService))]
    [InlineData(typeof(IInteractiveUserService))]
    [InlineData(typeof(IProcessExecutor))]
    [InlineData(typeof(IUserPreferencesService))]
    [InlineData(typeof(ICompatibleSettingsRegistry))]
    [InlineData(typeof(IWindowsCompatibilityFilter))]
    [InlineData(typeof(IHardwareCompatibilityFilter))]
    [InlineData(typeof(IHardwareDetectionService))]
    [InlineData(typeof(IPowerSettingsQueryService))]
    [InlineData(typeof(IPowerSettingsValidationService))]
    [InlineData(typeof(IComboBoxResolver))]
    [InlineData(typeof(IComboBoxSetupService))]
    [InlineData(typeof(IDomainServiceRouter))]
    [InlineData(typeof(ISystemSettingsDiscoveryService))]
    [InlineData(typeof(ISettingApplicationService))]
    [InlineData(typeof(IConfigImportState))]
    [InlineData(typeof(IDependencyManager))]
    [InlineData(typeof(IInitializationService))]
    [InlineData(typeof(IGlobalSettingsRegistry))]
    [InlineData(typeof(IGlobalSettingsPreloader))]
    [InlineData(typeof(IScheduledTaskService))]
    [InlineData(typeof(IVersionService))]
    [InlineData(typeof(ITooltipDataService))]
    [InlineData(typeof(IConfigurationApplicationBridgeService))]
    [InlineData(typeof(IConfigMigrationService))]
    public void Resolve_CoreInfrastructureServices_AllNonNull(Type serviceType)
    {
        // Arrange
        using var provider = BuildProvider();

        // Act
        var service = provider.GetService(serviceType);

        // Assert
        service.Should().NotBeNull($"service {serviceType.Name} should be resolvable from the DI container");
    }

    [Fact]
    public void Resolve_TaskProgressService_SharedInstance()
    {
        // Arrange
        using var provider = BuildProvider();

        // Act
        var taskProgress = provider.GetService<ITaskProgressService>();
        var multiScript = provider.GetService<IMultiScriptProgressService>();

        // Assert
        taskProgress.Should().NotBeNull();
        multiScript.Should().NotBeNull();
        taskProgress.Should().BeSameAs(multiScript,
            "ITaskProgressService and IMultiScriptProgressService should resolve to the same TaskProgressService instance");
    }

    [Fact]
    public void Resolve_FactoryRegistrations_Succeed()
    {
        // Arrange
        using var provider = BuildProvider();

        // Act & Assert — these are registered via factory lambdas
        var connectivity = provider.GetService<IInternetConnectivityService>();
        connectivity.Should().NotBeNull("IInternetConnectivityService (factory registration) should resolve");

        var recommended = provider.GetService<IRecommendedSettingsService>();
        recommended.Should().NotBeNull("IRecommendedSettingsService (factory registration) should resolve");
    }

    [Fact]
    public void Resolve_AllSingletons_ReturnSameInstance()
    {
        // Arrange
        using var provider = BuildProvider();

        // Act
        var log1 = provider.GetService<ILogService>();
        var log2 = provider.GetService<ILogService>();
        var registry1 = provider.GetService<IWindowsRegistryService>();
        var registry2 = provider.GetService<IWindowsRegistryService>();

        // Assert
        log1.Should().BeSameAs(log2, "singleton ILogService should return same instance");
        registry1.Should().BeSameAs(registry2, "singleton IWindowsRegistryService should return same instance");
    }

    [Fact]
    public void Container_BuildsSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructureServices();

        // Act & Assert — building the provider should succeed
        // Note: ValidateOnBuild is not used here because some services have
        // cross-layer dependencies (e.g., DomainServiceRouter needs domain services
        // registered by the UI layer). We verify individual resolution instead.
        var action = () => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
        });

        action.Should().NotThrow("the DI container should build successfully");
    }
}
