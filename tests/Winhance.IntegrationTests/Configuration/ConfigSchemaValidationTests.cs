using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Winhance.Core.Features.Common.Models;
using Winhance.IntegrationTests.Helpers;
using Xunit;

namespace Winhance.IntegrationTests.Configuration;

/// <summary>
/// Validates that all embedded .winhance config files conform to the ConfigurationItem
/// schema — catching type mismatches (e.g. string vs string[]) that would cause
/// deserialization failures or silent data loss at runtime.
/// </summary>
[Trait("Category", "Integration")]
public class ConfigSchemaValidationTests
{
    private static readonly string ConfigDir = Path.Combine(
        TestContext.SolutionDir,
        "src", "Winhance.UI", "Features", "Common", "Resources", "Configs");

    /// <summary>
    /// Maps each ConfigurationItem property to its expected JSON token types.
    /// This is the source of truth — if the model changes, update this map.
    /// </summary>
    private static readonly Dictionary<string, HashSet<JsonValueKind>> ExpectedPropertyTypes = new()
    {
        ["Id"] = new() { JsonValueKind.String },
        ["Name"] = new() { JsonValueKind.String },
        ["IsSelected"] = new() { JsonValueKind.True, JsonValueKind.False, JsonValueKind.Null },
        ["InputType"] = new() { JsonValueKind.Number },
        ["AppxPackageName"] = new() { JsonValueKind.Array, JsonValueKind.String, JsonValueKind.Null },
        ["WinGetPackageId"] = new() { JsonValueKind.String, JsonValueKind.Null },
        ["CapabilityName"] = new() { JsonValueKind.String, JsonValueKind.Null },
        ["OptionalFeatureName"] = new() { JsonValueKind.String, JsonValueKind.Null },
        ["SelectedIndex"] = new() { JsonValueKind.Number, JsonValueKind.Null },
        ["CustomStateValues"] = new() { JsonValueKind.Object, JsonValueKind.Null },
        ["PowerSettings"] = new() { JsonValueKind.Object, JsonValueKind.Null },
        ["PowerPlanGuid"] = new() { JsonValueKind.String, JsonValueKind.Null },
        ["PowerPlanName"] = new() { JsonValueKind.String, JsonValueKind.Null },
        ["SelectedValue"] = new() { JsonValueKind.String, JsonValueKind.Null },
        ["CustomProperties"] = new() { JsonValueKind.Object, JsonValueKind.Null },
    };

    public static IEnumerable<object[]> ConfigFiles()
    {
        foreach (var file in Directory.GetFiles(ConfigDir, "*.winhance"))
        {
            yield return new object[] { Path.GetFileName(file) };
        }
    }

    [Theory]
    [MemberData(nameof(ConfigFiles))]
    public void ConfigFile_AllItemProperties_MatchExpectedJsonTypes(string fileName)
    {
        var filePath = Path.Combine(ConfigDir, fileName);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var errors = new List<string>();

        // Validate items in WindowsApps, ExternalApps
        ValidateItemsInSection(root, "WindowsApps", fileName, errors);
        ValidateItemsInSection(root, "ExternalApps", fileName, errors);

        // Validate items in Customize.Features and Optimize.Features
        ValidateItemsInFeatureGroup(root, "Customize", fileName, errors);
        ValidateItemsInFeatureGroup(root, "Optimize", fileName, errors);

        errors.Should().BeEmpty(
            "all config item properties should match expected JSON types, but found:\n" +
            string.Join("\n", errors));
    }

    [Theory]
    [MemberData(nameof(ConfigFiles))]
    public void ConfigFile_NoUnrecognizedProperties_InItems(string fileName)
    {
        var filePath = Path.Combine(ConfigDir, fileName);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var errors = new List<string>();

        var knownProperties = GetConfigurationItemPropertyNames();

        void CheckItem(JsonElement item, string location)
        {
            foreach (var prop in item.EnumerateObject())
            {
                if (!knownProperties.Contains(prop.Name))
                {
                    errors.Add($"{location}: unrecognized property '{prop.Name}' " +
                               $"(not in ConfigurationItem model — will be silently ignored)");
                }
            }
        }

        EnumerateAllItems(root, fileName, CheckItem);

        errors.Should().BeEmpty(
            "config files should not contain properties unknown to ConfigurationItem:\n" +
            string.Join("\n", errors));
    }

