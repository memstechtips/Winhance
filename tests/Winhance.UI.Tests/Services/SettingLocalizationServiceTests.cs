using System.Collections.Generic;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

// Slice B2: LocalizeSetting (+ its GetLocalized* / Localize* helpers) was retired; display localization moved to
// SettingViewModelFactory on the catalog path (covered by LocalizeDisplayReadSwapEquivalenceTests +
// SettingViewModelFactoryTests). This service now only builds the cross-group info banner, so only those tests remain.
public class SettingLocalizationServiceTests
{
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<ICatalogSettingsRegistry> _catalogSettingsRegistry = new();
    private readonly Mock<IWindowsVersionFilterService> _windowsVersionFilter = new();

    public SettingLocalizationServiceTests()
    {
        // Default: return the key wrapped in brackets to indicate "not found"
        _localizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => $"[{k}]");
        // Default: filter ON (the normal mode) -> the service passes includeOtherOsVersions: false
        _windowsVersionFilter.Setup(f => f.IsFilterEnabled).Returns(true);
    }

    private SettingLocalizationService CreateSut() => new(
        _localizationService.Object,
        _catalogSettingsRegistry.Object,
        _windowsVersionFilter.Object);

    private SettingDefinition CreateTestSetting(
        string id = "test-setting",
        string name = "Test Setting",
        string description = "Test Description",
        string? groupName = "TestGroup",
        ComboBoxMetadata? comboBox = null,
        NumericRangeMetadata? numericRange = null,
        string? versionCompatibilityMessage = null,
        Dictionary<string, string>? crossGroupChildSettings = null) => new()
    {
        Id = id,
        Name = name,
        Description = description,
        GroupName = groupName,
        ComboBox = comboBox,
        NumericRange = numericRange,
        VersionCompatibilityMessage = versionCompatibilityMessage,
        CrossGroupChildSettings = crossGroupChildSettings
    };

    // --- BuildCrossGroupInfoMessage ---

    [Fact]
    public void BuildCrossGroupInfoMessage_WhenNoCustomProperties_ReturnsNull()
    {
        var sut = CreateSut();
        var setting = CreateTestSetting();

        var result = sut.BuildCrossGroupInfoMessage(setting);

        result.Should().BeNull();
    }

    [Fact]
    public void BuildCrossGroupInfoMessage_WhenNoCrossGroupSettings_ReturnsNull()
    {
        var sut = CreateSut();
        var setting = CreateTestSetting(crossGroupChildSettings: new Dictionary<string, string>());

        var result = sut.BuildCrossGroupInfoMessage(setting);

        result.Should().BeNull();
    }

    [Fact]
    public void BuildCrossGroupInfoMessage_WhenChildSettingsExist_BuildsMessage()
    {
        var crossGroupSettings = new Dictionary<string, string>
        {
            ["privacy-child1"] = "Setting_Child1_Name"
        };

        var childSetting = new Setting
        {
            Id = "privacy-child1",
            Display = new Display
            {
                Name = "Child Setting 1",
                Description = "Child desc",
                GroupName = "Privacy_Group",
            },
        };
        _catalogSettingsRegistry.Setup(r => r.GetById("privacy-child1", It.IsAny<bool>()))
            .Returns(childSetting);

        _localizationService.Setup(l => l.GetString("Setting_CrossGroupWarning_Header"))
            .Returns("Warning Header");
        _localizationService.Setup(l => l.GetString("Setting_Child1_Name"))
            .Returns("Localized Child");
        _localizationService.Setup(l => l.GetString("Feature_Privacy_Name"))
            .Returns("Privacy & Security");
        _localizationService.Setup(l => l.GetString("SettingGroup_Privacy_Group"))
            .Returns("Privacy Group Localized");

        var sut = CreateSut();
        var setting = CreateTestSetting(crossGroupChildSettings: crossGroupSettings);

        var result = sut.BuildCrossGroupInfoMessage(setting);

        result.Should().NotBeNull();
        result.Should().Contain("Warning Header");
        result.Should().Contain("Localized Child");
        // Pin the group-key construction (feature name + localized group) so a wrong Display field read fails.
        result.Should().Contain("Privacy & Security (Privacy Group Localized)");
        // Pin the mode threading: filter ON must query the current-OS scope (includeOtherOsVersions: false).
        _catalogSettingsRegistry.Verify(r => r.GetById("privacy-child1", false), Times.Once);
    }

    [Fact]
    public void BuildCrossGroupInfoMessage_WhenChildNotResolved_SkipsSetting()
    {
        var crossGroupSettings = new Dictionary<string, string>
        {
            ["unknown-child1"] = "Setting_Unknown_Name"
        };

        // An id outside the mode-scoped catalog membership resolves to null (was: feature-index miss)
        _catalogSettingsRegistry.Setup(r => r.GetById("unknown-child1", It.IsAny<bool>()))
            .Returns((Setting?)null);

        var sut = CreateSut();
        var setting = CreateTestSetting(crossGroupChildSettings: crossGroupSettings);

        var result = sut.BuildCrossGroupInfoMessage(setting);

        result.Should().BeNull();
    }

    [Fact]
    public void BuildCrossGroupInfoMessage_WhenFilterOff_QueriesOtherOsScope()
    {
        var crossGroupSettings = new Dictionary<string, string>
        {
            ["privacy-child1"] = "Setting_Child1_Name"
        };

        _windowsVersionFilter.Setup(f => f.IsFilterEnabled).Returns(false);

        var childSetting = new Setting
        {
            Id = "privacy-child1",
            Display = new Display
            {
                Name = "Child Setting 1",
                Description = "Child desc",
                GroupName = "Privacy_Group",
            },
        };
        // Strict on the scope arg: only includeOtherOsVersions: true resolves - a false arg returns
        // null and fails NotBeNull, so the !IsFilterEnabled threading is load-bearing here.
        _catalogSettingsRegistry.Setup(r => r.GetById("privacy-child1", true))
            .Returns(childSetting);

        var sut = CreateSut();
        var setting = CreateTestSetting(crossGroupChildSettings: crossGroupSettings);

        var result = sut.BuildCrossGroupInfoMessage(setting);

        result.Should().NotBeNull();
        _catalogSettingsRegistry.Verify(r => r.GetById("privacy-child1", true), Times.Once);
    }
}
