using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.IntegrationTests.Helpers;

/// <summary>
/// Factory methods for creating test data objects with known values.
/// </summary>
public static class TestSettingFactory
{
    public static ConfigurationItem CreateToggleItem(
        string id,
        string name = "Test Toggle",
        bool? isSelected = true)
    {
        return new ConfigurationItem
        {
            Id = id,
            Name = name,
            IsSelected = isSelected,
            InputType = InputType.Toggle,
        };
    }

    public static ConfigurationItem CreateSelectionItem(
        string id,
        string name = "Test Selection",
        int? selectedIndex = 1,
        Dictionary<string, object>? customStateValues = null,
        Dictionary<string, object>? powerSettings = null)
    {
        return new ConfigurationItem
        {
            Id = id,
            Name = name,
            InputType = InputType.Selection,
            SelectedIndex = selectedIndex,
            CustomStateValues = customStateValues,
            PowerSettings = powerSettings,
        };
    }

    public static ConfigurationItem CreateAppItem(
        string id,
        string name = "Test App",
        string? appxPackageName = null,
        string[]? subPackages = null,
        string? winGetPackageId = null,
        string? capabilityName = null)
    {
        return new ConfigurationItem
        {
            Id = id,
            Name = name,
            IsSelected = true,
            AppxPackageName = appxPackageName,
            SubPackages = subPackages,
            WinGetPackageId = winGetPackageId,
            CapabilityName = capabilityName,
        };
    }

    public static ConfigSection CreateSection(
        bool isIncluded = true,
        params ConfigurationItem[] items)
    {
        return new ConfigSection
        {
            IsIncluded = isIncluded,
            Items = items.ToList(),
        };
    }

    public static FeatureGroupSection CreateFeatureGroup(
        bool isIncluded = true,
        Dictionary<string, ConfigSection>? features = null)
    {
        return new FeatureGroupSection
        {
            IsIncluded = isIncluded,
            Features = features ?? new Dictionary<string, ConfigSection>(),
        };
    }

    public static UnifiedConfigurationFile CreateFullConfig()
    {
        var toggleItem = CreateToggleItem("toggle1", "Toggle Setting", true);
        var falseToggle = CreateToggleItem("toggle2", "Disabled Toggle", false);
        var nullToggle = CreateToggleItem("toggle3", "Null Toggle", null);

        var selectionItem = CreateSelectionItem(
            "selection1",
            "Selection Setting",
            selectedIndex: 2,
            customStateValues: new Dictionary<string, object> { ["key1"] = "value1" },
            powerSettings: new Dictionary<string, object> { ["plan"] = "high" });

        var appItem = CreateAppItem(
            "app1",
            "Test Windows App",
            appxPackageName: "Microsoft.TestApp",
            subPackages: new[] { "Microsoft.TestApp.Sub1", "Microsoft.TestApp.Sub2" },
            winGetPackageId: "TestVendor.TestApp",
            capabilityName: "TestCapability");

        return new UnifiedConfigurationFile
        {
            Version = "2.0",
            CreatedAt = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            WindowsApps = CreateSection(true, appItem),
            ExternalApps = CreateSection(true, CreateToggleItem("ext1", "External App")),
            Customize = CreateFeatureGroup(true, new Dictionary<string, ConfigSection>
            {
                ["Explorer"] = CreateSection(true, toggleItem, falseToggle),
            }),
            Optimize = CreateFeatureGroup(true, new Dictionary<string, ConfigSection>
            {
                ["Privacy"] = CreateSection(true, nullToggle),
                ["Power"] = CreateSection(true, selectionItem),
            }),
        };
    }

    public static SettingDefinition CreateSettingDefinition(
        string id,
        string name = "Test Setting",
        int? minBuild = null,
        int? maxBuild = null,
        bool requiresBattery = false,
        bool requiresLid = false,
        bool requiresDesktop = false,
        IReadOnlyList<(int MinBuild, int MaxBuild)>? supportedBuildRanges = null)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = name,
            Description = $"Test setting: {name}",
            MinimumBuildNumber = minBuild,
            MaximumBuildNumber = maxBuild,
            RequiresBattery = requiresBattery,
            RequiresLid = requiresLid,
            RequiresDesktop = requiresDesktop,
            SupportedBuildRanges = supportedBuildRanges ?? Array.Empty<(int, int)>(),
        };
    }
}
