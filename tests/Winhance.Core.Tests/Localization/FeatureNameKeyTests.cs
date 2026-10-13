using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Localization;

// A feature's displayed name has ONE definition, its en.json key, and that key is derived from the feature id
// (BaseSettingsFeatureViewModel.GetDisplayName). These pin the derivation, so a new feature whose key does not
// follow its id fails here instead of rendering the raw "[Feature_X_Name]" marker on a page header.
public class FeatureNameKeyTests
{
    private static Dictionary<string, string> English()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.LocalizationDir(), "en.json")));
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty);
    }

    [Fact]
    public void FeatureNameKeys_FollowTheirFeatureId()
    {
        var en = English();

        SettingCatalog.ByFeature.Keys
            .Where(featureId => !en.ContainsKey($"Feature_{featureId}_Name"))
            .Should().BeEmpty("the page header reads Feature_<id>_Name and nothing else supplies it");
    }

    [Fact]
    public void DefaultName_SaysWhatTheAppSays()
    {
        var en = English();

        FeatureDefinitions.All
            .Where(feature => en.ContainsKey($"Feature_{feature.Id}_Name"))
            .Where(feature => en[$"Feature_{feature.Id}_Name"] != feature.DefaultName)
            .Select(feature => $"{feature.Id}: '{feature.DefaultName}' vs en.json '{en[$"Feature_{feature.Id}_Name"]}'")
            .Should().BeEmpty("the generated script heads its sections with DefaultName, which must name the same feature the UI does");
    }

    [Theory]
    [InlineData("ExplorerCustomization", FeatureIds.Explorer)]
    [InlineData("Privacy", FeatureIds.Privacy)]
    [InlineData("NotAFeature", "NotAFeature")]
    public void A_retired_feature_id_normalizes_to_the_current_one(string saved, string expected) =>
        FeatureIdAliases.Normalize(saved).Should().Be(expected);

    [Fact]
    public void No_alias_points_at_a_feature_the_catalog_lost()
    {
        var known = SettingCatalog.ByFeature.Keys.ToHashSet(StringComparer.Ordinal);

        FeatureIdAliases.Normalize("ExplorerCustomization").Should().BeOneOf(known.ToArray());
    }
}
