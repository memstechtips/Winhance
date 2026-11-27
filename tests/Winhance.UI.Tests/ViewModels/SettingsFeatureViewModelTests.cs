using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.TestSupport;
using Winhance.UI.Features.Common.Extensions.DI;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Customize.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SettingsFeatureViewModelTests
{
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();

    private static IEnumerable<string> SectionModuleIds =>
        OptimizeViewModel.Sections.Concat(CustomizeViewModel.Sections).Select(s => s.ModuleId);

    public static IEnumerable<object[]> SharedViewModelModuleIds() =>
        SectionModuleIds.Where(id => id != FeatureIds.Power).Select(id => new object[] { id });

    private IServiceProvider BaseDependencies() => new ServiceCollection()
        .AddSingleton(new Mock<ISettingsLoadingService>().Object)
        .AddSingleton(new Mock<ILogService>().Object)
        .AddSingleton(_mockLocalizationService.Object)
        .AddSingleton(new Mock<IDispatcherService>().Object)
        .AddSingleton(new Mock<IEventBus>().Object)
        .AddSingleton(new Mock<IApplicationModeService>().Object)
        .BuildServiceProvider();

    // A section with no registered view model drops off its page without an error, so resolve what AddUIServices
    // actually registers and compare it with both pages' Sections.
    [Fact]
    public void AddUIServices_RegistersExactlyOneFeatureViewModelPerPageSection()
    {
        var provider = BaseDependencies();

        var registeredIds = new ServiceCollection().AddUIServices()
            .Where(d => d.ServiceType == typeof(ISettingsFeatureViewModel))
            .Select(d => d.ImplementationType == typeof(PowerOptimizationsViewModel)
                ? FeatureIds.Power
                : ((ISettingsFeatureViewModel)d.ImplementationFactory!(provider)).ModuleId)
            .ToList();

        registeredIds.Should().OnlyHaveUniqueItems();
        registeredIds.Should().BeEquivalentTo(SectionModuleIds);
    }

    [Fact]
    public void PageSections_CoverEveryOptimizeAndCustomizeFeature()
    {
        OptimizeViewModel.Sections.Select(s => s.ModuleId).Should().BeEquivalentTo(FeatureDefinitions.OptimizeFeatures);
        CustomizeViewModel.Sections.Select(s => s.ModuleId).Should().BeEquivalentTo(FeatureDefinitions.CustomizeFeatures);
    }

    [Theory]
    [MemberData(nameof(SharedViewModelModuleIds))]
    public void DisplayName_ComesFromTheFeatureNameKey(string moduleId)
    {
        _mockLocalizationService
            .Setup(l => l.GetString($"Feature_{moduleId}_Name"))
            .Returns("Localized name");
        _mockLocalizationService.MirrorTryGetString();

        var vm = ActivatorUtilities.CreateInstance<SettingsFeatureViewModel>(BaseDependencies(), moduleId);

        vm.DisplayName.Should().Be("Localized name");
    }
}
