using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.TestSupport;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class KeyedSelectionReviewDiffTests : IDisposable
{
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<ICatalogSettingsRegistry> _registry = new Mock<ICatalogSettingsRegistry>().ResolveIdsFromFeatures();
    private readonly Mock<ICatalogSettingStateProvider> _stateProvider = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IWindowsVersionService> _windowsVersionService = new();

    private ConfigReviewService? _service;

    public void Dispose()
    {
        _service?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static SettingStateResult MachineOn(string? key) => FakeOptionProvider.StateOn(key);

    private async Task<ConfigReviewDiff?> EagerDiffFor(
        Setting setting,
        ConfigurationItem configItem,
        SettingStateResult machineState)
    {
        _registry
            .Setup(r => r.GetByFeature(FeatureIds.Privacy, It.IsAny<CatalogScope>()))
            .Returns(new[] { setting });

        _stateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult> { [setting.Id] = machineState });

        _service = new ConfigReviewService(
            _logService.Object,
            _registry.Object,
            _stateProvider.Object,
            _localizationService.Object,
            _windowsVersionService.Object,
            new FakeOptionProviderRegistry());

        await _service.EnterReviewModeAsync(new WinhanceConfigFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    [FeatureIds.Privacy] = new ConfigSection { IsIncluded = true, Items = [configItem] },
                },
            },
        });

        return _service.GetDiffForSetting(configItem.Id);
    }

    [Fact]
    public async Task A_config_naming_another_key_is_a_diff_labelled_by_both_sides_own_wording()
    {
        var diff = await EagerDiffFor(
            FakeOptionProvider.SettingFor(),
            new ConfigurationItem { Id = "fake-keyed", SelectedKey = "gamma", SelectedKeyLabel = "Gamma zone" },
            MachineOn("beta"));

        diff.Should().NotBeNull();
        diff!.CurrentValueDisplay.Should().Be("Beta zone");
        diff.ConfigValueDisplay.Should().Be("Gamma zone");
    }

    [Fact]
    public async Task A_config_naming_the_key_the_machine_is_already_on_is_not_a_diff()
    {
        var diff = await EagerDiffFor(
            FakeOptionProvider.SettingFor(),
            new ConfigurationItem { Id = "fake-keyed", SelectedKey = "beta", SelectedKeyLabel = "Beta zone" },
            MachineOn("beta"));

        diff.Should().BeNull();
    }

    [Fact]
    public async Task A_key_this_machine_does_not_offer_is_still_named_by_the_label_the_file_saved()
    {
        var diff = await EagerDiffFor(
            FakeOptionProvider.SettingFor(),
            new ConfigurationItem { Id = "fake-keyed", SelectedKey = "delta", SelectedKeyLabel = "Delta zone" },
            MachineOn("beta"));

        diff.Should().NotBeNull();
        diff!.CurrentValueDisplay.Should().Be("Beta zone");
        diff.ConfigValueDisplay.Should().Be("Delta zone");
    }

    [Fact]
    public async Task A_file_with_no_label_falls_back_to_naming_the_key()
    {
        var diff = await EagerDiffFor(
            FakeOptionProvider.SettingFor(),
            new ConfigurationItem { Id = "fake-keyed", SelectedKey = "delta" },
            MachineOn("beta"));

        diff.Should().NotBeNull();
        diff!.ConfigValueDisplay.Should().Be("delta");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task An_unread_machine_key_is_still_named_rather_than_left_blank(string? machineKey)
    {
        // A source that cannot read a selection still reports Success, so the item is not filtered out of the review.
        _localizationService.PresentKey("ConfigReview_UnknownValue", "Not known");

        var diff = await EagerDiffFor(
            FakeOptionProvider.SettingFor(),
            new ConfigurationItem { Id = "fake-keyed", SelectedKey = "gamma", SelectedKeyLabel = "Gamma zone" },
            MachineOn(machineKey));

        diff.Should().NotBeNull();
        diff!.CurrentValueDisplay.Should().Be("Not known");
        diff.ConfigValueDisplay.Should().Be("Gamma zone");
    }
}
