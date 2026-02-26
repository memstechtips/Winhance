using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.IntegrationTests.Helpers;
using Xunit;

namespace Winhance.IntegrationTests.Configuration;

[Trait("Category", "Integration")]
public class ConfigRoundTripTests
{
    private static readonly JsonSerializerOptions Options = ConfigFileConstants.JsonOptions;

    [Fact]
    public void RoundTrip_FullConfig_PreservesAllFields()
    {
        // Arrange
        var original = TestSettingFactory.CreateFullConfig();

        // Act
        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, Options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Version.Should().Be(original.Version);
        deserialized.CreatedAt.Should().Be(original.CreatedAt);

        deserialized.WindowsApps.IsIncluded.Should().Be(original.WindowsApps.IsIncluded);
        deserialized.WindowsApps.Items.Should().HaveCount(original.WindowsApps.Items.Count);

        deserialized.ExternalApps.IsIncluded.Should().Be(original.ExternalApps.IsIncluded);
        deserialized.ExternalApps.Items.Should().HaveCount(original.ExternalApps.Items.Count);

        deserialized.Customize.IsIncluded.Should().Be(original.Customize.IsIncluded);
        deserialized.Customize.Features.Should().HaveCount(original.Customize.Features.Count);

        deserialized.Optimize.IsIncluded.Should().Be(original.Optimize.IsIncluded);
        deserialized.Optimize.Features.Should().HaveCount(original.Optimize.Features.Count);
    }

    [Fact]
    public void RoundTrip_ToggleItems_PreservesIsSelected()
    {
        // Arrange
        var config = new UnifiedConfigurationFile
        {
            Customize = TestSettingFactory.CreateFeatureGroup(true, new Dictionary<string, ConfigSection>
            {
                ["TestFeature"] = TestSettingFactory.CreateSection(true,
                    TestSettingFactory.CreateToggleItem("t1", "True Toggle", true),
                    TestSettingFactory.CreateToggleItem("t2", "False Toggle", false),
                    TestSettingFactory.CreateToggleItem("t3", "Null Toggle", null)),
            }),
        };

        // Act
        var json = JsonSerializer.Serialize(config, Options);
        var deserialized = JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, Options);

