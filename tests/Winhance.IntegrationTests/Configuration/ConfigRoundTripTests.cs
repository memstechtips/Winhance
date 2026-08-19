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
    private static readonly string[] CalculatorPackages = ["Microsoft.WindowsCalculator", "Microsoft.WindowsCalculator.Sub1"];
    private static readonly string[] TestAppPackage = ["Microsoft.TestApp"];
    private static readonly string[] TestAppPackages = ["Microsoft.TestApp", "Microsoft.TestApp.Sub1"];

    [Fact]
    public void RoundTrip_FullConfig_PreservesAllFields()
    {
        var original = TestSettingFactory.CreateFullConfig();

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<WinhanceConfigFile>(json, Options);

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
        var config = new WinhanceConfigFile
        {
            Customize = TestSettingFactory.CreateFeatureGroup(true, new Dictionary<string, ConfigSection>
            {
                ["TestFeature"] = TestSettingFactory.CreateSection(true,
                    TestSettingFactory.CreateToggleItem("t1", "True Toggle", true),
                    TestSettingFactory.CreateToggleItem("t2", "False Toggle", false),
                    TestSettingFactory.CreateToggleItem("t3", "Null Toggle", null)),
            }),
        };

        var json = JsonSerializer.Serialize(config, Options);
        var deserialized = JsonSerializer.Deserialize<WinhanceConfigFile>(json, Options);

        var items = deserialized!.Customize.Features["TestFeature"].Items;
        items.Should().HaveCount(3);
        items[0].IsSelected.Should().BeTrue();
        items[1].IsSelected.Should().BeFalse();
        items[2].IsSelected.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_SelectionItems_PreservesSelectedIndex()
    {
        var customState = new Dictionary<string, object> { ["mode"] = "advanced", ["level"] = 5 };
        var powerSettings = new Dictionary<string, object> { ["planGuid"] = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" };
        var item = TestSettingFactory.CreateSelectionItem("sel1", "Power Plan",
            selectedIndex: 3, customStateValues: customState, powerSettings: powerSettings);

        var config = new WinhanceConfigFile
        {
            Optimize = TestSettingFactory.CreateFeatureGroup(true, new Dictionary<string, ConfigSection>
            {
                ["Power"] = TestSettingFactory.CreateSection(true, item),
            }),
        };

        var json = JsonSerializer.Serialize(config, Options);
        var deserialized = JsonSerializer.Deserialize<WinhanceConfigFile>(json, Options);

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
        var item = TestSettingFactory.CreateAppItem("app1", "Calculator",
            appxPackageName: CalculatorPackages,
            winGetPackageId: "Microsoft.WindowsCalculator",
            capabilityName: "MathRecognizer");

        var config = new WinhanceConfigFile
        {
            WindowsApps = TestSettingFactory.CreateSection(true, item),
        };

        var json = JsonSerializer.Serialize(config, Options);
        var deserialized = JsonSerializer.Deserialize<WinhanceConfigFile>(json, Options);

        var result = deserialized!.WindowsApps.Items[0];
        result.AppxPackageName.Should().BeEquivalentTo(CalculatorPackages);
        result.WinGetPackageId.Should().Be("Microsoft.WindowsCalculator");
        result.CapabilityName.Should().Be("MathRecognizer");
    }

    [Fact]
    public void RoundTrip_NullProperties_OmittedFromJson()
    {
        var item = TestSettingFactory.CreateToggleItem("t1", "Simple Toggle", true);
        item.AppxPackageName.Should().BeNull();

        var config = new WinhanceConfigFile
        {
            Customize = TestSettingFactory.CreateFeatureGroup(true, new Dictionary<string, ConfigSection>
            {
                ["Test"] = TestSettingFactory.CreateSection(true, item),
            }),
        };

        var json = JsonSerializer.Serialize(config, Options);

        json.Should().NotContain("\"AppxPackageName\"");
        json.Should().NotContain("\"WinGetPackageId\"");
        json.Should().NotContain("\"CapabilityName\"");
        json.Should().NotContain("\"SelectedIndex\"");
        json.Should().NotContain("\"CustomStateValues\"");
        json.Should().NotContain("\"PowerSettings\"");
    }

    [Fact]
    public void RoundTrip_CaseInsensitive_DeserializesCorrectly()
    {
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

        var config = JsonSerializer.Deserialize<WinhanceConfigFile>(json, Options);

        config.Should().NotBeNull();
        config!.Version.Should().Be("2.0");
        config.WindowsApps.IsIncluded.Should().BeTrue();
        config.WindowsApps.Items.Should().HaveCount(1);
        config.WindowsApps.Items[0].Id.Should().Be("app1");
    }

    [Fact]
    public void RoundTrip_DateTime_PreservesCreatedAt()
    {
        var original = new WinhanceConfigFile
        {
            CreatedAt = new DateTime(2025, 12, 25, 10, 30, 45, DateTimeKind.Utc),
        };

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<WinhanceConfigFile>(json, Options);

        deserialized!.CreatedAt.Should().Be(original.CreatedAt);
    }

    [Fact]
    public void RoundTrip_EmptyConfig_ProducesValidJson()
    {
        var original = new WinhanceConfigFile();

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<WinhanceConfigFile>(json, Options);

        json.Should().NotBeNullOrEmpty();
        deserialized.Should().NotBeNull();
        deserialized!.Version.Should().Be("2.0");
        deserialized.WindowsApps.Should().NotBeNull();
        deserialized.ExternalApps.Should().NotBeNull();
        deserialized.Customize.Should().NotBeNull();
        deserialized.Optimize.Should().NotBeNull();
    }

    [Fact]
    public void Deserialize_AppxPackageName_AsString_ConvertsToArray()
    {
        // Older configs stored AppxPackageName as a plain string.
        var json = """
        {
            "Version": "2.0",
            "WindowsApps": {
                "IsIncluded": true,
                "Items": [
                    {
                        "Id": "app1",
                        "Name": "Test App",
                        "IsSelected": true,
                        "InputType": 0,
                        "AppxPackageName": "Microsoft.TestApp"
                    }
                ]
            },
            "ExternalApps": { "IsIncluded": false, "Items": [] },
            "Customize": { "IsIncluded": false, "Features": {} },
            "Optimize": { "IsIncluded": false, "Features": {} }
        }
        """;

        var config = JsonSerializer.Deserialize<WinhanceConfigFile>(json, Options);

        config.Should().NotBeNull();
        config!.WindowsApps.Items.Should().HaveCount(1);
        config.WindowsApps.Items[0].AppxPackageName.Should().BeEquivalentTo(TestAppPackage);
    }

    [Fact]
    public void Deserialize_AppxPackageName_AsArray_WorksNormally()
    {
        var json = """
        {
            "Version": "2.0",
            "WindowsApps": {
                "IsIncluded": true,
                "Items": [
                    {
                        "Id": "app1",
                        "Name": "Test App",
                        "IsSelected": true,
                        "InputType": 0,
                        "AppxPackageName": ["Microsoft.TestApp", "Microsoft.TestApp.Sub1"]
                    }
                ]
            },
            "ExternalApps": { "IsIncluded": false, "Items": [] },
            "Customize": { "IsIncluded": false, "Features": {} },
            "Optimize": { "IsIncluded": false, "Features": {} }
        }
        """;

        var config = JsonSerializer.Deserialize<WinhanceConfigFile>(json, Options);

        config.Should().NotBeNull();
        config!.WindowsApps.Items[0].AppxPackageName.Should().BeEquivalentTo(
            TestAppPackages);
    }

    [Theory]
    [InlineData("Winhance_Default_Config_Windows10_22H2.winhance")]
    [InlineData("Winhance_Default_Config_Windows11_25H2.winhance")]
    [InlineData("Winhance_Recommended_Config.winhance")]
    public void EmbeddedConfigFile_DeserializesWithoutErrors(string fileName)
    {
        var configDir = Path.Combine(
            TestContext.SolutionDir,
            "src", "Winhance.UI", "Features", "Common", "Resources", "Configs");
        var filePath = Path.Combine(configDir, fileName);
        var json = File.ReadAllText(filePath);

        var config = JsonSerializer.Deserialize<WinhanceConfigFile>(json, Options);

        config.Should().NotBeNull();
        config!.Version.Should().Be("2.0");

        // Verify all AppxPackageName entries are arrays (not strings that failed to deserialize)
        if (config.WindowsApps?.Items != null)
        {
            foreach (var item in config.WindowsApps.Items)
            {
                if (item.AppxPackageName != null)
                {
                    item.AppxPackageName.Should().NotBeEmpty(
                        $"AppxPackageName for '{item.Name}' should be a non-empty array");
                }
            }
        }
    }
}
