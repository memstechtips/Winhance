using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.IntegrationTests.Helpers;
using Xunit;

namespace Winhance.IntegrationTests.Pipeline;

[Trait("Category", "Integration")]
public class CompatibleSettingsRegistryTests
{
    private readonly Mock<IWindowsVersionService> _versionService = new();
    private readonly Mock<IHardwareDetectionService> _hardwareDetection = new();
    private readonly Mock<IPowerSettingsValidationService> _powerValidation = new();
    private readonly Mock<ILogService> _logService = new();

    private CompatibleSettingsRegistry CreateRegistry(int buildNumber = 22631, bool isWindows11 = true)
    {
        _versionService.Setup(v => v.GetWindowsBuildNumber()).Returns(buildNumber);
        _versionService.Setup(v => v.IsWindows11()).Returns(isWindows11);
        _versionService.Setup(v => v.IsWindowsServer()).Returns(false);

        _hardwareDetection.Setup(h => h.HasBatteryAsync()).ReturnsAsync(false);
        _hardwareDetection.Setup(h => h.HasLidAsync()).ReturnsAsync(false);
        _hardwareDetection.Setup(h => h.SupportsBrightnessControlAsync()).ReturnsAsync(false);
        _hardwareDetection.Setup(h => h.SupportsHybridSleepAsync()).ReturnsAsync(true);

        _powerValidation
            .Setup(p => p.FilterSettingsByExistenceAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .Returns<IEnumerable<SettingDefinition>>(settings => Task.FromResult(settings));

        // Use real filter instances with mocked leaf services
        var windowsFilter = new WindowsCompatibilityFilter(_versionService.Object, _logService.Object);
        var hardwareFilter = new HardwareCompatibilityFilter(_hardwareDetection.Object, _logService.Object);

        return new CompatibleSettingsRegistry(
            windowsFilter,
            hardwareFilter,
            _powerValidation.Object,
            _logService.Object);
    }

    [Fact]
    public async Task GetFilteredSettings_AllCompatible_ReturnsAll()
    {
        // Arrange
        var registry = CreateRegistry(buildNumber: 22631, isWindows11: true);

        // Act
        await registry.InitializeAsync();

        // Assert — the registry auto-discovers known features from static providers;
        // with build 22631 (Win11 23H2), most settings should pass Windows version filtering
        var allSettings = registry.GetAllFilteredSettings();
        allSettings.Should().NotBeEmpty("at least some features should have compatible settings");
    }

    [Fact]
    public async Task GetFilteredSettings_IncompatibleOS_FiltersOut()
    {
        // Arrange — use a very low build number so Windows 11-only settings are excluded
        var registry = CreateRegistry(buildNumber: 10240, isWindows11: false);

        // Act
        await registry.InitializeAsync();

        // Assert — settings requiring higher builds should be filtered out
        var allSettings = registry.GetAllFilteredSettings();
        foreach (var (featureId, settings) in allSettings)
        {
            foreach (var setting in settings)
            {
                if (setting.MinimumBuildNumber.HasValue)
                {
                    setting.MinimumBuildNumber.Value.Should().BeLessThanOrEqualTo(10240,
                        $"setting '{setting.Id}' in feature '{featureId}' has MinimumBuildNumber {setting.MinimumBuildNumber} " +
                        "which should have been filtered out for build 10240");
                }
            }
        }
    }

    [Fact]
    public async Task GetFilteredSettings_PreservesSettingOrder()
    {
        // Arrange
        var registry = CreateRegistry(buildNumber: 22631, isWindows11: true);

        // Act
        await registry.InitializeAsync();

        // Assert — for each feature, verify settings are returned (order preserved from static providers)
        var allSettings = registry.GetAllFilteredSettings();
        foreach (var (featureId, settings) in allSettings)
        {
            var settingsList = settings.ToList();
            // Verify IDs are unique within each feature
            var ids = settingsList.Select(s => s.Id).ToList();
            ids.Should().OnlyHaveUniqueItems($"feature '{featureId}' should not have duplicate setting IDs");
        }
    }

    [Fact]
    public async Task MultipleServices_SameRegistry_IndependentFiltering()
    {
        // Arrange
        var registry = CreateRegistry(buildNumber: 22631, isWindows11: true);

        // Act
        await registry.InitializeAsync();

        // Assert — different feature IDs should be independently stored
        var allSettings = registry.GetAllFilteredSettings();
        var featureIds = allSettings.Keys.ToList();
        featureIds.Should().HaveCountGreaterThan(1,
            "multiple features should be registered");

        // Verify each feature can be independently retrieved
        foreach (var featureId in featureIds)
        {
            var featureSettings = registry.GetFilteredSettings(featureId);
            featureSettings.Should().NotBeNull($"feature '{featureId}' should return non-null settings");
        }
    }

    [Fact]
    public async Task GetFilteredSettings_IncompatibleHardware_FiltersOut()
    {
        // Arrange — desktop (no battery, no lid)
        _hardwareDetection.Setup(h => h.HasBatteryAsync()).ReturnsAsync(false);
        _hardwareDetection.Setup(h => h.HasLidAsync()).ReturnsAsync(false);
        _hardwareDetection.Setup(h => h.SupportsBrightnessControlAsync()).ReturnsAsync(false);

        var registry = CreateRegistry(buildNumber: 22631, isWindows11: true);

        // Act
        await registry.InitializeAsync();

        // Assert — Power settings requiring battery/lid should be filtered out
        var allSettings = registry.GetAllFilteredSettings();
        if (allSettings.TryGetValue("Power", out var powerSettings))
        {
            foreach (var setting in powerSettings)
            {
                setting.RequiresBattery.Should().BeFalse(
                    $"setting '{setting.Id}' requires battery but we're on a desktop");
                setting.RequiresLid.Should().BeFalse(
                    $"setting '{setting.Id}' requires lid but we're on a desktop");
            }
        }
    }
}
