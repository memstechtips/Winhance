using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
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
        await _sut.InitializeAsync();

        // Now that providers are explicit (not reflection-based),
        // we can test with real FeatureIds directly.
        var result = _sut.GetFilteredSettings(FeatureIds.Privacy);

        result.Should().NotBeEmpty("Privacy settings should be registered via explicit provider");
        _windowsFilter.Verify(
            f => f.FilterSettingsByWindowsVersion(It.IsAny<IEnumerable<SettingDefinition>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(nameof(FeatureIds.ExplorerCustomization))]
    [InlineData(nameof(FeatureIds.StartMenu))]
    [InlineData(nameof(FeatureIds.Taskbar))]
    [InlineData(nameof(FeatureIds.WindowsTheme))]
    [InlineData(nameof(FeatureIds.Power))]
    [InlineData(nameof(FeatureIds.GamingPerformance))]
    [InlineData(nameof(FeatureIds.Notifications))]
    [InlineData(nameof(FeatureIds.Privacy))]
    [InlineData(nameof(FeatureIds.Sound))]
    [InlineData(nameof(FeatureIds.Update))]
    public async Task InitializeAsync_RegistersAllExplicitProviders(string featureIdFieldName)
    {
        // Resolve the actual FeatureId string from the field name
        var featureId = typeof(FeatureIds)
            .GetField(featureIdFieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .GetValue(null) as string;

        await _sut.InitializeAsync();

        var result = _sut.GetFilteredSettings(featureId!);
        result.Should().NotBeEmpty($"Feature '{featureId}' should have settings registered via explicit provider");
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

    [Fact]
    public void GetById_BeforeInitialize_ThrowsInvalidOperationException()
    {
        Action act = () => _sut.GetById("any-id");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsSetting()
    {
        await _sut.InitializeAsync();

        var result = _sut.GetById("security-uac-level");

        result.Should().NotBeNull();
        result!.Id.Should().Be("security-uac-level");
    }

    [Fact]
    public async Task GetById_MissingId_ReturnsNull()
    {
        await _sut.InitializeAsync();

        _sut.GetById("does-not-exist").Should().BeNull();
    }

    [Fact]
    public async Task GetFeatureIdForSetting_ExistingId_ReturnsFeatureId()
    {
        await _sut.InitializeAsync();

        _sut.GetFeatureIdForSetting("security-uac-level").Should().Be(FeatureIds.Privacy);
    }

    [Fact]
    public async Task GetFeatureIdForSetting_MissingId_ReturnsNull()
    {
        await _sut.InitializeAsync();

        _sut.GetFeatureIdForSetting("does-not-exist").Should().BeNull();
    }

    [Fact]
    public async Task GetById_NullSettingId_ThrowsArgumentNullException()
    {
        // Must be initialized first so we exercise the null-guard rather than the not-init throw.
        await _sut.InitializeAsync();

        Action act = () => _sut.GetById(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetFeatureIdForSetting_NullSettingId_ThrowsArgumentNullException()
    {
        // Must be initialized first so we exercise the null-guard rather than the not-init throw.
        await _sut.InitializeAsync();

        Action act = () => _sut.GetFeatureIdForSetting(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task InitializeAsync_OnWindows10_MergedThisPcSettings_ExposedUnderCanonicalId()
    {
        // Simulate the Windows-10 compatibility filter: drop IsWindows11Only defs, keep the rest (including the
        // IsWindows10Only "-win10" merged This PC variants). The live UI must then expose those variants under
        // their CANONICAL catalog id so detection/apply/view-model pairing converge on the merged build-gated Setting.
        _windowsFilter
            .Setup(f => f.FilterSettingsByWindowsVersion(It.IsAny<IEnumerable<SettingDefinition>>()))
            .Returns((IEnumerable<SettingDefinition> s) => s.Where(d => !d.IsWindows11Only).ToList());
        _windowsFilter
            .Setup(f => f.FilterSettingsByWindowsVersion(It.IsAny<IEnumerable<SettingDefinition>>(), It.IsAny<bool>()))
            .Returns((IEnumerable<SettingDefinition> s, bool applyFilter) =>
                applyFilter ? s.Where(d => !d.IsWindows11Only).ToList() : s.ToList());

        await _sut.InitializeAsync();

        var explorer = _sut.GetFilteredSettings(FeatureIds.ExplorerCustomization).ToList();

        const string canonical = "explorer-customization-thispc-folder-desktop";
        const string win10 = "explorer-customization-thispc-folder-desktop-win10";

        // The -win10 variant is exposed under its canonical id, exactly once (normalize + dedupe), not the retired id.
        explorer.Count(d => d.Id == canonical).Should().Be(1);
        explorer.Should().NotContain(d => d.Id == win10);
        explorer.Should().NotContain(d => d.Id.EndsWith("-win10"), "no retired -win10 id leaks into the filtered registry");

        // ...and the canonical id pairs to the merged catalog Setting (so the new build-aware engine drives it).
        SettingCatalog.All.Should().Contain(s => s.Id == canonical);
        _sut.GetById(canonical).Should().NotBeNull();
        _sut.GetFeatureIdForSetting(canonical).Should().Be(FeatureIds.ExplorerCustomization);
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