    [Theory]
    [MemberData(nameof(ConfigFiles))]
    public void ConfigFile_AllItems_HaveRequiredFields(string fileName)
    {
        var filePath = Path.Combine(ConfigDir, fileName);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var errors = new List<string>();

        void CheckItem(JsonElement item, string location)
        {
            if (!item.TryGetProperty("Id", out _))
                errors.Add($"{location}: missing required property 'Id'");
            if (!item.TryGetProperty("Name", out _))
                errors.Add($"{location}: missing required property 'Name'");
            if (!item.TryGetProperty("InputType", out _))
                errors.Add($"{location}: missing required property 'InputType'");
        }

        EnumerateAllItems(root, fileName, CheckItem);

        errors.Should().BeEmpty(
            "all config items should have required fields (Id, Name, InputType):\n" +
            string.Join("\n", errors));
    }

    [Theory]
    [MemberData(nameof(ConfigFiles))]
    public void ConfigFile_AppxPackageName_ArrayElementsAreStrings(string fileName)
    {
        var filePath = Path.Combine(ConfigDir, fileName);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var errors = new List<string>();

        void CheckItem(JsonElement item, string location)
        {
            if (item.TryGetProperty("AppxPackageName", out var appxProp) &&
                appxProp.ValueKind == JsonValueKind.Array)
            {
                int idx = 0;
                foreach (var element in appxProp.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.String)
                    {
                        errors.Add($"{location}.AppxPackageName[{idx}]: expected String, got {element.ValueKind}");
                    }
                    idx++;
                }
            }
        }

        EnumerateAllItems(root, fileName, CheckItem);

        errors.Should().BeEmpty(
            "all AppxPackageName array elements should be strings:\n" +
            string.Join("\n", errors));
    }

    private void ValidateItemsInSection(JsonElement root, string sectionName, string fileName, List<string> errors)
    {
        if (!root.TryGetProperty(sectionName, out var section))
            return;
        if (!section.TryGetProperty("Items", out var items))
            return;

        int itemIdx = 0;
        foreach (var item in items.EnumerateArray())
        {
            var itemId = item.TryGetProperty("Id", out var idProp) ? idProp.GetString() : $"[{itemIdx}]";
            ValidateItemProperties(item, $"{fileName} > {sectionName}.Items > {itemId}", errors);
            itemIdx++;
        }
    }

    private void ValidateItemsInFeatureGroup(JsonElement root, string groupName, string fileName, List<string> errors)
    {
        if (!root.TryGetProperty(groupName, out var group))
            return;
        if (!group.TryGetProperty("Features", out var features))
            return;

        foreach (var feature in features.EnumerateObject())
        {
            if (!feature.Value.TryGetProperty("Items", out var items))
                continue;

            int itemIdx = 0;
            foreach (var item in items.EnumerateArray())
            {
                var itemId = item.TryGetProperty("Id", out var idProp) ? idProp.GetString() : $"[{itemIdx}]";
                ValidateItemProperties(item, $"{fileName} > {groupName}.{feature.Name} > {itemId}", errors);
                itemIdx++;
            }
        }
    }

    private void ValidateItemProperties(JsonElement item, string location, List<string> errors)
    {
        foreach (var prop in item.EnumerateObject())
        {
            if (ExpectedPropertyTypes.TryGetValue(prop.Name, out var allowedTypes))
            {
                if (!allowedTypes.Contains(prop.Value.ValueKind))
                {
                    errors.Add(
                        $"{location}.{prop.Name}: expected [{string.Join("|", allowedTypes)}], " +
                        $"got {prop.Value.ValueKind}");
                }
            }
        }
    }

    private void EnumerateAllItems(JsonElement root, string fileName, Action<JsonElement, string> visitor)
    {
        foreach (var sectionName in new[] { "WindowsApps", "ExternalApps" })
        {
            if (root.TryGetProperty(sectionName, out var section) &&
                section.TryGetProperty("Items", out var items))
            {
                int idx = 0;
                foreach (var item in items.EnumerateArray())
                {
                    var itemId = item.TryGetProperty("Id", out var idProp) ? idProp.GetString() : $"[{idx}]";
                    visitor(item, $"{fileName} > {sectionName} > {itemId}");
                    idx++;
                }
            }
        }

        foreach (var groupName in new[] { "Customize", "Optimize" })
        {
            if (root.TryGetProperty(groupName, out var group) &&
                group.TryGetProperty("Features", out var features))
            {
                foreach (var feature in features.EnumerateObject())
                {
                    if (!feature.Value.TryGetProperty("Items", out var items))
                        continue;
                    int idx = 0;
                    foreach (var item in items.EnumerateArray())
                    {
                        var itemId = item.TryGetProperty("Id", out var idProp) ? idProp.GetString() : $"[{idx}]";
                        visitor(item, $"{fileName} > {groupName}.{feature.Name} > {itemId}");
                        idx++;
                    }
                }
            }
        }
    }

    private static HashSet<string> GetConfigurationItemPropertyNames()
    {
        return typeof(ConfigurationItem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
