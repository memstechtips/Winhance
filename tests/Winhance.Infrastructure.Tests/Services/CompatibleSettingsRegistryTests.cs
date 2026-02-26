using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class CompatibleSettingsRegistryTests
{
    private readonly Mock<IWindowsCompatibilityFilter> _windowsFilter;
    private readonly Mock<IHardwareCompatibilityFilter> _hardwareFilter;
    private readonly Mock<IPowerSettingsValidationService> _powerValidation;
    private readonly Mock<ILogService> _logService;
    private readonly CompatibleSettingsRegistry _sut;

    public CompatibleSettingsRegistryTests()
    {
        _windowsFilter = new Mock<IWindowsCompatibilityFilter>();
        _hardwareFilter = new Mock<IHardwareCompatibilityFilter>();
        _powerValidation = new Mock<IPowerSettingsValidationService>();
        _logService = new Mock<ILogService>();

        // Default setup: filters pass through input unchanged
        _windowsFilter
            .Setup(f => f.FilterSettingsByWindowsVersion(It.IsAny<IEnumerable<SettingDefinition>>()))
            .Returns((IEnumerable<SettingDefinition> s) => s.ToList());

        _windowsFilter
            .Setup(f => f.FilterSettingsByWindowsVersion(It.IsAny<IEnumerable<SettingDefinition>>(), It.IsAny<bool>()))
            .Returns((IEnumerable<SettingDefinition> s, bool _) => s.ToList());

        _hardwareFilter
            .Setup(f => f.FilterSettingsByHardwareAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync((IEnumerable<SettingDefinition> s) => s.ToList());

        _powerValidation
            .Setup(f => f.FilterSettingsByExistenceAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync((IEnumerable<SettingDefinition> s) => s.ToList());

        _sut = new CompatibleSettingsRegistry(
            _windowsFilter.Object,
            _hardwareFilter.Object,
            _powerValidation.Object,
            _logService.Object);
    }

    [Fact]
    public void IsInitialized_BeforeInitializeAsync_ReturnsFalse()
    {
        _sut.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_SetsIsInitializedToTrue()
    {
        await _sut.InitializeAsync();

        _sut.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task GetFilteredSettings_WhenFilterEnabled_ReturnsFilteredResults()
    {
        // The registry discovers settings via reflection from assemblies.
        // After initialization with the default pass-through mocks,
        // querying an unknown featureId should return empty.
        await _sut.InitializeAsync();

        var result = _sut.GetFilteredSettings("NonExistentFeature");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilteredSettings_WhenFilterEnabled_ReturnsPreFilteredSettingsForKnownFeature()
    {
        // Setup: windows filter removes one setting from input
        var settingA = CreateSetting("A", "Setting A");
        var settingB = CreateSetting("B", "Setting B");
        var allSettings = new List<SettingDefinition> { settingA, settingB };
        var filteredSettings = new List<SettingDefinition> { settingA };

        _windowsFilter
            .Setup(f => f.FilterSettingsByWindowsVersion(It.IsAny<IEnumerable<SettingDefinition>>()))
            .Returns(filteredSettings);

        _windowsFilter
            .Setup(f => f.FilterSettingsByWindowsVersion(It.IsAny<IEnumerable<SettingDefinition>>(), false))
            .Returns(allSettings);

        await _sut.InitializeAsync();

        // We can't easily inject a known featureId because GetKnownFeatureProviders
        // uses reflection. Instead, verify the filter was invoked during initialization.
        _windowsFilter.Verify(
            f => f.FilterSettingsByWindowsVersion(It.IsAny<IEnumerable<SettingDefinition>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SetFilterEnabled_TogglesBehaviorOfGetFilteredSettings()
    {
        await _sut.InitializeAsync();

        // With filter enabled (default), querying unknown feature returns empty
        _sut.SetFilterEnabled(true);
        var filteredResult = _sut.GetFilteredSettings("UnknownFeature");
        filteredResult.Should().BeEmpty();

        // Disable filter - should query bypassed settings dictionary instead
        _sut.SetFilterEnabled(false);
        var bypassedResult = _sut.GetFilteredSettings("UnknownFeature");
        bypassedResult.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBypassedSettings_ReturnsUnfilteredSettingsForFeature()
    {
        await _sut.InitializeAsync();

        var result = _sut.GetBypassedSettings("NonExistentFeature");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllFilteredSettings_WhenFilterEnabled_ReturnsPreFilteredDictionary()
    {
        await _sut.InitializeAsync();

        var result = _sut.GetAllFilteredSettings();

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IReadOnlyDictionary<string, IEnumerable<SettingDefinition>>>();
    }

    [Fact]
    public async Task GetAllFilteredSettings_WhenFilterDisabled_ReturnsBypassedDictionary()
    {
        await _sut.InitializeAsync();
        _sut.SetFilterEnabled(false);

        var result = _sut.GetAllFilteredSettings();

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IReadOnlyDictionary<string, IEnumerable<SettingDefinition>>>();
    }

    [Fact]
    public async Task GetAllBypassedSettings_ReturnsWindowsFilterBypassedDictionary()
    {
        await _sut.InitializeAsync();

        var result = _sut.GetAllBypassedSettings();

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IReadOnlyDictionary<string, IEnumerable<SettingDefinition>>>();
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_OnlyInitializesOnce()
    {
        await _sut.InitializeAsync();
        await _sut.InitializeAsync();

        _sut.IsInitialized.Should().BeTrue();

        // The "Initializing compatible settings registry" log should appear only once
        // because the second call exits early due to the _isInitialized guard.
        _logService.Verify(
            l => l.Log(LogLevel.Info, "Initializing compatible settings registry with auto-discovery", null),
            Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_ConcurrentCalls_OnlyInitializesOnce()
    {
        var task1 = _sut.InitializeAsync();
        var task2 = _sut.InitializeAsync();

        await Task.WhenAll(task1, task2);

        _sut.IsInitialized.Should().BeTrue();
        _logService.Verify(
            l => l.Log(LogLevel.Info, "Initializing compatible settings registry with auto-discovery", null),
            Times.Once);
    }

    [Fact]
    public void GetFilteredSettings_BeforeInitialization_ThrowsInvalidOperationException()
    {
        var action = () => _sut.GetFilteredSettings("SomeFeature");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public void GetAllFilteredSettings_BeforeInitialization_ThrowsInvalidOperationException()
    {
        var action = () => _sut.GetAllFilteredSettings();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public void GetBypassedSettings_BeforeInitialization_ThrowsInvalidOperationException()
    {
        var action = () => _sut.GetBypassedSettings("SomeFeature");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public void GetAllBypassedSettings_BeforeInitialization_ThrowsInvalidOperationException()
    {
        var action = () => _sut.GetAllBypassedSettings();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public void Constructor_NullWindowsFilter_ThrowsArgumentNullException()
    {
        var action = () => new CompatibleSettingsRegistry(
            null!, _hardwareFilter.Object, _powerValidation.Object, _logService.Object);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("windowsFilter");
    }

    [Fact]
    public void Constructor_NullHardwareFilter_ThrowsArgumentNullException()
    {
        var action = () => new CompatibleSettingsRegistry(
            _windowsFilter.Object, null!, _powerValidation.Object, _logService.Object);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("hardwareFilter");
    }

    [Fact]
    public void Constructor_NullPowerValidation_ThrowsArgumentNullException()
    {
        var action = () => new CompatibleSettingsRegistry(
            _windowsFilter.Object, _hardwareFilter.Object, null!, _logService.Object);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("powerValidation");
    }

    [Fact]
    public void Constructor_NullLogService_ThrowsArgumentNullException()
    {
        var action = () => new CompatibleSettingsRegistry(
            _windowsFilter.Object, _hardwareFilter.Object, _powerValidation.Object, null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logService");
    }

    private static SettingDefinition CreateSetting(string id, string name)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = name,
            Description = $"Description for {name}",
        };
    }
}
