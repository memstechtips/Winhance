using System.Collections.Generic;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

// Slice B2: LocalizeSetting (+ its GetLocalized* / Localize* helpers) was retired; display localization moved to
// SettingViewModelFactory on the catalog path (covered by LocalizeDisplayReadSwapEquivalenceTests +
// SettingViewModelFactoryTests). This service now only builds the cross-group info banner, so only those tests remain.
public class SettingLocalizationServiceTests
{
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<ICompatibleSettingsRegistry> _compatibleSettingsRegistry = new();

    public SettingLocalizationServiceTests()
    {
        // Default: return the key wrapped in brackets to indicate "not found"
        _localizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => $"[{k}]");
    }

    private SettingLocalizationService CreateSut() => new(
        _localizationService.Object,
        _compatibleSettingsRegistry.Object);

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

        _compatibleSettingsRegistry.Setup(r => r.GetFeatureIdForSetting("privacy-child1"))
            .Returns("Privacy");

        var childSetting = new SettingDefinition
        {
            Id = "privacy-child1",
            Name = "Child Setting 1",
            Description = "Child desc",
            GroupName = "Privacy_Group"
        };
        _compatibleSettingsRegistry.Setup(r => r.GetFilteredSettings("Privacy"))
            .Returns(new[] { childSetting });

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
    }

    [Fact]
    public void BuildCrossGroupInfoMessage_WhenFeatureIdNotFound_SkipsSetting()
    {
        var crossGroupSettings = new Dictionary<string, string>
        {
            ["unknown-child1"] = "Setting_Unknown_Name"
        };

        _compatibleSettingsRegistry.Setup(r => r.GetFeatureIdForSetting("unknown-child1"))
            .Returns((string?)null);

        var sut = CreateSut();
        var setting = CreateTestSetting(crossGroupChildSettings: crossGroupSettings);

        var result = sut.BuildCrossGroupInfoMessage(setting);

        result.Should().BeNull();
    }
}
