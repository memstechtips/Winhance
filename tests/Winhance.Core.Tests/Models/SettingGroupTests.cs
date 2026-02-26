using FluentAssertions;
using Winhance.Core.Features.Common.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class SettingGroupTests
{
    private static SettingDefinition CreateSetting(string id) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
    };

    [Fact]
    public void Construction_WithRequiredProperties_Succeeds()
    {
        var settings = new[] { CreateSetting("s1"), CreateSetting("s2") };

        var group = new SettingGroup
        {
            Name = "Test Group",
            FeatureId = "feature-1",
            Settings = settings,
        };

        group.Name.Should().Be("Test Group");
        group.FeatureId.Should().Be("feature-1");
        group.Settings.Should().HaveCount(2);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var settings = Array.Empty<SettingDefinition>();

        var group1 = new SettingGroup { Name = "G", FeatureId = "F", Settings = settings };
        var group2 = new SettingGroup { Name = "G", FeatureId = "F", Settings = settings };

        group1.Should().Be(group2);
    }

    [Fact]
    public void RecordEquality_DifferentNames_AreNotEqual()
    {
        var settings = Array.Empty<SettingDefinition>();

        var group1 = new SettingGroup { Name = "G1", FeatureId = "F", Settings = settings };
        var group2 = new SettingGroup { Name = "G2", FeatureId = "F", Settings = settings };

        group1.Should().NotBe(group2);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        var original = new SettingGroup
        {
            Name = "Original",
            FeatureId = "F",
            Settings = Array.Empty<SettingDefinition>(),
        };

        var modified = original with { Name = "Modified" };

        modified.Name.Should().Be("Modified");
        modified.FeatureId.Should().Be("F");
        original.Name.Should().Be("Original");
    }

    [Fact]
    public void Settings_CanBeAccessedByIndex()
    {
        var s1 = CreateSetting("first");
        var s2 = CreateSetting("second");

        var group = new SettingGroup
        {
            Name = "Group",
            FeatureId = "Feature",
            Settings = new[] { s1, s2 },
        };

        group.Settings[0].Id.Should().Be("first");
        group.Settings[1].Id.Should().Be("second");
    }
}
