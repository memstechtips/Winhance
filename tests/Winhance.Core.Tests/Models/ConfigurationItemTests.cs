using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class ConfigurationItemTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var item = new ConfigurationItem();

        item.Id.Should().BeEmpty();
        item.Name.Should().BeEmpty();
        item.IsSelected.Should().BeNull();
        item.InputType.Should().Be(InputType.Toggle);
        item.AppxPackageName.Should().BeNull();
        item.WinGetPackageId.Should().BeNull();
        item.CapabilityName.Should().BeNull();
        item.OptionalFeatureName.Should().BeNull();
        item.SubPackages.Should().BeNull();
        item.SelectedIndex.Should().BeNull();
        item.CustomStateValues.Should().BeNull();
        item.PowerSettings.Should().BeNull();
        item.PowerPlanGuid.Should().BeNull();
        item.PowerPlanName.Should().BeNull();
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var item = new ConfigurationItem
        {
            Id = "test-id",
            Name = "Test Setting",
            IsSelected = true,
            InputType = InputType.Selection,
            AppxPackageName = "Microsoft.Test",
            WinGetPackageId = "test.package",
            SelectedIndex = 2,
        };

        item.Id.Should().Be("test-id");
        item.Name.Should().Be("Test Setting");
        item.IsSelected.Should().BeTrue();
        item.InputType.Should().Be(InputType.Selection);
        item.AppxPackageName.Should().Be("Microsoft.Test");
        item.WinGetPackageId.Should().Be("test.package");
        item.SelectedIndex.Should().Be(2);
    }

    [Fact]
    public void JsonSerialization_OmitsNullProperties()
    {
        var item = new ConfigurationItem
        {
            Id = "test",
            Name = "Test",
        };

        var json = JsonSerializer.Serialize(item);

        json.Should().Contain("\"Id\"");
        json.Should().Contain("\"Name\"");
        json.Should().NotContain("\"AppxPackageName\"");
        json.Should().NotContain("\"WinGetPackageId\"");
        json.Should().NotContain("\"CapabilityName\"");
        json.Should().NotContain("\"SubPackages\"");
        json.Should().NotContain("\"PowerSettings\"");
    }

    [Fact]
    public void JsonSerialization_IncludesNonNullOptionalProperties()
    {
        var item = new ConfigurationItem
        {
            Id = "test",
            Name = "Test",
            AppxPackageName = "Microsoft.App",
            SelectedIndex = 1,
        };

        var json = JsonSerializer.Serialize(item);

        json.Should().Contain("\"AppxPackageName\":\"Microsoft.App\"");
        json.Should().Contain("\"SelectedIndex\":1");
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllProperties()
    {
        var original = new ConfigurationItem
        {
            Id = "power-setting",
            Name = "Power Plan",
            IsSelected = true,
            InputType = InputType.Selection,
            SelectedIndex = 3,
            PowerPlanGuid = "381b4222-f694-41f0-9685-ff5bb260df2e",
            PowerPlanName = "Balanced",
            CustomStateValues = new Dictionary<string, object>
            {
                { "key1", "value1" },
                { "key2", 42L },
            },
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ConfigurationItem>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(original.Id);
        deserialized.Name.Should().Be(original.Name);
        deserialized.IsSelected.Should().Be(original.IsSelected);
        deserialized.InputType.Should().Be(original.InputType);
        deserialized.SelectedIndex.Should().Be(original.SelectedIndex);
        deserialized.PowerPlanGuid.Should().Be(original.PowerPlanGuid);
        deserialized.PowerPlanName.Should().Be(original.PowerPlanName);
        deserialized.CustomStateValues.Should().ContainKey("key1");
    }

    [Fact]
    public void JsonDeserialization_HandlesUnknownProperties()
    {
        var json = """{"Id":"test","Name":"Test","UnknownProp":"value"}""";

        var item = JsonSerializer.Deserialize<ConfigurationItem>(json);

        item.Should().NotBeNull();
        item!.Id.Should().Be("test");
        item.Name.Should().Be("Test");
    }

    [Fact]
    public void JsonDeserialization_HandlesMissingProperties()
    {
        var json = """{"Id":"test"}""";

        var item = JsonSerializer.Deserialize<ConfigurationItem>(json);

        item.Should().NotBeNull();
        item!.Id.Should().Be("test");
        item.Name.Should().BeEmpty();
        item.IsSelected.Should().BeNull();
    }
}
