using System.Text.Json;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class SettingLocalizationServiceTests
{
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IDomainServiceRouter> _domainServiceRouter = new();
    private readonly Mock<ICompatibleSettingsRegistry> _compatibleSettingsRegistry = new();

    public SettingLocalizationServiceTests()
    {
        // Default: return the key wrapped in brackets to indicate "not found"
        _localizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => $"[{k}]");
    }

    private SettingLocalizationService CreateSut() => new(
        _localizationService.Object,
        _domainServiceRouter.Object,
        _compatibleSettingsRegistry.Object);

    private SettingDefinition CreateTestSetting(
        string id = "test-setting",
        string name = "Test Setting",
        string description = "Test Description",
        string? groupName = "TestGroup",
        Dictionary<string, object>? customProperties = null) => new()
    {
        Id = id,
        Name = name,
        Description = description,
        GroupName = groupName,
        CustomProperties = customProperties != null
            ? new Dictionary<string, object>(customProperties)
            : new Dictionary<string, object>()
    };

    // --- LocalizeSetting: Name ---

    [Fact]
    public void LocalizeSetting_WhenNameKeyExists_ReturnsLocalizedName()
    {
        _localizationService.Setup(l => l.GetString("Setting_test-setting_Name"))
            .Returns("Localized Name");

        var sut = CreateSut();
        var setting = CreateTestSetting();

        var result = sut.LocalizeSetting(setting);

        result.Name.Should().Be("Localized Name");
    }

    [Fact]
    public void LocalizeSetting_WhenNameKeyMissing_ReturnsFallbackName()
    {
        // Default returns [key], which triggers fallback
        var sut = CreateSut();
        var setting = CreateTestSetting(name: "Original Name");

        var result = sut.LocalizeSetting(setting);

        result.Name.Should().Be("Original Name");
    }

    // --- LocalizeSetting: Description ---

    [Fact]
    public void LocalizeSetting_WhenDescriptionKeyExists_ReturnsLocalizedDescription()
    {
        _localizationService.Setup(l => l.GetString("Setting_test-setting_Description"))
            .Returns("Localized Description");

        var sut = CreateSut();
        var setting = CreateTestSetting();

        var result = sut.LocalizeSetting(setting);

        result.Description.Should().Be("Localized Description");
    }

    [Fact]
    public void LocalizeSetting_WhenDescriptionKeyMissing_ReturnsFallbackDescription()
    {
        var sut = CreateSut();
        var setting = CreateTestSetting(description: "Original Description");

        var result = sut.LocalizeSetting(setting);

        result.Description.Should().Be("Original Description");
    }

    // --- LocalizeSetting: GroupName ---

    [Fact]
    public void LocalizeSetting_WhenGroupNameKeyExists_ReturnsLocalizedGroupName()
    {
        _localizationService.Setup(l => l.GetString("SettingGroup_TestGroup"))
            .Returns("Localized Group");

        var sut = CreateSut();
        var setting = CreateTestSetting(groupName: "TestGroup");

        var result = sut.LocalizeSetting(setting);

        result.GroupName.Should().Be("Localized Group");
    }

    [Fact]
    public void LocalizeSetting_WhenGroupNameIsNull_ReturnsNull()
    {
        var sut = CreateSut();
        var setting = CreateTestSetting(groupName: null);

        var result = sut.LocalizeSetting(setting);

        result.GroupName.Should().BeNull();
    }

    [Fact]
    public void LocalizeSetting_GroupNameWithSpacesAndAmpersand_TriesCompactedFormat()
    {
        _localizationService.Setup(l => l.GetString("SettingGroup_PrivacySecurity"))
            .Returns("Privacy & Security Localized");

        var sut = CreateSut();
        var setting = CreateTestSetting(groupName: "Privacy & Security");

        var result = sut.LocalizeSetting(setting);

        result.GroupName.Should().Be("Privacy & Security Localized");
    }

    [Fact]
    public void LocalizeSetting_GroupName_FallsBackToSnakeCaseFormat()
    {
        // Compacted format returns [key] (not found)
        _localizationService.Setup(l => l.GetString("SettingGroup_PrivacySecurity"))
            .Returns("[SettingGroup_PrivacySecurity]");
        // Snake case format found
        _localizationService.Setup(l => l.GetString("SettingGroup_Privacy_Security"))
            .Returns("Privacy Security Snake");

        var sut = CreateSut();
        var setting = CreateTestSetting(groupName: "Privacy & Security");

        var result = sut.LocalizeSetting(setting);

        result.GroupName.Should().Be("Privacy Security Snake");
    }

    [Fact]
    public void LocalizeSetting_GroupName_WhenBothFormatsMissing_ReturnsFallback()
    {
        var sut = CreateSut();
        var setting = CreateTestSetting(groupName: "Unknown Group");

        var result = sut.LocalizeSetting(setting);

        result.GroupName.Should().Be("Unknown Group");
    }

    // --- LocalizeSetting: CustomProperties ---

    [Fact]
    public void LocalizeSetting_WhenNoCustomProperties_ReturnsSettingWithoutCustomPropertyChanges()
    {
        var sut = CreateSut();
        var setting = CreateTestSetting();

        var result = sut.LocalizeSetting(setting);

        result.CustomProperties.Should().BeEmpty();
    }

    // --- ComboBoxDisplayNames ---

    [Fact]
    public void LocalizeSetting_LocalizesComboBoxDisplayNames()
    {
        _localizationService.Setup(l => l.GetString("Setting_test-setting_Option_0"))
            .Returns("Localized Option A");
        _localizationService.Setup(l => l.GetString("Setting_test-setting_Option_1"))
            .Returns("Localized Option B");

        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Option A", "Option B" }
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        var names = (string[])result.CustomProperties[CustomPropertyKeys.ComboBoxDisplayNames];
        names[0].Should().Be("Localized Option A");
        names[1].Should().Be("Localized Option B");
    }

    [Fact]
    public void LocalizeSetting_ComboBoxNames_WhenLocalizationKeyUsed_UsesKeyDirectly()
    {
        _localizationService.Setup(l => l.GetString("Template_MyOption"))
            .Returns("Template Localized");

        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Template_MyOption" }
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        var names = (string[])result.CustomProperties[CustomPropertyKeys.ComboBoxDisplayNames];
        names[0].Should().Be("Template Localized");
    }

    // --- CustomStateDisplayName ---

    [Fact]
    public void LocalizeSetting_LocalizesCustomStateDisplayName()
    {
        _localizationService.Setup(l => l.GetString("Setting_test-setting_CustomState"))
            .Returns("Localized Custom State");

        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.CustomStateDisplayName] = "Original State"
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        result.CustomProperties[CustomPropertyKeys.CustomStateDisplayName].Should().Be("Localized Custom State");
    }

    // --- Units ---

    [Fact]
    public void LocalizeSetting_LocalizesMinutesUnits()
    {
        _localizationService.Setup(l => l.GetString("Common_Unit_Minutes"))
            .Returns("min");

        var props = new Dictionary<string, object>
        {
            ["Units"] = "Minutes"
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        result.CustomProperties["Units"].Should().Be("min");
    }

    [Fact]
    public void LocalizeSetting_LocalizesMillisecondsUnits()
    {
        _localizationService.Setup(l => l.GetString("Common_Unit_Milliseconds"))
            .Returns("ms");

        var props = new Dictionary<string, object>
        {
            ["Units"] = "Milliseconds"
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        result.CustomProperties["Units"].Should().Be("ms");
    }

    [Fact]
    public void LocalizeSetting_PercentUnits_ReturnsSameValue()
    {
        var props = new Dictionary<string, object>
        {
            ["Units"] = "%"
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        result.CustomProperties["Units"].Should().Be("%");
    }

    [Fact]
    public void LocalizeSetting_UnknownUnits_ReturnsSameValue()
    {
        var props = new Dictionary<string, object>
        {
            ["Units"] = "Pixels"
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        result.CustomProperties["Units"].Should().Be("Pixels");
    }

    // --- OptionWarnings (Dictionary<int, string>) ---

    [Fact]
    public void LocalizeSetting_LocalizesOptionWarnings_FromDictionary()
    {
        _localizationService.Setup(l => l.GetString("Setting_test-setting_OptionWarning_1"))
            .Returns("Localized Warning");

        var warnings = new Dictionary<int, string> { [1] = "Original Warning" };
        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.OptionWarnings] = warnings
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        var localizedWarnings = (Dictionary<int, string>)result.CustomProperties[CustomPropertyKeys.OptionWarnings];
        localizedWarnings[1].Should().Be("Localized Warning");
    }

    // --- OptionWarnings (JsonElement) ---

    [Fact]
    public void LocalizeSetting_LocalizesOptionWarnings_FromJsonElement()
    {
        _localizationService.Setup(l => l.GetString("Setting_test-setting_OptionWarning_0"))
            .Returns("JSON Localized Warning");

        var json = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["0"] = "Warning" });
        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.OptionWarnings] = json
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        var localizedWarnings = (Dictionary<int, string>)result.CustomProperties[CustomPropertyKeys.OptionWarnings];
        localizedWarnings[0].Should().Be("JSON Localized Warning");
    }

    // --- OptionTooltips (Dictionary<int, string>) ---

    [Fact]
    public void LocalizeSetting_LocalizesOptionTooltips_FromDictionary()
    {
        _localizationService.Setup(l => l.GetString("Setting_test-setting_OptionTooltip_2"))
            .Returns("Localized Tooltip");

        var tooltips = new Dictionary<int, string> { [2] = "Original Tooltip" };
        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.OptionTooltips] = tooltips
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        var localizedTooltips = (Dictionary<int, string>)result.CustomProperties[CustomPropertyKeys.OptionTooltips];
        localizedTooltips[2].Should().Be("Localized Tooltip");
    }

    // --- OptionTooltips (JsonElement) ---

    [Fact]
    public void LocalizeSetting_LocalizesOptionTooltips_FromJsonElement()
    {
        _localizationService.Setup(l => l.GetString("Setting_test-setting_OptionTooltip_1"))
            .Returns("JSON Localized Tooltip");

        var json = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["1"] = "Tooltip" });
        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.OptionTooltips] = json
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        var localizedTooltips = (Dictionary<int, string>)result.CustomProperties[CustomPropertyKeys.OptionTooltips];
        localizedTooltips[1].Should().Be("JSON Localized Tooltip");
    }

    // --- OptionConfirmations (JsonElement) ---

    [Fact]
    public void LocalizeSetting_LocalizesOptionConfirmations_FromJsonElement()
    {
        _localizationService.Setup(l => l.GetString("ConfirmTitle"))
            .Returns("Localized Title");
        _localizationService.Setup(l => l.GetString("ConfirmMessage"))
            .Returns("Localized Message");

        var confirmJson = new Dictionary<string, object>
        {
            ["0"] = new Dictionary<string, string>
            {
                ["Title"] = "ConfirmTitle",
                ["Message"] = "ConfirmMessage"
            }
        };
        var json = JsonSerializer.SerializeToElement(confirmJson);
        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.OptionConfirmations] = json
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        var localizedConfirms = (Dictionary<int, (string Title, string Message)>)
            result.CustomProperties[CustomPropertyKeys.OptionConfirmations];
        localizedConfirms[0].Title.Should().Be("Localized Title");
        localizedConfirms[0].Message.Should().Be("Localized Message");
    }

    // --- OptionConfirmations (Dictionary<int, (string, string)>) ---

    [Fact]
    public void LocalizeSetting_LocalizesOptionConfirmations_FromDictionary()
    {
        _localizationService.Setup(l => l.GetString("OldTitle"))
            .Returns("Localized Title");
        _localizationService.Setup(l => l.GetString("OldMessage"))
            .Returns("Localized Message");

        var confirmDict = new Dictionary<int, (string Title, string Message)>
        {
            [0] = ("OldTitle", "OldMessage")
        };
        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.OptionConfirmations] = confirmDict
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        var localizedConfirms = (Dictionary<int, (string Title, string Message)>)
            result.CustomProperties[CustomPropertyKeys.OptionConfirmations];
        localizedConfirms[0].Title.Should().Be("Localized Title");
        localizedConfirms[0].Message.Should().Be("Localized Message");
    }

    // --- VersionCompatibilityMessage ---

    [Fact]
    public void LocalizeSetting_LocalizesVersionCompatibilityMessage_SimpleKey()
    {
        _localizationService.Setup(l => l.GetString("Compatibility_NotSupported"))
            .Returns("This setting is not supported");

        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.VersionCompatibilityMessage] = "Compatibility_NotSupported"
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        result.CustomProperties[CustomPropertyKeys.VersionCompatibilityMessage]
            .Should().Be("This setting is not supported");
    }

    [Fact]
    public void LocalizeSetting_LocalizesVersionCompatibilityMessage_WithArgs()
    {
        _localizationService.Setup(l => l.GetString("Compatibility_RequiresVersion"))
            .Returns("Requires version {0} or higher");

        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.VersionCompatibilityMessage] = "Compatibility_RequiresVersion|22H2"
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        result.CustomProperties[CustomPropertyKeys.VersionCompatibilityMessage]
            .Should().Be("Requires version 22H2 or higher");
    }

    [Fact]
    public void LocalizeSetting_VersionCompatibilityMessage_NonCompatibilityKey_NotLocalized()
    {
        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.VersionCompatibilityMessage] = "Some raw message"
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.LocalizeSetting(setting);

        result.CustomProperties[CustomPropertyKeys.VersionCompatibilityMessage]
            .Should().Be("Some raw message");
    }

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
        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.CrossGroupChildSettings] = new Dictionary<string, string>()
        };

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

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
        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.CrossGroupChildSettings] = crossGroupSettings
        };

        var mockDomainService = new Mock<IDomainService>();
        mockDomainService.Setup(d => d.DomainName).Returns("Privacy");
        _domainServiceRouter.Setup(r => r.GetDomainService("privacy-child1"))
            .Returns(mockDomainService.Object);

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
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.BuildCrossGroupInfoMessage(setting);

        result.Should().NotBeNull();
        result.Should().Contain("Warning Header");
        result.Should().Contain("Localized Child");
    }

    [Fact]
    public void BuildCrossGroupInfoMessage_WhenDomainServiceThrows_SkipsSetting()
    {
        var crossGroupSettings = new Dictionary<string, string>
        {
            ["unknown-child1"] = "Setting_Unknown_Name"
        };
        var props = new Dictionary<string, object>
        {
            [CustomPropertyKeys.CrossGroupChildSettings] = crossGroupSettings
        };

        _domainServiceRouter.Setup(r => r.GetDomainService("unknown-child1"))
            .Throws(new InvalidOperationException("Unknown setting"));

        var sut = CreateSut();
        var setting = CreateTestSetting(customProperties: props);

        var result = sut.BuildCrossGroupInfoMessage(setting);

        result.Should().BeNull();
    }

    // --- Immutability of original setting ---

    [Fact]
    public void LocalizeSetting_DoesNotModifyOriginalSetting()
    {
        _localizationService.Setup(l => l.GetString("Setting_test-setting_Name"))
            .Returns("Localized Name");

        var sut = CreateSut();
        var setting = CreateTestSetting(name: "Original");

        var result = sut.LocalizeSetting(setting);

        setting.Name.Should().Be("Original");
        result.Name.Should().Be("Localized Name");
    }
}
