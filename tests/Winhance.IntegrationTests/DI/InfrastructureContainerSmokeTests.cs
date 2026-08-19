using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Interfaces;
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
    [InlineData(typeof(IHardwareDetectionService))]
    [InlineData(typeof(IPowerSettingsQueryService))]
    [InlineData(typeof(IComboBoxResolver))]
    [InlineData(typeof(ISettingApplicationService))]
    [InlineData(typeof(IConfigImportState))]
    [InlineData(typeof(IInitializationService))]
    [InlineData(typeof(IScheduledTaskService))]
    [InlineData(typeof(IScheduledTaskStateService))]
    [InlineData(typeof(IVersionService))]
    [InlineData(typeof(ISponsorsService))]
    [InlineData(typeof(IConfigurationApplicationBridgeService))]
    [InlineData(typeof(IConfigMigrationService))]
    [InlineData(typeof(IPolicyCleanupService))]
    [InlineData(typeof(IChangeHistoryService))]
    [InlineData(typeof(ISystemDetectionContextFactory))]
    [InlineData(typeof(ICatalogDetectionService))]
    [InlineData(typeof(IStateWriter))]
    [InlineData(typeof(IRegImportService))]
    [InlineData(typeof(ISpecialSettingHandlerRegistry))]
    [InlineData(typeof(IAutounattendScriptBuilder))]
    [InlineData(typeof(ISettingSnapshotSource))]
    [InlineData(typeof(IConfigFileWriter))]
    public void Resolve_CoreInfrastructureServices_AllNonNull(Type serviceType)
    {
        using var provider = BuildProvider();

        var service = provider.GetService(serviceType);

        service.Should().NotBeNull($"service {serviceType.Name} should be resolvable from the DI container");
    }

    [Fact]
    public void Resolve_TaskProgressService_SharedInstance()
    {
        using var provider = BuildProvider();

        var taskProgress = provider.GetService<ITaskProgressService>();
        var multiScript = provider.GetService<IMultiScriptProgressService>();

        taskProgress.Should().NotBeNull();
        multiScript.Should().NotBeNull();
        taskProgress.Should().BeSameAs(multiScript,
            "ITaskProgressService and IMultiScriptProgressService should resolve to the same TaskProgressService instance");
    }

    [Fact]
    public void Resolve_FactoryRegistrations_Succeed()
    {
        using var provider = BuildProvider();

        var taskProgress = provider.GetService<ITaskProgressService>();
        taskProgress.Should().NotBeNull("ITaskProgressService (factory registration) should resolve");
    }

    [Fact]
    public void Resolve_AllSingletons_ReturnSameInstance()
    {
        using var provider = BuildProvider();

        var log1 = provider.GetService<ILogService>();
        var log2 = provider.GetService<ILogService>();
        var registry1 = provider.GetService<IWindowsRegistryService>();
        var registry2 = provider.GetService<IWindowsRegistryService>();

        log1.Should().BeSameAs(log2, "singleton ILogService should return same instance");
        registry1.Should().BeSameAs(registry2, "singleton IWindowsRegistryService should return same instance");
    }

    [Fact]
    public void Container_BuildsSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddInfrastructureServices();

        // The one contract the host must supply: WindowsAppsService needs a human's answer mid-install, and
        // only the UI has one. Everything else must resolve from Infrastructure alone.
        services.AddSingleton(Mock.Of<IInstallConsent>());

        var action = () => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });

        action.Should().NotThrow("the DI container should build successfully");
    }
}