        // Assert
        var items = deserialized!.Customize.Features["TestFeature"].Items;
        items.Should().HaveCount(3);
        items[0].IsSelected.Should().BeTrue();
        items[1].IsSelected.Should().BeFalse();
        items[2].IsSelected.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_SelectionItems_PreservesSelectedIndex()
    {
        // Arrange
        var customState = new Dictionary<string, object> { ["mode"] = "advanced", ["level"] = 5 };
        var powerSettings = new Dictionary<string, object> { ["planGuid"] = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" };
        var item = TestSettingFactory.CreateSelectionItem("sel1", "Power Plan",
            selectedIndex: 3, customStateValues: customState, powerSettings: powerSettings);

        var config = new UnifiedConfigurationFile
        {
            Optimize = TestSettingFactory.CreateFeatureGroup(true, new Dictionary<string, ConfigSection>
            {
                ["Power"] = TestSettingFactory.CreateSection(true, item),
            }),
        };

        // Act
        var json = JsonSerializer.Serialize(config, Options);
        var deserialized = JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, Options);

        // Assert
        var result = deserialized!.Optimize.Features["Power"].Items[0];
        result.SelectedIndex.Should().Be(3);
        result.InputType.Should().Be(InputType.Selection);
        result.CustomStateValues.Should().NotBeNull();
        result.CustomStateValues!["mode"].ToString().Should().Be("advanced");
        result.PowerSettings.Should().NotBeNull();
        result.PowerSettings!["planGuid"].ToString().Should().Be("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    }

    [Fact]
    public void RoundTrip_AppItems_PreservesAppxFields()
    {
        // Arrange
        var item = TestSettingFactory.CreateAppItem("app1", "Calculator",
            appxPackageName: "Microsoft.WindowsCalculator",
            subPackages: new[] { "Microsoft.WindowsCalculator.Sub1" },
            winGetPackageId: "Microsoft.WindowsCalculator",
            capabilityName: "MathRecognizer");

        var config = new UnifiedConfigurationFile
        {
            WindowsApps = TestSettingFactory.CreateSection(true, item),
        };

        // Act
        var json = JsonSerializer.Serialize(config, Options);
        var deserialized = JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, Options);

        // Assert
        var result = deserialized!.WindowsApps.Items[0];
        result.AppxPackageName.Should().Be("Microsoft.WindowsCalculator");
        result.SubPackages.Should().BeEquivalentTo(new[] { "Microsoft.WindowsCalculator.Sub1" });
        result.WinGetPackageId.Should().Be("Microsoft.WindowsCalculator");
        result.CapabilityName.Should().Be("MathRecognizer");
    }

    [Fact]
    public void RoundTrip_NullProperties_OmittedFromJson()
    {
        // Arrange
        var item = TestSettingFactory.CreateToggleItem("t1", "Simple Toggle", true);
        // These should all be null and omitted from JSON
        item.AppxPackageName.Should().BeNull();
        item.SubPackages.Should().BeNull();

        var config = new UnifiedConfigurationFile
        {
            Customize = TestSettingFactory.CreateFeatureGroup(true, new Dictionary<string, ConfigSection>
            {
                ["Test"] = TestSettingFactory.CreateSection(true, item),
            }),
        };

        // Act
        var json = JsonSerializer.Serialize(config, Options);

        // Assert — null properties should not appear in JSON at all
        json.Should().NotContain("\"AppxPackageName\"");
        json.Should().NotContain("\"SubPackages\"");
        json.Should().NotContain("\"WinGetPackageId\"");
        json.Should().NotContain("\"CapabilityName\"");
        json.Should().NotContain("\"SelectedIndex\"");
        json.Should().NotContain("\"CustomStateValues\"");
        json.Should().NotContain("\"PowerSettings\"");
    }

    [Fact]
    public void RoundTrip_CaseInsensitive_DeserializesCorrectly()
    {
        // Arrange — manually construct JSON with different casing
        var json = """
        {
            "version": "2.0",
            "createdAt": "2025-06-15T12:00:00Z",
            "windowsApps": {
                "isIncluded": true,
                "items": [
                    {
                        "id": "app1",
                        "name": "Test App",
                        "isSelected": true,
                        "inputType": 0
                    }
                ]
            },
            "externalApps": { "isIncluded": false, "items": [] },
            "customize": { "isIncluded": false, "features": {} },
            "optimize": { "isIncluded": false, "features": {} }
        }
        """;

        // Act
        var config = JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, Options);

        // Assert
        config.Should().NotBeNull();
        config!.Version.Should().Be("2.0");
        config.WindowsApps.IsIncluded.Should().BeTrue();
        config.WindowsApps.Items.Should().HaveCount(1);
        config.WindowsApps.Items[0].Id.Should().Be("app1");
    }

    [Fact]
    public void RoundTrip_DateTime_PreservesCreatedAt()
    {
        // Arrange
        var original = new UnifiedConfigurationFile
        {
            CreatedAt = new DateTime(2025, 12, 25, 10, 30, 45, DateTimeKind.Utc),
        };

        // Act
        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, Options);

        // Assert
        deserialized!.CreatedAt.Should().Be(original.CreatedAt);
    }

    [Fact]
    public void RoundTrip_EmptyConfig_ProducesValidJson()
    {
        // Arrange
        var original = new UnifiedConfigurationFile();

        // Act
        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<UnifiedConfigurationFile>(json, Options);

        // Assert
        json.Should().NotBeNullOrEmpty();
        deserialized.Should().NotBeNull();
        deserialized!.Version.Should().Be("2.0");
        deserialized.WindowsApps.Should().NotBeNull();
        deserialized.ExternalApps.Should().NotBeNull();
        deserialized.Customize.Should().NotBeNull();
        deserialized.Optimize.Should().NotBeNull();
    }
}
